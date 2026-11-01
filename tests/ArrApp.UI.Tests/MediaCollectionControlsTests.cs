using ArrApp.Core;
using ArrApp.Core.Api;
using ArrApp.Core.Models;
using ArrApp.Core.Presentation;
using ArrApp.UI.Errors;
using ArrApp.UI.ViewModels;

namespace ArrApp.UI.Tests;

/// <summary>Search box, sort chip and filter chips of the collection view.</summary>
public class MediaCollectionControlsTests {
    private sealed class FixedClient(IReadOnlyList<MediaItem> library) : IArrClient {
        public ArrType Type => ArrType.Radarr;
        public Task<IReadOnlyList<MediaItem>> GetLibraryAsync(CancellationToken cancellationToken = default) => Task.FromResult(library);
        public Task<MediaItem> GetItemAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<QualityProfile>> GetQualityProfilesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RootFolder>> GetRootFoldersAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Tag>> GetTagsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FixedClients(Func<ServerConnection, IReadOnlyList<MediaItem>> library) : IArrClientFactory {
        public IArrClient Create(ServerConnection server) => new FixedClient(library(server));
        public ISonarrClient CreateSonarr(ServerConnection server) => throw new NotSupportedException();
        public IRadarrClient CreateRadarr(ServerConnection server) => throw new NotSupportedException();
    }

