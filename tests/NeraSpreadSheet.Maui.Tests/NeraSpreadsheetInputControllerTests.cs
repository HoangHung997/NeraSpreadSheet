using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace NeraSpreadSheet.Maui.Tests;

[TestClass]
public sealed class NeraSpreadsheetInputControllerTests
{
    [TestMethod]
    public void TapBelowMovementThresholdSelectsReleasePoint()
    {
        using var harness = new InputHarness();

        harness.Process(1L, SKTouchAction.Pressed, 40f, 50f);
        harness.Process(1L, SKTouchAction.Released, 41f, 51f);

        Assert.IsTrue(harness.SelectedPoint.HasValue);
        Assert.AreEqual(
            new SKPoint(41f, 51f),
            harness.SelectedPoint.Value);
        Assert.AreEqual(0, harness.Diagnostics.ActiveTouchCount);
        Assert.AreEqual(1L, harness.Diagnostics.TapSelections);
        Assert.AreEqual(0L, harness.Diagnostics.PanUpdates);
        Assert.IsFalse(harness.Diagnostics.IsPinching);
        Assert.IsFalse(harness.Diagnostics.IsTapEligible);
    }

    [TestMethod]
    public void PanUsesFractionalOffsetAndSuppressesTap()
    {
        using var harness = new InputHarness
        {
            OffsetX = 10.5d,
            OffsetY = 20.25d,
            Zoom = 2d,
        };

        harness.Process(3L, SKTouchAction.Pressed, 100f, 100f);
        harness.Process(3L, SKTouchAction.Moved, 80f, 70f);
        harness.Process(3L, SKTouchAction.Released, 80f, 70f);

        Assert.AreEqual(20.5d, harness.OffsetX, 1e-9);
        Assert.AreEqual(35.25d, harness.OffsetY, 1e-9);
        Assert.IsNull(harness.SelectedPoint);
        Assert.AreEqual(1L, harness.Diagnostics.PanUpdates);
        Assert.AreEqual(0L, harness.Diagnostics.TapSelections);
    }

    [TestMethod]
    public void PinchPreservesDocumentAnchorAcrossZoom()
    {
        using var harness = new InputHarness
        {
            OffsetX = 10d,
            OffsetY = 20d,
            Zoom = 1d,
            RowHeaderWidth = 40d,
            ColumnHeaderHeight = 20d,
        };

        harness.Process(10L, SKTouchAction.Pressed, 100f, 100f);
        harness.Process(11L, SKTouchAction.Pressed, 200f, 100f);
        harness.Process(11L, SKTouchAction.Moved, 250f, 100f);

        Assert.AreEqual(1.5d, harness.Zoom, 1e-9);
        Assert.AreEqual(43.3333333333333d, harness.OffsetX, 1e-9);
        Assert.AreEqual(53.3333333333333d, harness.OffsetY, 1e-9);
        Assert.AreEqual(1L, harness.Diagnostics.PinchUpdates);
        Assert.IsTrue(harness.Diagnostics.IsPinching);
        Assert.IsFalse(harness.Diagnostics.IsTapEligible);
    }

    [TestMethod]
    public void WheelDeltaIsScaledByCurrentZoom()
    {
        using var harness = new InputHarness
        {
            Zoom = 2d,
            WheelPixelsPerNotch = 96d,
        };

        harness.Process(
            0L,
            SKTouchAction.WheelChanged,
            0f,
            0f,
            wheelDelta: -120);

        Assert.AreEqual(48d, harness.QueuedWheelDelta, 1e-9);
        Assert.AreEqual(1L, harness.Diagnostics.WheelEvents);
        Assert.AreEqual(0, harness.Diagnostics.ActiveTouchCount);
    }

