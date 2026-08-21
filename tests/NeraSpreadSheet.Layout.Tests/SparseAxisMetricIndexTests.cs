using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Layout.Tests;

[TestClass]
public sealed class SparseAxisMetricIndexTests
{
    [TestMethod]
    public void GetOffset_Should_UseDefaultAndOverrides_When_SizesDiffer()
    {
        var index = new SparseAxisMetricIndex(10, 20d);
        index.SetSize(1, 50d);

        Assert.AreEqual(0d, index.GetOffset(0), 1e-9);
        Assert.AreEqual(20d, index.GetOffset(1), 1e-9);
        Assert.AreEqual(70d, index.GetOffset(2), 1e-9);
        Assert.AreEqual(90d, index.GetOffset(3), 1e-9);
    }

    [TestMethod]
    public void FindIndexAtOffset_Should_ReturnNextVisibleRow_When_PreviousRowIsHidden()
    {
        var index = new SparseAxisMetricIndex(5, 20d);
        index.SetSize(0, 0d);

        Assert.AreEqual(1, index.FindIndexAtOffset(0d));
    }

    [TestMethod]
    public void GetSlots_Should_PreservePartialPixelOffset_When_RowIsClipped()
    {
        var index = new SparseAxisMetricIndex(100, 20d);

        var slots = index.GetSlots(7.5d, 40d);

        Assert.IsTrue(slots.Count >= 3);
        Assert.AreEqual(0, slots[0].Index);
        Assert.AreEqual(-7.5d, slots[0].Start, 1e-9);
    }

    [TestMethod]
    public void HiddenRangesCompressExtentAndSkipRowsWithoutPerRowOverrides()
    {
        var index = new SparseAxisMetricIndex(6, 20d);
        index.SetSize(2, 30d);

        index.SetHiddenRanges([
            new AxisIndexRange(1, 3),
        ]);

        Assert.AreEqual(1, index.HiddenRangeCount);
        Assert.AreEqual(1, index.OverrideCount);
        Assert.AreEqual(60d, index.TotalExtent, 1e-9);
        Assert.AreEqual(20d, index.GetOffset(4), 1e-9);
        Assert.AreEqual(4, index.FindIndexAtOffset(20d));
        CollectionAssert.AreEqual(
            new[] { 0, 4, 5 },
            index.GetSlots(0d, 60d)
                .Select(static slot => slot.Index)
                .ToArray());
    }

    [TestMethod]
    public void ClearingHiddenRangesRestoresOriginalSparseSizes()
    {
        var index = new SparseAxisMetricIndex(4, 20d);
        index.SetHiddenRanges([
            new AxisIndexRange(1, 1),
        ]);
        index.SetSize(1, 55d);

        Assert.AreEqual(0d, index.GetSize(1), 1e-9);

        index.ClearHiddenRanges();

        Assert.AreEqual(55d, index.GetSize(1), 1e-9);
        Assert.AreEqual(115d, index.TotalExtent, 1e-9);
    }
}
