using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class SpreadsheetSplitHeaderResizeGeometryTests
{
    private static readonly SpreadsheetRenderTheme Theme = new()
    {
        ShowHeaders = true,
    };

    [TestMethod]
    public void RowResizeUsesTheLeftEdgePaneLocalLayout()
    {
        var panes = CreateFourPaneLayouts();
        var pointerY = Theme.ColumnHeaderHeight + 206d + 22d;

        var hit = SpreadsheetSplitHeaderResizeGeometry.TryHitResizeHandle(
            20d,
            pointerY,
            848d,
            624d,
            Theme,
            panes,
            out var handle);

        Assert.IsTrue(hit);
        Assert.AreEqual(SpreadsheetPaneId.BottomLeft, handle.PaneId);
        Assert.AreEqual(WorksheetAxis.Row, handle.Axis);
        Assert.AreEqual(10, handle.Index);
        Assert.AreEqual(pointerY, handle.EdgeCoordinate, 0.001d);
        Assert.AreEqual(22d, handle.OriginalSize, 0.001d);
    }

    [TestMethod]
    public void ColumnResizeUsesTheTopEdgePaneLocalLayout()
    {
        var panes = CreateFourPaneLayouts();
        var pointerX = Theme.RowHeaderWidth + 306d + 90d;

        var hit = SpreadsheetSplitHeaderResizeGeometry.TryHitResizeHandle(
            pointerX,
            12d,
            848d,
            624d,
            Theme,
            panes,
            out var handle);

        Assert.IsTrue(hit);
        Assert.AreEqual(SpreadsheetPaneId.TopRight, handle.PaneId);
        Assert.AreEqual(WorksheetAxis.Column, handle.Axis);
        Assert.AreEqual(8, handle.Index);
        Assert.AreEqual(pointerX, handle.EdgeCoordinate, 0.001d);
        Assert.AreEqual(90d, handle.OriginalSize, 0.001d);
    }

    [TestMethod]
    public void SplitSeparatorContinuationDoesNotExposeDimensionHandles()
    {
        var panes = CreateFourPaneLayouts();

        Assert.IsFalse(SpreadsheetSplitHeaderResizeGeometry.TryHitResizeHandle(
            20d,
            Theme.ColumnHeaderHeight + 202d,
            848d,
            624d,
            Theme,
            panes,
            out _));
        Assert.IsFalse(SpreadsheetSplitHeaderResizeGeometry.TryHitResizeHandle(
            Theme.RowHeaderWidth + 302d,
            12d,
            848d,
            624d,
            Theme,
            panes,
            out _));
    }

    [TestMethod]
    public void CalculateSizeUsesTheFullControlCoordinate()
    {
        var panes = CreateFourPaneLayouts();
        var edge = Theme.ColumnHeaderHeight + 206d + 22d;
        Assert.IsTrue(SpreadsheetSplitHeaderResizeGeometry.TryHitResizeHandle(
            20d,
            edge,
            848d,
            624d,
            Theme,
            panes,
            out var handle));

        var size = SpreadsheetSplitHeaderResizeGeometry.CalculateSize(
            handle,
            20d,
            edge + 13.5d);

        Assert.AreEqual(35.5d, size, 0.001d);
    }

    [TestMethod]
    public void MismatchedPaneViewportMetadataIsRejected()
    {
        var panes = new[]
        {
            new SpreadsheetSplitPaneChromeLayout(
                SpreadsheetPaneId.TopLeft,
                new RectD(0d, 0d, 300d, 200d),
                CreateViewport(
                    299d,
                    200d,
                    [new AxisSlot(0, 0d, 20d)],
                    [new AxisSlot(0, 0d, 80d)])),
        };

        Assert.ThrowsExactly<ArgumentException>(() =>
            SpreadsheetSplitHeaderResizeGeometry.TryHitResizeHandle(
                20d,
                Theme.ColumnHeaderHeight + 20d,
                848d,
                624d,
                Theme,
                panes,
                out _));
    }

    private static SpreadsheetSplitPaneChromeLayout[] CreateFourPaneLayouts() =>
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
