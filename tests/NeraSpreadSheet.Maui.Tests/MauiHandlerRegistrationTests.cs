using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkiaSharp.Views.Maui.Controls;
using SkiaSharp.Views.Maui.Handlers;

namespace NeraSpreadSheet.Maui.Tests;

[TestClass]
public sealed class MauiHandlerRegistrationTests
{
    [TestMethod]
    public void DerivedSpreadsheetViewResolvesThroughSkiaGpuBaseHandler()
    {
        using var app = MauiApp.CreateBuilder()
            .UseNeraSpreadSheet()
            .Build();
        var handlers = app.Services.GetRequiredService<IMauiHandlersFactory>();

        Assert.AreEqual(
            typeof(SKGLViewHandler),
            handlers.GetHandlerType(typeof(SKGLView)));
        Assert.AreEqual(
            typeof(SKGLViewHandler),
            handlers.GetHandlerType(typeof(NeraSpreadsheetView)));
    }

    [TestMethod]
    public void BuilderExtensionRejectsNullBuilder()
    {
        MauiAppBuilder? builder = null;

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            builder!.UseNeraSpreadSheet());
    }
}
