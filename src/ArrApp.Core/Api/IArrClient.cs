using ArrApp.Core.Models;

namespace ArrApp.Core.Api;

/// <summary>What Sonarr and Radarr both offer. ViewModels depend on this wherever the Arr doesn't matter.</summary>
public interface IArrClient {
    ArrType Type { get; }

    /// <summary>Every series (Sonarr) or movie (Radarr) in the library.</summary>
    Task<IReadOnlyList<MediaItem>> GetLibraryAsync(CancellationToken cancellationToken = default);

    /// <exception cref="ArrApiException">Kind <see cref="ArrErrorKind.NotFound"/> when the item no longer exists.</exception>
    Task<MediaItem> GetItemAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QualityProfile>> GetQualityProfilesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RootFolder>> GetRootFoldersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tag>> GetTagsAsync(CancellationToken cancellationToken = default);
}

public interface ISonarrClient : IArrClient {
    Task<IReadOnlyList<Series>> GetSeriesAsync(CancellationToken cancellationToken = default);
    Task<Series> GetSeriesByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Episode>> GetEpisodesAsync(int seriesId, CancellationToken cancellationToken = default);
}

public interface IRadarrClient : IArrClient {
    Task<IReadOnlyList<Movie>> GetMoviesAsync(CancellationToken cancellationToken = default);
    Task<Movie> GetMovieAsync(int id, CancellationToken cancellationToken = default);
}
