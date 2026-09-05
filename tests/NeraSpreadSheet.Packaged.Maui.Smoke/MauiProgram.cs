using Microsoft.Maui.Hosting;
using NeraSpreadSheet.Maui;

namespace Packaged.Maui.Smoke;

internal static class MauiProgram
{
    public static MauiApp CreateMauiApp() => MauiApp.CreateBuilder()
        .UseMauiApp<SmokeApplication>()
        .UseNeraSpreadSheet()
        .Build();
}
