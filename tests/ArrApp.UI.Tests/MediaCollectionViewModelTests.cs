using ArrApp.Core;
using ArrApp.Core.Api;
using ArrApp.Core.Models;
using ArrApp.Core.Presentation;
using ArrApp.UI.Errors;
using ArrApp.UI.ViewModels;

namespace ArrApp.UI.Tests;

public class MediaCollectionViewModelTests {
    private sealed class FakeClient(Func<CancellationToken, Task<IReadOnlyList<MediaItem>>> library) : IArrClient {
        public ArrType Type => ArrType.Sonarr;
        public Task<IReadOnlyList<MediaItem>> GetLibraryAsync(CancellationToken cancellationToken = default) => library(cancellationToken);
        public Task<MediaItem> GetItemAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<QualityProfile>> GetQualityProfilesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RootFolder>> GetRootFoldersAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Tag>> GetTagsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeClients(Func<ServerConnection, IArrClient> create) : IArrClientFactory {
        public int Created { get; private set; }

        public IArrClient Create(ServerConnection server) {
            Created++;
            return create(server);
        }

        public ISonarrClient CreateSonarr(ServerConnection server) => throw new NotSupportedException();
        public IRadarrClient CreateRadarr(ServerConnection server) => throw new NotSupportedException();
    }

    private static ServerConnection Server(int id, string name, ArrType type = ArrType.Sonarr)
        => new() { Id = id, Name = name, ArrType = type, BaseUrl = $"http://{name}", ApiKey = "K" };

    private static IReadOnlyList<MediaItem> Library(params string[] titles)
        => titles.Select((t, i) => (MediaItem)new Series { Id = i + 1, Title = t, Year = 2020, Monitored = true }).ToList();

    private sealed class Fixture {
        public MemorySettings Settings { get; } = new();
        public ToastService Toasts { get; } = new();
        public ArrContext Context { get; }
        public FakeClients Clients { get; }
        public MediaCollectionViewModel Vm { get; }

        public Fixture(Func<ServerConnection, IArrClient> create) {
            Context = new ArrContext(new MemoryRepository(), Settings);
            Clients = new FakeClients(create);
            Vm = new MediaCollectionViewModel(Context, Clients, Settings, Toasts);
        }

        public static Fixture With(params string[] titles) => new(_ => new FakeClient(_ => Task.FromResult(Library(titles))));

        public async Task ActivateAsync(ServerConnection server) {
            Context.SetActive(server);
            await Vm.WhenIdleAsync();
        }
    }

    private static string[] Titles(MediaCollectionViewModel vm) => vm.Items.Select(i => i.Title).ToArray();

    [Fact]
    public async Task Nothing_Loads_Until_A_Server_Is_Active() {
        var f = Fixture.With("A");

        Assert.Empty(f.Vm.Items);
        Assert.Equal(0, f.Clients.Created);
        Assert.False(f.Vm.IsLoading);
        Assert.False(f.Vm.IsEmpty); // no server is not the same as an empty library
        await f.Vm.WhenIdleAsync();
    }

    [Fact]
    public async Task The_Library_Loads_When_A_Server_Becomes_Active_Sorted_By_Title() {
        var f = Fixture.With("Zulu", "Alpha", "Mike");

        await f.ActivateAsync(Server(1, "home"));

        Assert.Equal(["Alpha", "Mike", "Zulu"], Titles(f.Vm));
        Assert.Equal(3, f.Vm.TotalCount);
        Assert.False(f.Vm.IsLoading);
        Assert.True(f.Vm.ShowContent);
        Assert.False(f.Vm.IsEmpty);
        Assert.Null(f.Vm.ErrorMessage);
    }

    [Fact]
    public async Task While_The_First_Load_Runs_It_Shows_Loading_And_No_Content() {
        var gate = new TaskCompletionSource<IReadOnlyList<MediaItem>>();
        var f = new Fixture(_ => new FakeClient(_ => gate.Task));

        f.Context.SetActive(Server(1, "home"));
        await Task.Yield();

        Assert.True(f.Vm.IsLoading);
        Assert.False(f.Vm.ShowContent);
        Assert.False(f.Vm.IsEmpty);

        gate.SetResult(Library("A"));
        await f.Vm.WhenIdleAsync();

        Assert.False(f.Vm.IsLoading);
        Assert.True(f.Vm.ShowContent);
    }

