using Microsoft.Maui;
using Microsoft.Maui.Controls;
using WinUiWindow = Microsoft.UI.Xaml.Window;

namespace NeraSpreadSheet.Maui.Windows.Smoke;

public sealed class SmokeApplication : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Table-filter lifecycle has its own loaded native smoke immediately
        // before this job step. Keep this application focused on repeated GPU
        // context, input and surface recreation so it does not construct and
        // dispose a second native visual tree before the real Window exists.
        var window = new Window(new SmokePage())
        {
            Title = "NeraSpreadSheet MAUI GPU smoke",
            Width = 960d,
            Height = 640d,
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
