using System;
using System.Threading.Tasks;
using ArrApp.UI.Errors;
using ArrApp.UI.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArrApp.UI.ViewModels;

/// <summary>Reusable bottom-sheet confirmation ("Delete X?"). Configure it through <see cref="IBottomSheetService.Show{T}"/>.</summary>
public sealed partial class ConfirmViewModel(IBottomSheetService sheets, IErrorReporter errors) : ViewModelBase {
    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string _message = "";

    [ObservableProperty]
    private string _confirmText = "Confirm";

    [ObservableProperty]
    private bool _isDestructive;

    public bool IsNotDestructive => !IsDestructive;

    /// <summary>Runs after the sheet has closed.</summary>
    public Func<Task>? OnConfirm { get; set; }

    partial void OnIsDestructiveChanged(bool value) => OnPropertyChanged(nameof(IsNotDestructive));

    [RelayCommand]
    private async Task ConfirmAsync() {
        sheets.Hide();
        if (OnConfirm is null)
            return;

        try {
            await OnConfirm();
        } catch (Exception ex) {
            errors.Report(ex);
        }
    }

    [RelayCommand]
    private void Cancel() => sheets.Hide();
}
