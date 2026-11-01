using System.Collections.Concurrent;

namespace ArrApp.Core.Images;

public interface IImageLoader {
    /// <summary>The image bytes, or null when none of the sources worked. Never throws for a missing or broken image.</summary>
    Task<byte[]?> LoadAsync(ImageRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Disk cache first, then the server (with the API key), then the public URL (without it). Identical requests made at
/// the same time share one download, concurrent downloads are capped, and a source that just failed is not retried for
/// a few minutes so a missing poster doesn't hit the server again on every scroll.
/// </summary>
public sealed class ImageLoader : IImageLoader {
    public static readonly TimeSpan NegativeCacheDuration = TimeSpan.FromMinutes(5);
    public const long MaxImageBytes = 10 * 1024 * 1024;

    private readonly HttpClient _serverHttp;
    private readonly HttpClient _remoteHttp;
    private readonly IImageCache _cache;
    private readonly TimeProvider _time;
    private readonly TimeSpan _timeout;
    private readonly SemaphoreSlim _gate;
    private readonly ConcurrentDictionary<string, Lazy<Task<byte[]?>>> _inFlight = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _failedUntil = new();

    /// <param name="serverHttp">For the user's own server. Must NOT follow redirects: they would carry the API key elsewhere.</param>
    /// <param name="remoteHttp">For public image hosts; the key is never sent, so following redirects (CDNs) is fine.</param>
    public ImageLoader(HttpClient serverHttp, HttpClient remoteHttp, IImageCache cache,
        TimeProvider? time = null, int maxConcurrentDownloads = 4, TimeSpan? timeout = null) {
        _serverHttp = serverHttp;
        _remoteHttp = remoteHttp;
        _cache = cache;
        _time = time ?? TimeProvider.System;
        _timeout = timeout ?? TimeSpan.FromSeconds(20);
        _gate = new SemaphoreSlim(maxConcurrentDownloads);
    }

    public async Task<byte[]?> LoadAsync(ImageRequest request, CancellationToken cancellationToken = default) {
        var candidates = request.Candidates();
        if (candidates.Count == 0)
            return null;

        foreach (var candidate in candidates) {
            if (await _cache.GetAsync(candidate.Url).ConfigureAwait(false) is { } cached)
                return cached;
        }

        var key = string.Join("|", candidates.Select(c => c.Url));
        if (_failedUntil.TryGetValue(key, out var until) && until > _time.GetUtcNow())
            return null;

        // Lazy: the entry is in the dictionary before the download starts, so even an instantly-finishing
        // download can't remove itself before it was added.
        var download = _inFlight.GetOrAdd(key, _ => new Lazy<Task<byte[]?>>(() => DownloadAsync(key, candidates))).Value;

        // The download belongs to everyone waiting for it: one caller giving up must not cancel it for the others.
        return await download.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]?> DownloadAsync(string key, IReadOnlyList<ImageCandidate> candidates) {
        try {
            await _gate.WaitAsync().ConfigureAwait(false);
            try {
                foreach (var candidate in candidates) {
                    var bytes = await TryDownloadAsync(candidate).ConfigureAwait(false);
                    if (bytes is null)
                        continue;

                    await _cache.PutAsync(candidate.Url, bytes).ConfigureAwait(false);
                    _failedUntil.TryRemove(key, out _);
                    return bytes;
                }
            } finally {
                _gate.Release();
            }

            _failedUntil[key] = _time.GetUtcNow() + NegativeCacheDuration;
            return null;
        } finally {
            _inFlight.TryRemove(key, out _);
        }
    }

    private async Task<byte[]?> TryDownloadAsync(ImageCandidate candidate) {
        using var request = new HttpRequestMessage(HttpMethod.Get, candidate.Url);
        if (candidate.ApiKey is { } apiKey)
            request.Headers.TryAddWithoutValidation("X-Api-Key", apiKey);

        using var cts = new CancellationTokenSource(_timeout);
        try {
            var http = candidate.SendApiKey ? _serverHttp : _remoteHttp;
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return null;
            if (response.Content.Headers.ContentType?.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) != true)
                return null; // an HTML login page or error page is not a poster
            if (response.Content.Headers.ContentLength is > MaxImageBytes)
                return null;

            var bytes = await response.Content.ReadAsByteArrayAsync(cts.Token).ConfigureAwait(false);
            return bytes.Length > 0 && bytes.Length <= MaxImageBytes ? bytes : null;
        } catch (Exception) {
            // Timed out, connection dropped, or a platform-specific I/O exception (Android throws its own types):
            // whatever went wrong, the contract is "no image", never an exception.
            return null;
        }
    }
}
