using Foundation;

namespace NeraSpreadSheet.Maui.MacCatalyst.AnalyticsSmoke;

[Register("AppDelegate")]
public sealed class SmokeApplicationHost : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
