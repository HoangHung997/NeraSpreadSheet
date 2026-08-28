using Foundation;

namespace NeraSpreadSheet.Maui.MacCatalyst.AnalyticsSmoke;

[Register("AppDelegate")]
public sealed class SmokeApplicationHost : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp()
    {
        SmokeTrace.Append("appdelegate-create-maui-app-enter");
        try
        {
            var app = MauiProgram.CreateMauiApp();
            SmokeTrace.Append("appdelegate-create-maui-app-success");
            return app;
        }
        catch (Exception exception)
        {
            SmokeTrace.Append($"appdelegate-create-maui-app-catch:{exception.GetType().FullName}");
            throw;
        }
    }
}
