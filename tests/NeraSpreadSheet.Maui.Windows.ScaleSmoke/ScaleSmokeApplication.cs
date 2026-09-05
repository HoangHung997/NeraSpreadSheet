using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace NeraSpreadSheet.Maui.Windows.ScaleSmoke;

public sealed class ScaleSmokeApplication : Application
{
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new ScaleSmokePage())
        {
            Title = "NeraSpreadSheet MAUI scale smoke",
            Width = 960d,
            Height = 640d,
        };
}
