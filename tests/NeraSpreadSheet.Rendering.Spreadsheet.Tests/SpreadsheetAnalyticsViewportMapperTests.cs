using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class SpreadsheetAnalyticsViewportMapperTests
{
    [TestMethod]
    public void ScrollOnlyMappingPreservesPixelOffsets()
    {
        var placement = Placement(new RectD(100d, 80d, 200d, 120d));
        var layout = Layout(
            scrollX: 40d,
            scrollY: 20d,
            width: 400d,
            height: 300d);

        var fragments = SpreadsheetAnalyticsViewportMapper.Map(
            placement,
            layout);

        Assert.AreEqual(1, fragments.Count);
        Assert.AreEqual(new RectD(60d, 60d, 200d, 120d), fragments[0].VisibleBounds);
        Assert.AreEqual(60d, fragments[0].TranslationX);
        Assert.AreEqual(60d, fragments[0].TranslationY);
        Assert.IsFalse(fragments[0].IsFrozenX);
        Assert.IsFalse(fragments[0].IsFrozenY);
    }

    [TestMethod]
    public void CrossingFrozenAxesProducesFourIndependentlyMappedFragments()
    {
        var placement = Placement(new RectD(80d, 30d, 100d, 100d));
        var layout = Layout(
            scrollX: 40d,
            scrollY: 20d,
            width: 300d,
            height: 200d,
            frozenWidth: 100d,
            frozenHeight: 50d);

        var fragments = SpreadsheetAnalyticsViewportMapper.Map(
            placement,
            layout);

        Assert.AreEqual(4, fragments.Count);
        Assert.IsTrue(fragments.Any(fragment =>
            fragment.IsFrozenX &&
            fragment.IsFrozenY &&
            fragment.VisibleBounds == new RectD(80d, 30d, 20d, 20d)));
        Assert.IsTrue(fragments.Any(fragment =>
            !fragment.IsFrozenX &&
            fragment.IsFrozenY &&
            fragment.VisibleBounds == new RectD(100d, 30d, 40d, 20d)));
        Assert.IsTrue(fragments.Any(fragment =>
            fragment.IsFrozenX &&
            !fragment.IsFrozenY &&
            fragment.VisibleBounds == new RectD(80d, 50d, 20d, 60d)));
        Assert.IsTrue(fragments.Any(fragment =>
            !fragment.IsFrozenX &&
            !fragment.IsFrozenY &&
            fragment.VisibleBounds == new RectD(100d, 50d, 40d, 60d)));
    }

    [TestMethod]
    public void OffscreenPlacementProducesNoFragments()
    {
        var placement = Placement(new RectD(900d, 700d, 120d, 80d));
        var layout = Layout(
            scrollX: 20d,
            scrollY: 10d,
            width: 320d,
            height: 240d);

        Assert.AreEqual(
            0,
            SpreadsheetAnalyticsViewportMapper.Map(placement, layout).Count);
    }

    [TestMethod]
    public void MultiplePlacementsMapInStableZOrder()
    {
        var low = new SpreadsheetAnalyticsPlacement(
            SpreadsheetAnalyticsItemKey.ForChart(Guid.NewGuid()),
            new RectD(20d, 20d, 100d, 80d),
            1);
        var high = new SpreadsheetAnalyticsPlacement(
            SpreadsheetAnalyticsItemKey.ForPivot(Guid.NewGuid()),
            new RectD(40d, 40d, 100d, 80d),
            7);
        var layout = Layout(0d, 0d, 300d, 200d);

        var fragments = SpreadsheetAnalyticsViewportMapper.Map(
            [high, low],
            layout);

        Assert.AreEqual(2, fragments.Count);
        Assert.AreEqual(low.Item, fragments[0].Placement.Item);
        Assert.AreEqual(high.Item, fragments[1].Placement.Item);
    }

    private static SpreadsheetAnalyticsPlacement Placement(RectD bounds) =>
        new(
            SpreadsheetAnalyticsItemKey.ForChart(Guid.NewGuid()),
            bounds,
            0);

    private static ViewportLayout Layout(
        double scrollX,
        double scrollY,
        double width,
        double height,
        double frozenWidth = 0d,
        double frozenHeight = 0d) =>
        new(
            scrollX,
            scrollY,
            new SizeD(width, height),
            2000d,
            2000d,
            frozenWidth,
            frozenHeight,
            [],
            []);
}
