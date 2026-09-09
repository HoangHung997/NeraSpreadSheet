using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Interaction;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetWorksheetViewStateTests
{
    [TestMethod]
    public void ActivationShouldRestoreDirectedSelectionsZoomAndFractionalOffsetsRepeatedly()
    {
        var session = new SpreadsheetSession(new Workbook());
        var first = session.ActiveWorksheet;
        var second = session.Workbook.AddWorksheet("Second");
        var firstSelection = new SelectionSnapshot(new CellAddress(4, 2), new CellAddress(2, 4),
            new[] { new CellRange(new CellAddress(2, 2), new CellAddress(4, 4)), new CellRange(new CellAddress(8, 8), new CellAddress(9, 9)) }, 0);
        session.Selection.Restore(firstSelection);
        session.View.SetWorksheetViewport(first, 31.125, 57.375, 1.35);
        session.ActivateWorksheet(second);
        session.Selection.SetActiveCell(new CellAddress(15, 10));
        session.View.SetWorksheetViewport(second, 409.25, 917.625, 0.85);
        for (var pass = 0; pass < 3; pass++)
        {
            session.ActivateWorksheet(first);
            Assert.AreEqual(firstSelection.ActiveCell, session.Selection.ActiveCell);
            Assert.AreEqual(firstSelection.AnchorCell, session.Selection.AnchorCell);
            Assert.IsTrue(firstSelection.Ranges.SequenceEqual(session.Selection.Ranges));
            Assert.AreEqual(1.35, session.View.WorksheetState.Zoom);
            Assert.AreEqual(31.125, session.View.WorksheetState.OffsetX);
            Assert.AreEqual(57.375, session.View.WorksheetState.OffsetY);
            session.ActivateWorksheet(second);
            Assert.AreEqual(new CellAddress(15, 10), session.Selection.ActiveCell);
            Assert.AreEqual(0.85, session.View.WorksheetState.Zoom);
            Assert.AreEqual(409.25, session.View.WorksheetState.OffsetX);
            Assert.AreEqual(917.625, session.View.WorksheetState.OffsetY);
        }
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public void TwoSessionsShouldKeepIndependentStateOverTheSameWorkbook()
    {
        var workbook = new Workbook();
        var second = workbook.AddWorksheet("Second");
        var firstSession = new SpreadsheetSession(workbook);
        var otherSession = new SpreadsheetSession(workbook);
        firstSession.Selection.SetActiveCell(new CellAddress(2, 2));
        firstSession.View.SetWorksheetViewport(firstSession.ActiveWorksheet, 10.5, 20.75, 1.5);
        otherSession.Selection.SetActiveCell(new CellAddress(7, 7));
        otherSession.View.SetWorksheetViewport(otherSession.ActiveWorksheet, 700.5, 800.75, 0.5);
        firstSession.ActivateWorksheet(second);
        firstSession.ActivateWorksheet(workbook.Worksheets[0]);
        Assert.AreEqual(new CellAddress(2, 2), firstSession.Selection.ActiveCell);
        Assert.AreEqual(new CellAddress(7, 7), otherSession.Selection.ActiveCell);
        Assert.AreEqual(10.5, firstSession.View.WorksheetState.OffsetX);
        Assert.AreEqual(700.5, otherSession.View.WorksheetState.OffsetX);
    }

    [TestMethod]
    public void RestoreEventsShouldObserveNewStateAndSuppressFeedbackWrites()
    {
        var session = new SpreadsheetSession(new Workbook());
        var second = session.Workbook.AddWorksheet("Second");
        var selection = new SelectionSnapshot(new CellAddress(4, 4), new CellAddress(2, 2),
            new[] { new CellRange(new CellAddress(2, 2), new CellAddress(4, 4)) }, 0);
        session.View.SetWorksheetState(second, new SpreadsheetWorksheetViewState(selection, 1.25,
            default(SpreadsheetSplitViewState).WithPaneScroll(SpreadsheetSplitViewPane.TopLeft, 321.5, 654.25)));
        var observed = false;
        session.ActiveWorksheetChanged += (_, _) =>
        {
            observed = true;
            Assert.IsTrue(session.View.IsRestoringWorksheetView);
            Assert.AreSame(second, session.ActiveWorksheet);
            Assert.AreEqual(selection.ActiveCell, session.Selection.ActiveCell);
            Assert.IsFalse(session.View.SetWorksheetViewport(second, 0, 0, 1));
            Assert.IsFalse(session.View.ClearSplitPanes());
        };
        session.ActivateWorksheet(second);
        Assert.IsTrue(observed);
        Assert.IsFalse(session.View.IsRestoringWorksheetView);
        Assert.AreEqual(321.5, session.View.WorksheetState.OffsetX);
    }

    [TestMethod]
    public void RenameShouldKeepIdentityAndRemovedSheetShouldNotDonateStateToSameName()
    {
        var session = new SpreadsheetSession(new Workbook());
        var first = session.ActiveWorksheet;
        var second = session.Workbook.AddWorksheet("Second");
        session.Selection.SetActiveCell(new CellAddress(12, 12));
        session.View.SetWorksheetViewport(first, 111.5, 222.25, 2);
        session.Workbook.RenameWorksheet(first, "Renamed");
        session.ActivateWorksheet(second);
        session.ActivateWorksheet(first);
        Assert.AreEqual(new CellAddress(12, 12), session.Selection.ActiveCell);
        session.ActivateWorksheet(second);
        session.Workbook.RemoveWorksheet(first);
        session.View.PruneRemovedWorksheetStates();
        var replacement = session.Workbook.AddWorksheet("Renamed");
        session.ActivateWorksheet(replacement);
        Assert.AreEqual(default(CellAddress), session.Selection.ActiveCell);
        Assert.AreEqual(1d, session.View.WorksheetState.Zoom);
        Assert.AreEqual(0d, session.View.WorksheetState.OffsetX);
    }

    [TestMethod]
    public void SplitFreezeAndInactivePaneOffsetsShouldSurviveActivation()
    {
        var session = new SpreadsheetSession(new Workbook());
        var first = session.ActiveWorksheet;
        var second = session.Workbook.AddWorksheet("Second");
        session.View.SetFrozenPanes(2, 1);
        session.View.SetSplitTopology(SpreadsheetSplitViewMode.Both, 250.5, 150.25);
        session.View.SetSplitPaneScroll(SpreadsheetSplitViewPane.BottomRight, 234.125, 345.875);
        session.View.SetSplitActivePane(SpreadsheetSplitViewPane.BottomRight);
        var before = session.View.SplitState;
        session.ActivateWorksheet(second);
        Assert.IsFalse(session.View.HasFrozenPanes);
        session.ActivateWorksheet(first);
        Assert.AreEqual(before, session.View.SplitState);
        Assert.AreEqual(2, session.View.FrozenRows);
        Assert.AreEqual(1, session.View.FrozenColumns);
    }

    [TestMethod]
    public void StateShouldDefensivelyCopySelectionsAndRejectInvalidNumbers()
    {
        var ranges = new[] { new CellRange(default, default) };
        var state = new SpreadsheetWorksheetViewState(new SelectionSnapshot(default, default, ranges, 0));
        ranges[0] = new CellRange(new CellAddress(4, 4), new CellAddress(4, 4));
        Assert.AreEqual(new CellRange(default, default), state.Selection.Ranges[0]);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new SpreadsheetWorksheetViewState(state.Selection, double.NaN));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new SpreadsheetWorksheetViewState(state.Selection, 0));
        var session = new SpreadsheetSession(new Workbook());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => session.View.SetWorksheetViewport(session.ActiveWorksheet, double.PositiveInfinity, 0, 1));
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public void ExplicitClampShouldPreserveFractionsAndOnlyChangeTargetWorksheet()
    {
        var session = new SpreadsheetSession(new Workbook());
        var first = session.ActiveWorksheet;
        var second = session.Workbook.AddWorksheet("Second");
        session.View.SetWorksheetViewport(first, 123.75, 456.125, 1.25);
        session.View.SetWorksheetViewport(second, 1000.5, 2000.25, 1.5);
        Assert.IsTrue(session.View.ClampWorksheetViewport(first, 100.125, 400.375));
        Assert.AreEqual(100.125, session.View.GetWorksheetState(first).OffsetX);
        Assert.AreEqual(400.375, session.View.GetWorksheetState(first).OffsetY);
        Assert.AreEqual(1000.5, session.View.GetWorksheetState(second).OffsetX);
        Assert.AreEqual(1.25, session.View.GetWorksheetState(first).Zoom);
    }

    [TestMethod]
    public void ReentrantActivationShouldBeRejectedAndRestoreGuardShouldRecover()
    {
        var session = new SpreadsheetSession(new Workbook());
        var first = session.ActiveWorksheet;
        var second = session.Workbook.AddWorksheet("Second");
        session.ActiveWorksheetChanged += (_, _) =>
            Assert.ThrowsExactly<InvalidOperationException>(() => session.ActivateWorksheet(first));
        session.ActivateWorksheet(second);
        Assert.AreSame(second, session.ActiveWorksheet);
        Assert.IsFalse(session.View.IsRestoringWorksheetView);
    }
    [TestMethod]
    public void ActivationShouldMoveSingleHiddenCellToAVisibleAddress()
    {
        var session = new SpreadsheetSession(new Workbook());
        var first = session.ActiveWorksheet;
        var second = session.Workbook.AddWorksheet("Second");
        session.Selection.SetActiveCell(new CellAddress(2, 2));
        session.ActivateWorksheet(second);
        first.Dimensions.HideRows(2);
        first.Dimensions.HideColumns(2);
        session.ActivateWorksheet(first);
        Assert.AreEqual(new CellAddress(3, 3), session.Selection.ActiveCell);
        Assert.AreEqual(1, session.Selection.Ranges.Count);
        Assert.IsTrue(session.Selection.Contains(new CellAddress(3, 3)));
    }

    [TestMethod]
    public void ActivationShouldResolveNewMergedAnchorWithoutChangingOtherSheets()
    {
        var session = new SpreadsheetSession(new Workbook());
        var first = session.ActiveWorksheet;
        var second = session.Workbook.AddWorksheet("Second");
        session.Selection.SetActiveCell(new CellAddress(2, 2));
        session.ActivateWorksheet(second);
        session.Selection.SetActiveCell(new CellAddress(7, 7));
        first.MergeCells(new CellRange(new CellAddress(1, 1), new CellAddress(3, 3)));
        session.ActivateWorksheet(first);
        Assert.AreEqual(new CellAddress(1, 1), session.Selection.ActiveCell);
        Assert.AreEqual(1, session.Selection.Ranges.Count);
        session.ActivateWorksheet(second);
        Assert.AreEqual(new CellAddress(7, 7), session.Selection.ActiveCell);
    }

}
