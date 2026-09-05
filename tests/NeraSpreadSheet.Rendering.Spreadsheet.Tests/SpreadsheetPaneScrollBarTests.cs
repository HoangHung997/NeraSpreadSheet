using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class SpreadsheetPaneScrollBarTests
{
    [TestMethod]
    public void FourPaneLayoutCreatesIndependentHorizontalAndVerticalBars()
    {
        var split = CreateFourPaneLayout();
        var theme = new SpreadsheetRenderTheme();
        var scrollBars = SpreadsheetPaneScrollBarLayoutEngine.Compute(
            split,
            CreateStates(split),
            theme);

        Assert.AreEqual(8, scrollBars.Bars.Count);
        Assert.AreEqual(4, scrollBars.Corners.Count);
        Assert.IsTrue(scrollBars.TryGetBar(
            SpreadsheetPaneId.TopLeft,
            SpreadsheetScrollBarOrientation.Horizontal,
            out var topLeftHorizontal));
        Assert.IsTrue(scrollBars.TryGetBar(
            SpreadsheetPaneId.TopRight,
            SpreadsheetScrollBarOrientation.Horizontal,
            out var topRightHorizontal));
        Assert.IsTrue(scrollBars.TryGetBar(
            SpreadsheetPaneId.BottomLeft,
            SpreadsheetScrollBarOrientation.Vertical,
            out var bottomLeftVertical));

        Assert.AreEqual(25.5d, topLeftHorizontal.Offset, 0.001d);
        Assert.AreEqual(500.25d, topRightHorizontal.Offset, 0.001d);
        Assert.IsTrue(
            topRightHorizontal.ThumbBounds.Left > topLeftHorizontal.ThumbBounds.Left,
            "Different pane offsets must produce independent thumb positions.");
        Assert.AreEqual(300.5d, bottomLeftVertical.Offset, 0.001d);
        Assert.IsTrue(split.TryGetPane(
            SpreadsheetPaneId.BottomLeft,
            out var bottomLeft));
        Assert.IsTrue(bottomLeft.Bounds.Contains(bottomLeftVertical.Bounds));
    }

    [TestMethod]
    public void ThumbTravelMapsLinearlyToContinuousPixelOffset()
    {
        var split = CreateFourPaneLayout();
        var scrollBars = SpreadsheetPaneScrollBarLayoutEngine.Compute(
            split,
            CreateStates(split),
            new SpreadsheetRenderTheme());
        Assert.IsTrue(scrollBars.TryGetBar(
            SpreadsheetPaneId.BottomRight,
            SpreadsheetScrollBarOrientation.Vertical,
            out var bar));

        var thumbStart = bar.TrackBounds.Top + (bar.TrackTravel * 0.375d);
        var offset = bar.GetOffsetForThumbStart(thumbStart);

        Assert.AreEqual(bar.MaximumOffset * 0.375d, offset, 0.001d);
        Assert.AreEqual(0d, bar.GetOffsetForThumbStart(double.MinValue), 0.001d);
        Assert.AreEqual(bar.MaximumOffset, bar.GetOffsetForThumbStart(double.MaxValue), 0.001d);
    }

    [TestMethod]
    public void HitTestDistinguishesThumbButtonsAndTrackSides()
    {
        var split = SpreadsheetSplitLayoutEngine.Compute(new SpreadsheetSplitRequest(
            new SizeD(640d, 480d)));
        var states = new[]
        {
            new SpreadsheetPaneScrollBarState(
                SpreadsheetPaneId.TopLeft,
                split.Panes[0].Bounds,
                300d,
                200d,
                2200d,
                1600d),
        };
        var scrollBars = SpreadsheetPaneScrollBarLayoutEngine.Compute(
            split,
            states,
            new SpreadsheetRenderTheme());
        Assert.IsTrue(scrollBars.TryGetBar(
            SpreadsheetPaneId.TopLeft,
            SpreadsheetScrollBarOrientation.Horizontal,
            out var bar));

        Assert.AreEqual(
            SpreadsheetScrollBarPart.DecreaseButton,
            bar.HitTest(Center(bar.DecreaseButtonBounds)));
        Assert.AreEqual(
            SpreadsheetScrollBarPart.Thumb,
            bar.HitTest(Center(bar.ThumbBounds)));
        Assert.AreEqual(
            SpreadsheetScrollBarPart.IncreaseButton,
            bar.HitTest(Center(bar.IncreaseButtonBounds)));

        if (bar.ThumbBounds.Left > bar.TrackBounds.Left)
        {
            Assert.AreEqual(
                SpreadsheetScrollBarPart.TrackBeforeThumb,
                bar.HitTest(new PointD(
                    (bar.TrackBounds.Left + bar.ThumbBounds.Left) / 2d,
                    bar.TrackBounds.Top + (bar.TrackBounds.Height / 2d))));
        }
        if (bar.ThumbBounds.Right < bar.TrackBounds.Right)
        {
            Assert.AreEqual(
                SpreadsheetScrollBarPart.TrackAfterThumb,
                bar.HitTest(new PointD(
                    (bar.ThumbBounds.Right + bar.TrackBounds.Right) / 2d,
                    bar.TrackBounds.Top + (bar.TrackBounds.Height / 2d))));
        }

        Assert.IsTrue(scrollBars.TryHitTest(
            Center(bar.ThumbBounds),
            out var hit));
        Assert.AreEqual(SpreadsheetPaneId.TopLeft, hit.PaneId);
        Assert.AreEqual(
            SpreadsheetScrollBarOrientation.Horizontal,
            hit.Orientation);
        Assert.AreEqual(SpreadsheetScrollBarPart.Thumb, hit.Part);
    }

    [TestMethod]
    public void ButtonsAndTrackUsePixelLineAndViewportPageMovement()
    {
        var split = SpreadsheetSplitLayoutEngine.Compute(new SpreadsheetSplitRequest(
            new SizeD(640d, 480d)));
        var state = new SpreadsheetPaneScrollBarState(
            SpreadsheetPaneId.TopLeft,
            split.Panes[0].Bounds,
            400d,
            0d,
            2200d,
            480d);
        var scrollBars = SpreadsheetPaneScrollBarLayoutEngine.Compute(
            split,
            [state],
            new SpreadsheetRenderTheme());
        Assert.IsTrue(scrollBars.TryGetBar(
            SpreadsheetPaneId.TopLeft,
            SpreadsheetScrollBarOrientation.Horizontal,
            out var bar));

        Assert.AreEqual(352d, bar.GetLineOffset(increase: false, 48d), 0.001d);
        Assert.AreEqual(448d, bar.GetLineOffset(increase: true, 48d), 0.001d);
        Assert.AreEqual(0d, bar.GetPageOffset(increase: false, 0.9d), 0.001d);
        Assert.AreEqual(976d, bar.GetPageOffset(increase: true, 0.9d), 0.001d);
    }

    [TestMethod]
    public void BarsAreHiddenWhenContentFitsOrThemeDisablesThem()
    {
        var split = SpreadsheetSplitLayoutEngine.Compute(new SpreadsheetSplitRequest(
            new SizeD(640d, 480d)));
        var state = new SpreadsheetPaneScrollBarState(
            SpreadsheetPaneId.TopLeft,
            split.Panes[0].Bounds,
            0d,
            0d,
            640d,
            480d);

        var fitted = SpreadsheetPaneScrollBarLayoutEngine.Compute(
            split,
            [state],
            new SpreadsheetRenderTheme());
        var disabled = SpreadsheetPaneScrollBarLayoutEngine.Compute(
            split,
            [state with { ContentWidth = 2200d, ContentHeight = 1600d }],
            new SpreadsheetRenderTheme { ShowSplitPaneScrollBars = false });

        Assert.AreEqual(0, fitted.Bars.Count);
        Assert.AreEqual(0, fitted.Corners.Count);
        Assert.AreSame(SpreadsheetPaneScrollBarSet.Empty, disabled);
    }

    [TestMethod]
    public void ScrollBarComposerKeepsBodyByReferenceAndAddsOverlayCommands()
    {
        var split = SpreadsheetSplitLayoutEngine.Compute(new SpreadsheetSplitRequest(
            new SizeD(640d, 480d)));
        var state = new SpreadsheetPaneScrollBarState(
            SpreadsheetPaneId.TopLeft,
            split.Panes[0].Bounds,
            100d,
            200d,
            2200d,
            1600d);
        var theme = new SpreadsheetRenderTheme();
        var scrollBars = SpreadsheetPaneScrollBarLayoutEngine.Compute(
            split,
            [state],
            theme);
        var bodyBuilder = new DisplayListBuilder();
        bodyBuilder.FillRectangle(
            new RectD(0d, 0d, 640d, 480d),
            ColorRgba.White);
        var body = bodyBuilder.Build();

        var composed = SpreadsheetPaneScrollBarDisplayListComposer.Compose(
            body,
            scrollBars,
            SpreadsheetPaneId.TopLeft,
            theme);

        Assert.AreNotSame(body, composed);
        Assert.IsTrue(composed.Commands.OfType<DrawDisplayListCommand>().Any(
            command => ReferenceEquals(command.DisplayList, body)));
        Assert.IsTrue(composed.Commands.OfType<FillRectangleCommand>().Any(
            command => command.Color == theme.ScrollBarActiveThumb));
    }

    private static SpreadsheetSplitLayout CreateFourPaneLayout() =>
        SpreadsheetSplitLayoutEngine.Compute(new SpreadsheetSplitRequest(
            new SizeD(800d, 600d),
            SplitX: 300d,
            SplitY: 200d,
            SeparatorThickness: 6d,
            MinimumPaneExtent: 64d));

    private static SpreadsheetPaneScrollBarState[] CreateStates(
        SpreadsheetSplitLayout split)
    {
        var offsets = new Dictionary<SpreadsheetPaneId, PointD>
        {
            [SpreadsheetPaneId.TopLeft] = new PointD(25.5d, 40.25d),
            [SpreadsheetPaneId.TopRight] = new PointD(500.25d, 80.5d),
            [SpreadsheetPaneId.BottomLeft] = new PointD(75.75d, 300.5d),
            [SpreadsheetPaneId.BottomRight] = new PointD(650.5d, 420.75d),
        };
        return split.Panes
            .Select(pane => new SpreadsheetPaneScrollBarState(
                pane.PaneId,
                pane.Bounds,
                offsets[pane.PaneId].X,
                offsets[pane.PaneId].Y,
                2400d,
                1800d))
            .ToArray();
    }

    private static PointD Center(RectD bounds) => new(
        bounds.Left + (bounds.Width / 2d),
        bounds.Top + (bounds.Height / 2d));
}
