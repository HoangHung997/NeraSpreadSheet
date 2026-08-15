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
}
