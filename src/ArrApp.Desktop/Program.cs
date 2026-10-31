using Avalonia;
using System;
using System.IO;
using ArrApp.Data.Security;
using ArrApp.UI;
using ArrApp.UI.Logging;

namespace ArrApp.Desktop;

sealed class Program {
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) {
        PlatformServices.ApiKeyProtector = OperatingSystem.IsWindows()
            ? new DpapiApiKeyProtector()
            : KeyFileApiKeyProtector.Create(Path.Combine(AppPaths.DataDirectory, "apikey.key"));

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
