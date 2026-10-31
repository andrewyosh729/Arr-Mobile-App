using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ArrApp.Core.Api;
using ArrApp.Core.Models;

namespace ArrApp.Core.Tests;

/// <summary>
/// Parses JSON shaped like real Sonarr/Radarr v3 API responses. The fixtures are written by hand from the documented
/// API (with the extra fields real servers send), not recorded from a live server.
/// </summary>
public class ModelParsingTests {
    private static T Load<T>(string file, JsonTypeInfo<T> info)
        => JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", file)), info)!;

    private static ArrJsonContext Json => ArrJsonContext.Default;

    // ---- Series ----

    [Fact]
    public void Series_Parses_Shared_And_Sonarr_Fields() {
        var series = Load("series.json", Json.ListSeries)[0];

        Assert.Equal(12, series.Id);
        Assert.Equal("Severance", series.Title);
        Assert.Equal(2022, series.Year);
        Assert.Equal("continuing", series.Status);
        Assert.True(series.Monitored);
        Assert.Equal(4, series.QualityProfileId);
        Assert.Equal("/tv/Severance", series.Path);
        Assert.Equal("Apple TV+", series.Network);
        Assert.Equal("standard", series.SeriesType);
        Assert.Equal(371980, series.TvdbId);
        Assert.Equal(95396, series.TmdbId);
        Assert.Equal(["Drama", "Mystery", "Sci-Fi & Fantasy"], series.Genres);
        Assert.Equal([1, 3], series.Tags);
        Assert.Equal(new DateTimeOffset(2023, 4, 2, 10, 15, 30, TimeSpan.Zero), series.Added);
        Assert.Equal(new DateTimeOffset(2022, 2, 18, 0, 0, 0, TimeSpan.Zero), series.FirstAired);
        Assert.True(series.IsInLibrary);
    }

    [Fact]
    public void Series_Statistics_And_Seasons_Drive_Size_And_Progress() {
        var series = Load("series.json", Json.ListSeries)[0];

        Assert.Equal(15_000_000_000, series.SizeOnDisk);
        Assert.Equal(15, series.Statistics!.EpisodeFileCount);
        Assert.Equal(19, series.Statistics.EpisodeCount);
        Assert.Equal([0, 1, 2], series.Seasons.Select(s => s.SeasonNumber));
        Assert.False(series.Seasons[0].Monitored);
        Assert.Equal(100.0, series.Seasons[1].Statistics!.PercentOfEpisodes);
        Assert.Equal(6, series.Seasons[2].Statistics!.EpisodeFileCount);
    }

    [Fact]
    public void Series_Images_Are_Found_By_Cover_Type() {
        var series = Load("series.json", Json.ListSeries)[0];

        Assert.Equal("/MediaCover/12/poster.jpg?lastWrite=638", series.Poster!.Url);
        Assert.Equal("https://artworks.thetvdb.com/fanart.jpg", series.Fanart!.RemoteUrl);
        Assert.Equal("https://artworks.thetvdb.com/banner.jpg", series.Image("BANNER")!.RemoteUrl); // case-insensitive
        Assert.Null(series.Image("clearlogo"));
    }

    [Fact]
    public void A_Barely_Populated_Series_Still_Parses_With_Safe_Defaults() {
        var series = Load("series.json", Json.ListSeries)[1];

        Assert.Equal("Barely There", series.Title);
        Assert.False(series.Monitored);
        Assert.Equal(0, series.SizeOnDisk);
        Assert.Null(series.Poster);
        Assert.Empty(series.Genres);
        Assert.Empty(series.Seasons);
        Assert.Null(series.Statistics);
        Assert.Null(series.Added);
    }

    [Fact]
    public void Unknown_Fields_Are_Ignored() {
        const string json = """{"id":1,"title":"X","someFutureField":{"a":[1,2,3]},"anotherOne":"y"}""";

        var series = JsonSerializer.Deserialize(json, Json.Series)!;

        Assert.Equal("X", series.Title);
    }

    // ---- Episodes ----

