using Android.App;
using Android.Runtime;

namespace Packaged.Maui.Smoke.Platforms.Android;

[Application(Theme = "@style/Maui.MainTheme.NoActionBar")]
public sealed class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership) : base(handle, ownership) { }
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
