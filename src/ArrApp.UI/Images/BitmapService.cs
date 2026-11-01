using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ArrApp.Core.Images;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace ArrApp.UI.Images;

/// <summary>
/// Turns image bytes into bitmaps decoded at the size they will be shown (a poster in a list is a few hundred pixels,
/// not the 2000px original), and keeps recently used bitmaps in memory so scrolling back is instant.
/// </summary>
public sealed class BitmapService(IImageLoader loader, int maxEntries = 200) {
    private readonly object _gate = new();
    private readonly Dictionary<string, LinkedListNode<(string Key, Bitmap Bitmap)>> _byKey = [];
    private readonly LinkedList<(string Key, Bitmap Bitmap)> _recent = new(); // front = most recent

    /// <summary>The bitmap if it is already in memory, for showing without any delay or fade.</summary>
    public Bitmap? TryGetCached(ImageRequest request, int decodeWidth)
        => KeyFor(request, decodeWidth) is { } key ? Touch(key) : null;

    /// <summary>The bitmap, or null when it could not be loaded or decoded. Never throws for a bad image.</summary>
    public async Task<Bitmap?> GetAsync(ImageRequest request, int decodeWidth, CancellationToken cancellationToken = default) {
        if (KeyFor(request, decodeWidth) is not { } key)
            return null;
        if (Touch(key) is { } hit)
            return hit;

        var bytes = await loader.LoadAsync(request, cancellationToken).ConfigureAwait(false);
        if (bytes is null)
            return null;

        var bitmap = await Task.Run(() => Decode(bytes, decodeWidth), cancellationToken).ConfigureAwait(false);
        if (bitmap is not null)
            Remember(key, bitmap);
        return bitmap;
    }

    private static Bitmap? Decode(byte[] bytes, int width) {
        try {
            using var stream = new MemoryStream(bytes);
            return Bitmap.DecodeToWidth(stream, width, BitmapInterpolationMode.MediumQuality);
        } catch (Exception) {
            return null; // corrupt or unsupported image: show the placeholder instead of failing
        }
    }

    private static string? KeyFor(ImageRequest request, int decodeWidth) {
        var candidates = request.Candidates();
        return candidates.Count == 0 ? null : $"{candidates[0].Url}@{decodeWidth}";
    }

    private Bitmap? Touch(string key) {
        lock (_gate) {
            if (!_byKey.TryGetValue(key, out var node))
                return null;
            _recent.Remove(node);
            _recent.AddFirst(node);
            return node.Value.Bitmap;
        }
    }

    private void Remember(string key, Bitmap bitmap) {
        lock (_gate) {
            if (_byKey.TryGetValue(key, out var existing)) {
                _recent.Remove(existing);
                _byKey.Remove(key);
            }

            _byKey[key] = _recent.AddFirst((key, bitmap));
            while (_recent.Count > maxEntries) {
                _byKey.Remove(_recent.Last!.Value.Key);
                _recent.RemoveLast(); // not disposed: it may still be on screen; the GC reclaims it when it is not
            }
        }
    }
}
