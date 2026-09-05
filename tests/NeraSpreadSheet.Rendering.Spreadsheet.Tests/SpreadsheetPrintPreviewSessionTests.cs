using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class SpreadsheetPrintPreviewSessionTests
{
    [TestMethod]
    public void ComposeMaterializesOnlyVisiblePages()
    {
        var fixture = CreateFixture(rowCount: 200);
        var session = new SpreadsheetPrintPreviewSession(
            fixture.Snapshot,
            fixture.Plan,
            fixture.Workbook.Styles,
            previewOptions: new SpreadsheetPrintPreviewOptions
            {
                Zoom = 0.25d,
                OverscanDips = 20d,
                PageGapDips = 12d,
            });
        session.SetViewportSize(400d, 300d);
        session.ScrollTo(0d, 1_000.25d);

        var frame = session.Compose();

        Assert.IsTrue(frame.Pages.Count > 0);
        Assert.IsTrue(frame.Pages.Count < fixture.Plan.Pages.Count);
        Assert.IsTrue(frame.Pages.Count < 10);
        Assert.AreEqual(1_000.25d, frame.Layout.OffsetYDips, 0.000001d);
    }

    [TestMethod]
    public void ZoomKeepsTheAnchoredContentPointStable()
    {
        var fixture = CreateFixture(rowCount: 100);
        var session = new SpreadsheetPrintPreviewSession(
            fixture.Snapshot,
            fixture.Plan,
            fixture.Workbook.Styles,
            previewOptions: new SpreadsheetPrintPreviewOptions
            {
                Zoom = 0.5d,
                PageGapDips = 0d,
                OverscanDips = 0d,
            });
        session.SetViewportSize(300d, 200d);
        session.ScrollTo(20d, 400d);
        const double anchorX = 50d;
        const double anchorY = 80d;
        var previousContentX = session.OffsetX + anchorX;
        var previousContentY = session.OffsetY + anchorY;

        session.SetZoom(1d, anchorX, anchorY);

        Assert.AreEqual(
            previousContentX * 2d,
            session.OffsetX + anchorX,
            0.000001d);
        Assert.AreEqual(
            previousContentY * 2d,
            session.OffsetY + anchorY,
            0.000001d);
    }

    [TestMethod]
    public void ScrollOffsetsAreClampedToContentExtent()
    {
        var fixture = CreateFixture(rowCount: 20);
        var session = new SpreadsheetPrintPreviewSession(
            fixture.Snapshot,
            fixture.Plan,
            fixture.Workbook.Styles);
        session.SetViewportSize(300d, 300d);

        session.ScrollTo(-100d, double.MaxValue);
        var frame = session.Compose();

        Assert.AreEqual(0d, session.OffsetX, 0.000001d);
        Assert.AreEqual(
            Math.Max(
                0d,
                frame.Layout.ContentSizeDips.Height - 300d),
            session.OffsetY,
            0.000001d);
    }

    [TestMethod]
    public void CacheRemainsBoundedWhileScrollingManyPages()
    {
        var fixture = CreateFixture(rowCount: 150);
        var session = new SpreadsheetPrintPreviewSession(
            fixture.Snapshot,
            fixture.Plan,
            fixture.Workbook.Styles,
            previewOptions: new SpreadsheetPrintPreviewOptions
            {
                Zoom = 0.2d,
                PageGapDips = 8d,
                OverscanDips = 0d,
            },
            maximumCachedPages: 3);
        session.SetViewportSize(250d, 180d);

        for (var index = 0; index < 20; index++)
        {
            session.ScrollTo(0d, index * 220d);
            session.Compose();
        }

        Assert.IsTrue(session.CachedPageCount <= 3);
        session.ClearPageCache();
        Assert.AreEqual(0, session.CachedPageCount);
    }

    [TestMethod]
    public void HitTestReturnsPageAndLocalPoint()
    {
        var fixture = CreateFixture(rowCount: 10);
        var session = new SpreadsheetPrintPreviewSession(
            fixture.Snapshot,
            fixture.Plan,
            fixture.Workbook.Styles,
            previewOptions: new SpreadsheetPrintPreviewOptions
            {
                Zoom = 0.25d,
                PageGapDips = 10d,
                OverscanDips = 0d,
            });
        session.SetViewportSize(400d, 400d);
        var frame = session.Compose();
        var slot = frame.Pages[0].Slot;

        Assert.IsTrue(session.TryHitTest(
            slot.BoundsDips.X + 5d,
            slot.BoundsDips.Y + 7d,
            out var page,
            out var localPoint));
        Assert.AreEqual(slot.PageNumber, page.PageNumber);
        Assert.AreEqual(5d, localPoint.X, 0.000001d);
        Assert.AreEqual(7d, localPoint.Y, 0.000001d);
    }

    [TestMethod]
    public void InvalidViewportAndZoomInputsAreRejected()
    {
        var fixture = CreateFixture(rowCount: 2);
        var session = new SpreadsheetPrintPreviewSession(
            fixture.Snapshot,
            fixture.Plan,
            fixture.Workbook.Styles);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            session.SetViewportSize(-1d, 10d));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            session.SetZoom(0d));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            session.SetColumns(0));
    }

    private static Fixture CreateFixture(int rowCount)
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.Dimensions.SetColumnWidth(0, 120d);
        for (var row = 0; row < rowCount; row++)
        {
            worksheet.Dimensions.SetRowHeight(row, 30d);
            worksheet.SetValue(
                new CellAddress(row, 0),
                $"Row {row}");
        }
        var snapshot = WorksheetSnapshot.Capture(worksheet);
        var plan = SpreadsheetPageLayoutPlanner.CreatePlan(
            snapshot,
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(rowCount - 1, 0)),
            new SpreadsheetPageSetup
            {
                PaperSize = new SpreadsheetPaperSize(4d, 4d),
                Margins = new SpreadsheetPageMargins(
                    0.25d,
                    0.25d,
                    0.25d,
                    0.25d),
            });
        return new Fixture(workbook, snapshot, plan);
    }

    private sealed record Fixture(
        Workbook Workbook,
        WorksheetSnapshot Snapshot,
        SpreadsheetPageLayoutPlan Plan);
}
