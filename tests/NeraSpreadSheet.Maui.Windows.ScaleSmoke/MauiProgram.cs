using Microsoft.Maui.Hosting;
using NeraSpreadSheet.Maui;

namespace NeraSpreadSheet.Maui.Windows.ScaleSmoke;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp() =>
        MauiApp.CreateBuilder()
            .UseMauiApp<ScaleSmokeApplication>()
            .UseNeraSpreadSheet()
            .Build();
}
