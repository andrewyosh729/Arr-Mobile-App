using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
            // Draw under the system bars. Avalonia then pads this view (Padding) by the safe area itself.
            if (top.InsetsManager is { } insets)
                insets.DisplayEdgeToEdgePreference = true;
        }
        Focus();
    }

    protected override void OnUnloaded(RoutedEventArgs e) {
        if (TopLevel.GetTopLevel(this) is { } top)
            top.BackRequested -= OnBackRequested;
        base.OnUnloaded(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
        base.OnPropertyChanged(change);
        if (change.Property == PaddingProperty)
            UpdateOverlayForSafeArea();
    }

    // Page and toast stay inside the system bars (the padding). The scrim cancels the padding so it covers the whole
    // screen, and the sheet grows by the bottom inset so it reaches the edge without its buttons sitting under the nav bar.
    private void UpdateOverlayForSafeArea() {
        var safe = Padding;
        OverlayLayer.Margin = new Thickness(-safe.Left, -safe.Top, -safe.Right, -safe.Bottom);
        SheetHost.Padding = new Thickness(16, 16, 16, 16 + safe.Bottom);
    }

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
