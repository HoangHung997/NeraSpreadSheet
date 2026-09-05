using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class SpreadsheetAnalyticsPlacementTests
{
    [TestMethod]
    public void PlacementRequiresNonEmptyItemAndPositiveDocumentBounds()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new SpreadsheetAnalyticsItemKey(
                SpreadsheetAnalyticsItemKind.Chart,
                Guid.Empty));

        var item = SpreadsheetAnalyticsItemKey.ForChart(Guid.NewGuid());
        Assert.ThrowsExactly<ArgumentException>(() =>
            new SpreadsheetAnalyticsPlacement(
                item,
                RectD.Empty,
                0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new SpreadsheetAnalyticsPlacement(
                item,
                new RectD(-1d, 0d, 100d, 80d),
                0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new SpreadsheetAnalyticsPlacement(
                item,
                new RectD(0d, 0d, 100d, 80d),
                -1));
    }

    [TestMethod]
    public void PlacementTransformsPreserveIdentityAndUnchangedFields()
    {
        var item = SpreadsheetAnalyticsItemKey.ForPivot(Guid.NewGuid());
        var placement = new SpreadsheetAnalyticsPlacement(
            item,
            new RectD(20d, 30d, 320d, 180d),
            4);

        var moved = placement.WithBounds(
            new RectD(45d, 55d, 320d, 180d));
        var reordered = moved.WithZIndex(9);

        Assert.AreEqual(item, moved.Item);
        Assert.AreEqual(4, moved.ZIndex);
        Assert.AreEqual(new RectD(45d, 55d, 320d, 180d), moved.DocumentBounds);
        Assert.AreEqual(moved.DocumentBounds, reordered.DocumentBounds);
        Assert.AreEqual(9, reordered.ZIndex);
    }
}
