using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ArrApp.UI.ViewModels;
using ArrApp.UI.Views;

namespace ArrApp.UI;

/// <summary>
/// Maps view models to views through an explicit, trim-safe registry (no reflection).
/// Register new pairs in <see cref="Views"/> as screens are added.
/// </summary>
public class ViewLocator : IDataTemplate {
    private static readonly Dictionary<Type, Func<Control>> Views = new() {
        [typeof(MainViewModel)] = () => new MainView(),
    };

    public Control? Build(object? param) {
        if (param is null)
            return null;

        return Views.TryGetValue(param.GetType(), out var factory)
            ? factory()
            : new TextBlock { Text = "No view registered for " + param.GetType().Name };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
