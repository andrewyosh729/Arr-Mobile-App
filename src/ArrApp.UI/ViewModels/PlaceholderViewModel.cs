using System;
using ArrApp.Core;
using ArrApp.UI.Errors;
using ArrApp.UI.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArrApp.UI.ViewModels;

/// <summary>Stand-in page that exercises push/pop/sheet until real screens exist.</summary>
public sealed partial class PlaceholderViewModel(INavigationService navigation, IBottomSheetService sheets, IToastService toasts, ArrContext context) : ViewModelBase {
    /// <summary>Shown on the page so it is visible which server the app has landed on.</summary>
    public ArrContext Context { get; } = context;

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


    [RelayCommand]
    private void Back() => navigation.Pop();

    public bool CanGoBack => navigation.CanGoBack;
}
