using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace NeraSpreadSheet.Maui.Windows.Smoke;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
