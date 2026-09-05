using Foundation;

namespace NeraSpreadSheet.Maui.iOS.AnalyticsSmoke;

[Register("AppDelegate")]
public sealed class SmokeApplicationHost : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
