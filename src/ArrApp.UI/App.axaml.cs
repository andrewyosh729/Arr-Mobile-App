using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ArrApp.UI.ViewModels;
using ArrApp.UI.Views;

namespace ArrApp.UI;

public partial class App : Application {
    public override void Initialize() {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted() {
        var mainView = new MainView { DataContext = new MainViewModel() };

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
