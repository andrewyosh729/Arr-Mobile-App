using Avalonia;
using Avalonia.iOS;
using Foundation;
using ArrApp.UI;

namespace ArrApp.iOS;

[Register("AppDelegate")]
public class AppDelegate : AvaloniaAppDelegate<App> {
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        => base.CustomizeAppBuilder(builder).WithInterFont();
}
