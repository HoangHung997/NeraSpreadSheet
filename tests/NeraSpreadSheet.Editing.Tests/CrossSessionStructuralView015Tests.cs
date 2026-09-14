using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Interaction;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class CrossSessionStructuralView015Tests
{
    [TestMethod]
    public void InsertRowsShouldRemapInactivePeerSelectionFreezeAndFractionalScroll()
    {
        var (workbook, target, origin, peer) = CreateInactivePeer();
        var before = CreateState(
            active: new CellAddress(5, 4),
            anchor: new CellAddress(4, 3),
            range: new CellRange(new CellAddress(4, 3), new CellAddress(7, 6)),
            zoom: 1.35,
            offsetX: 91.125,
            offsetY: 500.375,
            frozenRows: 3,
            frozenColumns: 2);
        peer.View.SetWorksheetState(target, before);
        var rowHeight = target.Dimensions.DefaultRowHeight;

        origin.Structure.InsertRows(2, 2);

        var mapped = peer.View.GetWorksheetState(target);
        Assert.AreEqual(new CellAddress(7, 4), mapped.Selection.ActiveCell);
        Assert.AreEqual(new CellAddress(6, 3), mapped.Selection.AnchorCell);
        Assert.AreEqual(
            new CellRange(new CellAddress(6, 3), new CellAddress(9, 6)),
            mapped.Selection.Ranges[0]);
        Assert.AreEqual(5, mapped.FrozenRows);
        Assert.AreEqual(2, mapped.FrozenColumns);
        Assert.AreEqual(91.125, mapped.OffsetX, 0.000001);
        Assert.AreEqual(500.375 + (2 * rowHeight), mapped.OffsetY, 0.000001);
        Assert.AreEqual(1.35, mapped.Zoom, 0.000001);
        Assert.AreSame(workbook.Worksheets[1], peer.ActiveWorksheet);
    }

    [TestMethod]
    public void DeleteColumnsShouldRemapInactivePeerAndPreserveFractionalOffsets()
    {
        var (_, target, origin, peer) = CreateInactivePeer();
        var before = CreateState(
            active: new CellAddress(8, 9),
            anchor: new CellAddress(7, 8),
            range: new CellRange(new CellAddress(6, 7), new CellAddress(10, 11)),
            zoom: 0.8,
            offsetX: 700.625,
            offsetY: 300.25,
            frozenRows: 1,
            frozenColumns: 8);
        peer.View.SetWorksheetState(target, before);
        var columnWidth = target.Dimensions.DefaultColumnWidth;

        origin.Structure.DeleteColumns(2, 3);

        var mapped = peer.View.GetWorksheetState(target);
        Assert.AreEqual(new CellAddress(8, 6), mapped.Selection.ActiveCell);
        Assert.AreEqual(new CellAddress(7, 5), mapped.Selection.AnchorCell);
        Assert.AreEqual(
            new CellRange(new CellAddress(6, 4), new CellAddress(10, 8)),
            mapped.Selection.Ranges[0]);
        Assert.AreEqual(5, mapped.FrozenColumns);
        Assert.AreEqual(700.625 - (3 * columnWidth), mapped.OffsetX, 0.000001);
        Assert.AreEqual(300.25, mapped.OffsetY, 0.000001);
    }

    [TestMethod]
    public void FullyDeletedPeerSelectionShouldFallBackToMappedActiveCell()
    {
        var (_, target, origin, peer) = CreateInactivePeer();
        peer.View.SetWorksheetState(
            target,
            CreateState(
                active: new CellAddress(3, 2),
                anchor: new CellAddress(4, 2),
                range: new CellRange(new CellAddress(3, 1), new CellAddress(4, 3)),
                zoom: 1,
                offsetX: 0,
                offsetY: 0,
                frozenRows: 0,
                frozenColumns: 0));

        origin.Structure.DeleteRows(3, 2);

        var mapped = peer.View.GetWorksheetState(target);
        Assert.AreEqual(new CellAddress(3, 2), mapped.Selection.ActiveCell);
        Assert.AreEqual(1, mapped.Selection.Ranges.Count);
        Assert.AreEqual(
            new CellRange(new CellAddress(3, 2), new CellAddress(3, 2)),
            mapped.Selection.Ranges[0]);
    }

    [TestMethod]
    public void UndoRedoShouldRestoreThenRecaptureInactivePeerState()
    {
        var (_, target, origin, peer) = CreateInactivePeer();
        var before = CreateState(
            active: new CellAddress(6, 5),
            anchor: new CellAddress(6, 4),
            range: new CellRange(new CellAddress(5, 4), new CellAddress(7, 6)),
            zoom: 1.2,
            offsetX: 200.5,
            offsetY: 450.75,
            frozenRows: 2,
            frozenColumns: 1);
        peer.View.SetWorksheetState(target, before);

        // Insert inside the frozen region so the freeze boundary must move too.
        origin.Structure.InsertRows(1, 1);
        Assert.AreEqual(
            new CellAddress(7, 5),
            peer.View.GetWorksheetState(target).Selection.ActiveCell);

        Assert.IsTrue(origin.Undo());
        AssertStateEquivalent(before, peer.View.GetWorksheetState(target));

        Assert.IsTrue(origin.Redo());
        var redone = peer.View.GetWorksheetState(target);
        Assert.AreEqual(new CellAddress(7, 5), redone.Selection.ActiveCell);
        Assert.AreEqual(3, redone.FrozenRows);
    }

    [TestMethod]
    public void UndoShouldNotOverwritePeerViewChangedAfterExecute()
    {
        var (_, target, origin, peer) = CreateInactivePeer();
        peer.View.SetWorksheetState(
            target,
            CreateState(
                active: new CellAddress(6, 5),
                anchor: new CellAddress(6, 5),
                range: new CellRange(new CellAddress(6, 5), new CellAddress(6, 5)),
                zoom: 1.1,
                offsetX: 100.25,
                offsetY: 200.5,
                frozenRows: 0,
                frozenColumns: 0));
        origin.Structure.InsertRows(2, 1);
        var mappedSelection = peer.View.GetWorksheetState(target).Selection;

        Assert.IsTrue(peer.View.SetWorksheetViewport(
            target,
            999.875,
            777.625,
            1.6,
            source: this));

        Assert.IsTrue(origin.Undo());
        var afterUndo = peer.View.GetWorksheetState(target);
        Assert.AreEqual(999.875, afterUndo.OffsetX, 0.000001);
        Assert.AreEqual(777.625, afterUndo.OffsetY, 0.000001);
        Assert.AreEqual(1.6, afterUndo.Zoom, 0.000001);
        Assert.AreEqual(mappedSelection.ActiveCell, afterUndo.Selection.ActiveCell);
    }

    [TestMethod]
    public void ActivePeerWorksheetShouldRemainOutsideInactiveCoordinatorScope()
    {
        var workbook = new Workbook();
        var target = workbook.Worksheets[0];
        var origin = new SpreadsheetSession(workbook, target);
        var peer = new SpreadsheetSession(workbook, target);
        peer.Selection.SetActiveCell(new CellAddress(8, 8));
        peer.View.SetWorksheetViewport(target, 88.5, 99.25, 1.4);

        origin.Structure.InsertRows(2, 2);

        Assert.AreEqual(new CellAddress(8, 8), peer.Selection.ActiveCell);
        Assert.AreEqual(88.5, peer.View.WorksheetState.OffsetX, 0.000001);
        Assert.AreEqual(99.25, peer.View.WorksheetState.OffsetY, 0.000001);
    }

    [TestMethod]
    public void RemovedWorksheetShouldBeIgnoredByPeerRestore()
    {
        var workbook = new Workbook();
        var target = workbook.Worksheets[0];
        var other = workbook.AddWorksheet("Other");
        var origin = new SpreadsheetSession(workbook, target);
        var peer = new SpreadsheetSession(workbook, other);
        peer.View.SetWorksheetState(
            target,
            CreateState(
                active: new CellAddress(5, 5),
                anchor: new CellAddress(5, 5),
                range: new CellRange(new CellAddress(5, 5), new CellAddress(5, 5)),
                zoom: 1,
                offsetX: 0,
                offsetY: 0,
                frozenRows: 0,
                frozenColumns: 0));

        origin.Structure.InsertRows(1, 1);
        peer.ActivateWorksheet(target);
        peer.ActivateWorksheet(other);
        workbook.RemoveWorksheet(target);
        peer.View.PruneRemovedWorksheetStates();

        Assert.IsFalse(workbook.Worksheets.Contains(target));
    }

    [TestMethod]
    public void CoordinatorSnapshotShouldRetainPeerOnlyThroughWeakReference()
    {
        var (_, target, origin, peer) = CreateInactivePeer();
        peer.View.SetWorksheetState(
            target,
            CreateState(
                active: new CellAddress(5, 5),
                anchor: new CellAddress(5, 5),
                range: new CellRange(new CellAddress(5, 5), new CellAddress(5, 5)),
                zoom: 1.1,
                offsetX: 123.5,
                offsetY: 456.25,
                frozenRows: 0,
                frozenColumns: 0));

        var snapshots = SpreadsheetCrossSessionStructuralViewCoordinator
            .CaptureInactivePeers(origin, target);

        Assert.AreEqual(1, snapshots.Length);
        var snapshot = snapshots[0];
        Assert.IsInstanceOfType<WeakReference<SpreadsheetSession>>(snapshot.Session);
        Assert.IsTrue(snapshot.Session.TryGetTarget(out var captured));
        Assert.AreSame(peer, captured);

        var fields = snapshot.GetType().GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsFalse(
            fields.Any(field => field.FieldType == typeof(SpreadsheetSession)),
            "Peer snapshots retained by structural history must not contain a strong SpreadsheetSession field.");
    }

    private static (
        Workbook Workbook,
        Worksheet Target,
        SpreadsheetSession Origin,
        SpreadsheetSession Peer) CreateInactivePeer()
    {
        var workbook = new Workbook();
        var target = workbook.Worksheets[0];
        var other = workbook.AddWorksheet("Other");
        var origin = new SpreadsheetSession(workbook, target);
        var peer = new SpreadsheetSession(workbook, other);
        return (workbook, target, origin, peer);
    }

    private static SpreadsheetWorksheetViewState CreateState(
        CellAddress active,
        CellAddress anchor,
        CellRange range,
        double zoom,
        double offsetX,
        double offsetY,
        int frozenRows,
        int frozenColumns)
    {
        var selection = new SelectionSnapshot(
            active,
            anchor,
            Array.AsReadOnly(new[] { range }),
            0);
        var split = default(SpreadsheetSplitViewState)
            .WithPaneScroll(
                SpreadsheetSplitViewPane.TopLeft,
                offsetX,
                offsetY)
            .WithPaneScroll(
                SpreadsheetSplitViewPane.BottomRight,
                offsetX + 0.375,
                offsetY + 0.625);
        return new SpreadsheetWorksheetViewState(
            selection,
            zoom,
            split,
            frozenRows,
            frozenColumns);
    }

    private static void AssertStateEquivalent(
        SpreadsheetWorksheetViewState expected,
        SpreadsheetWorksheetViewState actual)
    {
        Assert.AreEqual(expected.Selection.ActiveCell, actual.Selection.ActiveCell);
        Assert.AreEqual(expected.Selection.AnchorCell, actual.Selection.AnchorCell);
        Assert.IsTrue(expected.Selection.Ranges.SequenceEqual(actual.Selection.Ranges));
        Assert.AreEqual(expected.Zoom, actual.Zoom, 0.000001);
        Assert.AreEqual(expected.SplitState, actual.SplitState);
        Assert.AreEqual(expected.FrozenRows, actual.FrozenRows);
        Assert.AreEqual(expected.FrozenColumns, actual.FrozenColumns);
    }
}
