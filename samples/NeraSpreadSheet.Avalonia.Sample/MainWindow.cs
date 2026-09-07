using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Layout;
using global::Avalonia.Platform.Storage;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.OpenXml;

namespace NeraSpreadSheet.Avalonia.Sample;

public sealed partial class MainWindow : Window
{
    private static readonly string[] ExcelPatterns = ["*.xlsx"];
    private static readonly IReadOnlyList<FilePickerFileType> ExcelTypes =
        Array.AsReadOnly<FilePickerFileType>([new FilePickerFileType("Excel") { Patterns = ExcelPatterns }]);
    private readonly NeraSpreadsheetControl _sheet = new() { UseAdaptiveNavigationExtent = true };
    private readonly TextBox _formula = new() { MinWidth = 240 };
    private readonly TextBlock _address = new() { MinWidth = 70, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _status = new();
    private readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal, Spacing = 4 };
    private readonly ScrollBar _horizontal = new() { Orientation = Orientation.Horizontal, SmallChange = 24 };
    private readonly ScrollBar _vertical = new() { Orientation = Orientation.Vertical, SmallChange = 24 };
    private readonly NeraOpenXmlSpreadsheetSessionSerializer _serializer = new();
    private bool _updating;
    private bool _busy;
    private bool _isClosed;

