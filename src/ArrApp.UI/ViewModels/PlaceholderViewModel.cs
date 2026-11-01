using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ArrApp.Core;
using ArrApp.Core.Api;
using ArrApp.Core.Images;
using ArrApp.UI.Errors;
using ArrApp.UI.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArrApp.UI.ViewModels;

/// <summary>Stand-in page that exercises push/pop/sheet until real screens exist.</summary>
public sealed partial class PlaceholderViewModel(INavigationService navigation, IBottomSheetService sheets, IToastService toasts, ArrContext context, IArrClientFactory clients) : ViewModelBase {
    /// <summary>Shown on the page so it is visible which server the app has landed on.</summary>
    public ArrContext Context => context;

    [ObservableProperty]
    private string _title = "";

    [RelayCommand]
    private void OpenDetail() => navigation.Push<PlaceholderViewModel>(vm => vm.Title = Title + " detail");

    [RelayCommand]
    private void ShowSheet() => sheets.Show<PlaceholderViewModel>(vm => vm.Title = Title + " sheet");

    // Dev aid while screens are placeholders: proves the global handler turns a crash into a toast + log entry.
    [RelayCommand]
    private void ThrowTestError() => throw new InvalidOperationException("Test error from placeholder page");

    // Dev aid: round-trips a value through the platform's real key protector (Keystore on Android) and reports the result.
    [RelayCommand]
    private void TestKeyVault() {
        var protector = PlatformServices.ApiKeyProtector ?? throw new InvalidOperationException("No protector registered");
        var sealedText = protector.Protect("probe-key-123");
        var ok = protector.Unprotect(sealedText) == "probe-key-123" && !sealedText.Contains("probe-key-123");
        toasts.Show(ok ? $"Key vault OK ({sealedText[..sealedText.IndexOf(':')]})" : "Key vault FAILED",
            ok ? ToastKind.Success : ToastKind.Error);
    }


    // TEMP dev aid until the library screen (F-301/F-302): fetch the active server's library through the real client
    // and show the first posters through the real image pipeline (server + API key, public fallback, disk cache).
    public ObservableCollection<DevPoster> DevPosters { get; } = [];

    [RelayCommand]
    private async Task LoadLibraryDevAsync() {
        if (context.Active is not { } server)
            return;

        try {
            var library = await clients.Create(server).GetLibraryAsync();
            DevPosters.Clear();
            foreach (var item in library.Take(3)) {
                var image = item.Poster;
                DevPosters.Add(new DevPoster(item.Title, image is null ? new ImageRequest(null, null, null, null) : ImageRequest.For(server, image)));
            }

            toasts.Show($"{library.Count} in library");
        } catch (Exception ex) {
            toasts.Show(ex.Message, ToastKind.Error);
        }
    }

    [RelayCommand]
    private void Back() => navigation.Pop();

    public bool CanGoBack => navigation.CanGoBack;
}

/// <summary>TEMP: one poster in the dev display on the placeholder page.</summary>
public sealed record DevPoster(string Title, ImageRequest Request) {
    public string Initial => Title.Length > 0 ? Title[..1] : "?";
}
