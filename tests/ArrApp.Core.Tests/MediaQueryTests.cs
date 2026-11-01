using ArrApp.Core.Models;
using ArrApp.Core.Presentation;

namespace ArrApp.Core.Tests;

public class MediaQueryTests {
    private static Series Show(string title, int year = 2020, bool monitored = true, int files = 0, int episodes = 0,
        DateTimeOffset? added = null, long size = 0, string? sortTitle = null) => new() {
            Title = title, Year = year, Monitored = monitored, Added = added, SortTitle = sortTitle,
            Statistics = new SeriesStatistics { EpisodeFileCount = files, EpisodeCount = episodes, SizeOnDisk = size },
        };

    private static Movie Film(string title, int year = 2020, bool monitored = true, bool hasFile = false, bool available = true,
        DateTimeOffset? added = null, long size = 0, string? sortTitle = null) => new() {
            Title = title, Year = year, Monitored = monitored, HasFile = hasFile, IsAvailable = available, Added = added,
            SizeOnDiskBytes = size, SortTitle = sortTitle,
        };

    private static string[] Titles(IEnumerable<MediaItem> items) => items.Select(i => i.Title).ToArray();

    private static DateTimeOffset Day(int d) => new(2026, 1, d, 0, 0, 0, TimeSpan.Zero);

    // ---- Search ----

    [Fact]
    public void An_Empty_Query_Keeps_Everything_Sorted_By_Title() {
        MediaItem[] items = [Film("Zulu"), Film("Alien"), Film("Memento")];

        Assert.Equal(["Alien", "Memento", "Zulu"], Titles(MediaQueryEngine.Apply(items, new MediaQuery())));
    }

    [Theory]
    [InlineData("dune", new[] { "Dune: Part Two", "Dune" })]
    [InlineData("PART", new[] { "Dune: Part Two" })]
    [InlineData("  dune   two ", new[] { "Dune: Part Two" })]   // extra spaces and word order don't matter
    [InlineData("two dune", new[] { "Dune: Part Two" })]
    [InlineData("2024", new[] { "Dune: Part Two" })]            // the year is searchable too
    [InlineData("dune 2024", new[] { "Dune: Part Two" })]
    [InlineData("nothing like this", new string[0])]
    public void Search_Matches_Every_Word_In_The_Title_Or_Year(string search, string[] expected) {
        MediaItem[] items = [Film("Dune: Part Two", 2024), Film("Dune", 1984), Film("Alien", 1979)];

        var result = MediaQueryEngine.Apply(items, new MediaQuery(Search: search).SortedBy(MediaSortKey.Year));

        Assert.Equal(expected, Titles(result));
    }

    [Theory]
    [InlineData("ecoute")]
    [InlineData("ÉCOUTE")]
    [InlineData("écoute")]
    public void Search_Ignores_Case_And_Accents(string search) {
        MediaItem[] items = [Film("Écoute"), Film("Other")];

        Assert.Equal(["Écoute"], Titles(MediaQueryEngine.Apply(items, new MediaQuery(Search: search))));
    }

    [Fact]
    public void A_Year_Word_Only_Matches_Items_That_Have_A_Year() {
        MediaItem[] items = [Film("No Year", year: 0), Film("Has Year", year: 1999)];

        Assert.Equal(["Has Year"], Titles(MediaQueryEngine.Apply(items, new MediaQuery(Search: "1999"))));
        Assert.Empty(MediaQueryEngine.Apply(items, new MediaQuery(Search: "0")));
    }

    // ---- Sort ----

    [Fact]
    public void Title_Sort_Uses_The_Arrs_Sort_Title_So_The_Wire_Files_Under_W() {
        MediaItem[] items = [Show("The Wire", sortTitle: "wire"), Show("Breaking Bad", sortTitle: "breaking bad"), Show("Xena")];

        Assert.Equal(["Breaking Bad", "The Wire", "Xena"], Titles(MediaQueryEngine.Apply(items, new MediaQuery())));
        Assert.Equal(["Xena", "The Wire", "Breaking Bad"], Titles(MediaQueryEngine.Apply(items, new MediaQuery(Descending: true))));
    }

    [Fact]
    public void Title_Sort_Ignores_Case_And_Accents() {
        MediaItem[] items = [Film("zebra"), Film("Écran"), Film("apple"), Film("Eclair")];

        Assert.Equal(["apple", "Eclair", "Écran", "zebra"], Titles(MediaQueryEngine.Apply(items, new MediaQuery())));
    }

