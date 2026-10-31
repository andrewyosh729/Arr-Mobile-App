using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;
using ArrApp.UI;

namespace ArrApp.Android;

[Activity(
    Label = "Arr",
    Theme = "@style/Theme.AppCompat.NoActionBar",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App> {
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        => base.CustomizeAppBuilder(builder).WithInterFont();
}
