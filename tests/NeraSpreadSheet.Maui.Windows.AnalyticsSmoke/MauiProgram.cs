using Microsoft.Maui.Hosting;
using NeraSpreadSheet.Maui;

namespace NeraSpreadSheet.Maui.Windows.AnalyticsSmoke;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp() =>
        MauiApp.CreateBuilder()
            .UseMauiApp<SmokeApplication>()
            .UseNeraSpreadSheet()
            .Build();
}