    [Fact]
    public async Task An_Empty_Library_Is_Reported_As_Empty_Not_As_An_Error() {
        var f = Fixture.With();

        await f.ActivateAsync(Server(1, "home"));

        Assert.True(f.Vm.IsEmpty);
        Assert.False(f.Vm.ShowContent);
        Assert.False(f.Vm.HasError);
        Assert.False(f.Vm.NoMatches);
    }

    [Fact]
    public async Task A_Failed_First_Load_Shows_The_Message_And_Retry_Loads_It() {
        var attempts = 0;
        var f = new Fixture(_ => new FakeClient(_ => ++attempts == 1
            ? Task.FromException<IReadOnlyList<MediaItem>>(ArrApiException.For(ArrErrorKind.Unreachable))
            : Task.FromResult(Library("A", "B"))));

        await f.ActivateAsync(Server(1, "home"));

        Assert.True(f.Vm.HasError);
        Assert.Contains("reach", f.Vm.ErrorMessage);
        Assert.False(f.Vm.ShowContent);
        Assert.False(f.Vm.IsEmpty);
        Assert.False(f.Vm.IsLoading);

        await f.Vm.RetryCommand.ExecuteAsync(null);

        Assert.False(f.Vm.HasError);
        Assert.Equal(["A", "B"], Titles(f.Vm));
    }

    [Fact]
    public async Task A_Failed_Refresh_Keeps_The_Items_On_Screen_And_Tells_The_User_With_A_Toast() {
        var fail = false;
        var f = new Fixture(_ => new FakeClient(_ => fail
            ? Task.FromException<IReadOnlyList<MediaItem>>(ArrApiException.For(ArrErrorKind.Timeout))
            : Task.FromResult(Library("A", "B"))));
        await f.ActivateAsync(Server(1, "home"));
        fail = true;

        await f.Vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(["A", "B"], Titles(f.Vm));
        Assert.False(f.Vm.HasError);
        Assert.False(f.Vm.IsRefreshing);
        Assert.True(f.Toasts.Current!.IsError);
    }

    [Fact]
    public async Task A_Successful_Refresh_Replaces_The_Items_And_Does_Not_Flash_The_Loading_State() {
        var titles = new[] { "A" };
        var f = new Fixture(_ => new FakeClient(_ => Task.FromResult(Library(titles))));
        await f.ActivateAsync(Server(1, "home"));
        var sawLoading = false;
        f.Vm.PropertyChanged += (_, e) => sawLoading |= e.PropertyName == nameof(MediaCollectionViewModel.IsLoading) && f.Vm.IsLoading;
        titles = ["A", "B", "C"];

        await f.Vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(3, f.Vm.Items.Count);
        Assert.False(sawLoading);
        Assert.False(f.Vm.IsRefreshing);
    }

    [Fact]
    public async Task Switching_Server_Loads_The_New_Library_And_Drops_The_Old_Items_Immediately() {
        var f = new Fixture(server => new FakeClient(_ => Task.FromResult(server.Name == "home" ? Library("Show") : Library("Film 1", "Film 2"))));
        await f.ActivateAsync(Server(1, "home"));
        Assert.Equal(["Show"], Titles(f.Vm));

        await f.ActivateAsync(Server(2, "movies", ArrType.Radarr));

        Assert.Equal(["Film 1", "Film 2"], Titles(f.Vm));
        Assert.Equal("Movies", f.Vm.Title);
    }

    [Fact]
    public async Task A_Slow_Result_For_A_Server_The_User_Already_Left_Is_Discarded() {
        var slow = new TaskCompletionSource<IReadOnlyList<MediaItem>>();
        var f = new Fixture(server => new FakeClient(_ => server.Name == "slow" ? slow.Task : Task.FromResult(Library("Fast"))));

        f.Context.SetActive(Server(1, "slow"));
        await Task.Yield();
        await f.ActivateAsync(Server(2, "fast"));
        slow.SetResult(Library("Stale 1", "Stale 2"));
        await Task.Delay(50);

        Assert.Equal(["Fast"], Titles(f.Vm));
        Assert.False(f.Vm.IsLoading);
    }

