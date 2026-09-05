using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Interaction;

namespace NeraSpreadSheet.Interaction.Tests;

[TestClass]
public sealed class SelectionRestoreTests
{
    [TestMethod]
    public void RestoreRecoversActiveAnchorAndMultiRangeSelection()
    {
        var selection = new SelectionModel();
        selection.Select(new CellRange(new CellAddress(2, 3), new CellAddress(4, 5)));
        selection.AddRange(new CellRange(new CellAddress(8, 1), new CellAddress(9, 2)));
        var snapshot = selection.Capture();
        selection.SetActiveCell(default);

        selection.Restore(snapshot);

        Assert.AreEqual(snapshot.ActiveCell, selection.ActiveCell);
        Assert.AreEqual(snapshot.AnchorCell, selection.AnchorCell);
        CollectionAssert.AreEqual(snapshot.Ranges.ToArray(), selection.Ranges.ToArray());
        Assert.IsTrue(selection.Version > snapshot.Version);
    }

    [TestMethod]
    public void RestorePublishesOnlyWhenSelectionActuallyChanges()
    {
        var selection = new SelectionModel();
        var snapshot = selection.Capture();
        var changed = 0;
        selection.Changed += (_, _) => changed++;

        selection.Restore(snapshot);

        Assert.AreEqual(0, changed);
    }
}
