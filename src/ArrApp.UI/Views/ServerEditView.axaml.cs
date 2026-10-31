using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using ArrApp.UI.ViewModels;

namespace ArrApp.UI.Views;

public partial class ServerEditView : UserControl {
    public ServerEditView() {
        InitializeComponent();
    }

    // Clipboard access belongs to the view (it needs the TopLevel); the view model only sees the resulting text.
    private async void OnPasteClicked(object? sender, RoutedEventArgs e) {
        if (DataContext is not ServerEditViewModel vm || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
            return;

        var text = await clipboard.TryGetTextAsync();
        if (!string.IsNullOrWhiteSpace(text))
            vm.ApiKey = text.Trim();
    }
}
