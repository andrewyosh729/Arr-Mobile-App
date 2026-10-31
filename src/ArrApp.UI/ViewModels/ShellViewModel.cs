using ArrApp.UI.Errors;
using ArrApp.UI.Navigation;

namespace ArrApp.UI.ViewModels;

/// <summary>Root of the app: hosts the page stack, bottom sheet and toast, and owns back-button behaviour.</summary>
public sealed class ShellViewModel : ViewModelBase {
    public ShellViewModel(NavigationService navigation, BottomSheetService sheets, ToastService toasts, TabHostViewModel tabs) {
        Navigation = navigation;
        Sheets = sheets;
        Toasts = toasts;
        navigation.SetRoot(tabs);
    }

    public NavigationService Navigation { get; }
    public BottomSheetService Sheets { get; }
    public ToastService Toasts { get; }

    /// <summary>Closes the sheet, else pops a page. Returns false when nothing is left to go back to (let the OS handle it).</summary>
    public bool HandleBack() {
        if (Sheets.IsOpen) {
            Sheets.Hide();
            return true;
        }

        return Navigation.Pop();
    }
}
