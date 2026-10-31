using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using ArrApp.UI.ViewModels;

namespace ArrApp.UI.Navigation;

public sealed class NavigationService(IServiceProvider services) : ObservableObject, INavigationService {
    private readonly Stack<ViewModelBase> _stack = new();
    private ViewModelBase? _current;

    public ViewModelBase? Current {
        get => _current;
        private set => SetProperty(ref _current, value);
    }

    public bool CanGoBack => _stack.Count > 1;

    public void SetRoot(ViewModelBase root) {
        _stack.Clear();
        _stack.Push(root);
        Update();
    }

    public TViewModel Push<TViewModel>(Action<TViewModel>? configure = null) where TViewModel : ViewModelBase {
        var vm = services.GetRequiredService<TViewModel>();
        configure?.Invoke(vm);
        _stack.Push(vm);
        Update();
        return vm;
    }

    public bool Pop() {
        if (!CanGoBack)
            return false;
        _stack.Pop();
        Update();
        return true;
    }

    private void Update() {
        Current = _stack.Count > 0 ? _stack.Peek() : null;
        OnPropertyChanged(nameof(CanGoBack));
    }
}