    private static readonly IReadOnlyList<MediaItem> Films = [
        new Movie { Id = 1, Title = "Alien", Year = 1979, Monitored = true, HasFile = true, IsAvailable = true, SizeOnDiskBytes = 2_000, Added = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero) },
        new Movie { Id = 2, Title = "Dune", Year = 1984, Monitored = true, HasFile = false, IsAvailable = true, Added = new DateTimeOffset(2026, 1, 20, 0, 0, 0, TimeSpan.Zero) },
        new Movie { Id = 3, Title = "Dune: Part Two", Year = 2024, Monitored = true, HasFile = true, IsAvailable = true, SizeOnDiskBytes = 18_000, Added = new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero) },
        new Movie { Id = 4, Title = "Heat", Year = 1995, Monitored = false, HasFile = false, IsAvailable = true, Added = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) },
        new Movie { Id = 5, Title = "Zardoz", Year = 1974, Monitored = true, HasFile = false, IsAvailable = false },
    ];

    private sealed class Fixture {
        public ArrContext Context { get; }
        public MemorySettings Settings { get; } = new();
        public ArrApp.UI.Navigation.BottomSheetService Sheets { get; } = TestSheets.Create();
        public MediaCollectionViewModel Vm { get; }

        public Fixture(Func<ServerConnection, IReadOnlyList<MediaItem>>? library = null, TimeSpan? searchDelay = null) {
            Context = new ArrContext(new MemoryRepository(), Settings);
            Vm = new MediaCollectionViewModel(Context, new FixedClients(library ?? (_ => Films)), Settings, new ToastService(), Sheets,
                searchDelay ?? TimeSpan.Zero);
        }

        public async Task<Fixture> LoadAsync(int id = 1) {
            Context.SetActive(new ServerConnection { Id = id, Name = $"s{id}", ArrType = ArrType.Radarr, BaseUrl = "http://x", ApiKey = "K" });
            await Vm.WhenIdleAsync();
            return this;
        }
    }

    private static string[] Titles(MediaCollectionViewModel vm) => vm.Items.Select(i => i.Title).ToArray();

    // ---- Search ----

    [Fact]
    public async Task Typing_In_The_Search_Box_Narrows_The_List() {
        var f = await new Fixture().LoadAsync();

        f.Vm.SearchText = "dune";
        await f.Vm.WhenSearchAppliedAsync();

        Assert.Equal(["Dune", "Dune: Part Two"], Titles(f.Vm));
        Assert.True(f.Vm.IsNarrowed);
        Assert.Equal(5, f.Vm.TotalCount); // the library itself is unchanged
    }

    [Fact]
    public async Task Search_Text_Is_Trimmed_And_Clearing_It_Restores_Everything() {
        var f = await new Fixture().LoadAsync();

        f.Vm.SearchText = "  alien ";
        await f.Vm.WhenSearchAppliedAsync();
        Assert.Equal(["Alien"], Titles(f.Vm));

        f.Vm.SearchText = "";
        await f.Vm.WhenSearchAppliedAsync();
        Assert.Equal(5, f.Vm.Items.Count);
        Assert.False(f.Vm.IsNarrowed);
    }

    [Fact]
    public async Task Typing_Quickly_Applies_Only_The_Final_Text_After_A_Pause() {
        var f = await new Fixture(searchDelay: TimeSpan.FromMilliseconds(120)).LoadAsync();
        var applied = new List<string>();
        f.Vm.PropertyChanged += (_, e) => {
            if (e.PropertyName == nameof(MediaCollectionViewModel.IsNarrowed))
                applied.Add(f.Vm.Query.Search);
        };

        f.Vm.SearchText = "d";
        f.Vm.SearchText = "du";
        f.Vm.SearchText = "dune";
        Assert.Equal(5, f.Vm.Items.Count); // nothing applied yet: still waiting for typing to pause
        await f.Vm.WhenSearchAppliedAsync();

        Assert.Equal(["Dune", "Dune: Part Two"], Titles(f.Vm));
        Assert.Equal("dune", f.Vm.Query.Search);
    }

    [Fact]
    public async Task The_Placeholder_Says_What_Is_Being_Searched() {
        var f = await new Fixture().LoadAsync();

        Assert.Equal("Search 5 movies", f.Vm.SearchPlaceholder);
        Assert.True(f.Vm.HasItems);
    }

    [Fact]
    public async Task Setting_The_Query_Directly_Updates_The_Search_Box_Without_Searching_Twice() {
        var f = await new Fixture().LoadAsync();

        f.Vm.Query = new MediaQuery(Search: "heat");

        Assert.Equal("heat", f.Vm.SearchText);
        Assert.Equal(["Heat"], Titles(f.Vm));
    }

    // ---- Filters ----

    [Fact]
    public async Task Filter_Chips_Show_Counts_For_The_Whole_Library() {
        var f = await new Fixture().LoadAsync();

        var counts = f.Vm.FilterChips.ToDictionary(c => c.Filter, c => c.Count);

        Assert.Equal(5, counts[MediaFilter.All]);
        Assert.Equal(4, counts[MediaFilter.Monitored]);
        Assert.Equal(1, counts[MediaFilter.Unmonitored]);
        Assert.Equal(1, counts[MediaFilter.Missing]);      // Dune: out, monitored, no file
        Assert.Equal(2, counts[MediaFilter.Downloaded]);
        Assert.Equal("Missing 1", f.Vm.FilterChips.Single(c => c.Filter == MediaFilter.Missing).Text);
    }

    [Fact]
    public async Task Counts_Do_Not_Change_While_Searching() {
        var f = await new Fixture().LoadAsync();

        f.Vm.SearchText = "dune";
        await f.Vm.WhenSearchAppliedAsync();

        Assert.Equal(5, f.Vm.FilterChips.Single(c => c.Filter == MediaFilter.All).Count);
    }

    [Fact]
    public async Task Choosing_A_Filter_Keeps_Only_Matching_Items_And_Selects_Its_Chip() {
        var f = await new Fixture().LoadAsync();
        var missing = f.Vm.FilterChips.Single(c => c.Filter == MediaFilter.Missing);

        f.Vm.SetFilterCommand.Execute(missing);

        Assert.Equal(["Dune"], Titles(f.Vm));
        Assert.True(missing.IsSelected);
        Assert.Equal([MediaFilter.Missing], f.Vm.FilterChips.Where(c => c.IsSelected).Select(c => c.Filter));
        Assert.True(f.Vm.IsNarrowed);
    }

    [Fact]
    public async Task A_Search_And_A_Filter_Combine() {
        var f = await new Fixture().LoadAsync();

        f.Vm.SetFilterCommand.Execute(f.Vm.FilterChips.Single(c => c.Filter == MediaFilter.Downloaded));
        f.Vm.SearchText = "dune";
        await f.Vm.WhenSearchAppliedAsync();

        Assert.Equal(["Dune: Part Two"], Titles(f.Vm));
    }

    [Fact]
    public async Task When_Nothing_Matches_The_Library_Is_Not_Called_Empty_And_Clear_Filters_Brings_It_Back() {
        var f = await new Fixture().LoadAsync();
        f.Vm.SearchText = "zzz";
        await f.Vm.WhenSearchAppliedAsync();
        f.Vm.SetFilterCommand.Execute(f.Vm.FilterChips.Single(c => c.Filter == MediaFilter.Monitored));

        Assert.True(f.Vm.NoMatches);
        Assert.False(f.Vm.IsEmpty);

        f.Vm.ClearFiltersCommand.Execute(null);

        Assert.False(f.Vm.NoMatches);
        Assert.Equal(5, f.Vm.Items.Count);
        Assert.Equal("", f.Vm.SearchText);
        Assert.True(f.Vm.FilterChips.Single(c => c.Filter == MediaFilter.All).IsSelected);
        Assert.False(f.Vm.IsNarrowed);
    }

    [Fact]
    public async Task Clear_Filters_Keeps_The_Chosen_Sort() {
        var f = await new Fixture().LoadAsync();
        f.Vm.Query = new MediaQuery(Search: "dune", Filter: MediaFilter.Downloaded).SortedBy(MediaSortKey.Size);

        f.Vm.ClearFiltersCommand.Execute(null);

        Assert.Equal(MediaSortKey.Size, f.Vm.Query.SortKey);
        Assert.Equal(["Dune: Part Two", "Alien", "Dune", "Heat", "Zardoz"], Titles(f.Vm));
    }

    // ---- Sort ----

    [Fact]
    public async Task The_Sort_Chip_Says_How_The_List_Is_Ordered() {
        var f = await new Fixture().LoadAsync();
        Assert.Equal("Title · A to Z", f.Vm.SortChipText);

        f.Vm.Query = f.Vm.Query.SortedBy(MediaSortKey.DateAdded);

        Assert.Equal("Date added · Newest first", f.Vm.SortChipText);
    }

    [Fact]
    public async Task Tapping_The_Sort_Chip_Opens_A_Sheet_With_The_Four_Options_And_The_Current_One_Marked() {
        var f = await new Fixture().LoadAsync();

        f.Vm.ShowSortCommand.Execute(null);

        var picker = Assert.IsType<SortPickerViewModel>(f.Sheets.Current);
        Assert.Equal(["Title", "Date added", "Year", "Size on disk"], picker.Options.Select(o => o.Name));
        Assert.Equal([true, false, false, false], picker.Options.Select(o => o.IsSelected));
        Assert.Equal(["A to Z", "Newest first", "Latest first", "Largest first"], picker.Options.Select(o => o.Detail));
        Assert.Equal("Tap again to reverse", picker.Options[0].Hint);
        Assert.Equal("", picker.Options[1].Hint);
    }

    [Fact]
    public async Task Picking_Another_Sort_Uses_Its_Natural_Direction_And_Closes_The_Sheet() {
        var f = await new Fixture().LoadAsync();
        f.Vm.ShowSortCommand.Execute(null);
        var picker = (SortPickerViewModel)f.Sheets.Current!;

        picker.PickCommand.Execute(picker.Options.Single(o => o.Key == MediaSortKey.Size));

        Assert.False(f.Sheets.IsOpen);
        Assert.Equal((MediaSortKey.Size, true), (f.Vm.Query.SortKey, f.Vm.Query.Descending));
        Assert.Equal(["Dune: Part Two", "Alien"], Titles(f.Vm)[..2]); // biggest first
    }

    [Fact]
    public async Task Picking_The_Current_Sort_Again_Reverses_It() {
        var f = await new Fixture().LoadAsync();

        f.Vm.ShowSortCommand.Execute(null);
        ((SortPickerViewModel)f.Sheets.Current!).PickCommand.Execute(((SortPickerViewModel)f.Sheets.Current!).Options[0]);

        Assert.True(f.Vm.Query.Descending);
        Assert.Equal(["Zardoz", "Heat", "Dune: Part Two", "Dune", "Alien"], Titles(f.Vm));
        Assert.Equal("Title · Z to A", f.Vm.SortChipText);

        f.Vm.ShowSortCommand.Execute(null);
        var picker = (SortPickerViewModel)f.Sheets.Current!;
        Assert.Equal("Z to A", picker.Options[0].Detail); // the sheet shows the current direction
        picker.PickCommand.Execute(picker.Options[0]);
        Assert.False(f.Vm.Query.Descending);
    }

    [Fact]
    public async Task Sorting_Keeps_The_Search_And_Filter() {
        var f = await new Fixture().LoadAsync();
        f.Vm.SearchText = "dune";
        await f.Vm.WhenSearchAppliedAsync();

        f.Vm.ShowSortCommand.Execute(null);
        var picker = (SortPickerViewModel)f.Sheets.Current!;
        picker.PickCommand.Execute(picker.Options.Single(o => o.Key == MediaSortKey.Year));

        Assert.Equal(["Dune: Part Two", "Dune"], Titles(f.Vm)); // latest first, still only the Dune films
        Assert.Equal("dune", f.Vm.Query.Search);
    }

    // ---- Server switch ----

    [Fact]
    public async Task Switching_Server_Starts_From_A_Clean_Search_Filter_And_Sort() {
        var f = await new Fixture().LoadAsync(1);
        f.Vm.SearchText = "dune";
        await f.Vm.WhenSearchAppliedAsync();
        f.Vm.SetFilterCommand.Execute(f.Vm.FilterChips.Single(c => c.Filter == MediaFilter.Downloaded));
        f.Vm.Query = f.Vm.Query.SortedBy(MediaSortKey.Size);

        await f.LoadAsync(2);

        Assert.Equal("", f.Vm.SearchText);
        Assert.Equal(new MediaQuery(), f.Vm.Query);
        Assert.Equal(5, f.Vm.Items.Count);
        Assert.True(f.Vm.FilterChips.Single(c => c.Filter == MediaFilter.All).IsSelected);
        Assert.Equal("Title · A to Z", f.Vm.SortChipText);
    }

    [Fact]
    public async Task A_Refresh_Keeps_The_Current_Search_And_Filter() {
        var f = await new Fixture().LoadAsync();
        f.Vm.SearchText = "dune";
        await f.Vm.WhenSearchAppliedAsync();

        await f.Vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(["Dune", "Dune: Part Two"], Titles(f.Vm));
    }
}
