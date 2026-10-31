using ArrApp.UI.Navigation;
using ArrApp.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ArrApp.UI.Tests;

public class NavigationTests {
    /// <summary>A shell that has finished starting up with one saved server, so it is showing the main tabs.</summary>
    private static async Task<(ServiceProvider Services, ShellViewModel Shell)> StartAsync() {
        var repo = new MemoryRepository();
        await repo.AddAsync(new Core.ServerConnection { Name = "Home", ArrType = Core.ArrType.Sonarr, BaseUrl = "http://home", ApiKey = "KEY" });
        var services = TestApp.Build(repo);
        var shell = services.GetRequiredService<ShellViewModel>();
        await shell.InitializeAsync();
        return (services, shell);
    }

    [Fact]
    public async Task Shell_Starts_On_Tab_Host_With_Nothing_To_Go_Back_To() {
        var (services, shell) = await StartAsync();
        using var _ = services;

        Assert.IsType<TabHostViewModel>(shell.Navigation.Current);
        Assert.False(shell.Navigation.CanGoBack);
        Assert.False(shell.HandleBack());
    }

    [Fact]
    public async Task Push_Passes_Parameters_And_Pop_Returns_To_Previous() {
        var (services, shell) = await StartAsync();
        using var _ = services;
        var root = shell.Navigation.Current;

        var page = shell.Navigation.Push<PlaceholderViewModel>(vm => vm.Title = "Series 1");

        Assert.Same(page, shell.Navigation.Current);
        Assert.Equal("Series 1", page.Title);
        Assert.True(shell.Navigation.CanGoBack);

        Assert.True(shell.Navigation.Pop());
        Assert.Same(root, shell.Navigation.Current);
        Assert.False(shell.Navigation.Pop());
    }

    [Fact]
    public async Task Back_Closes_Sheet_Before_Popping_Page() {
        var (services, shell) = await StartAsync();
        using var _ = services;
        shell.Navigation.Push<PlaceholderViewModel>();
        shell.Sheets.Show<PlaceholderViewModel>();

        Assert.True(shell.HandleBack());
        Assert.False(shell.Sheets.IsOpen);
        Assert.True(shell.Navigation.CanGoBack);

        Assert.True(shell.HandleBack());
        Assert.False(shell.Navigation.CanGoBack);
    }

    [Fact]
    public async Task Navigation_Raises_PropertyChanged_For_Current() {
        var (services, shell) = await StartAsync();
        using var _ = services;
        var nav = services.GetRequiredService<NavigationService>();
        var changed = new List<string?>();
        nav.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        nav.Push<PlaceholderViewModel>();

        Assert.Contains(nameof(NavigationService.Current), changed);
        Assert.Contains(nameof(NavigationService.CanGoBack), changed);
    }
}
