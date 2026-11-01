using ArrApp.Core.Models;

namespace ArrApp.Core.Api;

public sealed class SonarrClient(ArrHttpClient http) : ArrClientBase(http, ArrType.Sonarr), ISonarrClient {
    public async Task<IReadOnlyList<Series>> GetSeriesAsync(CancellationToken cancellationToken = default)
        => await Http.GetAsync("series", ArrJsonContext.Default.ListSeries, cancellationToken).ConfigureAwait(false);

    public Task<Series> GetSeriesByIdAsync(int id, CancellationToken cancellationToken = default)
        => ItemAsync(() => Http.GetAsync($"series/{id}", ArrJsonContext.Default.Series, cancellationToken));

    public async Task<IReadOnlyList<Episode>> GetEpisodesAsync(int seriesId, CancellationToken cancellationToken = default)
        => await Http.GetAsync($"episode?seriesId={seriesId}", ArrJsonContext.Default.ListEpisode, cancellationToken).ConfigureAwait(false);

    public override async Task<IReadOnlyList<MediaItem>> GetLibraryAsync(CancellationToken cancellationToken = default)
        => await GetSeriesAsync(cancellationToken).ConfigureAwait(false);

    public override async Task<MediaItem> GetItemAsync(int id, CancellationToken cancellationToken = default)
        => await GetSeriesByIdAsync(id, cancellationToken).ConfigureAwait(false);
}
