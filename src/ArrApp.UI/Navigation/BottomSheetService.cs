using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using ArrApp.UI.ViewModels;

namespace ArrApp.UI.Navigation;

public sealed class BottomSheetService(IServiceProvider services) : ObservableObject, IBottomSheetService {
    private ViewModelBase? _current;

    public ViewModelBase? Current {
        get => _current;
        private set {
            if (SetProperty(ref _current, value))
                OnPropertyChanged(nameof(IsOpen));
        }
    }

    public bool IsOpen => _current is not null;

    public TViewModel Show<TViewModel>(Action<TViewModel>? configure = null) where TViewModel : ViewModelBase {
        var vm = services.GetRequiredService<TViewModel>();
        configure?.Invoke(vm);
        Current = vm;
        return vm;
    }

    public void Hide() => Current = null;
}
