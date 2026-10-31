using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Controls.Platform;
using ArrApp.UI.ViewModels;

namespace ArrApp.UI.Views;

public partial class ShellView : UserControl {
    public ShellView() {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e) {
        base.OnLoaded(e);
        if (TopLevel.GetTopLevel(this) is { } top) {
            top.BackRequested += OnBackRequested; // Android back button / gesture
            if (top.InsetsManager is { } insets) {
                insets.DisplayEdgeToEdgePreference = true; // draw under system bars, pad by the safe area
                insets.SafeAreaChanged += OnSafeAreaChanged;
                Padding = insets.SafeAreaPadding;
            }
        }
        Focus();
    }

    protected override void OnUnloaded(RoutedEventArgs e) {
        if (TopLevel.GetTopLevel(this) is { } top) {
            top.BackRequested -= OnBackRequested;
            if (top.InsetsManager is { } insets)
                insets.SafeAreaChanged -= OnSafeAreaChanged;
        }
        base.OnUnloaded(e);
    }

    private void OnSafeAreaChanged(object? sender, SafeAreaChangedArgs e) => Padding = e.SafeAreaPadding;

    private void OnBackRequested(object? sender, RoutedEventArgs e) {
        if (DataContext is ShellViewModel shell)
            e.Handled = shell.HandleBack();
    }

    protected override void OnKeyDown(KeyEventArgs e) {
        base.OnKeyDown(e);
        // Desktop stand-in for the Android back button.
        if (e.Key == Key.Escape && DataContext is ShellViewModel shell)
            e.Handled = shell.HandleBack();
    }

    private void OnToastPressed(object? sender, PointerPressedEventArgs e) {
        if (DataContext is ShellViewModel shell)
            shell.Toasts.Dismiss();
    }

    private void OnScrimPressed(object? sender, PointerPressedEventArgs e) {
        if (DataContext is ShellViewModel shell)
            shell.Sheets.Hide();
    }
}
