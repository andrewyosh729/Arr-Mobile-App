using System.Security.Cryptography;
using System.Text;

namespace ArrApp.Core.Images;

public interface IImageCache {
    Task<byte[]?> GetAsync(string url);
    Task PutAsync(string url, byte[] bytes);

    /// <summary>Current size of the cache on disk.</summary>
    long SizeBytes { get; }

    Task ClearAsync();
}

/// <summary>
/// Image bytes on disk, one file per URL, trimmed to a size budget by evicting the least recently used first.
/// Safe to share across threads; a failure to read or write is treated as a cache miss, never an error.
/// </summary>
public sealed class DiskImageCache : IImageCache {
    private const string Extension = ".img";

    private readonly string _directory;
    private readonly long _maxBytes;
    private readonly SemaphoreSlim _trimGate = new(1, 1);

    public DiskImageCache(string directory, long maxBytes = 200L * 1024 * 1024) {
        _directory = directory;
        _maxBytes = maxBytes;
    }

    public long SizeBytes {
        get {
            try {
                return Directory.Exists(_directory)
                    ? new DirectoryInfo(_directory).EnumerateFiles("*" + Extension).Sum(f => f.Length)
                    : 0;
            } catch (IOException) {
                return 0;
            }
        }
    }

    public async Task<byte[]?> GetAsync(string url) {
        var path = PathFor(url);
        try {
            if (!File.Exists(path))
                return null;

            var bytes = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow); // "recently used" for eviction
            return bytes;
        } catch (IOException) {
            return null;
        } catch (UnauthorizedAccessException) {
            return null;
        }
    }

    public async Task PutAsync(string url, byte[] bytes) {
        var path = PathFor(url);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            Directory.CreateDirectory(_directory);
            await File.WriteAllBytesAsync(temp, bytes).ConfigureAwait(false);
            File.Move(temp, path, overwrite: true); // readers never see a half-written file
        } catch (IOException) {
            TryDelete(temp);
            return;
        } catch (UnauthorizedAccessException) {
            TryDelete(temp);
            return;
        }

        await TrimAsync().ConfigureAwait(false);
    }

    public async Task ClearAsync() {
        await _trimGate.WaitAsync().ConfigureAwait(false);
        try {
            if (!Directory.Exists(_directory))
                return;
            foreach (var file in Directory.EnumerateFiles(_directory))
                TryDelete(file);
        } finally {
            _trimGate.Release();
        }
    }

    private async Task TrimAsync() {
        await _trimGate.WaitAsync().ConfigureAwait(false);
        try {
            var files = new DirectoryInfo(_directory).EnumerateFiles("*" + Extension).OrderBy(f => f.LastWriteTimeUtc).ToList();
            var total = files.Sum(f => f.Length);
            if (total <= _maxBytes)
                return;

            // Trim to 90% so a cache sitting at its limit doesn't re-trim on every single image.
            var target = _maxBytes * 9 / 10;
            foreach (var file in files) {
                if (total <= target)
                    break;
                total -= file.Length;
                TryDelete(file.FullName);
            }
        } catch (IOException) {
            // best effort
        } finally {
            _trimGate.Release();
        }
    }

    private string PathFor(string url)
        => Path.Combine(_directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))) + Extension);

    private static void TryDelete(string path) {
        try {
            File.Delete(path);
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
    }
}
