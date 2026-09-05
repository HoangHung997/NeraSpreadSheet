using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class SpreadsheetChromeGeometryTests
{
    [TestMethod]
    public void HeadersReduceBodyViewportWithoutChangingConfiguredHeaderExtent()
    {
        var theme = new SpreadsheetRenderTheme { ShowHeaders = true, RowHeaderWidth = 48d, ColumnHeaderHeight = 24d };

        var metrics = SpreadsheetChromeGeometry.Calculate(640d, 480d, theme);

        Assert.AreEqual(48d, metrics.RowHeaderWidth, 1e-9);
        Assert.AreEqual(24d, metrics.ColumnHeaderHeight, 1e-9);
        Assert.AreEqual(592d, metrics.BodyWidth, 1e-9);
        Assert.AreEqual(456d, metrics.BodyHeight, 1e-9);
    }

    [TestMethod]
    public void HitTestClassifiesCornerHeadersAndBodyInBodyLocalCoordinates()
    {
        var theme = new SpreadsheetRenderTheme { ShowHeaders = true, RowHeaderWidth = 48d, ColumnHeaderHeight = 24d };

        var corner = SpreadsheetChromeGeometry.HitTest(10d, 10d, 640d, 480d, theme);
        var row = SpreadsheetChromeGeometry.HitTest(10d, 44d, 640d, 480d, theme);
        var column = SpreadsheetChromeGeometry.HitTest(68d, 10d, 640d, 480d, theme);
        var body = SpreadsheetChromeGeometry.HitTest(68d, 44d, 640d, 480d, theme);

        Assert.AreEqual(SpreadsheetChromeRegion.Corner, corner.Region);
        Assert.AreEqual(SpreadsheetChromeRegion.RowHeader, row.Region);
        Assert.AreEqual(20d, row.BodyY, 1e-9);
        Assert.AreEqual(SpreadsheetChromeRegion.ColumnHeader, column.Region);
        Assert.AreEqual(20d, column.BodyX, 1e-9);
        Assert.AreEqual(SpreadsheetChromeRegion.Body, body.Region);
        Assert.AreEqual(20d, body.BodyX, 1e-9);
        Assert.AreEqual(20d, body.BodyY, 1e-9);
    }

    [TestMethod]
    public void DisabledHeadersTreatEntireSurfaceAsBody()
    {
        var theme = new SpreadsheetRenderTheme();

        var hit = SpreadsheetChromeGeometry.HitTest(20d, 30d, 640d, 480d, theme);

        Assert.AreEqual(SpreadsheetChromeRegion.Body, hit.Region);
        Assert.AreEqual(20d, hit.BodyX, 1e-9);
        Assert.AreEqual(30d, hit.BodyY, 1e-9);
    }
}
