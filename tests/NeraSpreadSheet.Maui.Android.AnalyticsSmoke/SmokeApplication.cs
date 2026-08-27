using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace NeraSpreadSheet.Maui.Android.AnalyticsSmoke;

public sealed class SmokeApplication : Application
{
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new SmokePage())
        {
            Title = "Nera Android analytics accessibility smoke",
        };
}