    public MainWindow()
    {
        Title = "NeraSpreadSheet — Avalonia";
        Width = 1180;
        Height = 740;
        MinWidth = 600;
        MinHeight = 400;
        var root = new DockPanel { Margin = new Thickness(8) };
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 0, 0, 6) };
        var open = new Button { Content = "Mở XLSX" };
        var save = new Button { Content = "Lưu XLSX" };
        open.Click += OpenClick;
        save.Click += SaveClick;
        tools.Children.Add(open);
        tools.Children.Add(save);
        tools.Children.Add(Tool("Hoàn tác", () => { _sheet.Session?.Undo(); }));
        tools.Children.Add(Tool("Làm lại", () => { _sheet.Session?.Redo(); }));
        tools.Children.Add(Tool("Đậm", () => { _sheet.Session?.Styles.ToggleBold(); }));
        tools.Children.Add(Tool("Nghiêng", () => { _sheet.Session?.Styles.ToggleItalic(); }));
        tools.Children.Add(Tool("Cố định", () => { _sheet.Session?.View.FreezeAtActiveCell(); }));
        tools.Children.Add(Tool("Bỏ cố định", () => { _sheet.Session?.View.Unfreeze(); }));
        tools.Children.Add(Tool("−", () => _sheet.Zoom = Math.Max(0.25, _sheet.Zoom - 0.1)));
        tools.Children.Add(Tool("+", () => _sheet.Zoom = Math.Min(4, _sheet.Zoom + 0.1)));
        DockPanel.SetDock(tools, Dock.Top);
        root.Children.Add(tools);
        var formulaRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(0, 0, 0, 6) };
        formulaRow.Children.Add(_address);
        Grid.SetColumn(_formula, 1);
        formulaRow.Children.Add(_formula);
        DockPanel.SetDock(formulaRow, Dock.Top);
        root.Children.Add(formulaRow);
        _status.Margin = new Thickness(0, 6, 0, 0);
        DockPanel.SetDock(_status, Dock.Bottom);
        root.Children.Add(_status);
        DockPanel.SetDock(_tabs, Dock.Bottom);
        root.Children.Add(_tabs);
        var viewport = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        };
        viewport.Children.Add(_sheet);
        Grid.SetColumn(_vertical, 1);
        viewport.Children.Add(_vertical);
        Grid.SetRow(_horizontal, 1);
        viewport.Children.Add(_horizontal);
        root.Children.Add(viewport);
        Content = root;
        _sheet.SessionChanged += OnSessionChanged;
        _sheet.SelectionChanged += OnSelectionChanged;
        _sheet.EditorDraftChanged += OnDraftChanged;
        _sheet.ViewportChanged += OnViewportChanged;
        _sheet.ZoomChanged += OnSelectionChanged;
        _sheet.InteractionFailed += OnInteractionFailed;
        _horizontal.ValueChanged += OnScrollChanged;
        _vertical.ValueChanged += OnScrollChanged;
        _formula.GotFocus += OnFormulaFocus;
        _formula.TextChanged += OnFormulaTextChanged;
        _formula.AddHandler(InputElement.KeyDownEvent, OnFormulaKeyDown, RoutingStrategies.Tunnel);
        Closed += OnClosed;
        _sheet.Session = CreateSample();
    }

    private Button Tool(string label, Action action)
    {
        var button = new Button { Content = label, Focusable = false };
        button.Click += (_, _) =>
        {
            if (_busy || _isClosed) return;
            try
            {
                if (_sheet.IsEditing && !_sheet.CommitEditor()) { _status.Text = "Giá trị chưa vượt qua kiểm tra dữ liệu."; return; }
                action();
                UpdateSelection();
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
            { _status.Text = exception.Message; }
        };
        return button;
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        _tabs.Children.Clear();
        if (_sheet.Session is { } session)
            foreach (var worksheet in session.Workbook.Worksheets)
            {
                var tab = new Button { Content = worksheet.Name };
                tab.Click += (_, _) => { if (!_busy && !_isClosed) session.ActivateWorksheet(worksheet); };
                _tabs.Children.Add(tab);
            }
        UpdateSelection();
    }

    private void OnSelectionChanged(object? sender, EventArgs e) => UpdateSelection();
    private void OnDraftChanged(object? sender, EventArgs e) => UpdateSelection();
    private void UpdateSelection()
    {
        if (_sheet.Session is not { } session || _updating || _isClosed) return;
        _updating = true;
        try
        {
            var address = session.Selection.ActiveCell;
            var cell = session.ActiveWorksheet.GetCell(address);
            _address.Text = address.ToString();
            var text = _sheet.IsEditing ? _sheet.EditorText : cell.Formula ?? cell.Value.ToString();
            if (!string.Equals(_formula.Text, text, StringComparison.Ordinal)) _formula.Text = text;
            _status.Text = $"{session.ActiveWorksheet.Name} · {_sheet.Zoom:P0} · Enter: xác nhận · Esc: hủy · Ctrl/Cmd+C/V: clipboard nội bộ Nera";
        }
        finally { _updating = false; }
    }

    private void OnFormulaFocus(object? sender, RoutedEventArgs e)
    {
        if (_updating || _busy || _isClosed) return;
        _sheet.BeginEdit(focusEditor: false);
        UpdateSelection();
    }

    private void OnFormulaTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_updating && !_busy && !_isClosed && _formula.IsFocused && _sheet.IsEditing)
            _sheet.SetEditorText(_formula.Text ?? string.Empty);
    }

    private void OnFormulaKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || _busy || _isClosed || e.Key is not (Key.Enter or Key.Escape)) return;
        e.Handled = true;
        try
        {
            if (e.Key == Key.Escape) _sheet.CancelEditor();
            else if (!_sheet.CommitEditor()) _status.Text = "Giá trị chưa vượt qua kiểm tra dữ liệu.";
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        { _status.Text = exception.Message; }
    }

    private void OnViewportChanged(object? sender, EventArgs e)
    {
        if (_updating || _isClosed) return;
        _updating = true;
        try
        {
            _horizontal.Maximum = Math.Max(0, _sheet.ContentWidth - _sheet.ViewportBodyWidth);
            _vertical.Maximum = Math.Max(0, _sheet.ContentHeight - _sheet.ViewportBodyHeight);
            _horizontal.ViewportSize = _sheet.ViewportBodyWidth;
            _vertical.ViewportSize = _sheet.ViewportBodyHeight;
            _horizontal.LargeChange = _sheet.ViewportBodyWidth;
            _vertical.LargeChange = _sheet.ViewportBodyHeight;
            _horizontal.Value = _sheet.ScrollSnapshot.OffsetX;
            _vertical.Value = _sheet.ScrollSnapshot.OffsetY;
        }
        finally { _updating = false; }
    }

    private void OnScrollChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_updating && !_busy && !_isClosed) _sheet.ScrollTo(_horizontal.Value, _vertical.Value);
    }

    private void OnInteractionFailed(object? sender, SpreadsheetInteractionFailedEventArgs e) => _status.Text = e.Exception.Message;

    private async void OpenClick(object? sender, RoutedEventArgs e) => await RunIo(async () =>
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Mở bảng tính", AllowMultiple = false, FileTypeFilter = ExcelTypes,
        });
        if (_isClosed || files.Count == 0) return;
        await using var stream = await files[0].OpenReadAsync();
        var session = await _serializer.LoadSessionAsync(stream, new OpenXmlImportOptions());
        if (!_isClosed) _sheet.Session = session;
    });

    private async void SaveClick(object? sender, RoutedEventArgs e) => await RunIo(async () =>
    {
        if (_sheet.Session is not { } session) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Lưu bảng tính", SuggestedFileName = "NeraSpreadSheet.xlsx", DefaultExtension = "xlsx",
            FileTypeChoices = ExcelTypes,
        });
        if (_isClosed || file is null) return;
        // Stage package construction before opening the destination for writing.
        using var staging = new MemoryStream();
        await _serializer.SaveSessionAsync(session, staging, new OpenXmlExportOptions());
        if (_isClosed) return;
        staging.Position = 0;
        await using var destination = await file.OpenWriteAsync();
        if (!destination.CanSeek) throw new NotSupportedException("Thiết bị lưu phải hỗ trợ seek.");
        destination.Position = 0;
        await staging.CopyToAsync(destination);
        destination.SetLength(staging.Length);
        await destination.FlushAsync();
        if (!_isClosed) _status.Text = "Đã lưu bảng tính.";
    });

    private async Task RunIo(Func<Task> operation)
    {
        if (_busy || _isClosed) return;
        _busy = true;
        try
        {
            if (_sheet.IsEditing && !_sheet.CommitEditor()) { _status.Text = "Cần xác nhận dữ liệu trước khi mở/lưu."; return; }
            _sheet.IsEnabled = false;
            _formula.IsEnabled = false;
            await operation();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            InvalidOperationException or ArgumentException or NotSupportedException)
        { if (!_isClosed) _status.Text = exception.Message; }
        finally
        {
            _busy = false;
            if (!_isClosed) { _sheet.IsEnabled = true; _formula.IsEnabled = true; }
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _isClosed = true;
        _smokeTimer?.Stop();
        _sheet.SessionChanged -= OnSessionChanged;
        _sheet.SelectionChanged -= OnSelectionChanged;
        _sheet.EditorDraftChanged -= OnDraftChanged;
        _sheet.ViewportChanged -= OnViewportChanged;
        _sheet.ZoomChanged -= OnSelectionChanged;
        _sheet.InteractionFailed -= OnInteractionFailed;
        _sheet.Dispose();
    }

    private static SpreadsheetSession CreateSample()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        workbook.RenameWorksheet(sheet, "Dự toán");
        workbook.AddWorksheet("Ghi chú").SetValue(default, "Avalonia dùng chung workbook và công thức Nera.");
        sheet.Dimensions.SetColumnWidth(0, 220);
        sheet.SetValue(new CellAddress(0, 0), "NeraSpreadSheet — Avalonia");
        sheet.SetValue(new CellAddress(2, 0), "Vật tư");
        sheet.SetValue(new CellAddress(2, 1), "Khối lượng");
        sheet.SetValue(new CellAddress(2, 2), "Đơn giá");
        sheet.SetValue(new CellAddress(2, 3), "Thành tiền");
        for (var row = 3; row < 53; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), $"Vật tư {row - 2}");
            sheet.SetValue(new CellAddress(row, 1), row - 1);
            sheet.SetValue(new CellAddress(row, 2), 12500d);
            sheet.SetFormula(new CellAddress(row, 3), $"=B{row + 1}*C{row + 1}");
        }
        var style = workbook.Styles.Intern(CellStyle.Default with
        {
            Font = CellStyle.Default.Font with { Weight = 700 },
            Fill = new CellFillStyle { IsVisible = true, Color = new ColorRgba(225, 236, 250) },
        });
        for (var column = 0; column < 4; column++) sheet.SetStyle(new CellAddress(2, column), style);
        var session = new SpreadsheetSession(workbook);
        session.Recalculate();
        return session;
    }
}
