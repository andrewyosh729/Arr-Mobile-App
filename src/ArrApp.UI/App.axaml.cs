using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using ArrApp.UI.ViewModels;
using ArrApp.UI.Views;

namespace ArrApp.UI;

public partial class App : Application {
    public IServiceProvider Services { get; private set; } = null!;

    public override void Initialize() {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted() {
        Services = new ServiceCollection().AddArrApp().BuildServiceProvider();

        var mainView = new MainView { DataContext = Services.GetRequiredService<MainViewModel>() };

        switch (ApplicationLifetime) {
            case IClassicDesktopStyleApplicationLifetime desktop:
                desktop.MainWindow = new Window {
                    Title = "Arr",
                    Width = 400,
                    Height = 800,
                    Content = mainView,
                };
                break;
            case ISingleViewApplicationLifetime singleView:
                singleView.MainView = mainView;
                break;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
