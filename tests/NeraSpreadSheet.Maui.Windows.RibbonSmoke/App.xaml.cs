using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace NeraSpreadSheet.Maui.Windows.RibbonSmoke;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