    [Fact]
    public void Date_Added_Sorts_Newest_First_By_Default_And_Never_Added_Goes_Last_Either_Way() {
        MediaItem[] items = [Film("Old", added: Day(1)), Film("Never"), Film("New", added: Day(20)), Film("Mid", added: Day(10))];
        var newestFirst = new MediaQuery().SortedBy(MediaSortKey.DateAdded);

        Assert.Equal(["New", "Mid", "Old", "Never"], Titles(MediaQueryEngine.Apply(items, newestFirst)));
        Assert.Equal(["Old", "Mid", "New", "Never"], Titles(MediaQueryEngine.Apply(items, newestFirst with { Descending = false })));
    }

    [Fact]
    public void Year_Sorts_Latest_First_By_Default_And_Unknown_Years_Go_Last_Either_Way() {
        MediaItem[] items = [Film("A", 1999), Film("B", 0), Film("C", 2024), Film("D", 2010)];
        var latestFirst = new MediaQuery().SortedBy(MediaSortKey.Year);

        Assert.Equal(["C", "D", "A", "B"], Titles(MediaQueryEngine.Apply(items, latestFirst)));
        Assert.Equal(["A", "D", "C", "B"], Titles(MediaQueryEngine.Apply(items, latestFirst with { Descending = false })));
    }

    [Fact]
    public void Size_Sorts_Biggest_First_By_Default_For_Series_And_Movies_Alike() {
        MediaItem[] items = [Film("Small", size: 100), Show("Big", size: 9_000), Film("Medium", size: 5_000), Show("Empty", size: 0)];

        var result = MediaQueryEngine.Apply(items, new MediaQuery().SortedBy(MediaSortKey.Size));

        Assert.Equal(["Big", "Medium", "Small", "Empty"], Titles(result));
    }

    [Fact]
    public void Ties_Fall_Back_To_Title_Order_In_Both_Directions() {
        MediaItem[] items = [Film("Charlie", 2020), Film("Alpha", 2020), Film("Bravo", 2020)];

        var up = MediaQueryEngine.Apply(items, new MediaQuery(SortKey: MediaSortKey.Year));
        var down = MediaQueryEngine.Apply(items, new MediaQuery(SortKey: MediaSortKey.Year, Descending: true));

        Assert.Equal(["Alpha", "Bravo", "Charlie"], Titles(up));
        Assert.Equal(["Alpha", "Bravo", "Charlie"], Titles(down)); // same year: title decides, regardless of direction
    }

    [Fact]
    public void The_Natural_Direction_Is_A_To_Z_But_Newest_Latest_And_Biggest_First() {
        Assert.False(MediaQuery.DefaultDescending(MediaSortKey.Title));
        Assert.True(MediaQuery.DefaultDescending(MediaSortKey.DateAdded));
        Assert.True(MediaQuery.DefaultDescending(MediaSortKey.Year));
        Assert.True(MediaQuery.DefaultDescending(MediaSortKey.Size));
    }

    [Fact]
    public void Applying_A_Query_Does_Not_Change_The_Input_And_Is_Repeatable() {
        List<MediaItem> items = [Film("B"), Film("A"), Film("C")];

        var first = MediaQueryEngine.Apply(items, new MediaQuery());
        var second = MediaQueryEngine.Apply(items, new MediaQuery());

        Assert.Equal(["B", "A", "C"], Titles(items));
        Assert.Equal(Titles(first), Titles(second));
    }

    // ---- Filters ----

    [Fact]
    public void Monitored_And_Unmonitored_Split_The_Library() {
        MediaItem[] items = [Film("On", monitored: true), Show("Off", monitored: false), Film("Also On")];

        Assert.Equal(["Also On", "On"], Titles(MediaQueryEngine.Apply(items, new MediaQuery(Filter: MediaFilter.Monitored))));
        Assert.Equal(["Off"], Titles(MediaQueryEngine.Apply(items, new MediaQuery(Filter: MediaFilter.Unmonitored))));
    }

    [Fact]
    public void Missing_Means_Something_Is_Still_To_Download() {
        MediaItem[] items = [
            Film("Wanted Movie"),                                   // out, monitored, no file: missing
            Film("Have Movie", hasFile: true),                      // downloaded
            Film("Not Out Yet", available: false),                  // can't be missing before release
            Film("Ignored Movie", monitored: false),                // unmonitored: not "missing"
            Show("Partial Show", files: 4, episodes: 12),           // some episodes missing
            Show("Empty Show", files: 0, episodes: 8),
            Show("Complete Show", files: 10, episodes: 10),
            Show("Brand New Show", files: 0, episodes: 0),          // nothing aired yet
        ];

        var missing = MediaQueryEngine.Apply(items, new MediaQuery(Filter: MediaFilter.Missing));

        Assert.Equal(["Empty Show", "Partial Show", "Wanted Movie"], Titles(missing));
    }

