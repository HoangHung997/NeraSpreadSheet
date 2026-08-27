using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Interaction.Tests;

[TestClass]
public sealed class SpreadsheetAnalyticsInteractionTests
{
    [TestMethod]
    public void HitTestChoosesTopmostItemAndSelectedResizeHandle()
    {
        var low = Target(
            SpreadsheetAnalyticsItemKey.ForChart(Guid.NewGuid()),
            new RectD(20d, 20d, 200d, 120d),
            zIndex: 1);
        var high = Target(
            SpreadsheetAnalyticsItemKey.ForPivot(Guid.NewGuid()),
            new RectD(60d, 50d, 200d, 120d),
            zIndex: 5);

        var bodyHit = SpreadsheetAnalyticsHitTester.HitTest(
            [low, high],
            new PointD(100d, 90d));
        Assert.IsTrue(bodyHit.HasValue);
        Assert.AreEqual(high.Item, bodyHit.Value.Item);
        Assert.AreEqual(
            SpreadsheetAnalyticsResizeHandle.Move,
            bodyHit.Value.Handle);

        var handleHit = SpreadsheetAnalyticsHitTester.HitTest(
            [low, high],
            new PointD(high.ViewportBounds.Left + 1d, high.ViewportBounds.Top + 1d),
            high.Item);
        Assert.IsTrue(handleHit.HasValue);
        Assert.AreEqual(high.Item, handleHit.Value.Item);
        Assert.AreEqual(
            SpreadsheetAnalyticsResizeHandle.NorthWest,
            handleHit.Value.Handle);
    }

    [TestMethod]
    public void TransformMathMovesAtPixelPrecisionAndClampsDocumentOrigin()
    {
        var start = new RectD(30.5d, 40.25d, 240d, 160d);

        var moved = SpreadsheetAnalyticsTransformMath.Apply(
            start,
            SpreadsheetAnalyticsResizeHandle.Move,
            17.75d,
            -12.5d);
        Assert.AreEqual(new RectD(48.25d, 27.75d, 240d, 160d), moved);

        var clamped = SpreadsheetAnalyticsTransformMath.Apply(
            start,
            SpreadsheetAnalyticsResizeHandle.Move,
            -500d,
            -500d);
        Assert.AreEqual(new RectD(0d, 0d, 240d, 160d), clamped);
    }

    [TestMethod]
    public void ResizeHandlesEnforceMinimumSizeAndOrigin()
    {
        var start = new RectD(20d, 30d, 200d, 120d);

        var northWest = SpreadsheetAnalyticsTransformMath.Apply(
            start,
            SpreadsheetAnalyticsResizeHandle.NorthWest,
            -100d,
            -100d,
            minimumWidth: 96d,
            minimumHeight: 64d);
        Assert.AreEqual(0d, northWest.Left);
        Assert.AreEqual(0d, northWest.Top);
        Assert.AreEqual(start.Right, northWest.Right);
        Assert.AreEqual(start.Bottom, northWest.Bottom);

        var southEast = SpreadsheetAnalyticsTransformMath.Apply(
            start,
            SpreadsheetAnalyticsResizeHandle.SouthEast,
            -1000d,
            -1000d,
            minimumWidth: 96d,
            minimumHeight: 64d);
        Assert.AreEqual(96d, southEast.Width);
        Assert.AreEqual(64d, southEast.Height);
        Assert.AreEqual(start.Left, southEast.Left);
        Assert.AreEqual(start.Top, southEast.Top);
    }

    [TestMethod]
    public void ControllerKeepsPointerMovesAsPreviewUntilSingleCommit()
    {
        var item = SpreadsheetAnalyticsItemKey.ForChart(Guid.NewGuid());
        var target = Target(
            item,
            new RectD(100d, 80d, 300d, 180d),
            zIndex: 2);
        var controller = new SpreadsheetAnalyticsInteractionController();
        var versions = new List<long>();
        controller.Changed += (_, _) => versions.Add(controller.Snapshot.Version);

        Assert.IsTrue(controller.TryBeginTransform(
            new PointD(150d, 120d),
            [target]));
        Assert.AreEqual(item, controller.SelectedItem);
        Assert.AreEqual(target.DocumentBounds, controller.PreviewDocumentBounds);

        Assert.IsTrue(controller.UpdateTransform(new PointD(161.5d, 128.25d)));
        Assert.AreEqual(
            new RectD(111.5d, 88.25d, 300d, 180d),
            controller.PreviewDocumentBounds);
        Assert.IsTrue(controller.UpdateTransform(new PointD(174d, 139d)));

        Assert.IsTrue(controller.TryCompleteTransform(
            new PointD(174d, 139d),
            out var commit));
        Assert.AreEqual(item, commit.Item);
        Assert.AreEqual(target.DocumentBounds, commit.BeforeBounds);
        Assert.AreEqual(new RectD(124d, 99d, 300d, 180d), commit.AfterBounds);
        Assert.IsTrue(commit.HasChanges);
        Assert.IsFalse(controller.IsTransforming);
        Assert.IsNull(controller.PreviewDocumentBounds);
        Assert.IsTrue(versions.Count >= 3);
    }

    [TestMethod]
    public void CancelTransformPreservesSelectionButProducesNoCommitState()
    {
        var item = SpreadsheetAnalyticsItemKey.ForPivot(Guid.NewGuid());
        var target = Target(
            item,
            new RectD(40d, 50d, 220d, 140d),
            zIndex: 0);
        var controller = new SpreadsheetAnalyticsInteractionController();

        Assert.IsTrue(controller.TryBeginTransform(
            new PointD(100d, 100d),
            [target]));
        Assert.IsTrue(controller.UpdateTransform(new PointD(130d, 120d)));
        Assert.IsTrue(controller.CancelTransform());

        Assert.AreEqual(item, controller.SelectedItem);
        Assert.IsFalse(controller.IsTransforming);
        Assert.IsNull(controller.PreviewDocumentBounds);
        Assert.IsFalse(controller.CancelTransform());
    }

    [TestMethod]
    public void EmptyHitClearsExistingAnalyticsSelection()
    {
        var item = SpreadsheetAnalyticsItemKey.ForChart(Guid.NewGuid());
        var controller = new SpreadsheetAnalyticsInteractionController();
        Assert.IsTrue(controller.Select(item));

        Assert.IsFalse(controller.TryBeginTransform(
            new PointD(500d, 500d),
            []));

        Assert.IsNull(controller.SelectedItem);
    }

    private static SpreadsheetAnalyticsInteractionTarget Target(
        SpreadsheetAnalyticsItemKey item,
        RectD bounds,
        int zIndex) =>
        new(
            item,
            bounds,
            bounds,
            new RectD(0d, 0d, 1000d, 800d),
            zIndex);
}
