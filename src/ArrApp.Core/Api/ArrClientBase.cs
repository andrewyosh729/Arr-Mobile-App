using ArrApp.Core.Models;

namespace ArrApp.Core.Api;

/// <summary>The calls that are identical in both Arrs (the endpoints, paths and JSON shapes match).</summary>
public abstract class ArrClientBase(ArrHttpClient http, ArrType type) : IArrClient {
    protected ArrHttpClient Http { get; } = http;

    public ArrType Type { get; } = type;

    public abstract Task<IReadOnlyList<MediaItem>> GetLibraryAsync(CancellationToken cancellationToken = default);
    public abstract Task<MediaItem> GetItemAsync(int id, CancellationToken cancellationToken = default);

    public async Task<IReadOnlyList<QualityProfile>> GetQualityProfilesAsync(CancellationToken cancellationToken = default)
        => await Http.GetAsync("qualityprofile", ArrJsonContext.Default.ListQualityProfile, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<RootFolder>> GetRootFoldersAsync(CancellationToken cancellationToken = default)
        => await Http.GetAsync("rootfolder", ArrJsonContext.Default.ListRootFolder, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<Tag>> GetTagsAsync(CancellationToken cancellationToken = default)
        => await Http.GetAsync("tag", ArrJsonContext.Default.ListTag, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Fetching one item by id: the HTTP layer's generic 404 text ("nothing responded at that address") would
    /// mislead here, since the server clearly answered. Say what actually happened.
    /// </summary>
    protected static async Task<T> ItemAsync<T>(Func<Task<T>> fetch) {
        try {
            return await fetch().ConfigureAwait(false);
        } catch (ArrApiException ex) when (ex.Kind == ArrErrorKind.NotFound) {
            throw new ArrApiException(ArrErrorKind.NotFound, "That item no longer exists on the server.", ex);
        }
    }
}
