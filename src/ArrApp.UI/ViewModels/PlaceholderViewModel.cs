using System;
using ArrApp.UI.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArrApp.UI.ViewModels;

/// <summary>Stand-in page that exercises push/pop/sheet until real screens exist.</summary>
public sealed partial class PlaceholderViewModel(INavigationService navigation, IBottomSheetService sheets) : ViewModelBase {
    [ObservableProperty]
    private string _title = "";

    [RelayCommand]
    private void OpenDetail() => navigation.Push<PlaceholderViewModel>(vm => vm.Title = Title + " detail");

    [RelayCommand]
    private void ShowSheet() => sheets.Show<PlaceholderViewModel>(vm => vm.Title = Title + " sheet");

    // Dev aid while screens are placeholders: proves the global handler turns a crash into a toast + log entry.
    [RelayCommand]
    private void ThrowTestError() => throw new InvalidOperationException("Test error from placeholder page");

    [RelayCommand]
    private void Back() => navigation.Pop();

    public bool CanGoBack => navigation.CanGoBack;
}
