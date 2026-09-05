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
        var page = new SmokePage();
        page.Appearing += static (_, _) => SmokeTrace.Append("smoke-page-appearing");
        page.Disappearing += static (_, _) => SmokeTrace.Append("smoke-page-disappearing");
        page.HandlerChanged += static (_, _) => SmokeTrace.Append("smoke-page-handler-changed");

        var window = new Window(page)
        {
            Title = "Nera Mac Catalyst analytics accessibility smoke",
            Width = 900d,
            Height = 620d,
        };
        window.HandlerChanged += static (_, _) => SmokeTrace.Append("smoke-window-handler-changed");
        window.Created += static (_, _) => SmokeTrace.Append("smoke-window-created");
        window.Resumed += static (_, _) => SmokeTrace.Append("smoke-window-resumed");
        window.Activated += static (_, _) => SmokeTrace.Append("smoke-window-activated");
        window.Deactivated += static (_, _) => SmokeTrace.Append("smoke-window-deactivated");
        window.Stopped += static (_, _) => SmokeTrace.Append("smoke-window-stopped");
        window.Destroying += static (_, _) => SmokeTrace.Append("smoke-window-destroying");

        SmokeTrace.Append("smoke-application-create-window-success");
        return window;
    }
}
