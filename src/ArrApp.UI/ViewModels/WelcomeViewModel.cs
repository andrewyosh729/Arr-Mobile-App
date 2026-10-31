using System;
using ArrApp.UI.Navigation;
using CommunityToolkit.Mvvm.Input;

namespace ArrApp.UI.ViewModels;

/// <summary>First-run screen, shown instead of the tabs while no server is saved.</summary>
public sealed partial class WelcomeViewModel(INavigationService navigation) : ViewModelBase {
    /// <summary>Called once the first server has been saved, so the shell can swap in the main tabs.</summary>
    public Action? Completed { get; set; }

    [RelayCommand]
    private void AddServer() => navigation.Push<ServerEditViewModel>(vm => vm.Saved = () => Completed?.Invoke());
}
