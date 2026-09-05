using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace Packaged.Maui.Smoke.Platforms.Windows;

public sealed partial class App : MauiWinUIApplication
{
    public App() => InitializeComponent();
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
