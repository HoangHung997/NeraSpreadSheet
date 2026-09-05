using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering.Spreadsheet;
using SkiaSharp;

namespace NeraSpreadSheet.Rendering.Skia.Tests;

[TestClass]
public sealed class SkiaTableStyleRenderingTests
{
    [TestMethod]
    public void SkiaShouldRasterizeSharedResolvedTableStyleColors()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var body = new ColorRgba(226, 238, 249);
        var header = new ColorRgba(37, 91, 154);
        var definition = new TableStyleDefinition(
            "custom:skia-raster",
            "SkiaRaster",
            [
                new TableStyleElement(
                    TableStyleElementType.WholeTable,
                    new TableStyleFormat
                    {
                        FillColor = TableStyleColor.FromRgb(body),
                    }),
                new TableStyleElement(
                    TableStyleElementType.HeaderRow,
                    new TableStyleFormat
                    {
                        FillColor = TableStyleColor.FromRgb(header),
                    }),
            ]);
        workbook.TableStyles.AddOrReplaceCustom(definition);
        worksheet.AddTable(new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(default, new CellAddress(3, 1)),
            [
                new SpreadsheetTableColumn(Guid.NewGuid(), "A"),
                new SpreadsheetTableColumn(Guid.NewGuid(), "B"),
            ],
            styleName: definition.Name,
            showRowStripes: false));
        var layout = new ViewportLayoutEngine(
                new SparseAxisMetricIndex(10, 20d),
                new SparseAxisMetricIndex(10, 80d))
            .Compute(new ViewportRequest(
                0d,
                0d,
                new SizeD(160d, 80d),
                0d));
        var displayList = SpreadsheetDisplayListComposer.Compose(
            WorksheetSnapshot.Capture(worksheet),
            layout,
            styles: workbook.Styles);
        using var surface = SKSurface.Create(new SKImageInfo(160, 80))
            ?? throw new AssertFailedException("Skia surface was not created.");
        using var renderer = new SkiaDisplayListRenderer();

        renderer.Render(surface.Canvas, displayList);
        surface.Canvas.Flush();

        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image)
            ?? throw new AssertFailedException("Skia snapshot was not created.");
        AssertColor(bitmap.GetPixel(20, 10), header);
        AssertColor(bitmap.GetPixel(20, 30), body);
    }

    private static void AssertColor(SKColor actual, ColorRgba expected)
    {
        Assert.AreEqual(expected.Red, actual.Red);
        Assert.AreEqual(expected.Green, actual.Green);
        Assert.AreEqual(expected.Blue, actual.Blue);
        Assert.AreEqual(expected.Alpha, actual.Alpha);
    }
}
