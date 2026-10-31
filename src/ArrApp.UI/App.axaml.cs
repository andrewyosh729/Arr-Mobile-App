using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ArrApp.Core;
using ArrApp.UI.Errors;
using ArrApp.UI.Theme;
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

        // The active server decides the accent colour (Sonarr cyan, Radarr gold) so the context is obvious at a glance.
        var context = Services.GetRequiredService<ArrContext>();
        context.PropertyChanged += (_, e) => {
            if (e.PropertyName == nameof(ArrContext.ArrType))
                ThemeManager.ApplyAccent(this, context.ArrType);
        };

        var shell = Services.GetRequiredService<ShellViewModel>();
        var shellView = new ShellView { DataContext = shell };
        _ = shell.InitializeAsync(); // shows the welcome screen or the tabs once the saved servers are loaded

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
