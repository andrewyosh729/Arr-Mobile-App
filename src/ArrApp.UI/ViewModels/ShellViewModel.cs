using ArrApp.UI.Navigation;

namespace ArrApp.UI.ViewModels;

/// <summary>Root of the app: hosts the page stack and the bottom sheet, and owns back-button behaviour.</summary>
public sealed class ShellViewModel : ViewModelBase {
    public ShellViewModel(NavigationService navigation, BottomSheetService sheets, TabHostViewModel tabs) {
        Navigation = navigation;
        Sheets = sheets;
        navigation.SetRoot(tabs);
    }

    public NavigationService Navigation { get; }
    public BottomSheetService Sheets { get; }

    /// <summary>Closes the sheet, else pops a page. Returns false when nothing is left to go back to (let the OS handle it).</summary>
    public bool HandleBack() {
        if (Sheets.IsOpen) {
            Sheets.Hide();
            return true;
        }

        return Navigation.Pop();
    }
}
