using System;
using ArrApp.UI.ViewModels;

namespace ArrApp.UI.Navigation;

/// <summary>Page stack. The bottom of the stack is the root (e.g. the tab host); Push/Pop move above it.</summary>
public interface INavigationService {
    ViewModelBase? Current { get; }
    bool CanGoBack { get; }

    void SetRoot(ViewModelBase root);

    /// <summary>Resolves <typeparamref name="TViewModel"/> from DI, lets the caller pass parameters, and pushes it.</summary>
    TViewModel Push<TViewModel>(Action<TViewModel>? configure = null) where TViewModel : ViewModelBase;

    /// <summary>Pops the top page. Returns false (and does nothing) when only the root remains.</summary>
    bool Pop();
}
