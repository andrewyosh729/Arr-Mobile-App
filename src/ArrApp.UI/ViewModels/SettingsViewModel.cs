using ArrApp.UI.Navigation;
using CommunityToolkit.Mvvm.Input;

namespace ArrApp.UI.ViewModels;

/// <summary>Settings tab. For now just the way into server management; theme, cache and about arrive with F-605.</summary>
public sealed partial class SettingsViewModel(INavigationService navigation) : ViewModelBase {
    [RelayCommand]
    private void OpenServers() => navigation.Push<ServersViewModel>(vm => _ = vm.LoadAsync());
}
