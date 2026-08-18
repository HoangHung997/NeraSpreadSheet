using Microsoft.Win32;
using System.Windows;
using System.Windows.Threading;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.OpenXml;
using NeraSpreadSheet.Wpf;

namespace NeraSpreadSheet.Wpf.Sample;

public partial class MainWindow : Window
{
    private readonly NeraOpenXmlSpreadsheetSessionSerializer _serializer = new();
    private readonly DispatcherTimer _diagnosticsTimer;
    private NeraSpreadsheetSplitController? _splitController;

    public MainWindow()
    {
        InitializeComponent();
        Spreadsheet.Session = CreateSampleSession();
        _diagnosticsTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(500d),
        };
        _diagnosticsTimer.Tick += OnDiagnosticsTick;
        _diagnosticsTimer.Start();
        Closed += OnClosed;
        UpdateDiagnostics();
    }

    private async void OpenClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Excel workbook (*.xlsx)|*.xlsx|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        await using var stream = File.OpenRead(dialog.FileName);
        var session = await _serializer.LoadSessionAsync(
            stream,
            new OpenXmlImportOptions());
        Spreadsheet.Session = session;
        ApplyLoadedSplitState(session);
        UpdateDiagnostics();
    }

    private async void SaveClick(object sender, RoutedEventArgs e)
    {
        if (Spreadsheet.Session is not { } session)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Excel workbook (*.xlsx)|*.xlsx",
            AddExtension = true,
            DefaultExt = ".xlsx",
            FileName = "NeraSpreadSheet.xlsx",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        await using var stream = File.Create(dialog.FileName);
        await _serializer.SaveSessionAsync(
            session,
            stream,
            new OpenXmlExportOptions());
    }

    private void BoldClick(object sender, RoutedEventArgs e) => Spreadsheet.Session?.Styles.ToggleBold();
    private void ItalicClick(object sender, RoutedEventArgs e) => Spreadsheet.Session?.Styles.ToggleItalic();
    private void MergeClick(object sender, RoutedEventArgs e) => Spreadsheet.Session?.Merge.MergeSelection();
    private void UnmergeClick(object sender, RoutedEventArgs e) => Spreadsheet.Session?.Merge.UnmergeActiveCell();

    private void FreezeClick(object sender, RoutedEventArgs e)
    {
        if (Spreadsheet.Session is not { } session)
        {
            return;
        }
        try
        {
            session.View.FreezeAtActiveCell();
        }
        catch (InvalidOperationException exception)
        {
            MessageBox.Show(this, exception.Message, "Cannot freeze panes", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        UpdateDiagnostics();
    }

    private void UnfreezeClick(object sender, RoutedEventArgs e)
    {
        Spreadsheet.Session?.View.Unfreeze();
        UpdateDiagnostics();
    }

    private void SplitVerticalClick(object sender, RoutedEventArgs e) =>
        SetSplitMode(SpreadsheetSplitPaneMode.Vertical);

    private void SplitHorizontalClick(object sender, RoutedEventArgs e) =>
        SetSplitMode(SpreadsheetSplitPaneMode.Horizontal);

    private void SplitBothClick(object sender, RoutedEventArgs e) =>
        SetSplitMode(SpreadsheetSplitPaneMode.Both);

    private void ClearSplitClick(object sender, RoutedEventArgs e)
    {
        if (_splitController is { IsDisposed: false })
        {
            _splitController.ClearSplit();
        }
        else
        {
            Spreadsheet.Session?.View.ClearSplitState();
        }
        UpdateDiagnostics();
    }

    private void SetSplitMode(SpreadsheetSplitPaneMode mode)
    {
        _splitController = Spreadsheet.EnableSplitPanes(mode);
        _splitController.SetMode(mode);
        UpdateDiagnostics();
    }

    private void ApplyLoadedSplitState(SpreadsheetSession session)
    {
        var mode = session.View.SplitState.Mode switch
        {
            SpreadsheetSplitViewMode.Vertical => SpreadsheetSplitPaneMode.Vertical,
            SpreadsheetSplitViewMode.Horizontal => SpreadsheetSplitPaneMode.Horizontal,
            SpreadsheetSplitViewMode.Both => SpreadsheetSplitPaneMode.Both,
            _ => SpreadsheetSplitPaneMode.None,
        };
        if (mode == SpreadsheetSplitPaneMode.None)
        {
            _splitController?.ClearSplit();
            return;
        }
        _splitController = Spreadsheet.EnableSplitPanes(mode);
        _splitController.SetMode(mode);
    }

    private async void InsertRowsClick(object sender, RoutedEventArgs e) =>
        await ExecuteStructureCommandAsync(SpreadsheetStructureCommandIds.InsertRows);

    private async void DeleteRowsClick(object sender, RoutedEventArgs e) =>
        await ExecuteStructureCommandAsync(SpreadsheetStructureCommandIds.DeleteRows);

    private async void InsertColumnsClick(object sender, RoutedEventArgs e) =>
        await ExecuteStructureCommandAsync(SpreadsheetStructureCommandIds.InsertColumns);

    private async void DeleteColumnsClick(object sender, RoutedEventArgs e) =>
        await ExecuteStructureCommandAsync(SpreadsheetStructureCommandIds.DeleteColumns);

    private async Task ExecuteStructureCommandAsync(CommandId commandId)
    {
        if (Spreadsheet.Session is not { } session)
        {
            return;
        }

        try
        {
            if (!await session.CommandDispatcher.TryExecuteAsync(commandId))
            {
                MessageBox.Show(
                    this,
                    "The structural command is not available for the current selection.",
                    "Command unavailable",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }
            Spreadsheet.Focus();
            UpdateDiagnostics();
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentOutOfRangeException)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "Cannot change worksheet structure",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void GpuClick(object sender, RoutedEventArgs e)
    {
        Spreadsheet.RenderingBackend = GpuToggle.IsChecked == true
            ? WpfRenderingBackend.Direct2DD3DImage
            : WpfRenderingBackend.DrawingContext;
        UpdateDiagnostics();
    }

    private void OnDiagnosticsTick(object? sender, EventArgs e) => UpdateDiagnostics();

    private void UpdateDiagnostics()
    {
        var pacing = Spreadsheet.FramePacing;
        var view = Spreadsheet.Session?.View;
        var freeze = view is { HasFrozenPanes: true }
            ? $" · freeze {view.FrozenRows}r/{view.FrozenColumns}c"
            : string.Empty;
        var split = _splitController is { IsDisposed: false, Mode: not SpreadsheetSplitPaneMode.None }
            ? $" · split {_splitController.Mode}/{_splitController.ActivePane}"
            : string.Empty;
        if (Spreadsheet.GpuDiagnostics is { } gpu)
        {
            PerfText.Text = string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{pacing.FramesPerSecond:F1} FPS · p95 {pacing.P95FrameIntervalMilliseconds:F2} ms · GPU {gpu.TextureWidth}×{gpu.TextureHeight} · layouts {gpu.CachedTextLayouts} · hit {gpu.TextLayoutCacheHits}/{gpu.TextLayoutCacheMisses}{freeze}{split}");
            return;
        }

        PerfText.Text = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{pacing.FramesPerSecond:F1} FPS · p95 {pacing.P95FrameIntervalMilliseconds:F2} ms · DrawingContext{freeze}{split}");
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _diagnosticsTimer.Stop();
        _diagnosticsTimer.Tick -= OnDiagnosticsTick;
        Closed -= OnClosed;
        _splitController?.Dispose();
        _splitController = null;
        Spreadsheet.Dispose();
    }

    private static SpreadsheetSession CreateSampleSession()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(0, 0), "NeraSpreadSheet — native spreadsheet SDK");
        sheet.MergeCells(new CellRange(new CellAddress(0, 0), new CellAddress(0, 4)));
        sheet.Dimensions.SetRowHeight(0, 34d);
        sheet.Dimensions.SetColumnWidth(0, 180d);
        for (var column = 1; column <= 4; column++)
        {
            sheet.Dimensions.SetColumnWidth(column, 120d);
        }

        sheet.SetValue(new CellAddress(2, 0), "Item");
        sheet.SetValue(new CellAddress(2, 1), "Quantity");
        sheet.SetValue(new CellAddress(2, 2), "Unit price");
        sheet.SetValue(new CellAddress(2, 3), "Amount");
        sheet.SetValue(new CellAddress(3, 0), "Material A");
        sheet.SetValue(new CellAddress(3, 1), 12d);
        sheet.SetValue(new CellAddress(3, 2), 25.5d);
        sheet.SetFormula(new CellAddress(3, 3), "=B4*C4");
        sheet.SetValue(new CellAddress(4, 0), "Material B");
        sheet.SetValue(new CellAddress(4, 1), 8d);
        sheet.SetValue(new CellAddress(4, 2), 40d);
        sheet.SetFormula(new CellAddress(4, 3), "=B5*C5");
        sheet.SetValue(new CellAddress(6, 2), "Total");
        sheet.SetFormula(new CellAddress(6, 3), "=SUM(D4:D5)");

        var titleStyleId = workbook.Styles.Intern(CellStyle.Default with
        {
            Font = CellStyle.Default.Font with { Size = 18d, Weight = 700, Color = new ColorRgba(25, 70, 130) },
            Fill = new CellFillStyle { IsVisible = true, Color = new ColorRgba(225, 236, 250) },
        });
        sheet.SetStyle(new CellAddress(0, 0), titleStyleId);

        var headerStyleId = workbook.Styles.Intern(CellStyle.Default with
        {
            Font = CellStyle.Default.Font with { Weight = 700 },
            Fill = new CellFillStyle { IsVisible = true, Color = new ColorRgba(235, 235, 235) },
        });
        for (var column = 0; column <= 3; column++)
        {
            sheet.SetStyle(new CellAddress(2, column), headerStyleId);
        }

        var session = new SpreadsheetSession(workbook);
        session.Recalculate();
        return session;
    }
}
