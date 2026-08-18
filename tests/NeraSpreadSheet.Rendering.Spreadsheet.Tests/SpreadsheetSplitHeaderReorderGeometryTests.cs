using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class SpreadsheetSplitHeaderReorderGeometryTests
{
    private static readonly SpreadsheetRenderTheme Theme = new()
    {
        ShowHeaders = true,
    };

    [TestMethod]
    public void RowSourceUsesLeftEdgePaneAndExcludesResizeHandle()
    {
        var panes = CreateFourPaneLayouts();
        var centerY = Theme.ColumnHeaderHeight + 206d + 11d;

        Assert.IsTrue(SpreadsheetSplitHeaderReorderGeometry.TryHitSource(
            20d,
            centerY,
            848d,
            624d,
            Theme,
            panes,
            out var source));
        Assert.AreEqual(SpreadsheetPaneId.BottomLeft, source.PaneId);
        Assert.AreEqual(WorksheetAxis.Row, source.Axis);
        Assert.AreEqual(10, source.Index);

        Assert.IsFalse(SpreadsheetSplitHeaderReorderGeometry.TryHitSource(
            20d,
            Theme.ColumnHeaderHeight + 206d + 21d,
            848d,
            624d,
            Theme,
            panes,
            out _));
    }

    [TestMethod]
    public void ColumnSourceUsesTopEdgePane()
    {
        var panes = CreateFourPaneLayouts();
        var centerX = Theme.RowHeaderWidth + 306d + 45d;

        Assert.IsTrue(SpreadsheetSplitHeaderReorderGeometry.TryHitSource(
            centerX,
            12d,
            848d,
            624d,
            Theme,
            panes,
            out var source));
        Assert.AreEqual(SpreadsheetPaneId.TopRight, source.PaneId);
        Assert.AreEqual(WorksheetAxis.Column, source.Axis);
        Assert.AreEqual(8, source.Index);
    }

    [TestMethod]
    public void RowDropUsesSlotMidpointAndCreatesFullWidthPreview()
    {
        var panes = CreateFourPaneLayouts();
        var pointerY = Theme.ColumnHeaderHeight + 206d + 22d + 24d;

        Assert.IsTrue(SpreadsheetSplitHeaderReorderGeometry.TryGetDropTarget(
            WorksheetAxis.Row,
            sourceIndex: 10,
            count: 1,
            pointerX: 20d,
            pointerY,
            fullWidth: 848d,
            fullHeight: 624d,
            Theme,
            panes,
            out var target));

        Assert.AreEqual(SpreadsheetPaneId.BottomLeft, target.PaneId);
        Assert.AreEqual(12, target.DestinationBoundary);
        Assert.IsFalse(target.IsNoOp);
        Assert.AreEqual(0d, target.PreviewBounds.Left);
        Assert.AreEqual(848d, target.PreviewBounds.Width);
        Assert.AreEqual(
            Theme.ColumnHeaderHeight + 206d + 52d,
            target.EdgeCoordinate,
            0.001d);
    }

    [TestMethod]
    public void AdjacentDropIsReportedAsNoOp()
    {
        var panes = CreateFourPaneLayouts();
        var pointerY = Theme.ColumnHeaderHeight + 206d + 22d + 4d;

        Assert.IsTrue(SpreadsheetSplitHeaderReorderGeometry.TryGetDropTarget(
            WorksheetAxis.Row,
            sourceIndex: 10,
            count: 1,
            pointerX: 20d,
            pointerY,
            fullWidth: 848d,
            fullHeight: 624d,
            Theme,
            panes,
            out var target));

        Assert.AreEqual(11, target.DestinationBoundary);
        Assert.IsTrue(target.IsNoOp);
    }

    [TestMethod]
    public void DragThresholdAndPreviewCompositionAreDeterministic()
    {
        Assert.IsFalse(
            SpreadsheetSplitHeaderReorderGeometry.HasExceededDragThreshold(
                new PointD(10d, 10d),
                new PointD(13d, 13d)));
        Assert.IsTrue(
            SpreadsheetSplitHeaderReorderGeometry.HasExceededDragThreshold(
                new PointD(10d, 10d),
                new PointD(16d, 10d)));

        var builder = new DisplayListBuilder();
        builder.FillRectangle(
            new RectD(0d, 0d, 100d, 100d),
            ColorRgba.White);
        var body = builder.Build();
        Assert.AreSame(
            body,
            SpreadsheetHeaderReorderPreviewDisplayListComposer.Compose(
                body,
                null,
                Theme));

        var target = new SpreadsheetSplitHeaderReorderDropTarget(
            SpreadsheetPaneId.TopLeft,
            new WorksheetAxisMove(
                WorksheetAxis.Row,
                sourceIndex: 2,
                count: 1,
                destinationBoundary: 6),
            40d,
            new RectD(0d, 38.5d, 100d, 3d));
        var composed =
            SpreadsheetHeaderReorderPreviewDisplayListComposer.Compose(
                body,
                target,
                Theme);
        Assert.AreEqual(2, composed.Count);
    }

    private static SpreadsheetSplitPaneChromeLayout[]
        CreateFourPaneLayouts() =>
    [
        new SpreadsheetSplitPaneChromeLayout(
            SpreadsheetPaneId.TopLeft,
            new RectD(0d, 0d, 300d, 200d),
            CreateViewport(
                300d,
                200d,
                [new AxisSlot(0, 0d, 20d), new AxisSlot(1, 20d, 20d)],
                [new AxisSlot(0, 0d, 80d), new AxisSlot(1, 80d, 80d)])),
        new SpreadsheetSplitPaneChromeLayout(
            SpreadsheetPaneId.TopRight,
            new RectD(306d, 0d, 494d, 200d),
            CreateViewport(
                494d,
                200d,
                [new AxisSlot(0, 0d, 20d), new AxisSlot(1, 20d, 20d)],
                [new AxisSlot(8, 0d, 90d), new AxisSlot(9, 90d, 70d)])),
        new SpreadsheetSplitPaneChromeLayout(
            SpreadsheetPaneId.BottomLeft,
            new RectD(0d, 206d, 300d, 394d),
            CreateViewport(
                300d,
                394d,
                [new AxisSlot(10, 0d, 22d), new AxisSlot(11, 22d, 30d)],
                [new AxisSlot(0, 0d, 80d), new AxisSlot(1, 80d, 80d)])),
        new SpreadsheetSplitPaneChromeLayout(
            SpreadsheetPaneId.BottomRight,
            new RectD(306d, 206d, 494d, 394d),
            CreateViewport(
                494d,
                394d,
                [new AxisSlot(10, 0d, 22d), new AxisSlot(11, 22d, 30d)],
                [new AxisSlot(8, 0d, 90d), new AxisSlot(9, 90d, 70d)])),
    ];

    private static ViewportLayout CreateViewport(
        double width,
        double height,
        IReadOnlyList<AxisSlot> rows,
        IReadOnlyList<AxisSlot> columns) => new(
        0d,
        0d,
        new SizeD(width, height),
        1600d,
        1200d,
        0d,
        0d,
        rows,
        columns);
}
