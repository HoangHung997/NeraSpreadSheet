using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Rendering;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Viewport;

public static class SpreadsheetSplitViewportFrameChromeExtensions
{
    public static DisplayList ComposeChrome(
        this SpreadsheetSplitViewportFrame frame,
        SelectionSnapshot selection,
        SpreadsheetRenderTheme theme)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(theme);

        var paneLayouts = frame.Panes
            .Select(static pane => new SpreadsheetSplitPaneChromeLayout(
                pane.Pane.PaneId,
                pane.Pane.Bounds,
                pane.ViewportFrame.Layout))
            .ToArray();
        return SpreadsheetSplitChromeDisplayListComposer.Compose(
            frame.DisplayList,
            frame.Layout,
            paneLayouts,
            selection,
            theme);
    }
}
