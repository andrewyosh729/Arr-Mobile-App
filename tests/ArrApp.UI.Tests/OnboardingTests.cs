using ArrApp.Core;
using ArrApp.Core.Api;
using ArrApp.UI.Navigation;
using ArrApp.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ArrApp.UI.Tests;

public class OnboardingTests {
    private sealed class ThrowingRepository : IServerConnectionRepository {
        private static Exception Boom => new InvalidOperationException("disk on fire");
        public Task<IReadOnlyList<ServerConnection>> GetAllAsync() => Task.FromException<IReadOnlyList<ServerConnection>>(Boom);
        public Task<ServerConnection?> GetAsync(int id) => throw Boom;
        public Task<ServerConnection?> GetDefaultAsync() => throw Boom;
        public Task AddAsync(ServerConnection connection) => throw Boom;
        public Task UpdateAsync(ServerConnection connection) => throw Boom;
        public Task DeleteAsync(int id) => throw Boom;
        public Task SetDefaultAsync(int id) => throw Boom;
        public Task ReorderAsync(IReadOnlyList<int> orderedIds) => throw Boom;
        public Task MarkConnectedAsync(int id, string version, DateTimeOffset when) => throw Boom;
    }

    private static ServerConnection Server(string name, ArrType type = ArrType.Sonarr)
        => new() { Name = name, ArrType = type, BaseUrl = $"http://{name.ToLowerInvariant()}:8989", ApiKey = "KEY" };

    [Fact]
    public async Task With_No_Servers_The_App_Starts_On_The_Welcome_Screen() {
        using var services = TestApp.Build(new MemoryRepository());
        var shell = services.GetRequiredService<ShellViewModel>();

        await shell.InitializeAsync();

        Assert.IsType<WelcomeViewModel>(shell.Navigation.Current);
        Assert.False(shell.Navigation.CanGoBack);
        Assert.False(shell.HandleBack()); // nothing behind it: let the OS leave the app
        Assert.Null(services.GetRequiredService<ArrContext>().Active);
    }

    [Fact]
    public async Task With_Servers_The_App_Lands_On_The_Default_One() {
        var repo = new MemoryRepository();
        await repo.AddAsync(Server("First"));
        var second = Server("Movies", ArrType.Radarr);
        await repo.AddAsync(second);
        await repo.SetDefaultAsync(second.Id);
        using var services = TestApp.Build(repo);
        var shell = services.GetRequiredService<ShellViewModel>();

        await shell.InitializeAsync();

        Assert.IsType<TabHostViewModel>(shell.Navigation.Current);
        var context = services.GetRequiredService<ArrContext>();
        Assert.Equal("Movies", context.Active!.Name);
        Assert.Equal(ArrType.Radarr, context.ArrType);
        Assert.Equal("Movies · Radarr", context.ActiveLabel);
    }

    [Fact]
    public async Task Adding_The_First_Server_Replaces_Welcome_With_The_Main_Tabs() {
        var repo = new MemoryRepository();
        using var services = TestApp.Build(repo);
        var shell = services.GetRequiredService<ShellViewModel>();
        await shell.InitializeAsync();
        var welcome = Assert.IsType<WelcomeViewModel>(shell.Navigation.Current);

        welcome.AddServerCommand.Execute(null);
        var editor = Assert.IsType<ServerEditViewModel>(shell.Navigation.Current);
        editor.Name = "Home";
        editor.Url = "home:8989";
        editor.ApiKey = "KEY";
        await editor.SaveCommand.ExecuteAsync(null);
        await Task.Yield(); // the shell swaps the root from a continuation

        Assert.IsType<TabHostViewModel>(shell.Navigation.Current);
        Assert.False(shell.Navigation.CanGoBack); // no way back into the welcome flow
        var context = services.GetRequiredService<ArrContext>();
        Assert.Equal("Home", context.Active!.Name);
        Assert.True(context.Active.IsDefault);
    }

    [Fact]
    public async Task Backing_Out_Of_The_First_Server_Returns_To_Welcome() {
        using var services = TestApp.Build(new MemoryRepository());
        var shell = services.GetRequiredService<ShellViewModel>();
        await shell.InitializeAsync();
        var welcome = Assert.IsType<WelcomeViewModel>(shell.Navigation.Current);
        welcome.AddServerCommand.Execute(null);

        Assert.True(shell.HandleBack());

        Assert.Same(welcome, shell.Navigation.Current);
    }

    [Fact]
    public async Task A_Storage_Failure_At_Startup_Is_Reported_And_The_App_Still_Shows_A_Screen() {
        using var services = TestApp.Build(new ThrowingRepository());
        var shell = services.GetRequiredService<ShellViewModel>();

        await shell.InitializeAsync();

        Assert.IsType<WelcomeViewModel>(shell.Navigation.Current);
        Assert.NotNull(shell.Toasts.Current);
        Assert.True(shell.Toasts.Current.IsError);
    }

    // ---- ArrContext ----

    [Fact]
    public async Task Context_Initializes_To_The_Default_Server() {
        var repo = new MemoryRepository();
        await repo.AddAsync(Server("A"));
        var b = Server("B");
        await repo.AddAsync(b);
        await repo.SetDefaultAsync(b.Id);
        var context = new ArrContext(repo, new MemorySettings());

        await context.InitializeAsync();

        Assert.Equal("B", context.Active!.Name);
    }

    [Fact]
    public async Task Context_Is_Empty_When_Nothing_Is_Saved() {
        var context = new ArrContext(new MemoryRepository(), new MemorySettings());

        await context.InitializeAsync();

        Assert.Null(context.Active);
        Assert.Null(context.ArrType);
        Assert.Equal("No server", context.ActiveLabel);
    }

    [Fact]
    public async Task Refresh_Keeps_The_Active_Server_And_Falls_Back_When_It_Is_Deleted() {
        var repo = new MemoryRepository();
        var a = Server("A");
        var b = Server("B");
        await repo.AddAsync(a);
        await repo.AddAsync(b);
        var context = new ArrContext(repo, new MemorySettings());
        await context.InitializeAsync();
        context.SetActive(b); // viewing B although A is the default

        await context.RefreshAsync();
        Assert.Equal("B", context.Active!.Name);

        await repo.DeleteAsync(b.Id);
        await context.RefreshAsync();
        Assert.Equal("A", context.Active!.Name);

        await repo.DeleteAsync(a.Id);
        await context.RefreshAsync();
        Assert.Null(context.Active);
    }

    [Fact]
    public async Task Context_Announces_Changes_To_The_Active_Server_And_Its_Type_Only_When_They_Change() {
        var repo = new MemoryRepository();
        var a = Server("A");
        await repo.AddAsync(a);
        var context = new ArrContext(repo, new MemorySettings());
        var raised = new List<string?>();
        context.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        await context.InitializeAsync();
        Assert.Contains(nameof(ArrContext.Active), raised);
        Assert.Contains(nameof(ArrContext.ArrType), raised);

        raised.Clear();
        await context.RefreshAsync(); // same server: label may refresh, but Active/ArrType are unchanged
        Assert.DoesNotContain(nameof(ArrContext.Active), raised);
        Assert.DoesNotContain(nameof(ArrContext.ArrType), raised);
    }
}
