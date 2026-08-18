using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class WorksheetAxisMoveTests
{
    [TestMethod]
    public void MoveUpMapsSourceAndInterveningIndexes()
    {
        var move = new WorksheetAxisMove(
            WorksheetAxis.Row,
            sourceIndex: 3,
            count: 2,
            destinationBoundary: 1);

        Assert.IsFalse(move.IsNoOp);
        Assert.AreEqual(1, move.InsertionIndex);
        Assert.AreEqual(1, move.AffectedStartIndex);
        Assert.AreEqual(4, move.AffectedEndIndex);
        CollectionAssert.AreEqual(
            new[] { 0, 3, 4, 1, 2, 5 },
            Enumerable.Range(0, 6).Select(move.MapIndex).ToArray());
    }

    [TestMethod]
    public void MoveDownUsesOriginalCoordinateDestinationBoundary()
    {
        var move = new WorksheetAxisMove(
            WorksheetAxis.Column,
            sourceIndex: 1,
            count: 2,
            destinationBoundary: 5);

        Assert.AreEqual(3, move.InsertionIndex);
        CollectionAssert.AreEqual(
            new[] { 0, 3, 4, 1, 2, 5 },
            Enumerable.Range(0, 6).Select(move.MapIndex).ToArray());
    }

    [TestMethod]
    public void DestinationInsideOrAdjacentToSourceIsNoOp()
    {
        foreach (var boundary in new[] { 4, 5, 6, 7 })
        {
            var move = new WorksheetAxisMove(
                WorksheetAxis.Row,
                sourceIndex: 4,
                count: 3,
                destinationBoundary: boundary);

            Assert.IsTrue(move.IsNoOp);
            Assert.AreEqual(5, move.MapIndex(5));
        }
    }

    [TestMethod]
    public void IntervalMappingReportsDiscontiguousImages()
    {
        var move = new WorksheetAxisMove(
            WorksheetAxis.Row,
            sourceIndex: 3,
            count: 2,
            destinationBoundary: 1);

        var split = move.MapInterval(2, 3);
        CollectionAssert.AreEqual(
            new[]
            {
                new WorksheetAxisInterval(1, 1),
                new WorksheetAxisInterval(4, 4),
            },
            split);
        Assert.IsFalse(move.TryMapContiguousInterval(2, 3, out _, out _));

        Assert.IsTrue(move.TryMapContiguousInterval(
            1,
            4,
            out var mappedStart,
            out var mappedEnd));
        Assert.AreEqual(1, mappedStart);
        Assert.AreEqual(4, mappedEnd);
    }

    [TestMethod]
    public void ConstructorRejectsInvalidCoordinates()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new WorksheetAxisMove(
                WorksheetAxis.Row,
                -1,
                1,
                0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new WorksheetAxisMove(
                WorksheetAxis.Column,
                0,
                0,
                0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new WorksheetAxisMove(
                WorksheetAxis.Column,
                0,
                1,
                SpreadsheetLimits.MaxColumns + 1));
    }
}
