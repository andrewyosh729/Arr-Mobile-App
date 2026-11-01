using System.Text.Json;
using ArrApp.Core.Api;
using ArrApp.Core.Models;
using ArrApp.Core.Presentation;

namespace ArrApp.Core.Tests;

public class MediaBadgeTests {
    private static Series SeriesWith(int files, int episodes)
        => new() { Statistics = new SeriesStatistics { EpisodeFileCount = files, EpisodeCount = episodes } };

    private static Movie Film(bool hasFile = false, bool available = true, bool monitored = true)
        => new() { HasFile = hasFile, IsAvailable = available, Monitored = monitored };

    // ---- Series ----

    [Fact]
    public void A_Complete_Series_Is_Green_And_Full() {
        var badge = MediaBadge.For(SeriesWith(10, 10));

        Assert.Equal(("10/10 eps", BadgeKind.Success, 1.0), (badge.Text, badge.Kind, badge.Progress));
    }

    [Fact]
    public void A_Partly_Downloaded_Series_Is_A_Warning_With_Proportional_Progress() {
        var badge = MediaBadge.For(SeriesWith(4, 12));

        Assert.Equal("4/12 eps", badge.Text);
        Assert.Equal(BadgeKind.Warning, badge.Kind);
        Assert.Equal(4.0 / 12, badge.Progress!.Value, 6);
    }

    [Fact]
    public void A_Series_With_Nothing_Downloaded_Is_Red_At_Zero() {
        var badge = MediaBadge.For(SeriesWith(0, 8));

        Assert.Equal(("0/8 eps", BadgeKind.Danger, 0.0), (badge.Text, badge.Kind, badge.Progress));
    }

    [Fact]
    public void More_Files_Than_Episodes_Still_Reads_As_Complete_And_Never_Exceeds_100_Percent() {
        var badge = MediaBadge.For(SeriesWith(12, 10));

        Assert.Equal(BadgeKind.Success, badge.Kind);
        Assert.Equal(1.0, badge.Progress);
    }

    [Fact]
    public void A_Series_Without_Aired_Episodes_Or_Statistics_Is_Neutral_With_No_Progress_Bar() {
        foreach (var series in new[] { SeriesWith(0, 0), new Series() }) {
            var badge = MediaBadge.For(series);

            Assert.Equal(("No episodes", BadgeKind.Neutral), (badge.Text, badge.Kind));
            Assert.Null(badge.Progress);
        }
    }

    // ---- Movies ----

    [Fact]
    public void A_Downloaded_Movie_Is_Green_Even_If_Flags_Say_Otherwise() {
        Assert.Equal(("Downloaded", BadgeKind.Success), Pair(MediaBadge.For(Film(hasFile: true))));
        Assert.Equal(("Downloaded", BadgeKind.Success), Pair(MediaBadge.For(Film(hasFile: true, available: false, monitored: false))));
    }

    [Fact]
    public void An_Unreleased_Movie_Is_Neutral_Whether_Or_Not_It_Is_Monitored() {
        Assert.Equal(("Unreleased", BadgeKind.Neutral), Pair(MediaBadge.For(Film(available: false))));
        Assert.Equal(("Unreleased", BadgeKind.Neutral), Pair(MediaBadge.For(Film(available: false, monitored: false))));
    }

    [Fact]
    public void An_Available_Monitored_Movie_Without_A_File_Is_Missing() {
        Assert.Equal(("Missing", BadgeKind.Danger), Pair(MediaBadge.For(Film())));
    }

    [Fact]
    public void An_Available_Unmonitored_Movie_Without_A_File_Is_Not_Called_Missing() {
        Assert.Equal(("Unmonitored", BadgeKind.Neutral), Pair(MediaBadge.For(Film(monitored: false))));
    }

    [Fact]
    public void Movies_Have_No_Progress_Bar() {
        Assert.Null(MediaBadge.For(Film(hasFile: true)).Progress);
        Assert.Null(MediaBadge.For(Film()).Progress);
    }

    // ---- Same results from real-shaped JSON ----

    private static T FirstFrom<T>(string file, System.Text.Json.Serialization.Metadata.JsonTypeInfo<List<T>> info)
        => JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", file)), info)![0];

    private static T SecondFrom<T>(string file, System.Text.Json.Serialization.Metadata.JsonTypeInfo<List<T>> info)
        => JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", file)), info)![1];

    [Fact]
    public void The_Parsed_Fixtures_Get_The_Expected_Badges() {
        var series = FirstFrom("series.json", ArrJsonContext.Default.ListSeries);
        var barely = SecondFrom("series.json", ArrJsonContext.Default.ListSeries);
        var dune = FirstFrom("movies.json", ArrJsonContext.Default.ListMovie);
        var notYet = SecondFrom("movies.json", ArrJsonContext.Default.ListMovie);

        Assert.Equal(("15/19 eps", BadgeKind.Warning), Pair(MediaBadge.For(series)));
        Assert.Equal(("No episodes", BadgeKind.Neutral), Pair(MediaBadge.For(barely)));
        Assert.Equal(("Downloaded", BadgeKind.Success), Pair(MediaBadge.For(dune)));
        Assert.Equal(("Unreleased", BadgeKind.Neutral), Pair(MediaBadge.For(notYet)));
    }

    // ---- Status wording ----

    [Theory]
    [InlineData("continuing", "Continuing")]
    [InlineData("ended", "Ended")]
    [InlineData("released", "Released")]
    [InlineData("inCinemas", "In cinemas")]
    [InlineData("announced", "Announced")]
    [InlineData("somethingNew", "SomethingNew")] // unknown values are shown, just capitalised
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Raw_Status_Values_Get_Readable_Labels(string? raw, string expected)
        => Assert.Equal(expected, MediaBadge.StatusLabel(raw));

    private static (string Text, BadgeKind Kind) Pair(MediaBadge badge) => (badge.Text, badge.Kind);
}
