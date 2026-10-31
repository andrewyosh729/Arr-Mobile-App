using ArrApp.Core;
using ArrApp.Core.Api;
using ArrApp.UI.Errors;
using ArrApp.UI.Navigation;
using ArrApp.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ArrApp.UI.Tests;

public class ServersViewModelTests {
    private sealed class RootViewModel : ViewModelBase;

    private sealed class Fixture : IDisposable {
        public MemoryRepository Repo { get; } = new();
        public FakeTester Tester { get; }
        public ServiceProvider Services { get; }
        public NavigationService Nav { get; }
        public BottomSheetService Sheets { get; }
        public ToastService Toasts { get; }

        public Fixture(Func<ArrType, string, string, ConnectionTestResult>? respond = null) {
            Tester = new FakeTester(respond ?? ((type, _, _) => ConnectionTestResult.Ok(type.ToString(), "4.0.9.2244")));
            Services = new ServiceCollection()
                .AddLogging()
                .AddSingleton<IServerConnectionRepository>(Repo)
                .AddSingleton<IArrConnectionTester>(Tester)
                .AddSingleton<NavigationService>()
                .AddSingleton<INavigationService>(sp => sp.GetRequiredService<NavigationService>())
                .AddSingleton<BottomSheetService>()
                .AddSingleton<IBottomSheetService>(sp => sp.GetRequiredService<BottomSheetService>())
                .AddSingleton<ToastService>()
                .AddSingleton<IToastService>(sp => sp.GetRequiredService<ToastService>())
                .AddSingleton<IErrorReporter, ErrorReporter>()
                .AddSingleton<ISettingsStore>(new MemorySettings())
                .AddSingleton<ArrContext>()
                .AddTransient<ServerEditViewModel>()
                .AddTransient<ConfirmViewModel>()
                .AddTransient<ServersViewModel>()
                .BuildServiceProvider();
            Nav = Services.GetRequiredService<NavigationService>();
            Sheets = Services.GetRequiredService<BottomSheetService>();
            Toasts = Services.GetRequiredService<ToastService>();
            Nav.SetRoot(new RootViewModel());
        }

        public async Task<ServersViewModel> OpenAsync() {
            var vm = Services.GetRequiredService<ServersViewModel>();
            Nav.Push<ServersViewModel>(); // page is on the stack, as in the app
            await vm.LoadAsync();
            return vm;
        }

        public async Task<ServerConnection> AddAsync(string name, ArrType type = ArrType.Sonarr, string key = "KEY") {
            var s = new ServerConnection { Name = name, ArrType = type, BaseUrl = $"http://{name.ToLowerInvariant()}:8989", ApiKey = key };
            await Repo.AddAsync(s);
            return s;
        }

        public void Dispose() => Services.Dispose();
    }

    [Fact]
    public async Task Lists_Servers_In_Order_With_Move_Flags() {
        using var f = new Fixture();
        await f.AddAsync("A");
        await f.AddAsync("B", ArrType.Radarr);
        await f.AddAsync("C");

        var vm = await f.OpenAsync();

        Assert.Equal(["A", "B", "C"], vm.Items.Select(i => i.Name));
        Assert.False(vm.IsEmpty);
        Assert.Equal([false, true, true], vm.Items.Select(i => i.CanMoveUp));
        Assert.Equal([true, true, false], vm.Items.Select(i => i.CanMoveDown));
        Assert.True(vm.Items[0].IsDefault);
        Assert.Equal("R", vm.Items[1].Initial);
    }

    [Fact]
    public async Task Empty_List_Shows_The_Empty_State() {
        using var f = new Fixture();

        var vm = await f.OpenAsync();

        Assert.True(vm.IsEmpty);
        Assert.Empty(vm.Items);
    }

    [Fact]
    public async Task Each_Server_Gets_Its_Own_Live_Status_And_Successful_Checks_Are_Recorded() {
        using var f = new Fixture((type, url, _) => url.Contains("down")
            ? ConnectionTestResult.Fail(ArrErrorKind.Unreachable, "nope")
            : ConnectionTestResult.Ok(type.ToString(), "4.0.9.2244"));
        var up = await f.AddAsync("Up");
        await f.AddAsync("Down");
        var vm = await f.OpenAsync();

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(ServerStatus.Online, vm.Items[0].Status);
        Assert.Equal("Online · v4.0.9.2244", vm.Items[0].StatusText);
        Assert.Equal(ServerStatus.Offline, vm.Items[1].Status);
        Assert.Equal("Unreachable", vm.Items[1].StatusText);
        Assert.Contains(f.Repo.Connected, c => c.Id == up.Id && c.Version == "4.0.9.2244");
        Assert.DoesNotContain(f.Repo.Connected, c => c.Id != up.Id);
    }

    [Fact]
    public async Task A_Server_Whose_Key_Could_Not_Be_Decrypted_Prompts_For_The_Key_Without_Calling_It() {
        using var f = new Fixture();
        await f.AddAsync("Locked", key: "");
        var vm = await f.OpenAsync();

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal("Re-enter API key", vm.Items[0].StatusText);
        Assert.Equal(ServerStatus.Offline, vm.Items[0].Status);
        Assert.Empty(f.Tester.Calls);
    }