    [Fact]
    public async Task Losing_The_Active_Server_Clears_The_Screen() {
        var f = Fixture.With("A");
        await f.ActivateAsync(Server(1, "home"));

        await f.ActivateAsync(null!);

        Assert.Empty(f.Vm.Items);
        Assert.False(f.Vm.IsEmpty);
        Assert.False(f.Vm.IsLoading);
    }

    // ---- Layout ----

    [Fact]
    public async Task The_Layout_Defaults_To_Grid_And_Toggling_Is_Remembered() {
        var f = Fixture.With("A");
        await f.ActivateAsync(Server(1, "home"));
        Assert.True(f.Vm.IsGrid);
        Assert.False(f.Vm.IsList);

        f.Vm.ToggleLayoutCommand.Execute(null);
        await Task.Delay(30);

        Assert.True(f.Vm.IsList);
        Assert.Equal("0", f.Settings.Values[MediaCollectionViewModel.GridLayoutSettingKey]);
    }

    [Fact]
    public async Task A_Saved_List_Layout_Is_Restored_On_The_Next_Launch() {
        var settings = new MemorySettings();
        settings.Values[MediaCollectionViewModel.GridLayoutSettingKey] = "0";
        var context = new ArrContext(new MemoryRepository(), settings);
        var vm = new MediaCollectionViewModel(context, new FakeClients(_ => new FakeClient(_ => Task.FromResult(Library("A")))), settings, new ToastService());

        context.SetActive(Server(1, "home"));
        await vm.WhenIdleAsync();

        Assert.True(vm.IsList);
        Assert.Equal("0", settings.Values[MediaCollectionViewModel.GridLayoutSettingKey]); // restoring doesn't rewrite it
    }

    // ---- Query ----

    [Fact]
    public async Task Changing_The_Query_Reorders_And_Filters_Without_Reloading() {
        var f = new Fixture(_ => new FakeClient(_ => Task.FromResult<IReadOnlyList<MediaItem>>([
            new Series { Id = 1, Title = "Alpha", Year = 2001, Monitored = true },
            new Series { Id = 2, Title = "Bravo", Year = 2020, Monitored = false },
            new Series { Id = 3, Title = "Charlie", Year = 2010, Monitored = true },
        ])));
        await f.ActivateAsync(Server(1, "home"));

        f.Vm.Query = new MediaQuery().SortedBy(MediaSortKey.Year);
        Assert.Equal(["Bravo", "Charlie", "Alpha"], Titles(f.Vm));

        f.Vm.Query = new MediaQuery(Filter: MediaFilter.Monitored);
        Assert.Equal(["Alpha", "Charlie"], Titles(f.Vm));

        f.Vm.Query = new MediaQuery(Search: "zzz");
        Assert.Empty(f.Vm.Items);
        Assert.True(f.Vm.NoMatches);
        Assert.False(f.Vm.IsEmpty);   // the library has items; the search just matches none
        Assert.True(f.Vm.ShowContent);
        Assert.Equal(3, f.Vm.TotalCount);
        Assert.Equal(1, f.Clients.Created);
    }
}

public class MediaCollectionRowsTests {
    private sealed class OneShotClient(IReadOnlyList<MediaItem> library) : IArrClient {
        public ArrType Type => ArrType.Sonarr;
        public Task<IReadOnlyList<MediaItem>> GetLibraryAsync(CancellationToken cancellationToken = default) => Task.FromResult(library);
        public Task<MediaItem> GetItemAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<QualityProfile>> GetQualityProfilesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RootFolder>> GetRootFoldersAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Tag>> GetTagsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class OneShotClients(IArrClient client) : IArrClientFactory {
        public IArrClient Create(ServerConnection server) => client;
        public ISonarrClient CreateSonarr(ServerConnection server) => throw new NotSupportedException();
        public IRadarrClient CreateRadarr(ServerConnection server) => throw new NotSupportedException();
    }

