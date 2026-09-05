using Microsoft.Maui;
using Microsoft.Maui.Controls;
using WinUiWindow = Microsoft.UI.Xaml.Window;

namespace NeraSpreadSheet.Maui.Windows.TableFilterSmoke;

public sealed class SmokeApplication : Application
{
    protected override Window CreateWindow(
        IActivationState? activationState)
    {
        var window = new Window(new SmokePage())
        {
            Title = "NeraSpreadSheet MAUI Table filter smoke",
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
