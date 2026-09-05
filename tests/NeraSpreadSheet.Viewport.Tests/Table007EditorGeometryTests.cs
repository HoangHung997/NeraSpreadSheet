using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Viewport.Tests;

[TestClass]
public sealed class Table007EditorGeometryTests
{
    [TestMethod]
    [DataRow(0d)]
    [DataRow(128d)]
    public void LayoutOnlyShouldMatchComposeWithFractionalScrollFreezeAndOverscan(double overscan)
    {
        var session = new SpreadsheetSession(new Workbook());
        session.View.SetFrozenPanes(1, 1);
        var sheet = session.ActiveWorksheet;
        sheet.Dimensions.SetColumnWidth(0, 91.5d);
        sheet.Dimensions.SetRowHeight(0, 31.25d);
        sheet.Dimensions.HideRows(3);
        sheet.Dimensions.HideColumns(2);
        var sentinel = new CellAddress(100, 100);
        sheet.SetCell(sentinel, new CellData(CellValue.FromNumber(999), "=1+1"));
        var engine = new SpreadsheetViewportEngine(session);
        var layout = engine.ComputeLayout(13.25d, 7.75d, 300d, 180d, overscan);
        Assert.AreEqual(0L, engine.SnapshotRefreshCount);
        Assert.AreEqual(0L, engine.DisplayListCacheMissCount);
        Assert.AreEqual(0, engine.DisplayListCacheEntryCount);
        var composed = engine.Compose(13.25d, 7.75d, 300d, 180d, overscan).Layout;
        Assert.AreEqual(layout.ScrollX, composed.ScrollX);
        Assert.AreEqual(layout.ScrollY, composed.ScrollY);
        Assert.AreEqual(layout.FrozenWidth, composed.FrozenWidth);
        Assert.AreEqual(layout.FrozenHeight, composed.FrozenHeight);
        CollectionAssert.AreEqual(layout.Rows.ToArray(), composed.Rows.ToArray());
        CollectionAssert.AreEqual(layout.Columns.ToArray(), composed.Columns.ToArray());
        Assert.AreEqual(999d, sheet.GetValue(sentinel));
        Assert.AreEqual(0, session.History.UndoCount);
    }

    [TestMethod]
    public void LayoutOnlyShouldReuseFilteredSnapshotsAndRefreshVisibilityChanges()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        var columnId = Guid.NewGuid();
        sheet.SetValue(new CellAddress(1, 0), "shown");
        sheet.SetValue(new CellAddress(2, 0), "hidden");
        sheet.AddTable(new SpreadsheetTable(Guid.NewGuid(), "Sales",
            new CellRange(default, new CellAddress(2, 0)),
            [new SpreadsheetTableColumn(columnId, "Value")],
            autoFilter: new TableAutoFilter([new TableFilterColumn(columnId, values: [CellValue.FromText("shown")])])));
        var session = new SpreadsheetSession(workbook);
        var engine = new SpreadsheetViewportEngine(session);
        var first = engine.ComputeLayout(0, 0, 200, 120, 0);
        Assert.IsFalse(first.Rows.Any(row => row.Index == 2));
        var snapshots = engine.SnapshotRefreshCount;
        Assert.AreEqual(1L, snapshots);
        engine.ComputeLayout(3.5d, 8.25d, 200, 120, 0);
        Assert.AreEqual(snapshots, engine.SnapshotRefreshCount);
        sheet.SetValue(new CellAddress(2, 0), "shown");
        var changed = engine.ComputeLayout(0, 0, 200, 120, 0);
        Assert.IsTrue(changed.Rows.Any(row => row.Index == 2));
        Assert.AreEqual(snapshots + 1, engine.SnapshotRefreshCount);
        Assert.AreEqual(0L, engine.DisplayListCacheMissCount);
        Assert.AreEqual(0, session.History.UndoCount);
    }

    [TestMethod]
    public void LayoutOnlyShouldFollowActiveSheetDimensionsAndMergedBounds()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        var engine = new SpreadsheetViewportEngine(session);
        var initial = engine.ComputeLayout(0, 0, 300, 160);
        session.ActiveWorksheet.Dimensions.SetColumnWidth(0, 120);
        var resized = engine.ComputeLayout(0, 0, 300, 160);
        Assert.AreNotEqual(initial.Columns[0].Size, resized.Columns[0].Size);
        var other = workbook.AddWorksheet("Other");
        other.Dimensions.SetColumnWidth(0, 140);
        session.ActivateWorksheet(other);
        var switched = engine.ComputeLayout(0, 0, 300, 160);
        Assert.AreEqual(140d, switched.Columns[0].Size);
        other.MergeCells(new CellRange(default, new CellAddress(1, 1)));
        Assert.IsTrue(engine.TryGetCellBounds(new CellAddress(1, 1), 0, 0, out var bounds));
        Assert.AreEqual(140d + other.Dimensions.DefaultColumnWidth, bounds.Width);
        Assert.AreEqual(0L, engine.SnapshotRefreshCount);
    }

    [TestMethod]
    public void LayoutOnlyShouldKeepComposeValidationForZeroAndInvalidGeometry()
    {
        var engine = new SpreadsheetViewportEngine(new SpreadsheetSession(new Workbook()));
        Assert.AreEqual(0L, engine.ComputeLayout(0, 0, 0, 0, 0).VisibleCellCount);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => engine.ComputeLayout(-1, 0, 10, 10));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => engine.ComputeLayout(0, double.NaN, 10, 10));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => engine.ComputeLayout(0, 0, double.PositiveInfinity, 10));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => engine.ComputeLayout(0, 0, 10, -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => engine.ComputeLayout(0, 0, 10, 10, -1));
    }
}
