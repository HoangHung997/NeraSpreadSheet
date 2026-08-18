using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Viewport.Tests;

[TestClass]
public sealed class SpreadsheetSplitScrollBarInteractionTests
{
    [TestMethod]
    public void ThumbDragProducesRequestForOnlyTheSelectedPaneAndAxis()
    {
        var layout = CreateLayout();
        Assert.IsTrue(layout.TryGet(
            SpreadsheetPaneId.TopRight,
            SpreadsheetScrollBarAxis.Horizontal,
            out var scrollBar));
        var controller = new SpreadsheetSplitScrollBarInteractionController();
        var grabOffset = scrollBar.ThumbLength / 2d;
        var start = new PointD(
            scrollBar.ThumbBounds.Left + grabOffset,
            scrollBar.ThumbBounds.Top + 2d);

        var begin = controller.BeginPointer(layout, start);
        var move = controller.MovePointer(new PointD(
            scrollBar.TrackBounds.Right - grabOffset,
            start.Y));

        Assert.IsTrue(begin.Handled);
        Assert.IsTrue(begin.IsDragging);
        Assert.IsNull(begin.ScrollRequest);
        Assert.IsTrue(move.Handled);
        Assert.IsTrue(move.IsDragging);
        Assert.IsNotNull(move.ScrollRequest);
        Assert.AreEqual(
            SpreadsheetPaneId.TopRight,
            move.ScrollRequest.Value.PaneId);
        Assert.AreEqual(
            SpreadsheetScrollBarAxis.Horizontal,
            move.ScrollRequest.Value.Axis);
        Assert.AreEqual(
            scrollBar.MaximumOffset,
            move.ScrollRequest.Value.Offset,
            0.001d);
        Assert.IsTrue(controller.EndPointer());
        Assert.IsFalse(controller.IsDragging);
    }

    [TestMethod]
    public void TrackClickReturnsImmediatePagedRequest()
    {
        var layout = CreateLayout();
        Assert.IsTrue(layout.TryGet(
            SpreadsheetPaneId.BottomLeft,
            SpreadsheetScrollBarAxis.Vertical,
            out var scrollBar));
        var controller = new SpreadsheetSplitScrollBarInteractionController();
        var point = new PointD(
            scrollBar.TrackBounds.Left + 2d,
            scrollBar.ThumbBounds.Bottom + 5d);

        var result = controller.BeginPointer(layout, point);

        Assert.IsTrue(result.Handled);
        Assert.IsFalse(result.IsDragging);
        Assert.IsNotNull(result.ScrollRequest);
        Assert.AreEqual(
            SpreadsheetPaneId.BottomLeft,
            result.ScrollRequest.Value.PaneId);
        Assert.AreEqual(
            SpreadsheetScrollBarAxis.Vertical,
            result.ScrollRequest.Value.Axis);
        Assert.IsTrue(
            result.ScrollRequest.Value.Offset > scrollBar.Offset);
    }

    [TestMethod]
    public void ApplyingRequestPreservesTheOtherAxisAndOtherPanes()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(200, 100), "extent");
        var engine = new SpreadsheetSplitViewportEngine(
            new SpreadsheetSession(workbook));
        engine.ScrollPaneTo(
            SpreadsheetPaneId.TopRight,
            100.25d,
            45.5d);
        engine.ScrollPaneTo(
            SpreadsheetPaneId.BottomLeft,
            35d,
            400d);

        engine.ApplyScrollRequest(new SpreadsheetSplitScrollRequest(
            SpreadsheetPaneId.TopRight,
            SpreadsheetScrollBarAxis.Horizontal,
            725.75d));

        Assert.AreEqual(
            new PointD(725.75d, 45.5d),
            engine.GetPaneScroll(SpreadsheetPaneId.TopRight));
        Assert.AreEqual(
            new PointD(35d, 400d),
            engine.GetPaneScroll(SpreadsheetPaneId.BottomLeft));
    }

    [TestMethod]
    public void ViewportFrameAdapterUsesIndependentPaneOffsets()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(200, 100), "extent");
        var engine = new SpreadsheetSplitViewportEngine(
            new SpreadsheetSession(workbook));
        engine.ScrollPaneTo(
            SpreadsheetPaneId.TopLeft,
            15.25d,
            30.5d);
        engine.ScrollPaneTo(
            SpreadsheetPaneId.TopRight,
            515.75d,
            45.25d);
        var frame = engine.Compose(new SpreadsheetSplitRequest(
            new SizeD(806d, 606d),
            splitX: 400d,
            splitY: 300d));

        var layout = frame.CreateScrollBarLayout(
            engine.GetContentExtent());

        Assert.IsTrue(layout.TryGet(
            SpreadsheetPaneId.TopLeft,
            SpreadsheetScrollBarAxis.Horizontal,
            out var left));
        Assert.IsTrue(layout.TryGet(
            SpreadsheetPaneId.TopRight,
            SpreadsheetScrollBarAxis.Horizontal,
            out var right));
        Assert.AreEqual(15.25d, left.Offset, 0.001d);
        Assert.AreEqual(515.75d, right.Offset, 0.001d);
        Assert.AreNotEqual(left.ThumbBounds.Left, right.ThumbBounds.Left);
    }

    [TestMethod]
    public void PointerOutsideScrollbarsDoesNotBeginInteraction()
    {
        var controller = new SpreadsheetSplitScrollBarInteractionController();

        var result = controller.BeginPointer(
            CreateLayout(),
            new PointD(200d, 120d));

        Assert.IsFalse(result.Handled);
        Assert.IsFalse(controller.IsDragging);
        Assert.IsFalse(controller.EndPointer());
    }

    private static SpreadsheetSplitScrollBarLayout CreateLayout() =>
        SpreadsheetSplitScrollBarGeometry.Create(
            new SizeD(806d, 606d),
            [
                new SpreadsheetSplitPaneScrollBarState(
                    SpreadsheetPaneId.TopLeft,
                    new RectD(0d, 0d, 400d, 300d),
                    20d,
                    30d,
                    1800d,
                    1400d),
                new SpreadsheetSplitPaneScrollBarState(
                    SpreadsheetPaneId.TopRight,
                    new RectD(406d, 0d, 400d, 300d),
                    600d,
                    40d,
                    1800d,
                    1400d),
                new SpreadsheetSplitPaneScrollBarState(
                    SpreadsheetPaneId.BottomLeft,
                    new RectD(0d, 306d, 400d, 300d),
                    35d,
                    550d,
                    1800d,
                    1400d),
                new SpreadsheetSplitPaneScrollBarState(
                    SpreadsheetPaneId.BottomRight,
                    new RectD(406d, 306d, 400d, 300d),
                    650d,
                    700d,
                    1800d,
                    1400d),
            ]);
}
