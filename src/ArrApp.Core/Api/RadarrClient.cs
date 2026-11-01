using ArrApp.Core.Models;

namespace ArrApp.Core.Api;

public sealed class RadarrClient(ArrHttpClient http) : ArrClientBase(http, ArrType.Radarr), IRadarrClient {
    public async Task<IReadOnlyList<Movie>> GetMoviesAsync(CancellationToken cancellationToken = default)
        => await Http.GetAsync("movie", ArrJsonContext.Default.ListMovie, cancellationToken).ConfigureAwait(false);

    public Task<Movie> GetMovieAsync(int id, CancellationToken cancellationToken = default)
        => ItemAsync(() => Http.GetAsync($"movie/{id}", ArrJsonContext.Default.Movie, cancellationToken));

    public override async Task<IReadOnlyList<MediaItem>> GetLibraryAsync(CancellationToken cancellationToken = default)
        => await GetMoviesAsync(cancellationToken).ConfigureAwait(false);

    public override async Task<MediaItem> GetItemAsync(int id, CancellationToken cancellationToken = default)
        => await GetMovieAsync(id, cancellationToken).ConfigureAwait(false);
}