    [Fact]
    public async Task SetDefault_Moves_The_Default_Marker() {
        using var f = new Fixture();
        await f.AddAsync("A");
        var b = await f.AddAsync("B");
        var vm = await f.OpenAsync();

        await vm.SetDefaultCommand.ExecuteAsync(vm.Items[1]);

        Assert.Equal([false, true], vm.Items.Select(i => i.IsDefault));
        Assert.Equal(b.Id, (await f.Repo.GetDefaultAsync())!.Id);
    }

    [Fact]
    public async Task Move_Down_And_Up_Reorder_Storage_And_List() {
        using var f = new Fixture();
        await f.AddAsync("A");
        await f.AddAsync("B");
        await f.AddAsync("C");
        var vm = await f.OpenAsync();

        await vm.MoveDownCommand.ExecuteAsync(vm.Items[0]);
        Assert.Equal(["B", "A", "C"], vm.Items.Select(i => i.Name));
        Assert.Equal(["B", "A", "C"], (await f.Repo.GetAllAsync()).Select(s => s.Name));

        await vm.MoveUpCommand.ExecuteAsync(vm.Items[2]);
        Assert.Equal(["B", "C", "A"], vm.Items.Select(i => i.Name));
        Assert.Equal([false, true, true], vm.Items.Select(i => i.CanMoveUp));
        Assert.Equal([true, true, false], vm.Items.Select(i => i.CanMoveDown));
    }

    [Fact]
    public async Task Moving_Past_The_Ends_Does_Nothing() {
        using var f = new Fixture();
        await f.AddAsync("A");
        await f.AddAsync("B");
        var vm = await f.OpenAsync();

        await vm.MoveUpCommand.ExecuteAsync(vm.Items[0]);
        await vm.MoveDownCommand.ExecuteAsync(vm.Items[1]);

        Assert.Equal(["A", "B"], vm.Items.Select(i => i.Name));
    }

    [Fact]
    public async Task Delete_Asks_First_And_Removes_Nothing_Until_Confirmed() {
        using var f = new Fixture();
        await f.AddAsync("A");
        await f.AddAsync("B");
        var vm = await f.OpenAsync();

        vm.DeleteCommand.Execute(vm.Items[1]);

        var confirm = Assert.IsType<ConfirmViewModel>(f.Sheets.Current);
        Assert.Equal("Delete B?", confirm.Title);
        Assert.True(confirm.IsDestructive);
        Assert.Equal(2, f.Repo.Items.Count);

        await confirm.ConfirmCommand.ExecuteAsync(null);

        Assert.False(f.Sheets.IsOpen);
        Assert.Equal(["A"], f.Repo.Items.Select(s => s.Name));
        Assert.Equal(["A"], vm.Items.Select(i => i.Name));
    }

    [Fact]
    public async Task Cancelling_The_Confirmation_Keeps_The_Server() {
        using var f = new Fixture();
        await f.AddAsync("A");
        var vm = await f.OpenAsync();
        vm.DeleteCommand.Execute(vm.Items[0]);

        Assert.IsType<ConfirmViewModel>(f.Sheets.Current).CancelCommand.Execute(null);

        Assert.False(f.Sheets.IsOpen);
        Assert.Single(f.Repo.Items);
    }

    [Fact]
    public async Task Deleting_The_Default_Promotes_The_Next_And_Last_Delete_Shows_The_Empty_State() {
        using var f = new Fixture();
        await f.AddAsync("A");
        await f.AddAsync("B");
        var vm = await f.OpenAsync();

        vm.DeleteCommand.Execute(vm.Items[0]);
        await ((ConfirmViewModel)f.Sheets.Current!).ConfirmCommand.ExecuteAsync(null);
        Assert.True(Assert.Single(vm.Items).IsDefault);

        vm.DeleteCommand.Execute(vm.Items[0]);
        await ((ConfirmViewModel)f.Sheets.Current!).ConfirmCommand.ExecuteAsync(null);
        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public async Task Add_Opens_The_Editor_And_Reloads_The_List_After_Saving() {
        using var f = new Fixture();
        var vm = await f.OpenAsync();

        vm.AddCommand.Execute(null);
        var editor = Assert.IsType<ServerEditViewModel>(f.Nav.Current);
        Assert.False(editor.IsEdit);

        editor.Name = "Fresh";
        editor.Url = "host:8989";
        editor.ApiKey = "KEY";
        await editor.SaveCommand.ExecuteAsync(null);

        Assert.IsType<ServersViewModel>(f.Nav.Current);
        Assert.Equal(["Fresh"], vm.Items.Select(i => i.Name));
    }

    [Fact]
    public async Task Edit_Opens_The_Editor_On_A_Copy_Of_The_Server() {
        using var f = new Fixture();
        await f.AddAsync("A");
        var vm = await f.OpenAsync();

        vm.EditCommand.Execute(vm.Items[0]);

        var editor = Assert.IsType<ServerEditViewModel>(f.Nav.Current);
        Assert.True(editor.IsEdit);
        Assert.Equal("A", editor.Name);

        editor.Name = "Renamed but not saved";
        Assert.Equal("A", vm.Items[0].Name);
    }

    [Fact]
    public async Task Back_Returns_To_The_Previous_Page() {
        using var f = new Fixture();
        var vm = await f.OpenAsync();

        vm.BackCommand.Execute(null);

        Assert.IsType<RootViewModel>(f.Nav.Current);
    }
}