    [Fact]
    public void Episode_With_A_File_Parses_Quality_And_Size() {
        var episode = Load("episodes.json", Json.ListEpisode)[0];

        Assert.Equal(9001, episode.Id);
        Assert.Equal(12, episode.SeriesId);
        Assert.Equal((1, 1), (episode.SeasonNumber, episode.EpisodeNumber));
        Assert.Equal("Good News About Hell", episode.Title);
        Assert.Equal(new DateOnly(2022, 2, 18), episode.AirDate);
        Assert.True(episode.HasFile);
        Assert.Equal(1_024_000_000, episode.EpisodeFile!.Size);
        Assert.Equal("WEBDL-1080p", episode.EpisodeFile.Quality!.Name);
        Assert.Equal(1080, episode.EpisodeFile.Quality.Quality.Resolution);
        Assert.Equal("English", Assert.Single(episode.EpisodeFile.Languages).Name);
    }

    [Fact]
    public void Unaired_Episodes_Have_No_Air_Date_Or_File() {
        var episodes = Load("episodes.json", Json.ListEpisode);

        Assert.Null(episodes[1].AirDate);
        Assert.Null(episodes[1].AirDateUtc);
        Assert.False(episodes[1].HasFile);
        Assert.Null(episodes[1].EpisodeFile);
        Assert.Equal(new DateOnly(2022, 3, 1), episodes[2].AirDate);
        Assert.Null(episodes[2].AirDateUtc);
    }

    // ---- Movies ----

    [Fact]
    public void Movie_Parses_Shared_And_Radarr_Fields() {
        var movie = Load("movies.json", Json.ListMovie)[0];

        Assert.Equal(7, movie.Id);
        Assert.Equal("Dune: Part Two", movie.Title);
        Assert.Equal(2024, movie.Year);
        Assert.Equal("released", movie.Status);
        Assert.Equal("Legendary Pictures", movie.Studio);
        Assert.Equal("announced", movie.MinimumAvailability);
        Assert.True(movie.HasFile);
        Assert.True(movie.IsAvailable);
        Assert.Equal(693134, movie.TmdbId);
        Assert.Equal(167, movie.Runtime);
        Assert.Equal(new DateTimeOffset(2024, 4, 16, 0, 0, 0, TimeSpan.Zero), movie.DigitalRelease);
        Assert.Equal(new DateTimeOffset(2024, 5, 14, 0, 0, 0, TimeSpan.Zero), movie.PhysicalRelease);
        Assert.Equal(new DateTimeOffset(2024, 2, 27, 0, 0, 0, TimeSpan.Zero), movie.InCinemas);
    }

    [Fact]
    public void Movie_Size_Comes_From_Its_Own_Field_And_Matches_The_File() {
        var movie = Load("movies.json", Json.ListMovie)[0];

        Assert.Equal(18_000_000_000, movie.SizeOnDisk);
        Assert.Equal("Bluray-2160p", movie.MovieFile!.Quality!.Name);
        Assert.Equal(movie.SizeOnDisk, movie.MovieFile.Size);
        Assert.Equal("/MediaCover/7/poster.jpg?lastWrite=638", movie.Poster!.Url);
    }

    [Fact]
    public void An_Unreleased_Movie_Has_Null_Dates_And_No_File() {
        var movie = Load("movies.json", Json.ListMovie)[1];

        Assert.Equal("Not Out Yet", movie.Title);
        Assert.Null(movie.InCinemas);
        Assert.Null(movie.DigitalRelease);
        Assert.False(movie.HasFile);
        Assert.Null(movie.MovieFile);
        Assert.Equal(0, movie.SizeOnDisk);
    }

    [Fact]
    public void Series_And_Movie_Can_Be_Handled_Through_The_Shared_Base_Type() {
        MediaItem[] items = [Load("series.json", Json.ListSeries)[0], Load("movies.json", Json.ListMovie)[0]];

        Assert.Equal(["Severance", "Dune: Part Two"], items.Select(i => i.Title));
        Assert.Equal([15_000_000_000L, 18_000_000_000L], items.Select(i => i.SizeOnDisk));
        Assert.All(items, i => Assert.NotNull(i.Poster));
    }

    [Fact]
    public void A_Lookup_Result_Is_Not_In_The_Library() {
        var lookup = JsonSerializer.Deserialize("""{"title":"New Show","year":2025,"tvdbId":42,"images":[]}""", Json.Series)!;

        Assert.False(lookup.IsInLibrary); // no id yet
    }

    // ---- Lookups ----

