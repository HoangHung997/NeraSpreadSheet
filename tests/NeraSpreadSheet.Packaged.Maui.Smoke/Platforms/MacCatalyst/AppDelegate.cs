using Foundation;

namespace Packaged.Maui.Smoke.Platforms.MacCatalyst;

[Register("AppDelegate")]
public sealed class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
