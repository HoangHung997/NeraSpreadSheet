using Microsoft.Maui.Controls;

namespace NeraSpreadSheet.Maui.MacCatalyst.AnalyticsSmoke;

public sealed class SmokeApplication : Application
{
    public SmokeApplication()
    {
        SmokeTrace.Append("smoke-application-constructor");
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        SmokeTrace.Append("smoke-application-create-window-enter");
        var window = new Window(new SmokePage())
        {
            Title = "Nera Mac Catalyst analytics accessibility smoke",
            Width = 900d,
            Height = 620d,
        };
        SmokeTrace.Append("smoke-application-create-window-success");
        return window;
    }
}
