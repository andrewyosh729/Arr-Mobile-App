using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace ArrApp.Android;

[Activity(
    Label = "Arr",
    Theme = "@style/Theme.AppCompat.NoActionBar",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity { }
