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
        if (TopLevel.GetTopLevel(this) is { } top)
            top.BackRequested += OnBackRequested; // Android back button / gesture
        Focus();
    }

    protected override void OnUnloaded(RoutedEventArgs e) {
        if (TopLevel.GetTopLevel(this) is { } top)
            top.BackRequested -= OnBackRequested;
        base.OnUnloaded(e);
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

    private void OnScrimPressed(object? sender, PointerPressedEventArgs e) {
        if (DataContext is ShellViewModel shell)
            shell.Sheets.Hide();
    }
}
