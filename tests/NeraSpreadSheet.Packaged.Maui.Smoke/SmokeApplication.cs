using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace Packaged.Maui.Smoke;

public sealed class SmokeApplication : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new SmokePage()) { Title = "Kiểm tra gói NeraSpreadSheet", Width = 900, Height = 700 };
#if WINDOWS
        window.HandlerChanged += (_, _) =>
        {
            if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window native) native.Activate();
        };
#endif
        return window;
    }
}
