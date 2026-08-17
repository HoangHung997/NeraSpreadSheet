using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Layout.Tests;

[TestClass]
public sealed class FrozenViewportLayoutTests
{
    [TestMethod]
    public void FrozenSlotsStayFixedWhileScrollableSlotsKeepFractionalOffset()
    {
        var rows = new SparseAxisMetricIndex(100, 20d);
        var columns = new SparseAxisMetricIndex(50, 80d);
        var engine = new ViewportLayoutEngine(rows, columns);

        var layout = engine.Compute(new ViewportRequest(
            13.25d,
            7.75d,
            new SizeD(320d, 200d),
            0d,
            FrozenRows: 1,
            FrozenColumns: 1));

        var frozenRow = layout.Rows.Single(slot => slot.Index == 0);
        var firstScrollableRow = layout.Rows.Single(slot => slot.Index == 1);
        var frozenColumn = layout.Columns.Single(slot => slot.Index == 0);
        var firstScrollableColumn = layout.Columns.Single(slot => slot.Index == 1);

        Assert.IsTrue(frozenRow.IsFrozen);
        Assert.AreEqual(0d, frozenRow.Start, 1e-9);
        Assert.IsFalse(firstScrollableRow.IsFrozen);
        Assert.AreEqual(12.25d, firstScrollableRow.Start, 1e-9);
        Assert.IsTrue(frozenColumn.IsFrozen);
        Assert.AreEqual(0d, frozenColumn.Start, 1e-9);
        Assert.IsFalse(firstScrollableColumn.IsFrozen);
        Assert.AreEqual(66.75d, firstScrollableColumn.Start, 1e-9);
        Assert.AreEqual(20d, layout.FrozenHeight, 1e-9);
        Assert.AreEqual(80d, layout.FrozenWidth, 1e-9);
    }

    [TestMethod]
    public void HiddenFrozenRowsDoNotConsumeViewportSpace()
    {
        var rows = new SparseAxisMetricIndex(10, 20d);
        var columns = new SparseAxisMetricIndex(10, 80d);
        rows.SetSize(0, 0d);
        var engine = new ViewportLayoutEngine(rows, columns);

        var layout = engine.Compute(new ViewportRequest(
            0d,
            5d,
            new SizeD(300d, 160d),
            0d,
            FrozenRows: 1));

        Assert.AreEqual(0d, layout.FrozenHeight, 1e-9);
        Assert.IsFalse(layout.Rows.Any(slot => slot.IsFrozen));
        Assert.AreEqual(15d, layout.Rows.Single(slot => slot.Index == 1).Start, 1e-9);
    }

    [TestMethod]
    public void FrozenExtentCanConsumeEntireViewportWithoutInvalidScrollSlots()
    {
        var rows = new SparseAxisMetricIndex(10, 40d);
        var columns = new SparseAxisMetricIndex(10, 80d);
        var engine = new ViewportLayoutEngine(rows, columns);

        var layout = engine.Compute(new ViewportRequest(
            100d,
            100d,
            new SizeD(160d, 80d),
            FrozenRows: 3,
            FrozenColumns: 3));

        Assert.AreEqual(0d, layout.ScrollX, 1e-9);
        Assert.AreEqual(0d, layout.ScrollY, 1e-9);
        Assert.IsTrue(layout.Rows.All(slot => slot.IsFrozen));
        Assert.IsTrue(layout.Columns.All(slot => slot.IsFrozen));
    }
}
