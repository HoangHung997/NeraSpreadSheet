using System.Windows;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Wpf;

namespace NeraSpreadSheet.Wpf.Sample;

public partial class MainWindow
{
    private bool _scrollBarSampleInitialized;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        if (_scrollBarSampleInitialized)
        {
            return;
        }

        _scrollBarSampleInitialized = true;
        ScrollBarsToggle.IsChecked =
            Spreadsheet.RenderTheme.ShowSplitPaneScrollBars;
        _diagnosticsTimer.Tick += OnScrollBarDiagnosticsTick;
        Closed += OnScrollBarSampleClosed;
        EnsureSampleScrollExtent();
        UpdateDiagnostics();
        AppendScrollBarDiagnostics();
    }

    private void ScrollBarsClick(object sender, RoutedEventArgs e)
    {
        var enabled = ScrollBarsToggle.IsChecked == true;
        Spreadsheet.RenderTheme = Spreadsheet.RenderTheme with
        {
            ShowSplitPaneScrollBars = enabled,
        };
        ScrollBarsToggle.Content = enabled
            ? "Pane Scrollbars ✓"
            : "Pane Scrollbars";

        if (enabled &&
            (_splitController is null ||
             _splitController.IsDisposed ||
             _splitController.Mode == SpreadsheetSplitPaneMode.None))
        {
            SetSplitMode(SpreadsheetSplitPaneMode.Both);
        }
        else
        {
            _splitController?.RenderNow();
            UpdateDiagnostics();
        }

        AppendScrollBarDiagnostics();
    }

    private void OnScrollBarDiagnosticsTick(object? sender, EventArgs e) =>
        AppendScrollBarDiagnostics();

    private void AppendScrollBarDiagnostics()
    {
        var enabled = Spreadsheet.RenderTheme.ShowSplitPaneScrollBars;
        var count = _splitController?.LastFrame?.ScrollBars.Bars.Count ?? 0;
        PerfText.Text += enabled
            ? $" · pane bars {count}"
            : " · pane bars off";
    }

    private void EnsureSampleScrollExtent()
    {
        Spreadsheet.Session?.ActiveWorksheet.SetValue(
            new CellAddress(180, 40),
            "Pane-local scrollbar extent");
    }

    private void OnScrollBarSampleClosed(object? sender, EventArgs e)
    {
        _diagnosticsTimer.Tick -= OnScrollBarDiagnosticsTick;
        Closed -= OnScrollBarSampleClosed;
    }
}
