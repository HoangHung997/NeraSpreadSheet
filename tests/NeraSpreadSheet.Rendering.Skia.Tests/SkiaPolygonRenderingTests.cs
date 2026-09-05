using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering;
using SkiaSharp;

namespace NeraSpreadSheet.Rendering.Skia.Tests;

[TestClass]
public sealed class SkiaPolygonRenderingTests
{
    [TestMethod]
    public void FilledPolygonRendersInteriorWithoutPaintingExterior()
    {
        using var surface = SKSurface.Create(new SKImageInfo(
            64,
            64,
            SKColorType.Bgra8888,
            SKAlphaType.Premul))
            ?? throw new AssertFailedException(
                "Skia raster surface could not be created.");
        using var renderer = new SkiaDisplayListRenderer();
        var fill = new ColorRgba(68, 114, 196);
        var builder = new DisplayListBuilder();
        builder.FillRectangle(
            new RectD(0d, 0d, 64d, 64d),
            ColorRgba.White);
        builder.FillPolygon(
            [
                new PointD(8d, 8d),
                new PointD(56d, 8d),
                new PointD(32d, 56d),
            ],
            fill);

        renderer.Render(surface.Canvas, builder.Build());
        surface.Canvas.Flush();

        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image)
            ?? throw new AssertFailedException(
                "Skia snapshot bitmap could not be created.");
        var inside = bitmap.GetPixel(32, 24);
        var outside = bitmap.GetPixel(4, 4);

        Assert.AreEqual(fill.Red, inside.Red);
        Assert.AreEqual(fill.Green, inside.Green);
        Assert.AreEqual(fill.Blue, inside.Blue);
        Assert.AreEqual(fill.Alpha, inside.Alpha);
        Assert.AreEqual((byte)255, outside.Red);
        Assert.AreEqual((byte)255, outside.Green);
        Assert.AreEqual((byte)255, outside.Blue);
        Assert.AreEqual((byte)255, outside.Alpha);
    }
}
