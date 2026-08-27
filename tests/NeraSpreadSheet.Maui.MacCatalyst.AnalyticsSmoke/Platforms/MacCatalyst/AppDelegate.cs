using Foundation;

namespace NeraSpreadSheet.Maui.MacCatalyst.AnalyticsSmoke;

[Register("AppDelegate")]
public sealed class SmokeApplicationDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
