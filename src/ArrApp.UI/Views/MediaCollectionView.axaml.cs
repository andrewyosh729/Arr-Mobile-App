using System;
using Avalonia;
using Avalonia.Controls;
using ArrApp.UI.ViewModels;

namespace ArrApp.UI.Views;

public partial class MediaCollectionView : UserControl {
    private const double MinCardWidth = 100;
    private const double CardSpacing = 10;
    private const double SidePadding = 32; // 16 each side

    public MediaCollectionView() {
        InitializeComponent();
    }

    // As many posters across as fit: 3 on a typical phone, more on a tablet or a wide window.
    protected override void OnSizeChanged(SizeChangedEventArgs e) {
        base.OnSizeChanged(e);
        if (DataContext is MediaCollectionViewModel vm && e.NewSize.Width > 0) {
            var columns = (int)Math.Floor((e.NewSize.Width - SidePadding + CardSpacing) / (MinCardWidth + CardSpacing));
            vm.SetColumns(Math.Max(2, columns));
        }
    }

    protected override void OnDataContextChanged(EventArgs e) {
        base.OnDataContextChanged(e);
        if (DataContext is MediaCollectionViewModel vm && Bounds.Width > 0) {
            var columns = (int)Math.Floor((Bounds.Width - SidePadding + CardSpacing) / (MinCardWidth + CardSpacing));
            vm.SetColumns(Math.Max(2, columns));
        }
    }

    // Pull-to-refresh: the container shows its spinner until the deferral completes.
    private async void OnRefreshRequested(object? sender, RefreshRequestedEventArgs e) {
        var deferral = e.GetDeferral();
        try {
            if (DataContext is MediaCollectionViewModel vm)
                await vm.RefreshCommand.ExecuteAsync(null);
        } finally {
            deferral.Complete();
        }
    }
}
