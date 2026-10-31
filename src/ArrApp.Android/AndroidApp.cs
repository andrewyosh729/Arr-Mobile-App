using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using ArrApp.UI;

namespace ArrApp.Android;

[Application]
public class AndroidApp : AvaloniaAndroidApplication<App> {
    public AndroidApp(nint javaReference, JniHandleOwnership transfer) : base(javaReference, transfer) { }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        => base.CustomizeAppBuilder(builder).WithInterFont();
}
