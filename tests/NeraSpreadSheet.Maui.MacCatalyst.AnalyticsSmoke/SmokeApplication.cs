using Microsoft.Maui.Controls;

namespace NeraSpreadSheet.Maui.MacCatalyst.AnalyticsSmoke;

public sealed class SmokeApplication : Application
{
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new SmokePage())
        {
            Title = "Nera Mac Catalyst analytics accessibility smoke",
            Width = 900d,
            Height = 620d,
        };
}
