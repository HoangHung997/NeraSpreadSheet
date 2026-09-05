using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Interaction;

namespace NeraSpreadSheet.Interaction.Tests;

[TestClass]
public sealed class DirtyCellRangeTrackerTests
{
    [TestMethod]
    public void AddMergesTouchingRanges()
    {
        var tracker = new DirtyCellRangeTracker();
        tracker.Add(new CellRange(new CellAddress(1, 1), new CellAddress(2, 2)));
        tracker.Add(new CellRange(new CellAddress(2, 3), new CellAddress(4, 4)));
        var ranges = tracker.Peek();
        Assert.AreEqual(1, ranges.Count);
        Assert.AreEqual(new CellRange(new CellAddress(1, 1), new CellAddress(4, 4)), ranges[0]);
    }

    [TestMethod]
    public void DrainClearsTracker()
    {
        var tracker = new DirtyCellRangeTracker();
        tracker.Add(new CellAddress(3, 3));
        var drained = tracker.Drain();
        Assert.AreEqual(1, drained.Count);
        Assert.IsTrue(tracker.IsEmpty);
    }
}
