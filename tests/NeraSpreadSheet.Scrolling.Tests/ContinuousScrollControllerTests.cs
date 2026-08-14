using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Scrolling;

namespace NeraSpreadSheet.Scrolling.Tests;

[TestClass]
public sealed class ContinuousScrollControllerTests
{
    [TestMethod]
    public void AdvanceFrame_Should_PreserveFractionalOffset_When_InputIsPrecision()
    {
        var controller = new ContinuousScrollController();
        controller.QueueDelta(new ScrollDelta(0.25d, 7.5d, ScrollInputKind.Precision));

        var result = controller.AdvanceFrame(
            TimeSpan.FromSeconds(1d / 60d),
            new ScrollBounds(1_000d, 1_000d));

        Assert.AreEqual(0.25d, result.Snapshot.OffsetX, 1e-9);
        Assert.AreEqual(7.5d, result.Snapshot.OffsetY, 1e-9);
    }

    [TestMethod]
    public void AdvanceFrame_Should_AnimateTowardTarget_When_InputIsWheel()
    {
        var controller = new ContinuousScrollController();
        controller.QueueDelta(new ScrollDelta(0d, 100d, ScrollInputKind.Wheel));

        var result = controller.AdvanceFrame(
            TimeSpan.FromSeconds(1d / 60d),
            new ScrollBounds(1_000d, 1_000d));

        Assert.IsTrue(result.Snapshot.OffsetY > 0d);
        Assert.IsTrue(result.Snapshot.OffsetY < 100d);
        Assert.AreEqual(100d, result.Snapshot.TargetY, 1e-9);
    }

    [TestMethod]
    public void AdvanceFrame_Should_ClampCurrentAndTarget_When_BoundsAreSmaller()
    {
        var controller = new ContinuousScrollController();
        controller.ScrollTo(500d, 500d, animated: false);

        var result = controller.AdvanceFrame(TimeSpan.Zero, new ScrollBounds(100d, 200d));

        Assert.AreEqual(100d, result.Snapshot.OffsetX, 1e-9);
        Assert.AreEqual(200d, result.Snapshot.OffsetY, 1e-9);
        Assert.AreEqual(100d, result.Snapshot.TargetX, 1e-9);
        Assert.AreEqual(200d, result.Snapshot.TargetY, 1e-9);
    }
}
