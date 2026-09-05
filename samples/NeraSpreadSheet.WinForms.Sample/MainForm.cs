using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Foundation.Performance;
using NeraSpreadSheet.OpenXml;
using NeraSpreadsheetControl = NeraSpreadSheet.WinForms.NeraSpreadsheetControl;
using NeraSpreadsheetSplitController = NeraSpreadSheet.WinForms.NeraSpreadsheetSplitController;
using NeraSpreadsheetSplitExtensions = NeraSpreadSheet.WinForms.NeraSpreadsheetSplitExtensions;
using SpreadsheetSplitPaneMode = NeraSpreadSheet.WinForms.SpreadsheetSplitPaneMode;
using WinFormsRenderingBackend = NeraSpreadSheet.WinForms.WinFormsRenderingBackend;

namespace NeraSpreadSheet.WinForms.Sample;

public sealed class MainForm : Form
{
    private readonly NeraOpenXmlSpreadsheetSessionSerializer _serializer = new();
    private readonly NeraSpreadsheetControl _spreadsheet = new() { Dock = DockStyle.Fill };
    private readonly ToolStripDropDownButton _rendererButton = new("Renderer: GDI+");
    private readonly ToolStripButton _scrollBarsButton = new("Pane Scrollbars")
    {
        CheckOnClick = true,
        Checked = true,
        DisplayStyle = ToolStripItemDisplayStyle.Text,
        ToolTipText = "Bật hoặc tắt thanh cuộn riêng cho từng khung. Khi bật mà chưa chia khung, mẫu sẽ tự chuyển sang Split 4.",
    };
    private readonly ToolStripLabel _rendererStatus = new("GDI+ fallback");
    private readonly FramePacingMonitor _framePacing = new();
    private readonly System.Windows.Forms.Timer _diagnosticsTimer;
    private NeraSpreadsheetSplitController? _splitController;

    public MainForm()
    {
        Text = "NeraSpreadSheet WinForms Sample";
        Width = 1200;
        Height = 760;
        StartPosition = FormStartPosition.CenterScreen;

        var toolbar = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
        };
        toolbar.Items.Add(CreateButton("Open XLSX", OpenClick));
        toolbar.Items.Add(CreateButton("Save XLSX", SaveClick));
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(CreateButton("Bold", BoldClick));
        toolbar.Items.Add(CreateButton("Italic", ItalicClick));
        toolbar.Items.Add(CreateButton("Merge", MergeClick));
        toolbar.Items.Add(CreateButton("Unmerge", UnmergeClick));
        toolbar.Items.Add(CreateButton("Freeze", FreezeClick));
        toolbar.Items.Add(CreateButton("Unfreeze", UnfreezeClick));
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(CreateButton("Split V", SplitVerticalClick));
        toolbar.Items.Add(CreateButton("Split H", SplitHorizontalClick));
        toolbar.Items.Add(CreateButton("Split 4", SplitBothClick));
        toolbar.Items.Add(CreateButton("Clear Split", ClearSplitClick));
        toolbar.Items.Add(_scrollBarsButton);
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(CreateButton("Insert Row", InsertRowsClick));
        toolbar.Items.Add(CreateButton("Delete Row", DeleteRowsClick));
        toolbar.Items.Add(CreateButton("Insert Col", InsertColumnsClick));
        toolbar.Items.Add(CreateButton("Delete Col", DeleteColumnsClick));
        toolbar.Items.Add(CreateButton("Ẩn hàng", HideRowsClick));
        toolbar.Items.Add(CreateButton("Hiện hàng", UnhideRowsClick));
        toolbar.Items.Add(CreateButton("Ẩn cột", HideColumnsClick));
        toolbar.Items.Add(CreateButton("Hiện cột", UnhideColumnsClick));
        toolbar.Items.Add(new ToolStripSeparator());
        ConfigureRendererMenu();
        toolbar.Items.Add(_rendererButton);
        toolbar.Items.Add(_rendererStatus);
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(new ToolStripLabel(
            "Headers: click/select · drag-resize · structural commands follow whole-axis selection"));

