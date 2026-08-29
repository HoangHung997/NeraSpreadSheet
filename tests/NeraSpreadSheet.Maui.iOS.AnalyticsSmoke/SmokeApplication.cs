using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace NeraSpreadSheet.Maui.iOS.AnalyticsSmoke;

public sealed class SmokeApplication : Application
{
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new SmokePage())
        {
            Title = "Nera iOS analytics accessibility smoke",
        };
}
