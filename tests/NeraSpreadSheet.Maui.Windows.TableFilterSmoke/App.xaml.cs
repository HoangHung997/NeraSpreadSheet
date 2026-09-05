using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace NeraSpreadSheet.Maui.Windows.TableFilterSmoke;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() =>
        MauiProgram.CreateMauiApp();
}
