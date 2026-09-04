using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class WorksheetDimensionsVisibilityTests
{
    [TestMethod]
    public void HideAndUnhideShouldRetainCustomSizeWithoutMaterializingRange()
    {
        var dimensions = new WorksheetDimensions();
        dimensions.SetRowHeight(3, 44d);

        dimensions.HideRows(3, 100_000);

        Assert.AreEqual(1, dimensions.GetHiddenRowRanges().Count);
        Assert.AreEqual(1, dimensions.GetRowOverrides().Count);
        Assert.AreEqual(0d, dimensions.GetRowHeight(3), 1e-9);
        Assert.AreEqual(0d, dimensions.GetRowHeight(100_002), 1e-9);
        Assert.AreEqual(44d, dimensions.GetUnhiddenRowHeight(3), 1e-9);

        dimensions.UnhideRows(3);

        Assert.AreEqual(44d, dimensions.GetRowHeight(3), 1e-9);
        Assert.AreEqual(
            new WorksheetAxisInterval(4, 100_002),
            dimensions.GetHiddenRowRanges()[0]);
    }

    [TestMethod]
    public void SettingPositiveSizeShouldUnhideOnlyRequestedAxisEntry()
    {
        var dimensions = new WorksheetDimensions();
        dimensions.HideColumns(1, 3);

        dimensions.SetColumnWidth(2, 125d);

        Assert.IsTrue(dimensions.IsColumnHidden(1));
        Assert.IsFalse(dimensions.IsColumnHidden(2));
        Assert.IsTrue(dimensions.IsColumnHidden(3));
        Assert.AreEqual(125d, dimensions.GetColumnWidth(2), 1e-9);
        Assert.AreEqual(2, dimensions.GetHiddenColumnRanges().Count);
    }
}
