using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Layout.Tests;

[TestClass]
public sealed class ViewportLayoutEngineTests
{
    [TestMethod]
    public void Compute_Should_NotSnapScrollOffset_When_RequestContainsFractions()
    {
        var rows = new SparseAxisMetricIndex(1_000, 20d);
        var columns = new SparseAxisMetricIndex(100, 80d);
        var engine = new ViewportLayoutEngine(rows, columns);

        var layout = engine.Compute(new ViewportRequest(3.25d, 7.5d, new SizeD(800d, 600d), 0d));

        Assert.AreEqual(3.25d, layout.ScrollX, 1e-9);
        Assert.AreEqual(7.5d, layout.ScrollY, 1e-9);
        Assert.AreEqual(-7.5d, layout.Rows[0].Start, 1e-9);
    }
}