    [Fact]
    public void Downloaded_Means_Everything_That_Has_Aired_Is_Here() {
        MediaItem[] items = [
            Film("Have Movie", hasFile: true),
            Film("Wanted Movie"),
            Show("Complete Show", files: 10, episodes: 10),
            Show("Partial Show", files: 4, episodes: 12),
            Show("Brand New Show", files: 0, episodes: 0),
        ];

        var downloaded = MediaQueryEngine.Apply(items, new MediaQuery(Filter: MediaFilter.Downloaded));

        Assert.Equal(["Complete Show", "Have Movie"], Titles(downloaded));
    }

    [Fact]
    public void A_Series_Without_Statistics_Is_Neither_Downloaded_Nor_Missing() {
        MediaItem[] items = [new Series { Title = "Bare" }];

        Assert.Empty(MediaQueryEngine.Apply(items, new MediaQuery(Filter: MediaFilter.Downloaded)));
        Assert.Empty(MediaQueryEngine.Apply(items, new MediaQuery(Filter: MediaFilter.Missing)));
        Assert.Single(MediaQueryEngine.Apply(items, new MediaQuery(Filter: MediaFilter.All)));
    }

    [Fact]
    public void Search_Filter_And_Sort_Work_Together() {
        MediaItem[] items = [
            Film("Dune", 1984, hasFile: true, size: 4_000),
            Film("Dune: Part Two", 2024, hasFile: true, size: 18_000),
            Film("Dune Messiah", 2027, available: false),
            Film("Alien", 1979, hasFile: true, size: 2_000),
        ];

        var query = new MediaQuery(Search: "dune", Filter: MediaFilter.Downloaded).SortedBy(MediaSortKey.Size);

        Assert.Equal(["Dune: Part Two", "Dune"], Titles(MediaQueryEngine.Apply(items, query)));
    }

    [Fact]
    public void Filter_Counts_Match_What_Each_Filter_Keeps() {
        MediaItem[] items = [Film("A", hasFile: true), Film("B"), Show("C", monitored: false, files: 1, episodes: 5)];

        Assert.Equal(3, MediaQueryEngine.Count(items, MediaFilter.All));
        Assert.Equal(2, MediaQueryEngine.Count(items, MediaFilter.Monitored));
        Assert.Equal(1, MediaQueryEngine.Count(items, MediaFilter.Unmonitored));
        Assert.Equal(2, MediaQueryEngine.Count(items, MediaFilter.Missing));
        Assert.Equal(1, MediaQueryEngine.Count(items, MediaFilter.Downloaded));
    }

    // ---- Filters and badges must tell the same story ----

    [Fact]
    public void The_Filters_Agree_With_The_Badge_Shown_On_The_Card() {
        MediaItem[] items = [
            Film("m1", hasFile: true), Film("m2"), Film("m3", available: false), Film("m4", monitored: false),
            Film("m5", hasFile: true, available: false),
            Show("s1", files: 10, episodes: 10), Show("s2", files: 4, episodes: 12), Show("s3", files: 0, episodes: 8),
            Show("s4"), Show("s5", files: 12, episodes: 10),
        ];

        foreach (var item in items) {
            var badge = MediaBadge.For(item);

            Assert.Equal(badge.Kind == BadgeKind.Success, MediaState.IsDownloaded(item));
            Assert.Equal(badge.Kind is BadgeKind.Danger or BadgeKind.Warning, MediaState.HasMissing(item));
        }
    }
}

public class MediaSortTextTests {
    [Theory]
    [InlineData(MediaSortKey.Title, false, "A to Z")]
    [InlineData(MediaSortKey.Title, true, "Z to A")]
    [InlineData(MediaSortKey.DateAdded, true, "Newest first")]
    [InlineData(MediaSortKey.DateAdded, false, "Oldest first")]
    [InlineData(MediaSortKey.Year, true, "Latest first")]
    [InlineData(MediaSortKey.Year, false, "Earliest first")]
    [InlineData(MediaSortKey.Size, true, "Largest first")]
    [InlineData(MediaSortKey.Size, false, "Smallest first")]
    public void Directions_Are_Described_In_Words(MediaSortKey key, bool descending, string expected)
        => Assert.Equal(expected, MediaSortText.Direction(key, descending));

    [Fact]
    public void Every_Sort_And_Filter_Has_A_Label() {
        foreach (var key in Enum.GetValues<MediaSortKey>())
            Assert.False(string.IsNullOrWhiteSpace(MediaSortText.Name(key)));
        foreach (var filter in Enum.GetValues<MediaFilter>())
            Assert.False(string.IsNullOrWhiteSpace(MediaSortText.Filter(filter)));
    }
}
