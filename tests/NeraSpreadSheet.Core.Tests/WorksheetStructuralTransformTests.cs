using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class WorksheetStructuralTransformTests
{
    [TestMethod]
    public void InsertRowsShiftsCellsAtAndAfterInsertionPoint()
    {
        var change = new WorksheetStructuralChange(
            WorksheetAxis.Row,
            WorksheetStructuralChangeKind.Insert,
            index: 5,
            count: 2);

        Assert.IsTrue(change.TryMapAddress(new CellAddress(4, 3), out var before));
        Assert.IsTrue(change.TryMapAddress(new CellAddress(5, 3), out var at));
        Assert.IsTrue(change.TryMapAddress(new CellAddress(9, 3), out var after));

        Assert.AreEqual(new CellAddress(4, 3), before);
        Assert.AreEqual(new CellAddress(7, 3), at);
        Assert.AreEqual(new CellAddress(11, 3), after);
    }

    [TestMethod]
    public void InsertInsideRangeExpandsRangeWhileInsertAtStartShiftsIt()
    {
        var range = new CellRange(new CellAddress(5, 2), new CellAddress(10, 4));
        var inside = new WorksheetStructuralChange(
            WorksheetAxis.Row,
            WorksheetStructuralChangeKind.Insert,
            index: 8,
            count: 2);
        var atStart = new WorksheetStructuralChange(
            WorksheetAxis.Row,
            WorksheetStructuralChangeKind.Insert,
            index: 5,
            count: 2);

        Assert.IsTrue(inside.TryMapRange(range, out var expanded));
        Assert.IsTrue(atStart.TryMapRange(range, out var shifted));

        Assert.AreEqual(5, expanded.Top);
        Assert.AreEqual(12, expanded.Bottom);
        Assert.AreEqual(7, shifted.Top);
        Assert.AreEqual(12, shifted.Bottom);
    }

    [TestMethod]
    public void DeleteBandRemovesCellsInsideAndShiftsCellsAfterBand()
    {
        var change = new WorksheetStructuralChange(
            WorksheetAxis.Column,
            WorksheetStructuralChangeKind.Delete,
            index: 3,
            count: 2);

        Assert.IsTrue(change.TryMapAddress(new CellAddress(7, 2), out var before));
        Assert.IsFalse(change.TryMapAddress(new CellAddress(7, 3), out _));
        Assert.IsFalse(change.TryMapAddress(new CellAddress(7, 4), out _));
        Assert.IsTrue(change.TryMapAddress(new CellAddress(7, 5), out var after));

        Assert.AreEqual(new CellAddress(7, 2), before);
        Assert.AreEqual(new CellAddress(7, 3), after);
    }

    [TestMethod]
    public void DeleteInsideRangeShrinksRange()
    {
        var change = new WorksheetStructuralChange(
            WorksheetAxis.Row,
            WorksheetStructuralChangeKind.Delete,
            index: 7,
            count: 2);
        var range = new CellRange(new CellAddress(5, 1), new CellAddress(10, 3));

        Assert.IsTrue(change.TryMapRange(range, out var mapped));

        Assert.AreEqual(5, mapped.Top);
        Assert.AreEqual(8, mapped.Bottom);
    }

    [TestMethod]
    public void DeleteCoveringRangeRemovesRange()
    {
        var change = new WorksheetStructuralChange(
            WorksheetAxis.Row,
            WorksheetStructuralChangeKind.Delete,
            index: 4,
            count: 8);
        var range = new CellRange(new CellAddress(5, 1), new CellAddress(10, 3));

        Assert.IsFalse(change.TryMapRange(range, out _));
    }

    [TestMethod]
    public void InsertThatWouldShiftContentPastWorksheetBoundaryIsRejectedByTryMap()
    {
        var change = new WorksheetStructuralChange(
            WorksheetAxis.Row,
            WorksheetStructuralChangeKind.Insert,
            index: SpreadsheetLimits.MaxRows - 2,
            count: 2);

        Assert.IsFalse(change.TryMapAddress(
            new CellAddress(SpreadsheetLimits.MaxRows - 1, 0),
            out _));
    }

    [TestMethod]
    public void DeleteBeforeRangeShiftsEntireRangeTowardOrigin()
    {
        var change = new WorksheetStructuralChange(
            WorksheetAxis.Column,
            WorksheetStructuralChangeKind.Delete,
            index: 2,
            count: 3);
        var range = new CellRange(new CellAddress(1, 7), new CellAddress(3, 10));

        Assert.IsTrue(change.TryMapRange(range, out var mapped));

        Assert.AreEqual(4, mapped.Left);
        Assert.AreEqual(7, mapped.Right);
    }
}
