using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Viewport.Tests;

[TestClass]
public sealed class CellBoundsTests
{
    [TestMethod]
    public void TryGetCellBoundsReturnsFractionalViewportCoordinates()
    {
        var workbook = new Workbook();
        var engine = new SpreadsheetViewportEngine(new SpreadsheetSession(workbook));
        Assert.IsTrue(engine.TryGetCellBounds(new CellAddress(2, 1), 13.25d, 7.5d, out var bounds));
        Assert.AreEqual(workbook.Worksheets[0].Dimensions.DefaultColumnWidth - 13.25d, bounds.X, 1e-9);
        Assert.AreEqual((2d * workbook.Worksheets[0].Dimensions.DefaultRowHeight) - 7.5d, bounds.Y, 1e-9);
    }

    [TestMethod]
    public void TryGetCellBoundsRejectsHiddenRow()
    {
        var workbook = new Workbook();
        workbook.Worksheets[0].Dimensions.SetRowHeight(3, 0d);
        var engine = new SpreadsheetViewportEngine(new SpreadsheetSession(workbook));
        Assert.IsFalse(engine.TryGetCellBounds(new CellAddress(3, 0), 0d, 0d, out _));
    }

    [TestMethod]
    public void HiddenAxisRangesShouldCompressViewportWithoutMaterializingEntries()
    {
        var workbook = new Workbook();
        var dimensions = workbook.Worksheets[0].Dimensions;
        dimensions.HideRows(1, 3);
        dimensions.HideColumns(1, 2);
        var engine = new SpreadsheetViewportEngine(new SpreadsheetSession(workbook));

        Assert.IsFalse(engine.TryGetCellBounds(
            new CellAddress(2, 0),
            0d,
            0d,
            out _));
        Assert.IsFalse(engine.TryGetCellBounds(
            new CellAddress(0, 2),
            0d,
            0d,
            out _));
        Assert.IsTrue(engine.TryGetCellBounds(
            new CellAddress(4, 3),
            0d,
            0d,
            out var bounds));
        Assert.AreEqual(dimensions.DefaultColumnWidth, bounds.X, 1e-9);
        Assert.AreEqual(dimensions.DefaultRowHeight, bounds.Y, 1e-9);
    }
}
