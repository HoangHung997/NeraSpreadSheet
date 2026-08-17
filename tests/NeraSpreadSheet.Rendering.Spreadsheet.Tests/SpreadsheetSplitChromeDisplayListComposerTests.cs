using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class SpreadsheetSplitChromeDisplayListComposerTests
{
    [TestMethod]
    public void HeadersDisabledReturnsOriginalSplitBody()
    {
        var body = CreateBody();
        var splitLayout = CreateDualSplitLayout();

        var result = SpreadsheetSplitChromeDisplayListComposer.Compose(
            body,
            splitLayout,
            CreatePaneLayouts(splitLayout),
            CreateSelection(),
            new SpreadsheetRenderTheme());

        Assert.AreSame(body, result);
    }

    [TestMethod]
    public void DualSplitHeadersUseTheTopAndLeftAdjacentPaneLayouts()
    {
        var body = CreateBody();
        var splitLayout = CreateDualSplitLayout();
        var theme = new SpreadsheetRenderTheme { ShowHeaders = true };

        var result = SpreadsheetSplitChromeDisplayListComposer.Compose(
            body,
            splitLayout,
            CreatePaneLayouts(splitLayout),
            CreateSelection(),
            theme);

        var texts = result.Commands.OfType<DrawTextCommand>().Select(command => command.Text).ToArray();
        CollectionAssert.Contains(texts, "A");
        CollectionAssert.Contains(texts, "B");
        CollectionAssert.Contains(texts, "K");
        CollectionAssert.Contains(texts, "L");
        CollectionAssert.Contains(texts, "1");
        CollectionAssert.Contains(texts, "2");
        CollectionAssert.Contains(texts, "21");
        CollectionAssert.Contains(texts, "22");
    }

    [TestMethod]
    public void SplitSeparatorsContinueThroughTheirHeaderBands()
    {
        var body = CreateBody();
        var splitLayout = CreateDualSplitLayout();
        var theme = new SpreadsheetRenderTheme { ShowHeaders = true };

        var result = SpreadsheetSplitChromeDisplayListComposer.Compose(
            body,
            splitLayout,
            CreatePaneLayouts(splitLayout),
            CreateSelection(),
            theme);

        var separatorFills = result.Commands
            .OfType<FillRectangleCommand>()
            .Where(command => command.Color == theme.SplitPaneSeparator)
            .Select(command => command.Bounds)
            .ToArray();
        CollectionAssert.Contains(
            separatorFills,
            new RectD(
                theme.RowHeaderWidth + splitLayout.VerticalSeparator.X,
                0d,
                splitLayout.VerticalSeparator.Width,
                theme.ColumnHeaderHeight));
        CollectionAssert.Contains(
            separatorFills,
            new RectD(
                0d,
                theme.ColumnHeaderHeight + splitLayout.HorizontalSeparator.Y,
                theme.RowHeaderWidth,
                splitLayout.HorizontalSeparator.Height));
    }

    [TestMethod]
    public void SplitBodyIsTranslatedBySharedHeaderChrome()
    {
        var splitLayout = CreateDualSplitLayout();
        var theme = new SpreadsheetRenderTheme { ShowHeaders = true };

        var result = SpreadsheetSplitChromeDisplayListComposer.Compose(
            CreateBody(),
            splitLayout,
            CreatePaneLayouts(splitLayout),
            CreateSelection(),
            theme);

        Assert.IsTrue(result.Commands.OfType<PushTranslationCommand>().Any(command =>
            Math.Abs(command.DeltaX - theme.RowHeaderWidth) < 1e-9 &&
            Math.Abs(command.DeltaY - theme.ColumnHeaderHeight) < 1e-9));
    }

    [TestMethod]
    public void MissingPaneChromeLayoutIsRejected()
    {
        var splitLayout = CreateDualSplitLayout();
        var paneLayouts = CreatePaneLayouts(splitLayout).Take(3).ToArray();

        Assert.ThrowsExactly<ArgumentException>(() =>
            SpreadsheetSplitChromeDisplayListComposer.Compose(
                CreateBody(),
                splitLayout,
                paneLayouts,
                CreateSelection(),
                new SpreadsheetRenderTheme { ShowHeaders = true }));
    }

    private static DisplayList CreateBody()
    {
        var builder = new DisplayListBuilder();
        builder.FillRectangle(new RectD(0d, 0d, 500d, 320d), ColorRgba.White);
        return builder.Build();
    }

    private static SpreadsheetSplitLayout CreateDualSplitLayout() =>
        SpreadsheetSplitLayoutEngine.Compute(new SpreadsheetSplitRequest(
            new SizeD(500d, 320d),
            SplitX: 200d,
            SplitY: 140d,
            SeparatorThickness: 4d,
            MinimumPaneExtent: 60d));

    private static SpreadsheetSplitPaneChromeLayout[] CreatePaneLayouts(
        SpreadsheetSplitLayout splitLayout) =>
        splitLayout.Panes.Select(pane => pane.PaneId switch
        {
            SpreadsheetPaneId.TopLeft => CreatePaneLayout(pane, 0, 0),
            SpreadsheetPaneId.TopRight => CreatePaneLayout(pane, 0, 10),
            SpreadsheetPaneId.BottomLeft => CreatePaneLayout(pane, 20, 0),
            SpreadsheetPaneId.BottomRight => CreatePaneLayout(pane, 20, 10),
            _ => throw new InvalidOperationException(),
        }).ToArray();

    private static SpreadsheetSplitPaneChromeLayout CreatePaneLayout(
        SpreadsheetPaneLayout pane,
        int firstRow,
        int firstColumn) => new(
            pane.PaneId,
            pane.Bounds,
            new ViewportLayout(
                0d,
                0d,
                new SizeD(pane.Bounds.Width, pane.Bounds.Height),
                1_600d,
                800d,
                0d,
                0d,
                [
                    new AxisSlot(firstRow, 0d, 20d),
                    new AxisSlot(firstRow + 1, 20d, 20d),
                ],
                [
                    new AxisSlot(firstColumn, 0d, 80d),
                    new AxisSlot(firstColumn + 1, 80d, 80d),
                ]));

    private static SelectionSnapshot CreateSelection() => new(
        default,
        default,
        [new CellRange(default, default)],
        0L);
}
