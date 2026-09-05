using Foundation;

namespace Packaged.Maui.Smoke.Platforms.iOS;

[Register("AppDelegate")]
public sealed class PlatformApplication : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