        Controls.Add(_spreadsheet);
        Controls.Add(toolbar);
        _spreadsheet.Session = CreateSampleSession();
        _spreadsheet.Paint += OnSpreadsheetPaint;
        _scrollBarsButton.CheckedChanged += PaneScrollBarsCheckedChanged;
        _diagnosticsTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _diagnosticsTimer.Tick += OnDiagnosticsTick;
        _diagnosticsTimer.Start();
        FormClosed += OnFormClosed;
        ApplyScrollBarVisibility();
        UpdateRendererStatus();
    }

    private void ConfigureRendererMenu()
    {
        _rendererButton.ToolTipText =
            "Chọn backend render để so sánh GDI+, Direct2D HWND và D3D11/DXGI flip-model.";
        _rendererButton.DropDownItems.Add(CreateRendererItem(
            "GDI+",
            WinFormsRenderingBackend.GdiPlus));
        _rendererButton.DropDownItems.Add(CreateRendererItem(
            "Direct2D HWND",
            WinFormsRenderingBackend.Direct2D));
        _rendererButton.DropDownItems.Add(CreateRendererItem(
            "D3D11/DXGI Flip",
            WinFormsRenderingBackend.Direct2DSwapChain));
    }

    private ToolStripMenuItem CreateRendererItem(
        string caption,
        WinFormsRenderingBackend backend)
    {
        var item = new ToolStripMenuItem(caption);
        item.Click += (_, _) => SelectRenderer(caption, backend);
        return item;
    }

    private void SelectRenderer(
        string caption,
        WinFormsRenderingBackend backend)
    {
        try
        {
            _spreadsheet.RenderingBackend = backend;
            _framePacing.Reset();
            _rendererButton.Text = $"Renderer: {caption}";
            _splitController?.RenderNow();
            UpdateRendererStatus();
        }
        catch (Exception exception) when (
            exception is PlatformNotSupportedException or InvalidOperationException)
        {
            _spreadsheet.RenderingBackend = WinFormsRenderingBackend.GdiPlus;
            _rendererButton.Text = "Renderer: GDI+";
            _rendererStatus.Text =
                $"GPU backend unavailable: {exception.Message}";
        }
    }

    private void PaneScrollBarsCheckedChanged(object? sender, EventArgs e)
    {
        ApplyScrollBarVisibility();
        if (_scrollBarsButton.Checked &&
            (_splitController is null ||
             _splitController.IsDisposed ||
             _splitController.Mode == SpreadsheetSplitPaneMode.None))
        {
            SetSplitMode(SpreadsheetSplitPaneMode.Both);
            return;
        }

        _splitController?.RenderNow();
        UpdateRendererStatus();
    }

    private void ApplyScrollBarVisibility()
    {
        _spreadsheet.RenderTheme = _spreadsheet.RenderTheme with
        {
            ShowSplitPaneScrollBars = _scrollBarsButton.Checked,
        };
        _scrollBarsButton.Text = _scrollBarsButton.Checked
            ? "Pane Scrollbars ✓"
            : "Pane Scrollbars";
    }

    private void OnSpreadsheetPaint(object? sender, PaintEventArgs e) =>
        _framePacing.RecordFrame();

    private void OnDiagnosticsTick(object? sender, EventArgs e) =>
        UpdateRendererStatus();

    private void UpdateRendererStatus()
    {
        var pacing = _framePacing.Capture();
        var view = _spreadsheet.Session?.View;
        var freeze = view is { HasFrozenPanes: true }
            ? $" · freeze {view.FrozenRows}r/{view.FrozenColumns}c"
            : string.Empty;
        var split = _splitController is
            {
                IsDisposed: false,
                Mode: not SpreadsheetSplitPaneMode.None,
            }
            ? $" · split {_splitController.Mode}/{_splitController.ActivePane}"
            : string.Empty;
        var scrollBars = _scrollBarsButton.Checked
            ? $" · pane bars {_splitController?.LastFrame?.ScrollBars.Bars.Count ?? 0}"
            : " · pane bars off";
        var prefix = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{pacing.FramesPerSecond:F1} FPS · p95 {pacing.P95FrameIntervalMilliseconds:F2} ms · ");
        if (_spreadsheet.SwapChainDiagnostics is { } swapChain)
        {
            _rendererStatus.Text =
                $"{prefix}{swapChain.AdapterName} · {swapChain.DeviceFeatureLevel} · VSync={swapChain.VSync} · layouts {swapChain.CachedTextLayouts}{freeze}{split}{scrollBars}";
            return;
        }
        if (_spreadsheet.Direct2DDiagnostics is { } direct2D)
        {
            _rendererStatus.Text =
                $"{prefix}Direct2D HWND · layouts {direct2D.CachedTextLayouts}/{direct2D.TextLayoutCacheCapacity}{freeze}{split}{scrollBars}";
            return;
        }
        _rendererStatus.Text =
            $"{prefix}GDI+ fallback{freeze}{split}{scrollBars}";
    }

    private void OnFormClosed(object? sender, FormClosedEventArgs e)
    {
        _diagnosticsTimer.Stop();
        _diagnosticsTimer.Tick -= OnDiagnosticsTick;
        _diagnosticsTimer.Dispose();
        _scrollBarsButton.CheckedChanged -= PaneScrollBarsCheckedChanged;
        _splitController?.Dispose();
        _splitController = null;
        _spreadsheet.Paint -= OnSpreadsheetPaint;
        FormClosed -= OnFormClosed;
    }

    private static ToolStripButton CreateButton(
        string text,
        EventHandler handler)
    {
        var button = new ToolStripButton(text)
        {
            DisplayStyle = ToolStripItemDisplayStyle.Text,
        };
        button.Click += handler;
        return button;
    }

    private async void OpenClick(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Excel workbook (*.xlsx)|*.xlsx|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await using var stream = File.OpenRead(dialog.FileName);
        var session = await _serializer.LoadSessionAsync(
            stream,
            new OpenXmlImportOptions());
        _spreadsheet.Session = session;
        ApplyLoadedSplitState(session);
        UpdateRendererStatus();
    }

    private async void SaveClick(object? sender, EventArgs e)
    {
        if (_spreadsheet.Session is not { } session)
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "Excel workbook (*.xlsx)|*.xlsx",
            AddExtension = true,
            DefaultExt = "xlsx",
            FileName = "NeraSpreadSheet.xlsx",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await using var stream = File.Create(dialog.FileName);
        await _serializer.SaveSessionAsync(
            session,
            stream,
            new OpenXmlExportOptions());
    }

    private void BoldClick(object? sender, EventArgs e) =>
        _spreadsheet.Session?.Styles.ToggleBold();

    private void ItalicClick(object? sender, EventArgs e) =>
        _spreadsheet.Session?.Styles.ToggleItalic();

    private void MergeClick(object? sender, EventArgs e) =>
        _spreadsheet.Session?.Merge.MergeSelection();

    private void UnmergeClick(object? sender, EventArgs e) =>
        _spreadsheet.Session?.Merge.UnmergeActiveCell();

    private void FreezeClick(object? sender, EventArgs e)
    {
        if (_spreadsheet.Session is not { } session)
        {
            return;
        }
        try
        {
            session.View.FreezeAtActiveCell();
        }
        catch (InvalidOperationException exception)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "Cannot freeze panes",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        UpdateRendererStatus();
    }

    private void UnfreezeClick(object? sender, EventArgs e)
    {
        _spreadsheet.Session?.View.Unfreeze();
        UpdateRendererStatus();
    }

    private void SplitVerticalClick(object? sender, EventArgs e) =>
        SetSplitMode(SpreadsheetSplitPaneMode.Vertical);

    private void SplitHorizontalClick(object? sender, EventArgs e) =>
        SetSplitMode(SpreadsheetSplitPaneMode.Horizontal);

    private void SplitBothClick(object? sender, EventArgs e) =>
        SetSplitMode(SpreadsheetSplitPaneMode.Both);

    private void ClearSplitClick(object? sender, EventArgs e)
    {
        if (_splitController is { IsDisposed: false })
        {
            _splitController.ClearSplit();
            _splitController.RenderNow();
        }
        else
        {
            _spreadsheet.Session?.View.ClearSplitPanes();
        }
        UpdateRendererStatus();
    }

    private void SetSplitMode(SpreadsheetSplitPaneMode mode)
    {
        _splitController = NeraSpreadsheetSplitExtensions.EnableSplitPanes(
            _spreadsheet,
            mode);
        _splitController.SetMode(mode);
        _splitController.RenderNow();
        UpdateRendererStatus();
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
            _splitController?.RenderNow();
            return;
        }
        _splitController = NeraSpreadsheetSplitExtensions.EnableSplitPanes(
            _spreadsheet,
            mode);
        _splitController.SetMode(mode);
        _splitController.RenderNow();
    }

    private async void InsertRowsClick(object? sender, EventArgs e) =>
        await ExecuteStructureCommandAsync(
            SpreadsheetStructureCommandIds.InsertRows);

    private async void DeleteRowsClick(object? sender, EventArgs e) =>
        await ExecuteStructureCommandAsync(
            SpreadsheetStructureCommandIds.DeleteRows);

    private async void InsertColumnsClick(object? sender, EventArgs e) =>
        await ExecuteStructureCommandAsync(
            SpreadsheetStructureCommandIds.InsertColumns);

    private async void DeleteColumnsClick(object? sender, EventArgs e) =>
        await ExecuteStructureCommandAsync(
            SpreadsheetStructureCommandIds.DeleteColumns);

    private async void HideRowsClick(object? sender, EventArgs e) =>
        await ExecuteStructureCommandAsync(
            SpreadsheetStructureCommandIds.HideRows);

    private async void UnhideRowsClick(object? sender, EventArgs e) =>
        await ExecuteStructureCommandAsync(
            SpreadsheetStructureCommandIds.UnhideRows);

    private async void HideColumnsClick(object? sender, EventArgs e) =>
        await ExecuteStructureCommandAsync(
            SpreadsheetStructureCommandIds.HideColumns);

    private async void UnhideColumnsClick(object? sender, EventArgs e) =>
        await ExecuteStructureCommandAsync(
            SpreadsheetStructureCommandIds.UnhideColumns);

    private async Task ExecuteStructureCommandAsync(CommandId commandId)
    {
        if (_spreadsheet.Session is not { } session)
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
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }
            _spreadsheet.Focus();
            _splitController?.RenderNow();
            UpdateRendererStatus();
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentOutOfRangeException)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "Cannot change worksheet structure",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    private static SpreadsheetSession CreateSampleSession()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(
            new CellAddress(0, 0),
            "NeraSpreadSheet — native spreadsheet SDK");
        sheet.MergeCells(new CellRange(
            new CellAddress(0, 0),
            new CellAddress(0, 4)));
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
        sheet.SetValue(
            new CellAddress(180, 40),
            "Pane-local scrollbar extent");

        var titleStyleId = workbook.Styles.Intern(CellStyle.Default with
        {
            Font = CellStyle.Default.Font with
            {
                Size = 18d,
                Weight = 700,
                Color = new ColorRgba(25, 70, 130),
            },
            Fill = new CellFillStyle
            {
                IsVisible = true,
                Color = new ColorRgba(225, 236, 250),
            },
        });
        sheet.SetStyle(new CellAddress(0, 0), titleStyleId);

        var headerStyleId = workbook.Styles.Intern(CellStyle.Default with
        {
            Font = CellStyle.Default.Font with { Weight = 700 },
            Fill = new CellFillStyle
            {
                IsVisible = true,
                Color = new ColorRgba(235, 235, 235),
            },
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
