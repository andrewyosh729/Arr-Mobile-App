using System;
using ArrApp.UI.ViewModels;

namespace ArrApp.UI.Navigation;

/// <summary>Single bottom sheet shown over the current page (add, interactive search, filters, ...).</summary>
public interface IBottomSheetService {
    ViewModelBase? Current { get; }
    bool IsOpen { get; }

    TViewModel Show<TViewModel>(Action<TViewModel>? configure = null) where TViewModel : ViewModelBase;
    void Hide();
}
