using Microsoft.Maui;
using Microsoft.Maui.Controls;
using WinUiWindow = Microsoft.UI.Xaml.Window;

namespace NeraSpreadSheet.Maui.Windows.AnalyticsSmoke;

public sealed class SmokeApplication : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new SmokePage())
        {
            Title = "NeraSpreadSheet MAUI analytics smoke",
            Width = 900d,
            Height = 620d,
        };
        window.HandlerChanged += (_, _) =>
        {
            if (window.Handler?.PlatformView is WinUiWindow nativeWindow)
            {
                nativeWindow.Activate();
            }
        };
        return window;
    }
}
