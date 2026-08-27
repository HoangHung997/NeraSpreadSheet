using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class SpreadsheetAnalyticsInteractionTargetMapperTests
{
    [TestMethod]
    public void FreezeCrossingCreatesFourTargetsWithFullObjectBoundsAndPaneClips()
    {
        var item = SpreadsheetAnalyticsItemKey.ForChart(Guid.NewGuid());
        var placement = new SpreadsheetAnalyticsPlacement(
            item,
            new RectD(80d, 30d, 100d, 100d),
            3);
        var layout = new ViewportLayout(
            40d,
            20d,
            new SizeD(300d, 200d),
            2000d,
            2000d,
            100d,
            50d,
            [],
            []);

        var targets = SpreadsheetAnalyticsInteractionTargetMapper.Map(
            [placement],
            layout);

        Assert.AreEqual(4, targets.Count);
        Assert.IsTrue(targets.All(target => target.Item == item));
        Assert.IsTrue(targets.All(target => target.DocumentBounds == placement.DocumentBounds));
        Assert.IsTrue(targets.All(target => target.ZIndex == 3));
        Assert.IsTrue(targets.Any(target =>
            target.ViewportBounds == new RectD(80d, 30d, 100d, 100d) &&
            target.ClipBounds == new RectD(0d, 0d, 100d, 50d)));
        Assert.IsTrue(targets.Any(target =>
            target.ViewportBounds == new RectD(40d, 30d, 100d, 100d) &&
            target.ClipBounds == new RectD(100d, 0d, 200d, 50d)));
        Assert.IsTrue(targets.Any(target =>
            target.ViewportBounds == new RectD(80d, 10d, 100d, 100d) &&
            target.ClipBounds == new RectD(0d, 50d, 100d, 150d)));
        Assert.IsTrue(targets.Any(target =>
            target.ViewportBounds == new RectD(40d, 10d, 100d, 100d) &&
            target.ClipBounds == new RectD(100d, 50d, 200d, 150d)));
    }

    [TestMethod]
    public void HitTesterCannotSelectHiddenPartOutsideFragmentClip()
    {
        var item = SpreadsheetAnalyticsItemKey.ForPivot(Guid.NewGuid());
        var target = new SpreadsheetAnalyticsInteractionTarget(
            item,
            new RectD(80d, 30d, 100d, 100d),
            new RectD(40d, 10d, 100d, 100d),
            new RectD(100d, 50d, 200d, 150d),
            1);

        var hidden = SpreadsheetAnalyticsHitTester.HitTest(
            [target],
            new PointD(60d, 30d));
        var visible = SpreadsheetAnalyticsHitTester.HitTest(
            [target],
            new PointD(110d, 60d));

        Assert.IsNull(hidden);
        Assert.IsTrue(visible.HasValue);
        Assert.AreEqual(item, visible.Value.Item);
    }
}
