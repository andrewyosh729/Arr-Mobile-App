using System;
using System.Threading.Tasks;
using ArrApp.Core;
using ArrApp.UI.Errors;
using ArrApp.UI.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace ArrApp.UI.ViewModels;

/// <summary>
/// Root of the app: hosts the page stack, bottom sheet and toast, owns back-button behaviour, and decides at startup
/// between the first-run welcome screen (no servers saved) and the main tabs (landing on the default server).
/// </summary>
public sealed class ShellViewModel(
    NavigationService navigation,
    BottomSheetService sheets,
    ToastService toasts,
    TabHostViewModel tabs,
    ArrContext context,
    IErrorReporter errors,
    IServiceProvider services) : ViewModelBase {
    public NavigationService Navigation { get; } = navigation;
    public BottomSheetService Sheets { get; } = sheets;
    public ToastService Toasts { get; } = toasts;

    /// <summary>Loads the saved servers and shows the right first screen. Call once at startup.</summary>
    public async Task InitializeAsync() {
        try {
            await context.InitializeAsync();
        } catch (Exception ex) {
            errors.Report(ex, "Couldn't load your servers.");
        }

        if (context.Active is null)
            ShowWelcome();
        else
            Navigation.SetRoot(tabs);
    }

    private void ShowWelcome() {
        var welcome = services.GetRequiredService<WelcomeViewModel>();
        welcome.Completed = () => _ = ShowMainAsync();
        Navigation.SetRoot(welcome);
    }

    private async Task ShowMainAsync() {
        try {
            await context.RefreshAsync(); // the server just saved becomes the default, and so the active one
        } catch (Exception ex) {
            errors.Report(ex, "Couldn't load your servers.");
        }

        Navigation.SetRoot(tabs);
    }

    /// <summary>Closes the sheet, else pops a page. Returns false when nothing is left to go back to (let the OS handle it).</summary>
    public bool HandleBack() {
        if (Sheets.IsOpen) {
            Sheets.Hide();
            return true;
        }

        return Navigation.Pop();
    }
}
