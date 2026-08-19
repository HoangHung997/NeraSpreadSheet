using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeraSpreadSheet.Maui.Tests;

[TestClass]
public sealed class NeraGpuContextLifecycleTests
{
    [TestMethod]
    public void FirstContextCompletesOneFrame()
    {
        using var lifecycle = new NeraGpuContextLifecycle();
        var context = new object();

        var frame = lifecycle.BeginFrame(context);
        Assert.IsTrue(frame.IsValid);
        Assert.IsTrue(lifecycle.TryCompleteFrame(frame));

        var diagnostics = lifecycle.Diagnostics;
        Assert.AreEqual(1L, diagnostics.ContextGeneration);
        Assert.AreEqual(1L, diagnostics.ContextCreatedCount);
        Assert.AreEqual(0L, diagnostics.ContextLostCount);
        Assert.AreEqual(1L, diagnostics.FramesStarted);
        Assert.AreEqual(1L, diagnostics.FramesCompleted);
        Assert.AreEqual(0L, diagnostics.FramesFailed);
        Assert.AreEqual(0L, diagnostics.FramesAbandoned);
        Assert.IsTrue(diagnostics.HasActiveContext);
        Assert.IsFalse(diagnostics.HasActiveFrame);
    }

    [TestMethod]
    public void ContextReplacementAbandonsOldFrameAndRejectsStaleCompletion()
    {
        using var lifecycle = new NeraGpuContextLifecycle();
        var firstContext = new object();
        var secondContext = new object();
        var firstFrame = lifecycle.BeginFrame(firstContext);

        var secondFrame = lifecycle.BeginFrame(secondContext);

        Assert.IsFalse(lifecycle.TryCompleteFrame(firstFrame));
        Assert.IsTrue(lifecycle.TryCompleteFrame(secondFrame));
        var diagnostics = lifecycle.Diagnostics;
        Assert.AreEqual(2L, diagnostics.ContextGeneration);
        Assert.AreEqual(2L, diagnostics.ContextCreatedCount);
        Assert.AreEqual(1L, diagnostics.ContextLostCount);
        Assert.AreEqual(1L, diagnostics.ContextRecreatedCount);
        Assert.AreEqual(2L, diagnostics.FramesStarted);
        Assert.AreEqual(1L, diagnostics.FramesCompleted);
        Assert.AreEqual(1L, diagnostics.FramesAbandoned);
        Assert.AreEqual(1L, diagnostics.StaleFrameTransitionsRejected);
    }

    [TestMethod]
    public void ContextLossIsIdempotentAndCanBeGuardedByExpectedContext()
    {
        using var lifecycle = new NeraGpuContextLifecycle();
        var context = new object();
        var frame = lifecycle.BeginFrame(context);

        lifecycle.NotifyContextLost(new object());
        Assert.IsTrue(lifecycle.Diagnostics.HasActiveContext);
        Assert.IsTrue(lifecycle.Diagnostics.HasActiveFrame);

        lifecycle.NotifyContextLost(context);
        lifecycle.NotifyContextLost(context);

        Assert.IsFalse(lifecycle.TryAbandonFrame(frame));
        var diagnostics = lifecycle.Diagnostics;
        Assert.AreEqual(1L, diagnostics.ContextLostCount);
        Assert.AreEqual(1L, diagnostics.FramesAbandoned);
        Assert.AreEqual(1L, diagnostics.StaleFrameTransitionsRejected);
        Assert.IsFalse(diagnostics.HasActiveContext);
        Assert.IsFalse(diagnostics.HasActiveFrame);
    }

    [TestMethod]
    public void FailedFrameAllowsTheNextFrameToComplete()
    {
        using var lifecycle = new NeraGpuContextLifecycle();
        var context = new object();
        var failedFrame = lifecycle.BeginFrame(context);

        Assert.IsTrue(lifecycle.TryFailFrame(failedFrame));
        var nextFrame = lifecycle.BeginFrame(context);
        Assert.IsTrue(lifecycle.TryCompleteFrame(nextFrame));

        var diagnostics = lifecycle.Diagnostics;
        Assert.AreEqual(2L, diagnostics.FramesStarted);
        Assert.AreEqual(1L, diagnostics.FramesCompleted);
        Assert.AreEqual(1L, diagnostics.FramesFailed);
        Assert.AreEqual(0L, diagnostics.FramesAbandoned);
    }

    [TestMethod]
    public void DisposeAbandonsActiveFrameAndPreventsNewFrames()
    {
        var lifecycle = new NeraGpuContextLifecycle();
        var frame = lifecycle.BeginFrame(new object());

        lifecycle.Dispose();
        lifecycle.Dispose();

        Assert.IsFalse(lifecycle.TryCompleteFrame(frame));
        Assert.ThrowsExactly<ObjectDisposedException>(() =>
            lifecycle.BeginFrame(new object()));
        var diagnostics = lifecycle.Diagnostics;
        Assert.IsTrue(diagnostics.IsDisposed);
        Assert.IsFalse(diagnostics.HasActiveContext);
        Assert.IsFalse(diagnostics.HasActiveFrame);
        Assert.AreEqual(1L, diagnostics.ContextLostCount);
        Assert.AreEqual(1L, diagnostics.FramesAbandoned);
        Assert.AreEqual(1L, diagnostics.StaleFrameTransitionsRejected);
    }
}
