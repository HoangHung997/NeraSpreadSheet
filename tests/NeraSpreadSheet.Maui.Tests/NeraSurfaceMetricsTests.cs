using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeraSpreadSheet.Maui.Tests;

[TestClass]
public sealed class NeraSurfaceMetricsTests
{
    [TestMethod]
    public void LogicalCanvasReportsPhysicalScaleAndMediumLandscapeClass()
    {
        var metrics = NeraSurfaceMetrics.Create(
            contextGeneration: 2L,
            frameSequence: 7L,
            viewportWidth: 800d,
            viewportHeight: 600d,
            canvasWidth: 800,
            canvasHeight: 600,
            rawPixelWidth: 1200,
            rawPixelHeight: 900,
            ignorePixelScaling: true);

        Assert.IsTrue(metrics.IsAvailable);
        Assert.AreEqual(2L, metrics.ContextGeneration);
        Assert.AreEqual(7L, metrics.FrameSequence);
        Assert.AreEqual(1d, metrics.CanvasUnitsPerViewportUnitX, 1e-9);
        Assert.AreEqual(1d, metrics.CanvasUnitsPerViewportUnitY, 1e-9);
        Assert.AreEqual(1.5d, metrics.RawPixelsPerViewportUnitX, 1e-9);
        Assert.AreEqual(1.5d, metrics.RawPixelsPerViewportUnitY, 1e-9);
        Assert.AreEqual(1.5d, metrics.RawPixelsPerCanvasUnitX, 1e-9);
        Assert.AreEqual(1.5d, metrics.RawPixelsPerCanvasUnitY, 1e-9);
        Assert.AreEqual(NeraSurfaceOrientation.Landscape, metrics.Orientation);
        Assert.AreEqual(NeraSurfaceWidthClass.Medium, metrics.WidthClass);
        Assert.IsTrue(metrics.IsRawPixelScaleUniform());
        Assert.IsTrue(metrics.IsCanvasScaleUniform());
    }

    [TestMethod]
    public void PhysicalCanvasKeepsLogicalPortraitAndCompactClassification()
    {
        var metrics = NeraSurfaceMetrics.Create(
            contextGeneration: 3L,
            frameSequence: 11L,
            viewportWidth: 520d,
            viewportHeight: 760d,
            canvasWidth: 780,
            canvasHeight: 1140,
            rawPixelWidth: 780,
            rawPixelHeight: 1140,
            ignorePixelScaling: false);

        Assert.IsFalse(metrics.IgnorePixelScaling);
        Assert.AreEqual(1.5d, metrics.CanvasUnitsPerViewportUnitX, 1e-9);
        Assert.AreEqual(1.5d, metrics.CanvasUnitsPerViewportUnitY, 1e-9);
        Assert.AreEqual(1.5d, metrics.RawPixelsPerViewportUnitX, 1e-9);
        Assert.AreEqual(1.5d, metrics.RawPixelsPerViewportUnitY, 1e-9);
        Assert.AreEqual(1d, metrics.RawPixelsPerCanvasUnitX, 1e-9);
        Assert.AreEqual(1d, metrics.RawPixelsPerCanvasUnitY, 1e-9);
        Assert.AreEqual(NeraSurfaceOrientation.Portrait, metrics.Orientation);
        Assert.AreEqual(NeraSurfaceWidthClass.Compact, metrics.WidthClass);
    }

    [TestMethod]
    public void ClassifiersUseStableLogicalBoundaries()
    {
        Assert.AreEqual(
            NeraSurfaceWidthClass.Compact,
            NeraSurfaceMetrics.ClassifyWidth(599.999d));
        Assert.AreEqual(
            NeraSurfaceWidthClass.Medium,
            NeraSurfaceMetrics.ClassifyWidth(600d));
        Assert.AreEqual(
            NeraSurfaceWidthClass.Medium,
            NeraSurfaceMetrics.ClassifyWidth(839.999d));
        Assert.AreEqual(
            NeraSurfaceWidthClass.Expanded,
            NeraSurfaceMetrics.ClassifyWidth(840d));
        Assert.AreEqual(
            NeraSurfaceOrientation.Square,
            NeraSurfaceMetrics.ClassifyOrientation(720d, 720.25d));
    }

    [TestMethod]
    public void InvalidDimensionsAndToleranceAreRejected()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            NeraSurfaceMetrics.Create(
                1L,
                1L,
                0d,
                600d,
                800,
                600,
                800,
                600,
                true));

        var metrics = NeraSurfaceMetrics.Create(
            1L,
            1L,
            800d,
            600d,
            800,
            600,
            800,
            600,
            true);

        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => metrics.IsRawPixelScaleUniform(-0.01d));
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => metrics.IsCanvasScaleUniform(double.NaN));
    }
}
