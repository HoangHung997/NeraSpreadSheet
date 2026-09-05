using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Interaction;

namespace NeraSpreadSheet.Interaction.Tests;

[TestClass]
public sealed class SelectionModelTests
{
    [TestMethod]
    public void ExtendToPreservesAnchorAndUpdatesActiveCell()
    {
        var selection = new SelectionModel();
        selection.SetActiveCell(new CellAddress(2, 3));
        selection.ExtendTo(new CellAddress(5, 7));
        Assert.AreEqual(new CellAddress(2, 3), selection.AnchorCell);
        Assert.AreEqual(new CellAddress(5, 7), selection.ActiveCell);
        Assert.AreEqual(new CellRange(new CellAddress(2, 3), new CellAddress(5, 7)), selection.Ranges[0]);
    }

    [TestMethod]
    public void AddRangeCreatesMultiRangeSelection()
    {
        var selection = new SelectionModel();
        selection.Select(new CellRange(new CellAddress(1, 1), new CellAddress(2, 2)));
        selection.AddRange(new CellRange(new CellAddress(8, 8), new CellAddress(9, 9)));
        Assert.AreEqual(2, selection.Ranges.Count);
        Assert.IsTrue(selection.Contains(new CellAddress(8, 9)));
    }

    [TestMethod]
    public void SelectRowSpansEntireColumnAxis()
    {
        var selection = new SelectionModel();

        selection.SelectRow(7);

        Assert.AreEqual(new CellAddress(7, 0), selection.ActiveCell);
        Assert.AreEqual(
            new CellRange(
                new CellAddress(7, 0),
                new CellAddress(7, SpreadsheetLimits.MaxColumns - 1)),
            selection.Ranges[0]);
    }

    [TestMethod]
    public void SelectColumnSpansEntireRowAxis()
    {
        var selection = new SelectionModel();

        selection.SelectColumn(4);

        Assert.AreEqual(new CellAddress(0, 4), selection.ActiveCell);
        Assert.AreEqual(
            new CellRange(
                new CellAddress(0, 4),
                new CellAddress(SpreadsheetLimits.MaxRows - 1, 4)),
            selection.Ranges[0]);
    }

    [TestMethod]
    public void ShiftExtendingRowsPreservesOriginalRowAnchor()
    {
        var selection = new SelectionModel();
        selection.SelectRow(5);

        selection.ExtendRowsTo(9);
        selection.ExtendRowsTo(3);

        Assert.AreEqual(new CellAddress(5, 0), selection.AnchorCell);
        Assert.AreEqual(new CellAddress(3, 0), selection.ActiveCell);
        Assert.AreEqual(3, selection.Ranges[0].Top);
        Assert.AreEqual(5, selection.Ranges[0].Bottom);
        Assert.AreEqual(0, selection.Ranges[0].Left);
        Assert.AreEqual(SpreadsheetLimits.MaxColumns - 1, selection.Ranges[0].Right);
    }

    [TestMethod]
    public void SelectAllSpansEntireWorksheetAddressSpace()
    {
        var selection = new SelectionModel();

        selection.SelectAll();

        Assert.AreEqual(0, selection.Ranges[0].Top);
        Assert.AreEqual(0, selection.Ranges[0].Left);
        Assert.AreEqual(SpreadsheetLimits.MaxRows - 1, selection.Ranges[0].Bottom);
        Assert.AreEqual(SpreadsheetLimits.MaxColumns - 1, selection.Ranges[0].Right);
    }
}
