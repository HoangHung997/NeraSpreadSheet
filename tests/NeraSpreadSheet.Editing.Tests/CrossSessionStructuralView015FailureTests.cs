using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Interaction;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class CrossSessionStructuralView015FailureTests
{
    [TestMethod]
    public void PeerObserverFailureShouldRollBackAllPeerStatesAndPreserveOriginalError()
    {
        var workbook = new Workbook();
        var target = workbook.Worksheets[0];
        var other = workbook.AddWorksheet("Other");
        var origin = new SpreadsheetSession(workbook, target);
        var firstPeer = new SpreadsheetSession(workbook, other);
        var throwingPeer = new SpreadsheetSession(workbook, other);

        var firstBefore = StateAt(new CellAddress(4, 4), 111.25, 222.5);
        var throwingBefore = StateAt(new CellAddress(7, 7), 333.75, 444.125);
        firstPeer.View.SetWorksheetState(target, firstBefore);
        throwingPeer.View.SetWorksheetState(target, throwingBefore);

        throwingPeer.View.WorksheetViewChanged += (_, args) =>
        {
            if (ReferenceEquals(args.Worksheet, target))
            {
                throw new InvalidOperationException("peer observer failure");
            }
        };

        var error = Assert.ThrowsExactly<InvalidOperationException>(
            () => origin.Structure.InsertRows(1, 1));

        Assert.AreEqual("peer observer failure", error.Message);
        AssertStateEquivalent(firstBefore, firstPeer.View.GetWorksheetState(target));
        AssertStateEquivalent(throwingBefore, throwingPeer.View.GetWorksheetState(target));
        Assert.AreEqual(0, origin.History.UndoCount);
    }

    private static SpreadsheetWorksheetViewState StateAt(
        CellAddress active,
        double offsetX,
        double offsetY)
    {
        var selection = new SelectionSnapshot(
            active,
            active,
            Array.AsReadOnly(new[] { new CellRange(active, active) }),
            0);
        var split = default(SpreadsheetSplitViewState)
            .WithPaneScroll(
                SpreadsheetSplitViewPane.TopLeft,
                offsetX,
                offsetY);
        return new SpreadsheetWorksheetViewState(selection, 1.25, split);
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
