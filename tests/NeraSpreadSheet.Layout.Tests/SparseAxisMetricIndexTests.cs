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
}
