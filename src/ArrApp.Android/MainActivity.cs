using Android.App;
using Android.Content.PM;
using Android.OS;
using Avalonia.Android;

namespace ArrApp.Android;

[Activity(
    Label = "Arr",
    Theme = "@style/Theme.AppCompat.NoActionBar",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity {
    protected override void OnPostResume() {
        base.OnPostResume();

        // Avalonia 12.0.2 reports the very first safe-area event in raw pixels instead of layout units, which pads the
        // top/bottom ~2.6x too much until another insets pass happens. Asking Android to re-dispatch the insets once the
        // window is up produces the correctly scaled event. Remove if a newer Avalonia fixes the first event.
        var decor = Window?.DecorView;
        decor?.PostDelayed(() => decor.RequestApplyInsets(), 50);
    }
}
