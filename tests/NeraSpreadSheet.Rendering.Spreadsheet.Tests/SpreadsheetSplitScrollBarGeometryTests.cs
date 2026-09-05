using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class SpreadsheetSplitScrollBarGeometryTests
{
    [TestMethod]
    public void FourPanesReceiveIndependentHorizontalAndVerticalThumbs()
    {
        var layout = SpreadsheetSplitScrollBarGeometry.Create(
            new SizeD(806d, 606d),
            CreateFourPaneStates());

        Assert.AreEqual(8, layout.Count);
        Assert.IsTrue(layout.TryGet(
            SpreadsheetPaneId.TopLeft,
            SpreadsheetScrollBarAxis.Horizontal,
            out var topLeftHorizontal));
        Assert.IsTrue(layout.TryGet(
            SpreadsheetPaneId.TopRight,
            SpreadsheetScrollBarAxis.Horizontal,
            out var topRightHorizontal));
        Assert.IsTrue(layout.TryGet(
            SpreadsheetPaneId.BottomRight,
            SpreadsheetScrollBarAxis.Vertical,
            out var bottomRightVertical));

        Assert.AreEqual(25.5d, topLeftHorizontal.Offset, 0.001d);
        Assert.AreEqual(680.25d, topRightHorizontal.Offset, 0.001d);
        Assert.AreEqual(780.75d, bottomRightVertical.Offset, 0.001d);
        Assert.IsTrue(
            topRightHorizontal.ThumbBounds.Left >
            topLeftHorizontal.ThumbBounds.Left);
        Assert.IsTrue(bottomRightVertical.ThumbBounds.Top > 306d);
    }

    [TestMethod]
    public void NonScrollableAxisDoesNotCreateScrollbar()
    {
        var layout = SpreadsheetSplitScrollBarGeometry.Create(
            new SizeD(500d, 320d),
            [
                new SpreadsheetSplitPaneScrollBarState(
                    SpreadsheetPaneId.TopLeft,
                    new RectD(0d, 0d, 500d, 320d),
                    0d,
                    120d,
                    500d,
                    900d),
            ]);

        Assert.AreEqual(1, layout.Count);
        Assert.IsFalse(layout.TryGet(
            SpreadsheetPaneId.TopLeft,
            SpreadsheetScrollBarAxis.Horizontal,
            out _));
        Assert.IsTrue(layout.TryGet(
            SpreadsheetPaneId.TopLeft,
            SpreadsheetScrollBarAxis.Vertical,
            out _));
    }

    [TestMethod]
    public void ThumbDragMapsContinuouslyToDoubleOffset()
    {
        var layout = SpreadsheetSplitScrollBarGeometry.Create(
            new SizeD(500d, 320d),
            [
                new SpreadsheetSplitPaneScrollBarState(
                    SpreadsheetPaneId.TopLeft,
                    new RectD(0d, 0d, 500d, 320d),
                    0d,
                    0d,
                    2000d,
                    320d),
            ]);
        Assert.IsTrue(layout.TryGet(
            SpreadsheetPaneId.TopLeft,
            SpreadsheetScrollBarAxis.Horizontal,
            out var scrollBar));

        var grabOffset = scrollBar.ThumbLength * 0.37d;
        var pointer = scrollBar.TrackStart +
            ((scrollBar.TrackLength - scrollBar.ThumbLength) * 0.4321d) +
            grabOffset;
        var offset = SpreadsheetSplitScrollBarGeometry.GetOffsetFromThumb(
            scrollBar,
            pointer,
            grabOffset);

        Assert.AreEqual(
            scrollBar.MaximumOffset * 0.4321d,
            offset,
            0.001d);
    }

    [TestMethod]
    public void TrackHitPagesOnlyTheTargetPaneAndAxis()
    {
        var layout = SpreadsheetSplitScrollBarGeometry.Create(
            new SizeD(806d, 606d),
            CreateFourPaneStates());
        Assert.IsTrue(layout.TryGet(
            SpreadsheetPaneId.BottomLeft,
            SpreadsheetScrollBarAxis.Vertical,
            out var scrollBar));
        var point = new PointD(
            scrollBar.TrackBounds.Left + 1d,
            scrollBar.ThumbBounds.Bottom + 4d);

        var hit = layout.HitTest(point);
        var offset = SpreadsheetSplitScrollBarGeometry.GetPagedOffset(
            hit,
            layout.Style.PageFactor);

        Assert.IsTrue(hit.IsHit);
        Assert.AreEqual(
            SpreadsheetPaneId.BottomLeft,
            hit.PaneId);
        Assert.AreEqual(
            SpreadsheetScrollBarAxis.Vertical,
            hit.Axis);
        Assert.AreEqual(
            SpreadsheetScrollBarHitKind.TrackAfterThumb,
            hit.Kind);
        Assert.AreEqual(
            Math.Min(
                scrollBar.MaximumOffset,
                scrollBar.Offset +
                (scrollBar.ViewportExtent * layout.Style.PageFactor)),
            offset,
            0.001d);
    }

    [TestMethod]
    public void DisplayComposerRetainsBodyAndHighlightsActivePaneThumb()
    {
        var bodyBuilder = new DisplayListBuilder();
        bodyBuilder.FillRectangle(
            new RectD(0d, 0d, 806d, 606d),
            ColorRgba.White);
        var body = bodyBuilder.Build();
        var layout = SpreadsheetSplitScrollBarGeometry.Create(
            new SizeD(806d, 606d),
            CreateFourPaneStates());

        var result = SpreadsheetSplitScrollBarDisplayListComposer.Compose(
            body,
            layout,
            SpreadsheetPaneId.BottomRight);

        Assert.IsInstanceOfType<DrawDisplayListCommand>(result.Commands[0]);
        var activeThumb = layout.ScrollBars.Single(scrollBar =>
            scrollBar.PaneId == SpreadsheetPaneId.BottomRight &&
            scrollBar.Axis == SpreadsheetScrollBarAxis.Vertical);
        Assert.IsTrue(result.Commands.OfType<FillRectangleCommand>().Any(command =>
            command.Bounds == activeThumb.ThumbBounds &&
            command.Color == layout.Style.ActiveThumbColor));
    }

    [TestMethod]
    public void GeometryRejectsDuplicatePaneIdsAndInvalidStyle()
    {
        var duplicate = new SpreadsheetSplitPaneScrollBarState(
            SpreadsheetPaneId.TopLeft,
            new RectD(0d, 0d, 300d, 200d),
            0d,
            0d,
            900d,
            700d);

        Assert.ThrowsExactly<ArgumentException>(() =>
            SpreadsheetSplitScrollBarGeometry.Create(
                new SizeD(600d, 400d),
                [duplicate, duplicate]));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            SpreadsheetSplitScrollBarGeometry.Create(
                new SizeD(600d, 400d),
                [duplicate],
                new SpreadsheetSplitScrollBarStyle
                {
                    Thickness = 0d,
                }));
    }

    private static SpreadsheetSplitPaneScrollBarState[] CreateFourPaneStates() =>
    [
        new(
            SpreadsheetPaneId.TopLeft,
            new RectD(0d, 0d, 400d, 300d),
            25.5d,
            40.25d,
            1800d,
            1400d),
        new(
            SpreadsheetPaneId.TopRight,
            new RectD(406d, 0d, 400d, 300d),
            680.25d,
            45.5d,
            1800d,
            1400d),
        new(
            SpreadsheetPaneId.BottomLeft,
            new RectD(0d, 306d, 400d, 300d),
            35.75d,
            620.5d,
            1800d,
            1400d),
        new(
            SpreadsheetPaneId.BottomRight,
            new RectD(406d, 306d, 400d, 300d),
            710.5d,
            780.75d,
            1800d,
            1400d),
    ];
}
