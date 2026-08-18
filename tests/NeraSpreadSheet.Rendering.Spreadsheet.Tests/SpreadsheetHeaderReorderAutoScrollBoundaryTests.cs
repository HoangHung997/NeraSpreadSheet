using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class SpreadsheetHeaderReorderAutoScrollBoundaryTests
{
    [TestMethod]
    public void VelocityIsZeroAtActivationBoundaryAndClampedOutsideViewport()
    {
        var viewport = new RectD(40d, 24d, 400d, 300d);
        var atBoundary = SpreadsheetHeaderReorderAutoScroll.CalculateVelocity(
            WorksheetAxis.Row,
            new PointD(40d, viewport.Bottom - SpreadsheetHeaderReorderAutoScroll.DefaultEdgeZone),
            viewport);
        var belowViewport = SpreadsheetHeaderReorderAutoScroll.CalculateVelocity(
            WorksheetAxis.Row,
            new PointD(40d, viewport.Bottom + 200d),
            viewport);

        Assert.AreEqual(default, atBoundary);
        Assert.AreEqual(
            SpreadsheetHeaderReorderAutoScroll.DefaultMaximumSpeed,
            belowViewport.Y,
            0.0001d);
    }
}
