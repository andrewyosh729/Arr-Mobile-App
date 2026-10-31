using ArrApp.Core;
using ArrApp.UI.Errors;
using ArrApp.UI.Navigation;
using ArrApp.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ArrApp.UI.Tests;

public class SwitcherTests {
    private sealed class ThrowingSettings : ISettingsStore {
        public Task<string?> GetAsync(string key) => throw new InvalidOperationException("settings broken");
        public Task SetAsync(string key, string value) => throw new InvalidOperationException("settings broken");
    }

    private static ServerConnection Server(string name, ArrType type) => new() {
        Name = name, ArrType = type, BaseUrl = $"http://{name.ToLowerInvariant()}", ApiKey = "KEY",
    };

    private sealed class Fixture : IDisposable {
        public MemoryRepository Repo { get; } = new();
        public MemorySettings Settings { get; } = new();
        public ServerConnection Tv { get; } = Server("Shows", ArrType.Sonarr);
        public ServerConnection Films { get; } = Server("Films", ArrType.Radarr);
        public ServiceProvider Services { get; private set; } = null!;

        public async Task<Fixture> StartAsync() {
            await Repo.AddAsync(Tv);
            await Repo.AddAsync(Films);
            Services = TestApp.Build(Repo, settings: Settings);
            await Services.GetRequiredService<ShellViewModel>().InitializeAsync();
            return this;
        }

        public ArrContext Context => Services.GetRequiredService<ArrContext>();
        public ServerSwitcherViewModel Switcher => Services.GetRequiredService<ServerSwitcherViewModel>();
        public BottomSheetService Sheets => Services.GetRequiredService<BottomSheetService>();
        public NavigationService Nav => Services.GetRequiredService<NavigationService>();

        public void Dispose() => Services.Dispose();
    }

    [Fact]
    public async Task Pill_Shows_The_Active_Server_And_Is_Part_Of_The_Main_Screen() {
        using var f = await new Fixture().StartAsync();

        Assert.Equal("Shows", f.Switcher.Name);
        Assert.Equal("Sonarr", f.Switcher.TypeLabel);
        var tabs = Assert.IsType<TabHostViewModel>(f.Nav.Current);
        Assert.Same(f.Switcher, tabs.Switcher);
    }

    [Fact]
    public async Task Tapping_The_Pill_Lists_All_Servers_With_The_Active_One_Marked() {
        using var f = await new Fixture().StartAsync();

        await f.Switcher.ShowCommand.ExecuteAsync(null);

        var picker = Assert.IsType<ServerPickerViewModel>(f.Sheets.Current);
        Assert.Equal(["Shows", "Films"], picker.Items.Select(i => i.Name));
        Assert.Equal([true, false], picker.Items.Select(i => i.IsActive));
    }

    [Fact]
    public async Task Picking_A_Server_Switches_Context_Closes_The_Sheet_And_Remembers_It() {
        using var f = await new Fixture().StartAsync();
        await f.Switcher.ShowCommand.ExecuteAsync(null);
        var picker = (ServerPickerViewModel)f.Sheets.Current!;
        var changed = new List<string?>();
        f.Switcher.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await picker.SelectCommand.ExecuteAsync(picker.Items[1]);

        Assert.False(f.Sheets.IsOpen);
        Assert.Equal("Films", f.Context.Active!.Name);
        Assert.Equal(ArrType.Radarr, f.Context.ArrType);
        Assert.Equal("Movies", f.Context.MediaNoun);
        Assert.Equal("Films", f.Switcher.Name);
        Assert.Equal("Radarr", f.Switcher.TypeLabel);
        Assert.Contains(nameof(ServerSwitcherViewModel.AccentBrush), changed);
        Assert.Equal(f.Films.Id.ToString(), f.Settings.Values[ArrContext.LastActiveServerKey]);
    }

    [Fact]
    public async Task Manage_Closes_The_Sheet_And_Opens_The_Servers_Screen() {
        using var f = await new Fixture().StartAsync();
        await f.Switcher.ShowCommand.ExecuteAsync(null);
        var picker = (ServerPickerViewModel)f.Sheets.Current!;

        await picker.ManageCommand.ExecuteAsync(null);

        Assert.False(f.Sheets.IsOpen);
        Assert.IsType<ServersViewModel>(f.Nav.Current);
    }

    // ---- remembering the last active server ----

    [Fact]
    public async Task The_Last_Active_Server_Is_Restored_On_The_Next_Launch() {
        using var first = await new Fixture().StartAsync();
        await first.Context.SelectAsync(first.Films);

        // "Relaunch": a brand-new context over the same storage.
        var relaunched = new ArrContext(first.Repo, first.Settings);
        await relaunched.InitializeAsync();

        Assert.Equal("Films", relaunched.Active!.Name);
    }

    [Fact]
    public async Task If_The_Remembered_Server_Was_Deleted_The_Default_Is_Used() {
        using var f = await new Fixture().StartAsync();
        await f.Context.SelectAsync(f.Films);
        await f.Repo.DeleteAsync(f.Films.Id);

        var relaunched = new ArrContext(f.Repo, f.Settings);
        await relaunched.InitializeAsync();

        Assert.Equal("Shows", relaunched.Active!.Name);
    }

    [Fact]
    public async Task Nothing_Remembered_Means_The_Default_Server() {
        using var f = await new Fixture().StartAsync();

        var relaunched = new ArrContext(f.Repo, new MemorySettings());
        await relaunched.InitializeAsync();

        Assert.Equal("Shows", relaunched.Active!.Name); // the first server added is the default
    }

    [Fact]
    public async Task A_Settings_Failure_Does_Not_Stop_The_App_Opening_On_The_Default() {
        using var f = await new Fixture().StartAsync();

        var context = new ArrContext(f.Repo, new ThrowingSettings());
        await context.InitializeAsync();

        Assert.Equal("Shows", context.Active!.Name);
    }

    [Fact]
    public async Task A_Failure_While_Remembering_The_Choice_Is_Reported_But_The_Switch_Still_Happens() {
        var repo = new MemoryRepository();
        var tv = Server("Shows", ArrType.Sonarr);
        var films = Server("Films", ArrType.Radarr);
        await repo.AddAsync(tv);
        await repo.AddAsync(films);
        using var services = TestApp.Build(repo, settings: new ThrowingSettings());
        await services.GetRequiredService<ShellViewModel>().InitializeAsync();
        var switcher = services.GetRequiredService<ServerSwitcherViewModel>();
        await switcher.ShowCommand.ExecuteAsync(null);
        var picker = (ServerPickerViewModel)services.GetRequiredService<BottomSheetService>().Current!;

        await picker.SelectCommand.ExecuteAsync(picker.Items[1]);

        Assert.Equal("Films", services.GetRequiredService<ArrContext>().Active!.Name);
        Assert.True(services.GetRequiredService<ToastService>().Current!.IsError);
    }
}
