using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class SpreadsheetHeaderResizeGeometryTests
{
    [TestMethod]
    public void RowResizeHandleUsesFreezeAwareFractionalSlotEdge()
    {
        var layout = CreateFrozenLayout();
        var theme = new SpreadsheetRenderTheme { ShowHeaders = true };
        var expectedEdge = theme.ColumnHeaderHeight + 32.25d;

        var hit = SpreadsheetHeaderResizeGeometry.TryHitResizeHandle(
            20d,
            expectedEdge - 1d,
            640d,
            480d,
            theme,
            layout,
            out var handle);

        Assert.IsTrue(hit);
        Assert.AreEqual(WorksheetAxis.Row, handle.Axis);
        Assert.AreEqual(1, handle.Index);
        Assert.AreEqual(expectedEdge, handle.EdgeCoordinate, 1e-9);
        Assert.AreEqual(20d, handle.OriginalSize, 1e-9);
    }

    [TestMethod]
    public void ColumnResizeHandleUsesFreezeAwareFractionalSlotEdge()
    {
        var layout = CreateFrozenLayout();
        var theme = new SpreadsheetRenderTheme { ShowHeaders = true };
        var expectedEdge = theme.RowHeaderWidth + 146.75d;

        var hit = SpreadsheetHeaderResizeGeometry.TryHitResizeHandle(
            expectedEdge + 1d,
            10d,
            640d,
            480d,
            theme,
            layout,
            out var handle);

        Assert.IsTrue(hit);
        Assert.AreEqual(WorksheetAxis.Column, handle.Axis);
        Assert.AreEqual(1, handle.Index);
        Assert.AreEqual(expectedEdge, handle.EdgeCoordinate, 1e-9);
        Assert.AreEqual(80d, handle.OriginalSize, 1e-9);
    }

    [TestMethod]
    public void ResizeSizeTracksPointerDeltaAndCanCollapseAxis()
    {
        var row = new SpreadsheetHeaderResizeHandle(WorksheetAxis.Row, 4, 100d, 20d);
        var column = new SpreadsheetHeaderResizeHandle(WorksheetAxis.Column, 3, 200d, 80d);

        Assert.AreEqual(27.5d, SpreadsheetHeaderResizeGeometry.CalculateSize(row, 0d, 107.5d), 1e-9);
        Assert.AreEqual(55d, SpreadsheetHeaderResizeGeometry.CalculateSize(column, 175d, 0d), 1e-9);
        Assert.AreEqual(0d, SpreadsheetHeaderResizeGeometry.CalculateSize(row, 0d, 10d), 1e-9);
    }

    [TestMethod]
    public void BodyAndCornerDoNotExposeResizeHandles()
    {
        var layout = CreateFrozenLayout();
        var theme = new SpreadsheetRenderTheme { ShowHeaders = true };

        Assert.IsFalse(SpreadsheetHeaderResizeGeometry.TryHitResizeHandle(
            300d, 200d, 640d, 480d, theme, layout, out _));
        Assert.IsFalse(SpreadsheetHeaderResizeGeometry.TryHitResizeHandle(
            10d, 10d, 640d, 480d, theme, layout, out _));
    }

    private static ViewportLayout CreateFrozenLayout()
    {
        var rows = new SparseAxisMetricIndex(100, 20d);
        var columns = new SparseAxisMetricIndex(50, 80d);
        return new ViewportLayoutEngine(rows, columns).Compute(new ViewportRequest(
            13.25d,
            7.75d,
            new SizeD(592d, 456d),
            0d,
            FrozenRows: 1,
            FrozenColumns: 1));
    }
}
