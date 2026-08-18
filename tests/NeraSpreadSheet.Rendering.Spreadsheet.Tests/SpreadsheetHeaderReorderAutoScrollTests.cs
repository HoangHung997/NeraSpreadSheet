using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class SpreadsheetHeaderReorderAutoScrollTests
{
    private static readonly RectD Viewport = new(40d, 24d, 400d, 300d);

    [TestMethod]
    public void RowVelocityUsesOnlyVerticalAxisAndAcceleratesAtEdges()
    {
        var center = SpreadsheetHeaderReorderAutoScroll.CalculateVelocity(
            WorksheetAxis.Row,
            new PointD(20d, 174d),
            Viewport);
        var nearTop = SpreadsheetHeaderReorderAutoScroll.CalculateVelocity(
            WorksheetAxis.Row,
            new PointD(20d, 36d),
            Viewport);
        var aboveTop = SpreadsheetHeaderReorderAutoScroll.CalculateVelocity(
            WorksheetAxis.Row,
            new PointD(20d, 0d),
            Viewport);
        var nearBottom = SpreadsheetHeaderReorderAutoScroll.CalculateVelocity(
            WorksheetAxis.Row,
            new PointD(20d, 312d),
            Viewport);

        Assert.AreEqual(default, center);
        Assert.AreEqual(0d, nearTop.X);
        Assert.IsTrue(nearTop.Y < 0d);
        Assert.IsTrue(aboveTop.Y < nearTop.Y);
        Assert.AreEqual(0d, nearBottom.X);
        Assert.IsTrue(nearBottom.Y > 0d);
    }

    [TestMethod]
    public void ColumnVelocityUsesOnlyHorizontalAxis()
    {
        var nearLeft = SpreadsheetHeaderReorderAutoScroll.CalculateVelocity(
            WorksheetAxis.Column,
            new PointD(44d, 40d),
            Viewport);
        var nearRight = SpreadsheetHeaderReorderAutoScroll.CalculateVelocity(
            WorksheetAxis.Column,
            new PointD(432d, 40d),
            Viewport);

        Assert.IsTrue(nearLeft.X < 0d);
        Assert.AreEqual(0d, nearLeft.Y);
        Assert.IsTrue(nearRight.X > 0d);
        Assert.AreEqual(0d, nearRight.Y);
    }

    [TestMethod]
    public void HostOverloadExcludesHeaderChromeFromScrollViewport()
    {
        var theme = new SpreadsheetRenderTheme
        {
            ShowHeaders = true,
            RowHeaderWidth = 48d,
            ColumnHeaderHeight = 28d,
        };

        var inHeader = SpreadsheetHeaderReorderAutoScroll.CalculateVelocity(
            WorksheetAxis.Column,
            pointerX: 10d,
            pointerY: 14d,
            fullWidth: 640d,
            fullHeight: 480d,
            theme);
        var inBodyCenter = SpreadsheetHeaderReorderAutoScroll.CalculateVelocity(
            WorksheetAxis.Column,
            pointerX: 320d,
            pointerY: 14d,
            fullWidth: 640d,
            fullHeight: 480d,
            theme);

        Assert.IsTrue(inHeader.X < 0d);
        Assert.AreEqual(default, inBodyCenter);
    }

    [TestMethod]
    public void DeltaScalesVelocityByElapsedTime()
    {
        var delta = SpreadsheetHeaderReorderAutoScroll.CalculateDelta(
            new PointD(120d, -240d),
            TimeSpan.FromMilliseconds(250d));

        Assert.AreEqual(30d, delta.X, 0.0001d);
        Assert.AreEqual(-60d, delta.Y, 0.0001d);
    }

    [TestMethod]
    public void InvalidConfigurationIsRejected()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            SpreadsheetHeaderReorderAutoScroll.CalculateVelocity(
                WorksheetAxis.Row,
                default,
                Viewport,
                edgeZone: 0d));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            SpreadsheetHeaderReorderAutoScroll.CalculateVelocity(
                WorksheetAxis.Column,
                default,
                Viewport,
                maximumSpeed: double.NaN));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            SpreadsheetHeaderReorderAutoScroll.CalculateDelta(
                default,
                TimeSpan.FromMilliseconds(-1d)));
    }
}
