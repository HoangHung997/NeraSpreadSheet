using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.WinForms;

internal static class SpreadsheetSplitViewportEngineExtensions
{
    public static bool SetActivePaneAndReport(
        this SpreadsheetSplitViewportEngine viewport,
        SpreadsheetPaneId paneId)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        var previous = viewport.ActivePane;
        viewport.SetActivePane(paneId);
        return previous != viewport.ActivePane;
    }
}
