using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering;
using SkiaSharp;

namespace NeraSpreadSheet.Rendering.Skia.Tests;

[TestClass]
public sealed class SkiaDisplayListRendererTests
{
    [TestMethod]
    public void NestedTranslationAndClipRenderExpectedPixels()
    {
        using var surface = CreateSurface(96, 72);
        using var renderer = new SkiaDisplayListRenderer();
        var child = new DisplayListBuilder();
        child.FillRectangle(
            new RectD(0d, 0d, 40d, 40d),
            new ColorRgba(220, 40, 30));

        var root = new DisplayListBuilder();
        root.FillRectangle(
            new RectD(0d, 0d, 96d, 72d),
            ColorRgba.White);
        root.PushClip(new RectD(10d, 8d, 40d, 44d));
        root.PushTranslation(20d, 10d);
        root.DrawDisplayList(child.Build());
        root.PopTranslation();
        root.PopClip();

        renderer.Render(surface.Canvas, root.Build());
        surface.Canvas.Flush();

        using var bitmap = Snapshot(surface);
        AssertColor(bitmap.GetPixel(25, 15), 220, 40, 30, 255);
        AssertColor(bitmap.GetPixel(55, 15), 255, 255, 255, 255);
        AssertColor(bitmap.GetPixel(25, 55), 255, 255, 255, 255);
    }

    [TestMethod]
    public void DrawLineAndTextProduceVisibleRasterContentAndReuseTypeface()
    {
        using var surface = CreateSurface(180, 80);
        using var renderer = new SkiaDisplayListRenderer();
        var builder = new DisplayListBuilder();
        builder.FillRectangle(new RectD(0d, 0d, 180d, 80d), ColorRgba.White);
        builder.DrawLine(
            new PointD(4d, 70d),
            new PointD(176d, 70d),
            2d,
            ColorRgba.Black);
        var style = new TextStyle(
            "sans-serif",
            18d,
            600,
            new ColorRgba(20, 70, 160));
        builder.DrawText("Nera", new RectD(8d, 8d, 120d, 32d), style);
        builder.DrawText("Spreadsheet", new RectD(8d, 34d, 160d, 30d), style);
        var displayList = builder.Build();

        renderer.Render(surface.Canvas, displayList);
        renderer.Render(surface.Canvas, displayList);
        surface.Canvas.Flush();

        using var bitmap = Snapshot(surface);
        var nonWhitePixels = 0;
        for (var y = 6; y < 72; y++)
        {
            for (var x = 4; x < 176; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.Red != 255 || pixel.Green != 255 || pixel.Blue != 255)
                {
                    nonWhitePixels++;
                }
            }
        }

