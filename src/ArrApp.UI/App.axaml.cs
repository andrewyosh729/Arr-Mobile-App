using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ArrApp.UI.Errors;
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
        GlobalExceptionHandler.Install(
            Services.GetRequiredService<ILoggerFactory>().CreateLogger("Global"),
            Services.GetRequiredService<IErrorReporter>());

        var shellView = new ShellView { DataContext = Services.GetRequiredService<ShellViewModel>() };

        switch (ApplicationLifetime) {
            case IClassicDesktopStyleApplicationLifetime desktop:
                desktop.MainWindow = new Window {
                    Title = "Arr",
                    Width = 400,
                    Height = 800,
                    Content = shellView,
                };
                break;
            case ISingleViewApplicationLifetime singleView:
                singleView.MainView = shellView;
                break;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
