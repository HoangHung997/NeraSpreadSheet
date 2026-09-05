using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Wpf.Sample;

public sealed partial class RibbonPreviewWindow
{
    private NeraSpreadsheetSplitController? _splitShell;
    private Worksheet? _splitShellWorksheet;
    private SpreadsheetRenderTheme? _splitShellTheme;

    private void OnNavigationSplitChanged(object? sender, SpreadsheetSplitViewChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Worksheet, _session.ActiveWorksheet)) return;
        // Pane scrolling is handled by the split host itself. Only a host transition
        // needs shell work; do not schedule extra layout for each pane scroll frame.
        if (e.State.HasSplitPanes != (_splitShell is not null))
        {
            CancelNavigationInput();
            QueueNavigationRefresh();
        }
    }

    private void SynchronizeSplitShell()
    {
        var state = _session.View.SplitState;
        var worksheetChanged = !ReferenceEquals(_splitShellWorksheet, _session.ActiveWorksheet);
        _splitShellWorksheet = _session.ActiveWorksheet;
        if (state.HasSplitPanes)
        {
            if (_splitShell is not null)
            {
                if (!ReferenceEquals(_splitShellTheme, _sheet.RenderTheme))
                {
                    _splitShellTheme = _sheet.RenderTheme;
                    _splitShell.RenderTheme = _splitShellTheme;
                }
                return;
            }
            CancelNavigationInput();
            // Enable synchronizes the stored state before applying this same mode.
            // No default topology, synthetic cell or replayed scroll commands.
            _splitShell = _sheet.EnableSplitPanes(state.Mode switch
            {
                SpreadsheetSplitViewMode.Vertical => SpreadsheetSplitPaneMode.Vertical,
                SpreadsheetSplitViewMode.Horizontal => SpreadsheetSplitPaneMode.Horizontal,
                SpreadsheetSplitViewMode.Both => SpreadsheetSplitPaneMode.Both,
                _ => throw new InvalidOperationException("A split shell requires a split topology."),
            });
            _splitShellTheme = _sheet.RenderTheme;
            _sheet.IsHitTestVisible = false;
            _sheet.Focusable = false;
            if (IsKeyboardFocusWithin) _splitShell.Focus();
            return;
        }

        var wasSplit = _splitShell is not null;
        if (wasSplit)
        {
            _sheet.DisableSplitPanes();
            _splitShell = null;
            _sheet.IsHitTestVisible = true;
            _sheet.Focusable = true;
        }
        if (wasSplit || worksheetChanged)
        {
            // A stored unsplit TopLeft offset is still view state. The standalone
            // host owns subsequent scrolling; opening a shell never writes it back.
            _sheet.ScrollTo(state.TopLeftScroll.OffsetX, state.TopLeftScroll.OffsetY);
            if (wasSplit && IsKeyboardFocusWithin) _sheet.Focus();
        }
    }
}