    [Fact]
    public void Quality_Profiles_Root_Folders_And_Tags_Parse() {
        var profiles = Load("qualityprofiles.json", Json.ListQualityProfile);
        var roots = Load("rootfolders.json", Json.ListRootFolder);
        var tags = Load("tags.json", Json.ListTag);

        Assert.Equal([(1, "Any", true), (4, "HD-1080p", false)], profiles.Select(p => (p.Id, p.Name, p.UpgradeAllowed)));
        Assert.Equal("/tv/", roots[0].Path);
        Assert.True(roots[0].Accessible);
        Assert.Equal(500_000_000_000, roots[0].FreeSpace);
        Assert.False(roots[1].Accessible);
        Assert.Equal([(1, "4k"), (2, "kids")], tags.Select(t => (t.Id, t.Label)));
    }

    // ---- Releases ----

    [Fact]
    public void Sonarr_Release_Parses_Indexer_Quality_And_Torrent_Details() {
        var release = Load("releases-sonarr.json", Json.ListRelease)[0];

        Assert.Equal("Severance.S02E01.1080p.WEB.h264-GRP", release.Title);
        Assert.Equal("NZBgeek", release.Indexer);
        Assert.Equal(2_147_483_648, release.Size);
        Assert.Equal(3, release.Age);
        Assert.Equal("WEBDL-1080p", release.Quality!.Name);
        Assert.True(release.IsTorrent);
        Assert.Equal((120, 4), (release.Seeders, release.Leechers));
        Assert.Equal(25, release.CustomFormatScore);
        Assert.True(release.Approved);
        Assert.True(release.DownloadAllowed);
        Assert.Empty(release.Rejections);
        Assert.Equal((false, 2), (release.FullSeason, release.SeasonNumber));
    }

    [Fact]
    public void A_Rejected_Usenet_Release_Carries_Its_Reasons_And_Has_No_Seeders() {
        var release = Load("releases-sonarr.json", Json.ListRelease)[1];

        Assert.False(release.IsTorrent);
        Assert.Null(release.Seeders);
        Assert.True(release.Rejected);
        Assert.False(release.Approved);
        Assert.False(release.DownloadAllowed);
        Assert.Equal(["Quality SDTV is not wanted in profile", "Release is older than retention"], release.Rejections);
        Assert.True(release.FullSeason);
        Assert.Equal(-5, release.CustomFormatScore);
    }

    [Fact]
    public void Radarr_Release_Parses_With_The_Same_Shared_Type() {
        var release = Load("releases-radarr.json", Json.ListRelease)[0];

        Assert.Equal("Bluray-2160p", release.Quality!.Name);
        Assert.Equal(100, release.CustomFormatScore);
        Assert.Equal(40_000_000_000, release.Size);
        Assert.True(release.IsTorrent);
        Assert.Null(release.SeasonNumber); // Sonarr-only field, absent here
    }

    // ---- Queue ----

    [Fact]
    public void Sonarr_Queue_Parses_Progress_And_Warnings() {
        var page = Load("queue-sonarr.json", Json.QueuePage);

        Assert.Equal((1, 20, 2), (page.Page, page.PageSize, page.TotalRecords));
        var downloading = page.Records[0];
        Assert.Equal("downloading", downloading.Status);
        Assert.Equal(250_000_000, downloading.SizeLeft);
        Assert.Equal(75.0, downloading.ProgressPercent);
        Assert.Equal("qBittorrent", downloading.DownloadClient);
        Assert.Equal((12, 9002), (downloading.SeriesId, downloading.EpisodeId));
        Assert.Null(downloading.MovieId);
        Assert.Equal(new DateTimeOffset(2025, 3, 22, 20, 30, 0, TimeSpan.Zero), downloading.EstimatedCompletionTime);

        var warning = page.Records[1];
        Assert.Equal("warning", warning.TrackedDownloadStatus);
        Assert.Equal("The download is stalled", warning.ErrorMessage);
        Assert.Equal("No files found are eligible for import", Assert.Single(Assert.Single(warning.StatusMessages).Messages));
        Assert.Equal(0, warning.ProgressPercent);
    }

    [Fact]
    public void Radarr_Queue_Uses_Movie_Id_And_A_Finished_Item_Is_At_100_Percent() {
        var item = Load("queue-radarr.json", Json.QueuePage).Records[0];

        Assert.Equal(7, item.MovieId);
        Assert.Null(item.SeriesId);
        Assert.Equal(100.0, item.ProgressPercent);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1000, 1500, 0)] // more left than the total: clamp
    [InlineData(1000, -100, 100)]
    [InlineData(1000, 500, 50)]
    public void Progress_Is_Clamped_To_0_To_100(long size, double left, double expected) {
        Assert.Equal(expected, new QueueItem { Size = size, SizeLeft = left }.ProgressPercent);
    }
}
