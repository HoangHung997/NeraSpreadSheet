using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Rendering.Spreadsheet;

public sealed record SpreadsheetSplitPaneChromeLayout(
    SpreadsheetPaneId PaneId,
    RectD Bounds,
    ViewportLayout ViewportLayout);

public static class SpreadsheetSplitChromeDisplayListComposer
{
    private const double GeometryEpsilon = 1e-9;

    public static DisplayList Compose(
        DisplayList body,
        SpreadsheetSplitLayout splitLayout,
        IReadOnlyList<SpreadsheetSplitPaneChromeLayout> paneLayouts,
        SelectionSnapshot selection,
        SpreadsheetRenderTheme theme)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(splitLayout);
        ArgumentNullException.ThrowIfNull(paneLayouts);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(theme);

        if (!theme.ShowHeaders)
        {
            return body;
        }
        SpreadsheetHeaderDisplayListComposer.ValidateTheme(theme);
        var layoutsByPane = ValidatePaneLayouts(splitLayout, paneLayouts);

        var headerWidth = theme.RowHeaderWidth;
        var headerHeight = theme.ColumnHeaderHeight;
        var bodyWidth = splitLayout.ViewportSize.Width;
        var bodyHeight = splitLayout.ViewportSize.Height;
        var builder = new DisplayListBuilder();

        builder.FillRectangle(
            new RectD(0d, 0d, headerWidth + bodyWidth, headerHeight + bodyHeight),
            theme.Background);
        builder.PushClip(new RectD(headerWidth, headerHeight, bodyWidth, bodyHeight));
        builder.PushTranslation(headerWidth, headerHeight);
        builder.Append(body);
        builder.PopTranslation();
        builder.PopClip();

        SpreadsheetHeaderDisplayListComposer.DrawCorner(
            builder,
            selection,
            theme,
            headerWidth,
            headerHeight);

        foreach (var pane in splitLayout.Panes)
        {
            var chrome = layoutsByPane[pane.PaneId];
            var touchesTop = Math.Abs(pane.Bounds.Top) <= GeometryEpsilon;
            var touchesLeft = Math.Abs(pane.Bounds.Left) <= GeometryEpsilon;
            if (touchesTop)
            {
                SpreadsheetHeaderDisplayListComposer.DrawColumnHeaders(
                    builder,
                    chrome.ViewportLayout,
                    pane.Bounds,
                    selection,
                    theme,
                    headerWidth,
                    headerHeight);
            }
            if (touchesLeft)
            {
                SpreadsheetHeaderDisplayListComposer.DrawRowHeaders(
                    builder,
                    chrome.ViewportLayout,
                    pane.Bounds,
                    selection,
                    theme,
                    headerWidth,
                    headerHeight);
            }

            SpreadsheetHeaderDisplayListComposer.DrawFreezeHeaderSeparators(
                builder,
                chrome.ViewportLayout,
                pane.Bounds,
                theme,
                headerWidth,
                headerHeight,
                drawColumnSeparator: touchesTop,
                drawRowSeparator: touchesLeft);
        }

        SpreadsheetHeaderDisplayListComposer.DrawSplitHeaderSeparators(
            builder,
            splitLayout,
            theme,
            headerWidth,
            headerHeight);
        return builder.Build();
    }

    private static IReadOnlyDictionary<SpreadsheetPaneId, SpreadsheetSplitPaneChromeLayout> ValidatePaneLayouts(
        SpreadsheetSplitLayout splitLayout,
        IReadOnlyList<SpreadsheetSplitPaneChromeLayout> paneLayouts)
    {
        var layoutsByPane = new Dictionary<SpreadsheetPaneId, SpreadsheetSplitPaneChromeLayout>();
        foreach (var paneLayout in paneLayouts)
        {
            ArgumentNullException.ThrowIfNull(paneLayout);
            ArgumentNullException.ThrowIfNull(paneLayout.ViewportLayout);
            if (!layoutsByPane.TryAdd(paneLayout.PaneId, paneLayout))
            {
                throw new ArgumentException(
                    $"Pane '{paneLayout.PaneId}' has more than one chrome layout.",
                    nameof(paneLayouts));
            }
        }

        foreach (var pane in splitLayout.Panes)
        {
            if (!layoutsByPane.TryGetValue(pane.PaneId, out var paneLayout))
            {
                throw new ArgumentException(
                    $"Pane '{pane.PaneId}' does not have a chrome layout.",
                    nameof(paneLayouts));
            }
            if (paneLayout.Bounds != pane.Bounds)
            {
                throw new ArgumentException(
                    $"Pane '{pane.PaneId}' chrome bounds do not match the split layout.",
                    nameof(paneLayouts));
            }
            if (Math.Abs(paneLayout.ViewportLayout.ViewportSize.Width - pane.Bounds.Width) > GeometryEpsilon ||
                Math.Abs(paneLayout.ViewportLayout.ViewportSize.Height - pane.Bounds.Height) > GeometryEpsilon)
            {
                throw new ArgumentException(
                    $"Pane '{pane.PaneId}' viewport size does not match its split bounds.",
                    nameof(paneLayouts));
            }
        }

        if (layoutsByPane.Count != splitLayout.Panes.Count)
        {
            throw new ArgumentException(
                "Chrome layouts contain panes that are not present in the split layout.",
                nameof(paneLayouts));
        }
        return layoutsByPane;
    }
}
