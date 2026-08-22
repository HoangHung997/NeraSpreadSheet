using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class SpreadsheetPrintPreviewLayoutTests
{
    [TestMethod]
    public void LargePlanMaterializesOnlyVisiblePageSlots()
    {
        var plan = CreatePlan(pageCount: 10_000);
        var options = new SpreadsheetPrintPreviewOptions
        {
            Zoom = 0.5d,
            PageGapDips = 20d,
            Columns = 2,
            OverscanDips = 50d,
        };

        var layout = SpreadsheetPrintPreviewLayoutEngine.Create(
            plan,
            new SizeD(900d, 700d),
            offsetXDips: 0d,
            offsetYDips: 50_000d,
            options);

        Assert.IsTrue(layout.VisiblePages.Count > 0);
        Assert.IsTrue(layout.VisiblePages.Count < 20);
        Assert.AreEqual(2, layout.Columns);
        Assert.IsTrue(layout.ContentSizeDips.Height > 1_000_000d);
    }

    [TestMethod]
    public void HitTestReturnsPageLocalCoordinates()
    {
        var plan = CreatePlan(pageCount: 3);
        var options = new SpreadsheetPrintPreviewOptions
        {
            Zoom = 0.25d,
            PageGapDips = 10d,
            Columns = 1,
            OverscanDips = 0d,
        };
        var layout = SpreadsheetPrintPreviewLayoutEngine.Create(
            plan,
            new SizeD(300d, 400d),
            offsetXDips: 0d,
            offsetYDips: 0d,
            options);
        var first = layout.VisiblePages[0];
        var x = first.BoundsDips.X + 12d;
        var y = first.BoundsDips.Y + 18d;

        Assert.IsTrue(layout.TryHitTest(
            x,
            y,
            out var hit,
            out var pagePoint));
        Assert.AreEqual(first.PageNumber, hit.PageNumber);
        Assert.AreEqual(12d, pagePoint.X, 0.000001d);
        Assert.AreEqual(18d, pagePoint.Y, 0.000001d);
        Assert.IsFalse(layout.TryHitTest(
            1d,
            1d,
            out _,
            out _));
    }

    [TestMethod]
    public void PageSlotsRespectFractionalPixelOffsets()
    {
        var plan = CreatePlan(pageCount: 5);
        var layout = SpreadsheetPrintPreviewLayoutEngine.Create(
            plan,
            new SizeD(400d, 300d),
            offsetXDips: 17.25d,
            offsetYDips: 31.75d,
            new SpreadsheetPrintPreviewOptions
            {
                Zoom = 0.2d,
                PageGapDips = 12.5d,
                Columns = 1,
            });

        Assert.AreEqual(17.25d, layout.OffsetXDips, 0.000001d);
        Assert.AreEqual(31.75d, layout.OffsetYDips, 0.000001d);
        Assert.IsTrue(layout.VisiblePages.Count > 0);
    }

    [TestMethod]
    public void InvalidZoomAndColumnCountsAreRejected()
    {
        var plan = CreatePlan(pageCount: 1);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            SpreadsheetPrintPreviewLayoutEngine.Create(
                plan,
                new SizeD(100d, 100d),
                0d,
                0d,
                new SpreadsheetPrintPreviewOptions
                {
                    Zoom = 0d,
                }));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            SpreadsheetPrintPreviewLayoutEngine.Create(
                plan,
                new SizeD(100d, 100d),
                0d,
                0d,
                new SpreadsheetPrintPreviewOptions
                {
                    Columns = 0,
                }));
    }

    private static SpreadsheetPageLayoutPlan CreatePlan(int pageCount)
    {
        var pages = Enumerable.Range(0, pageCount)
            .Select(index => new SpreadsheetPrintPage(
                index + 1,
                index,
                0,
                new CellRange(
                    new CellAddress(index, 0),
                    new CellAddress(index, 0)),
                RepeatedRows: null,
                RepeatedColumns: null,
                Scale: 1d,
                new SizeD(800d, 1000d),
                new RectD(50d, 50d, 700d, 900d),
                new SizeD(80d, 20d),
                new PointD(0d, 0d)))
            .ToArray();
        return new SpreadsheetPageLayoutPlan(
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(Math.Max(0, pageCount - 1), 0)),
            new SpreadsheetPageSetup(),
            1d,
            new SizeD(800d, 1000d),
            new RectD(50d, 50d, 700d, 900d),
            pages);
    }
}
