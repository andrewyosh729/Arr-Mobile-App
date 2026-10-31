using ArrApp.UI;
using ArrApp.UI.Navigation;
using ArrApp.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ArrApp.UI.Tests;

public class NavigationTests {
    private static ServiceProvider Build() => new ServiceCollection().AddArrApp().BuildServiceProvider();

    [Fact]
    public void Shell_Starts_On_Tab_Host_With_Nothing_To_Go_Back_To() {
        using var sp = Build();
        var shell = sp.GetRequiredService<ShellViewModel>();

        Assert.IsType<TabHostViewModel>(shell.Navigation.Current);
        Assert.False(shell.Navigation.CanGoBack);
        Assert.False(shell.HandleBack());
    }

    [Fact]
    public void Push_Passes_Parameters_And_Pop_Returns_To_Previous() {
        using var sp = Build();
        var shell = sp.GetRequiredService<ShellViewModel>();
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
    public void Back_Closes_Sheet_Before_Popping_Page() {
        using var sp = Build();
        var shell = sp.GetRequiredService<ShellViewModel>();
        shell.Navigation.Push<PlaceholderViewModel>();
        shell.Sheets.Show<PlaceholderViewModel>();

        Assert.True(shell.HandleBack());
        Assert.False(shell.Sheets.IsOpen);
        Assert.True(shell.Navigation.CanGoBack);

        Assert.True(shell.HandleBack());
        Assert.False(shell.Navigation.CanGoBack);
    }

    [Fact]
    public void Navigation_Raises_PropertyChanged_For_Current() {
        using var sp = Build();
        var nav = sp.GetRequiredService<NavigationService>();
        nav.SetRoot(sp.GetRequiredService<TabHostViewModel>());
        var changed = new List<string?>();
        nav.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        nav.Push<PlaceholderViewModel>();

        Assert.Contains(nameof(NavigationService.Current), changed);
        Assert.Contains(nameof(NavigationService.CanGoBack), changed);
    }
}