    private static async Task<MediaCollectionViewModel> LoadedAsync(int count, bool list = false) {
        var items = Enumerable.Range(1, count).Select(i => (MediaItem)new Series { Id = i, Title = $"Show {i:D2}", Year = 2020 }).ToList();
        var settings = new MemorySettings();
        if (list)
            settings.Values[MediaCollectionViewModel.GridLayoutSettingKey] = "0";
        var context = new ArrContext(new MemoryRepository(), settings);
        var vm = new MediaCollectionViewModel(context, new OneShotClients(new OneShotClient(items)), settings, new ToastService());
        context.SetActive(new ServerConnection { Id = 1, Name = "home", BaseUrl = "http://home", ApiKey = "K" });
        await vm.WhenIdleAsync();
        return vm;
    }

    [Fact]
    public async Task The_Grid_Groups_Cards_Into_Rows_And_Pads_The_Last_Row_So_Cards_Keep_Their_Size() {
        var vm = await LoadedAsync(7);

        var rows = vm.Rows.Cast<GridRowViewModel>().ToList();

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.Equal(3, r.Cells.Count));
        Assert.Equal(["Show 01", "Show 02", "Show 03"], rows[0].Cells.Select(c => c.Card!.Title));
        Assert.Equal(["Show 07", null, null], rows[2].Cells.Select(c => c.Card?.Title));
        Assert.Equal([true, false, false], rows[2].Cells.Select(c => c.HasCard));
    }

    [Fact]
    public async Task A_Full_Last_Row_Needs_No_Padding_And_An_Empty_Library_Has_No_Rows() {
        Assert.Equal(2, (await LoadedAsync(6)).Rows.Count);
        Assert.All((await LoadedAsync(6)).Rows.Cast<GridRowViewModel>(), r => Assert.All(r.Cells, c => Assert.True(c.HasCard)));
        Assert.Empty((await LoadedAsync(0)).Rows);
    }

    [Fact]
    public async Task The_List_Layout_Has_One_Card_Per_Row() {
        var vm = await LoadedAsync(5, list: true);

        var rows = vm.Rows.Cast<ListRowViewModel>().ToList();

        Assert.Equal(5, rows.Count);
        Assert.Equal("Show 03", rows[2].Card.Title);
    }

    [Fact]
    public async Task More_Columns_Means_Fewer_Rows_And_The_Count_Never_Drops_Below_One() {
        var vm = await LoadedAsync(10);
        Assert.Equal(4, vm.Rows.Count);

        vm.SetColumns(5);
        Assert.Equal(2, vm.Rows.Count);
        Assert.All(vm.Rows.Cast<GridRowViewModel>(), r => Assert.Equal(5, r.Cells.Count));

        vm.SetColumns(0);
        Assert.Equal(1, vm.Columns);
        Assert.Equal(10, vm.Rows.Count);
    }

    [Fact]
    public async Task Toggling_The_Layout_Rebuilds_The_Rows_And_Updates_The_Button_Label() {
        var vm = await LoadedAsync(4);
        Assert.IsType<GridRowViewModel>(vm.Rows[0]);
        Assert.Contains("List", vm.LayoutToggleText);

        vm.ToggleLayoutCommand.Execute(null);

        Assert.All(vm.Rows, r => Assert.IsType<ListRowViewModel>(r));
        Assert.Equal(4, vm.Rows.Count);
        Assert.Contains("Grid", vm.LayoutToggleText);
    }

    [Fact]
    public async Task Changing_The_Query_Rebuilds_The_Rows() {
        var vm = await LoadedAsync(8);

        vm.Query = new ArrApp.Core.Presentation.MediaQuery(Search: "Show 0");

        Assert.Equal(3, vm.Rows.Count); // Show 01..08 all contain "show 0": 8 cards in rows of 3
        vm.Query = new ArrApp.Core.Presentation.MediaQuery(Search: "Show 08");
        var only = Assert.Single(vm.Rows.Cast<GridRowViewModel>());
        Assert.Equal("Show 08", only.Cells[0].Card!.Title);
    }

    [Fact]
    public async Task A_Big_Library_Becomes_A_Short_List_Of_Rows_Built_In_One_Go() {
        var vm = await LoadedAsync(5000);

        Assert.Equal(1667, vm.Rows.Count);
        Assert.Equal(5000, vm.Items.Count);
    }
}
