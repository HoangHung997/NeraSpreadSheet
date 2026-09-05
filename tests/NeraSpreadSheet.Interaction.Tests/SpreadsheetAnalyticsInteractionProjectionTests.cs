using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Interaction.Tests;

[TestClass]
public sealed class SpreadsheetAnalyticsInteractionProjectionTests
{
    [TestMethod]
    public void PreviewReplacesOnlySelectedPlacementWithoutMutatingSource()
    {
        var selectedItem = SpreadsheetAnalyticsItemKey.ForChart(Guid.NewGuid());
        var otherItem = SpreadsheetAnalyticsItemKey.ForPivot(Guid.NewGuid());
        var selected = new SpreadsheetAnalyticsPlacement(
            selectedItem,
            new RectD(10d, 20d, 200d, 120d),
            0);
        var other = new SpreadsheetAnalyticsPlacement(
            otherItem,
            new RectD(300d, 40d, 180d, 100d),
            1);
        var preview = new RectD(45d, 55d, 240d, 160d);
        var snapshot = new SpreadsheetAnalyticsInteractionSnapshot(
            selectedItem,
            true,
            SpreadsheetAnalyticsResizeHandle.SouthEast,
            preview,
            7);

        var projected = SpreadsheetAnalyticsInteractionProjection.ApplyPreview(
            [selected, other],
            snapshot);

        Assert.AreEqual(2, projected.Count);
        Assert.AreEqual(preview, projected[0].DocumentBounds);
        Assert.AreEqual(other, projected[1]);
        Assert.AreEqual(new RectD(10d, 20d, 200d, 120d), selected.DocumentBounds);
    }

    [TestMethod]
    public void IdleSnapshotReturnsOriginalPlacementList()
    {
        IReadOnlyList<SpreadsheetAnalyticsPlacement> placements =
        [
            new SpreadsheetAnalyticsPlacement(
                SpreadsheetAnalyticsItemKey.ForChart(Guid.NewGuid()),
                new RectD(10d, 10d, 100d, 80d),
                0),
        ];
        var snapshot = new SpreadsheetAnalyticsInteractionSnapshot(
            null,
            false,
            SpreadsheetAnalyticsResizeHandle.None,
            null,
            0);

        var projected = SpreadsheetAnalyticsInteractionProjection.ApplyPreview(
            placements,
            snapshot);

        Assert.AreSame(placements, projected);
    }
}
