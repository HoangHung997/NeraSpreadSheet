using Microsoft.Maui.Hosting;
using NeraSpreadSheet.Maui;

namespace NeraSpreadSheet.Maui.Windows.RibbonSmoke;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder()
            .UseMauiApp<SmokeApplication>()
            .UseNeraSpreadSheet();
        return builder.Build();
    }
}
