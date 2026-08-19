using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace NeraSpreadSheet.Maui.Windows.Smoke;

public sealed class SmokeApplication : Application
{
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new SmokePage())
        {
            Title = "NeraSpreadSheet MAUI GPU smoke",
            Width = 960d,
            Height = 640d,
        };
}
