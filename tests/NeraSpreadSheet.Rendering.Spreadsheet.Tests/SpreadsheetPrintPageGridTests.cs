using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class SpreadsheetPrintPageGridTests
{
    [TestMethod]
    public void GridMapsRepeatedTitlesAndDataIntoPrintableCoordinates()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        for (var row = 0; row < 3; row++)
        {
            worksheet.Dimensions.SetRowHeight(row, 20d);
        }
        for (var column = 0; column < 3; column++)
        {
            worksheet.Dimensions.SetColumnWidth(column, 80d);
        }
        var snapshot = WorksheetSnapshot.Capture(worksheet);
        var page = new SpreadsheetPrintPage(
            1,
            0,
            0,
            new CellRange(
                new CellAddress(1, 1),
                new CellAddress(2, 2)),
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(0, 2)),
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(2, 0)),
            0.5d,
            new NeraSpreadSheet.Foundation.SizeD(800d, 1000d),
            new NeraSpreadSheet.Foundation.RectD(50d, 60d, 700d, 880d),
            new NeraSpreadSheet.Foundation.SizeD(240d, 60d),
            new NeraSpreadSheet.Foundation.PointD(10d, 15d));

        var grid = SpreadsheetPrintPageGridBuilder.Create(snapshot, page);

        CollectionAssert.AreEqual(
            new[] { 0, 1, 2 },
            grid.Rows.Select(static slot => slot.WorksheetIndex).ToArray());
        CollectionAssert.AreEqual(
            new[] { 0, 1, 2 },
            grid.Columns.Select(static slot => slot.WorksheetIndex).ToArray());
        Assert.IsTrue(grid.Rows[0].IsRepeated);
        Assert.IsFalse(grid.Rows[1].IsRepeated);
        Assert.IsTrue(grid.Columns[0].IsRepeated);
        Assert.IsFalse(grid.Columns[1].IsRepeated);
        Assert.IsTrue(grid.TryGetCellBounds(
            new CellAddress(1, 1),
            out var bounds));
        Assert.AreEqual(100d, bounds.X, 0.000001d);
        Assert.AreEqual(85d, bounds.Y, 0.000001d);
        Assert.AreEqual(40d, bounds.Width, 0.000001d);
        Assert.AreEqual(10d, bounds.Height, 0.000001d);
        Assert.IsFalse(grid.TryGetCellBounds(
            new CellAddress(10, 10),
            out _));
    }

    [TestMethod]
    public void HeaderFooterFormatterExpandsStandardTokens()
    {
        var context = new SpreadsheetHeaderFooterContext(
            2,
            7,
            "Estimate",
            "Budget.xlsx",
            new DateTime(2026, 8, 22, 14, 30, 0));
        var culture = CultureInfo.GetCultureInfo("en-US");

        var formatted = SpreadsheetHeaderFooterFormatter.Format(
            "&F — &A — Page &P of &N — &D &T — &&",
            context,
            culture);

        Assert.AreEqual(
            "Budget.xlsx — Estimate — Page 2 of 7 — 8/22/2026 2:30 PM — &",
            formatted);
    }

    [TestMethod]
    public void HeaderFooterFormatterPreservesUnknownTokens()
    {
        var result = SpreadsheetHeaderFooterFormatter.Format(
            "Value &Z",
            new SpreadsheetHeaderFooterContext(1, 1, "Sheet1"),
            CultureInfo.InvariantCulture);

        Assert.AreEqual("Value &Z", result);
    }

    [TestMethod]
    public void HeaderFooterFormatterRejectsInvalidPageContext()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            SpreadsheetHeaderFooterFormatter.Format(
                "&P",
                new SpreadsheetHeaderFooterContext(0, 1, "Sheet1")));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            SpreadsheetHeaderFooterFormatter.Format(
                "&P",
                new SpreadsheetHeaderFooterContext(2, 1, "Sheet1")));
    }
}