    [TestMethod]
    public void CancellingPinchTransitionsRemainingPointerToPanWithoutTap()
    {
        using var harness = new InputHarness
        {
            OffsetX = 30d,
            OffsetY = 40d,
        };

        harness.Process(20L, SKTouchAction.Pressed, 100f, 100f);
        harness.Process(21L, SKTouchAction.Pressed, 200f, 100f);
        harness.Process(21L, SKTouchAction.Cancelled, 200f, 100f);
        harness.Process(20L, SKTouchAction.Moved, 90f, 80f);
        harness.Process(20L, SKTouchAction.Released, 90f, 80f);

        Assert.AreEqual(40d, harness.OffsetX, 1e-9);
        Assert.AreEqual(60d, harness.OffsetY, 1e-9);
        Assert.IsNull(harness.SelectedPoint);
        Assert.AreEqual(1L, harness.Diagnostics.CancelledEvents);
        Assert.AreEqual(1L, harness.Diagnostics.PanUpdates);
        Assert.AreEqual(0, harness.Diagnostics.ActiveTouchCount);
        Assert.IsFalse(harness.Diagnostics.IsPinching);
    }

    [TestMethod]
    public void ReleasingOneOfThreePointersRebasesRemainingPinch()
    {
        using var harness = new InputHarness
        {
            RowHeaderWidth = 40d,
            ColumnHeaderHeight = 20d,
        };

        harness.Process(40L, SKTouchAction.Pressed, 100f, 100f);
        harness.Process(41L, SKTouchAction.Pressed, 200f, 100f);
        harness.Process(42L, SKTouchAction.Pressed, 300f, 100f);
        harness.Process(40L, SKTouchAction.Released, 100f, 100f);
        harness.Process(42L, SKTouchAction.Moved, 350f, 100f);

        Assert.AreEqual(1.5d, harness.Zoom, 1e-9);
        Assert.AreEqual(66.6666666666667d, harness.OffsetX, 1e-9);
        Assert.AreEqual(33.3333333333333d, harness.OffsetY, 1e-9);
        Assert.AreEqual(2, harness.Diagnostics.ActiveTouchCount);
        Assert.IsTrue(harness.Diagnostics.IsPinching);
    }

    [TestMethod]
    public void CancelAllClearsGestureStateAndDisposeRejectsInput()
    {
        using var harness = new InputHarness();
        harness.Process(30L, SKTouchAction.Pressed, 10f, 10f);

        harness.Controller.CancelAll();

        Assert.AreEqual(0, harness.Diagnostics.ActiveTouchCount);
        Assert.AreEqual(1L, harness.Diagnostics.GestureResetCount);
        Assert.IsFalse(harness.Diagnostics.IsTapEligible);

        harness.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() =>
            harness.Process(31L, SKTouchAction.Pressed, 20f, 20f));
        Assert.IsTrue(harness.Diagnostics.IsDisposed);
    }

    private sealed class InputHarness : IDisposable
    {
        public InputHarness()
        {
            Controller = new NeraSpreadsheetInputController(
                () => new NeraSpreadsheetInputState(
                    Zoom,
                    OffsetX,
                    OffsetY),
                _ => new NeraSpreadsheetInputChrome(
                    RowHeaderWidth,
                    ColumnHeaderHeight),
                () => WheelPixelsPerNotch,
                (offsetX, offsetY) =>
                {
                    OffsetX = offsetX;
                    OffsetY = offsetY;
                },
                (zoom, offsetX, offsetY) =>
                {
                    Zoom = zoom;
                    OffsetX = offsetX;
                    OffsetY = offsetY;
                },
                delta => QueuedWheelDelta = delta,
                point => SelectedPoint = point,
                NeraSpreadsheetView.MinimumZoom,
                NeraSpreadsheetView.MaximumZoom);
        }

        public NeraSpreadsheetInputController Controller { get; }

        public double Zoom { get; set; } = 1d;

        public double OffsetX { get; set; }

        public double OffsetY { get; set; }

        public double RowHeaderWidth { get; set; } = 40d;

        public double ColumnHeaderHeight { get; set; } = 20d;

        public double WheelPixelsPerNotch { get; set; } = 96d;

        public double QueuedWheelDelta { get; private set; }

        public SKPoint? SelectedPoint { get; private set; }

        public NeraSpreadsheetInputDiagnostics Diagnostics =>
            Controller.Diagnostics;

        public void Process(
            long id,
            SKTouchAction action,
            float x,
            float y,
            int wheelDelta = 0)
        {
            Controller.Process(new SKTouchEventArgs(
                id,
                action,
                SKMouseButton.Left,
                SKTouchDeviceType.Touch,
                new SKPoint(x, y),
                action is SKTouchAction.Pressed or SKTouchAction.Moved,
                wheelDelta));
        }

        public void Dispose()
        {
            Controller.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
