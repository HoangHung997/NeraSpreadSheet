using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class WorksheetStructuralBoundaryTests
{
    [TestMethod]
    public void InsertBeforeBoundaryExpandsFrozenPrefix()
    {
        var change = new WorksheetStructuralChange(
            WorksheetAxis.Row,
            WorksheetStructuralChangeKind.Insert,
            index: 2,
            count: 3);

        Assert.AreEqual(8, change.MapBoundary(5));
    }

    [TestMethod]
    public void InsertAtBoundaryDoesNotChangeFrozenPrefix()
    {
        var change = new WorksheetStructuralChange(
            WorksheetAxis.Row,
            WorksheetStructuralChangeKind.Insert,
            index: 5,
            count: 2);

        Assert.AreEqual(5, change.MapBoundary(5));
    }

    [TestMethod]
    public void DeleteBeforeBoundaryShrinksFrozenPrefix()
    {
        var change = new WorksheetStructuralChange(
            WorksheetAxis.Column,
            WorksheetStructuralChangeKind.Delete,
            index: 1,
            count: 2);

        Assert.AreEqual(4, change.MapBoundary(6));
    }

    [TestMethod]
    public void DeleteCrossingBoundaryMovesBoundaryToDeleteStart()
    {
        var change = new WorksheetStructuralChange(
            WorksheetAxis.Column,
            WorksheetStructuralChangeKind.Delete,
            index: 4,
            count: 4);

        Assert.AreEqual(4, change.MapBoundary(6));
    }

    [TestMethod]
    public void ChangeAfterBoundaryLeavesBoundaryUntouched()
    {
        var insert = new WorksheetStructuralChange(
            WorksheetAxis.Row,
            WorksheetStructuralChangeKind.Insert,
            index: 8,
            count: 2);
        var delete = new WorksheetStructuralChange(
            WorksheetAxis.Row,
            WorksheetStructuralChangeKind.Delete,
            index: 8,
            count: 2);

        Assert.AreEqual(5, insert.MapBoundary(5));
        Assert.AreEqual(5, delete.MapBoundary(5));
    }
}