        var diagnostics = renderer.GetDiagnostics();
        Assert.IsTrue(nonWhitePixels > 100);
        Assert.IsTrue(diagnostics.CachedTypefaceCount > 0);
        Assert.IsTrue(diagnostics.TypefaceCacheHits > 0);
        Assert.AreEqual(2L, diagnostics.SuccessfulRenderCount);
        Assert.IsTrue(diagnostics.ExecutedCommandCount >= 8L);
    }

    [TestMethod]
    public void WrappedTextRemainsInsideCommandClip()
    {
        using var surface = CreateSurface(150, 100);
        using var renderer = new SkiaDisplayListRenderer();
        var builder = new DisplayListBuilder();
        builder.FillRectangle(new RectD(0d, 0d, 150d, 100d), ColorRgba.White);
        builder.DrawText(
            "one two three four five six seven",
            new RectD(10d, 10d, 70d, 45d),
            new TextStyle(
                "sans-serif",
                14d,
                400,
                ColorRgba.Black,
                Wrap: true));

        renderer.Render(surface.Canvas, builder.Build());
        surface.Canvas.Flush();

        using var bitmap = Snapshot(surface);
        AssertColor(bitmap.GetPixel(120, 20), 255, 255, 255, 255);
        AssertColor(bitmap.GetPixel(120, 60), 255, 255, 255, 255);
    }

    [TestMethod]
    public void DpiScalingMapsLogicalPixelsAndRestoresCallerSaveDepth()
    {
        using var surface = CreateSurface(40, 40);
        using var renderer = new SkiaDisplayListRenderer();
        surface.Canvas.Clear(SKColors.White);

        var builder = new DisplayListBuilder();
        builder.FillRectangle(
            new RectD(0d, 0d, 10d, 10d),
            new ColorRgba(180, 30, 20));
        var originalSaveCount = surface.Canvas.SaveCount;

        renderer.Render(
            surface.Canvas,
            builder.Build(),
            dpiScaleX: 2d,
            dpiScaleY: 2d);
        surface.Canvas.Flush();

        Assert.AreEqual(originalSaveCount, surface.Canvas.SaveCount);
        using var bitmap = Snapshot(surface);
        AssertColor(bitmap.GetPixel(15, 15), 180, 30, 20, 255);
        AssertColor(bitmap.GetPixel(25, 15), 255, 255, 255, 255);
    }

    [TestMethod]
    public void TypefaceCacheIsBoundedAndReportsHitsMissesAndEviction()
    {
        using var surface = CreateSurface(160, 48);
        using var renderer = new SkiaDisplayListRenderer(typefaceCacheCapacity: 2);

        renderer.Render(surface.Canvas, BuildTextList("Nera-Missing-Family-A"));
        renderer.Render(surface.Canvas, BuildTextList("Nera-Missing-Family-B"));
        renderer.Render(surface.Canvas, BuildTextList("Nera-Missing-Family-B"));
        renderer.Render(surface.Canvas, BuildTextList("Nera-Missing-Family-C"));

        var diagnostics = renderer.GetDiagnostics();
        Assert.AreEqual(2, diagnostics.TypefaceCacheCapacity);
        Assert.AreEqual(2, diagnostics.CachedTypefaceCount);
        Assert.IsTrue(diagnostics.TypefaceCacheHits >= 1L);
        Assert.IsTrue(diagnostics.TypefaceCacheMisses >= 3L);
        Assert.IsTrue(diagnostics.TypefaceCacheEvictions >= 1L);
    }

    [TestMethod]
    public void FailedRenderRestoresCallerSaveDepthAndRecordsDiagnostics()
    {
        using var surface = CreateSurface(40, 40);
        using var renderer = new SkiaDisplayListRenderer();
        var originalSaveCount = surface.Canvas.SaveCount;
        var corruptList = CreateDisplayList(
            new PushTranslationCommand(5d, 7d),
            new UnsupportedRenderCommand());
        var threw = false;

        try
        {
            renderer.Render(surface.Canvas, corruptList);
        }
        catch (NotSupportedException)
        {
            threw = true;
        }

        Assert.IsTrue(threw);
        Assert.AreEqual(originalSaveCount, surface.Canvas.SaveCount);
        var diagnostics = renderer.GetDiagnostics();
        Assert.AreEqual(0L, diagnostics.SuccessfulRenderCount);
        Assert.AreEqual(1L, diagnostics.FailedRenderCount);
        Assert.AreEqual(2L, diagnostics.ExecutedCommandCount);
    }

    [TestMethod]
    public void InvalidDpiScaleIsRejectedBeforeCanvasStateChanges()
    {
        using var surface = CreateSurface(20, 20);
        using var renderer = new SkiaDisplayListRenderer();
        var originalSaveCount = surface.Canvas.SaveCount;
        var threw = false;

        try
        {
            renderer.Render(
                surface.Canvas,
                new DisplayListBuilder().Build(),
                dpiScaleX: 0d,
                dpiScaleY: 1d);
        }
        catch (ArgumentOutOfRangeException)
        {
            threw = true;
        }

        Assert.IsTrue(threw);
        Assert.AreEqual(originalSaveCount, surface.Canvas.SaveCount);
        Assert.AreEqual(0L, renderer.FailedRenderCount);
    }

    private static DisplayList BuildTextList(string fontFamily)
    {
        var builder = new DisplayListBuilder();
        builder.DrawText(
            "Nera",
            new RectD(0d, 0d, 120d, 32d),
            new TextStyle(fontFamily, 14d, 400, ColorRgba.Black));
        return builder.Build();
    }

    private static DisplayList CreateDisplayList(params RenderCommand[] commands)
    {
        var constructor = typeof(DisplayList).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(RenderCommand[])],
            modifiers: null)
            ?? throw new AssertFailedException(
                "DisplayList internal constructor was not found.");
        return (DisplayList)constructor.Invoke(new object?[] { commands });
    }

    private static SKSurface CreateSurface(int width, int height) =>
        SKSurface.Create(new SKImageInfo(
            width,
            height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul))
        ?? throw new AssertFailedException("Skia raster surface could not be created.");

    private static SKBitmap Snapshot(SKSurface surface)
    {
        using var image = surface.Snapshot();
        return SKBitmap.FromImage(image)
            ?? throw new AssertFailedException("Skia snapshot bitmap could not be created.");
    }

    private static void AssertColor(
        SKColor color,
        byte red,
        byte green,
        byte blue,
        byte alpha)
    {
        Assert.AreEqual(red, color.Red);
        Assert.AreEqual(green, color.Green);
        Assert.AreEqual(blue, color.Blue);
        Assert.AreEqual(alpha, color.Alpha);
    }

    private sealed record UnsupportedRenderCommand : RenderCommand;
}
