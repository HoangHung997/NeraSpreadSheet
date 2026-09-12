using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Layout;
using global::Avalonia.Platform.Storage;
using NeraSpreadSheet.Bars.Core;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.OpenXml;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Avalonia.Sample;

/// <summary>Runnable native shell using one workbook/session and the SDK's
/// canonical draft. The sample does not own a separate editor model.</summary>
public sealed partial class FullShellWindow : Window, IDisposable
{
    private static readonly string[] ExcelPatterns = ["*.xlsx", "*.dlda"];
    private static readonly IReadOnlyList<FilePickerFileType> ExcelTypes = Array.AsReadOnly<FilePickerFileType>([new FilePickerFileType("Excel") { Patterns = ExcelPatterns }]);
    private readonly NeraSpreadsheetSplitControl _split = new();
    private readonly CommandRegistry _registry = new();
    private readonly RibbonRuntimeController _runtime;
    private readonly NeraRibbonControl _ribbon;
    private readonly NeraBarPresenter _menu;
    private readonly TextBox _formula = new()
    {
        AcceptsReturn = true,
        MinHeight = 30,
        MaxHeight = 100,
        TextWrapping = global::Avalonia.Media.TextWrapping.NoWrap,
        HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
        VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
    };
    private readonly TextBlock _address = new() { MinWidth = 80, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _status = new() { Margin = new Thickness(6) };
    private readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal, Spacing = 5, Margin = new Thickness(6) };
    private readonly NeraOpenXmlSpreadsheetSessionSerializer _serializer = new();
    private readonly List<Window> _dialogs = [];
    private bool _updating;
    private bool _busy;
    private bool _closed;
    private bool _disposing;
    private int _commandExecutions;
    private SpreadsheetSession? _subscribedSession;
    private Worksheet? _subscribedWorksheet;

    public FullShellWindow()
    {
        Title = "NeraSpreadSheet — Avalonia"; Width = 1280; Height = 800; MinWidth = 740; MinHeight = 480;
        _split.Session = CreateWorkbook(); RegisterCommands();
        _runtime = new RibbonRuntimeController(NeraSpreadsheetRibbonPreset.Create(_registry), _registry);
        _ribbon = new NeraRibbonControl(_runtime);
        Opened += (_, _) => { _fontChoices = CreateSystemFontChoices(); _runtime.Refresh(); };
        _menu = new NeraBarPresenter(new BarRuntimeController(new BarDefinition("main-menu", BarKind.MainMenu,
            [BarItemDefinition.Submenu("Tệp", [BarItemDefinition.Command("Shell.Open"), BarItemDefinition.Command("Shell.Save")], "file"),
             BarItemDefinition.Submenu("Chỉnh sửa", [BarItemDefinition.Command("Edit.Undo"), BarItemDefinition.Command("Edit.Redo"), BarItemDefinition.Command("Edit.Copy"), BarItemDefinition.Command("Edit.Paste")], "edit")]), _registry));
        _ribbon.BindShortcuts(this); _menu.BindShortcuts(this);
        _ribbon.CommandActivationFailed += OnCommandFailure; _menu.CommandActivationFailed += OnCommandFailure;
        _ribbon.CustomizationRequested += OnCustomizationRequested;
        var root = new DockPanel();
        // Menu remains an independently tested SDK consumer, but is opt-in in
        // this Ribbon-first shell rather than duplicating the File surface.
        _menu.NativeControl.IsVisible = false;
        DockPanel.SetDock(_menu.NativeControl, Dock.Top); root.Children.Add(_menu.NativeControl);
        DockPanel.SetDock(_ribbon, Dock.Top); root.Children.Add(_ribbon);
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(6) };
        bar.Children.Add(_address); Grid.SetColumn(_formula, 1); bar.Children.Add(_formula);
        var reference = new Button { Content = "Chọn tham chiếu…", Focusable = false, Margin = new Thickness(6, 0, 0, 0) };
        AutomationProperties.SetAutomationId(reference, "nera-shell-reference-picker");
        reference.Click += (_, _) => ShowFormulaReferencePicker();
        Grid.SetColumn(reference, 2); bar.Children.Add(reference);
        DockPanel.SetDock(bar, Dock.Top); root.Children.Add(bar);
        DockPanel.SetDock(_status, Dock.Bottom); root.Children.Add(_status);
        InstallCompatibilityNotice(root);
        var tabScroll = new ScrollViewer { Content = _tabs, HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        DockPanel.SetDock(tabScroll, Dock.Bottom); root.Children.Add(tabScroll);
        root.Children.Add(_split); Content = root;
        AutomationProperties.SetAutomationId(_formula, "nera-shell-formula-bar"); AutomationProperties.SetName(_formula, "Thanh công thức");
        _formula.GotFocus += OnFormulaFocus; _formula.TextChanged += OnFormulaTextChanged; _formula.PropertyChanged += OnFormulaSelectionChanged;
        _formula.AddHandler(InputElement.KeyDownEvent, OnFormulaKeyDown, RoutingStrategies.Tunnel);
        _split.ActivePaneChanged += OnSelectionChanged; _split.SessionChanged += OnSessionChanged; _split.InteractionFailed += OnInteractionFailure;
        foreach (var pane in Enum.GetValues<SpreadsheetSplitViewPane>())
        {
            var sheet = _split.GetPane(pane); sheet.EditorDraftChanged += OnDraftChanged; sheet.ZoomChanged += OnSelectionChanged;
        }
        Closed += OnClosed; SubscribeSession(); BuildTabs(); RefreshSelection();
    }
    public SpreadsheetSession Session => _split.Session ?? throw new InvalidOperationException("The sample has no workbook.");
    public NeraSpreadsheetSplitControl Spreadsheet => _split;
    public NeraRibbonControl Ribbon => _ribbon;
    private NeraSpreadsheetControl DraftOwner => _split.EditingSpreadsheet ?? _split.ActiveSpreadsheet;

    private void OnFormulaFocus(object? sender, RoutedEventArgs e)
    {
        if (_updating || _busy || _closed) return;
        var editor = DraftOwner; editor.BeginEdit(focusEditor: false); editor.SetFormulaAssistanceAnchor(_formula); RefreshSelection();
    }
    private void OnFormulaTextChanged(object? sender, TextChangedEventArgs e) => PushFormulaDraft();
    private void OnFormulaSelectionChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextBox.SelectionStartProperty || e.Property == TextBox.SelectionEndProperty || e.Property == TextBox.CaretIndexProperty) PushFormulaDraft();
    }
    private void PushFormulaDraft()
    {
        if (_updating || _busy || _closed || !_formula.IsFocused || _split.EditingSpreadsheet is not { } editor) return;
        var text = _formula.Text ?? string.Empty;
        editor.UpdateEditorDraft(text, Math.Clamp(_formula.SelectionStart, 0, text.Length), Math.Clamp(_formula.SelectionEnd, 0, text.Length));
    }
    private void OnFormulaKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || _closed || _busy) return; var editor = DraftOwner;
        if (editor.TryHandleFormulaAssistanceKey(e.Key, e.KeyModifiers)) { e.Handled = true; RefreshSelection(); return; }
        if (e.Key == Key.Enter && (e.KeyModifiers & KeyModifiers.Alt) != 0) return;
        if (e.Key is not (Key.Enter or Key.Escape)) return; e.Handled = true;
        try
        {
            PushFormulaDraft(); if (e.Key == Key.Escape) editor.CancelEditor();
            else if (!editor.CommitEditor()) _status.Text = "Dữ liệu chưa vượt qua kiểm tra hợp lệ.";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException) { _status.Text = exception.Message; }
    }
    private void OnDraftChanged(object? sender, EventArgs e) => RefreshSelection();
    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        if (_closed) return; RefreshSelection(); _runtime.Refresh(); _menu.Runtime.Refresh();
    }
    private void RefreshSelection()
    {
        if (_updating || _closed) return; _updating = true;
        try
        {
            var owner = DraftOwner; var draft = owner.CurrentEditorDraft; var address = draft?.Address ?? Session.Selection.ActiveCell;
            var cell = Session.ActiveWorksheet.GetCell(address); var text = draft?.Text ?? cell.Formula ?? cell.Value.ToString();
            _address.Text = address.ToString(); if (!string.Equals(_formula.Text, text, StringComparison.Ordinal)) _formula.Text = text;
            if (draft is not null && (_formula.SelectionStart != draft.SelectionStart || _formula.SelectionEnd != draft.SelectionEnd))
            { _formula.CaretIndex = draft.CaretIndex; _formula.SelectionStart = draft.SelectionStart; _formula.SelectionEnd = draft.SelectionEnd; }
            _status.Text = $"{Session.ActiveWorksheet.Name} · {_split.State.ActivePane} · {owner.Zoom:P0} · Clipboard hệ điều hành · F2 sửa ô · Tab chọn gợi ý · Esc hủy";
        }
        finally { _updating = false; }
    }
    private void OnSessionChanged(object? sender, EventArgs e) { SubscribeSession(); BuildTabs(); OnSelectionChanged(sender, e); }
    private void OnWorksheetChanged(object? sender, EventArgs e) { SubscribeWorksheet(); BuildTabs(); OnSelectionChanged(sender, e); }
    private void SubscribeSession()
    {
        if (_subscribedSession is not null) { _subscribedSession.Selection.Changed -= OnSelectionChanged; _subscribedSession.ActiveWorksheetChanged -= OnWorksheetChanged; }
        _subscribedSession = _split.Session;
        if (_subscribedSession is not null) { _subscribedSession.Selection.Changed += OnSelectionChanged; _subscribedSession.ActiveWorksheetChanged += OnWorksheetChanged; }
        SubscribeWorksheet();
    }
    private void SubscribeWorksheet()
    {
        if (_subscribedWorksheet is not null) _subscribedWorksheet.CellsChanged -= OnSelectionChanged;
        _subscribedWorksheet = _subscribedSession?.ActiveWorksheet;
        if (_subscribedWorksheet is not null) _subscribedWorksheet.CellsChanged += OnSelectionChanged;
    }
    private void BuildTabs()
    {
        _tabs.Children.Clear();
        foreach (var worksheet in Session.Workbook.Worksheets)
        {
            var button = new Button { Content = worksheet.Name, IsEnabled = !_busy,
                FontWeight = ReferenceEquals(worksheet, Session.ActiveWorksheet) ? global::Avalonia.Media.FontWeight.SemiBold : global::Avalonia.Media.FontWeight.Normal };
            button.Click += (_, _) =>
            {
                if (_busy || _closed) return;
                if (_split.EditingSpreadsheet is { } editor && editor.EditorText.StartsWith('=')) ShowFormulaReferencePicker(worksheet);
                else Session.ActivateWorksheet(worksheet);
            };
            _tabs.Children.Add(button);
        }
    }
    private void OnCommandFailure(object? sender, NeraAvaloniaCommandActivationFailedEventArgs e) => _status.Text = e.Exception.Message;
    private void OnInteractionFailure(object? sender, SpreadsheetInteractionFailedEventArgs e) => _status.Text = e.Exception.Message;
    private void OnCustomizationRequested(object? sender, EventArgs e)
    {
        if (_busy || _closed) return;
        var editor = new NeraRibbonCustomizationControl(_runtime) { IconTheme = _ribbon.IconTheme };
        var window = new Window { Title = "Tùy biến Ribbon và QAT", Width = 1050, Height = 700, Content = editor };
        editor.Cancelled += (_, _) => window.Close(); TrackWindow(window); window.Show(this);
    }
    private void TrackWindow(Window window)
    {
        _dialogs.Add(window); window.Closed += (_, _) => _dialogs.Remove(window);
    }
    private async Task OpenAsync() => await RunIo(async () =>
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Mở bảng tính", FileTypeFilter = ExcelTypes, AllowMultiple = false });
        if (_closed || files.Count == 0) return;
        await using var stream = await files[0].OpenReadAsync();
        await OpenCompatibleStreamAsync(stream, files[0].Name);
    });
    private async Task SaveAsync() => await RunIo(async () =>
    {
        var session = Session;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Lưu bảng tính", SuggestedFileName = "NeraSpreadSheet.xlsx", DefaultExtension = "xlsx", FileTypeChoices = ExcelTypes });
        if (_closed || file is null) return;
        using var staging = new MemoryStream(); await SaveCompatibleStreamAsync(session, staging); if (_closed) return; staging.Position = 0;
        await using var destination = await file.OpenWriteAsync(); if (!destination.CanSeek) throw new NotSupportedException("Thiết bị lưu phải hỗ trợ seek.");
        destination.Position = 0; await staging.CopyToAsync(destination); destination.SetLength(staging.Length); await destination.FlushAsync();
    });
    private async Task RunIo(Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (_busy || _closed) return;
        _busy = true;
        try
        {
            RefreshIoPresentation();
            await operation();
        }
        finally
        {
            _busy = false;
            if (!_closed) RefreshIoPresentation();
        }
    }
    private void RefreshIoPresentation()
    {
        // A newly loaded session builds its sheet tabs while I/O is busy. Both
        // those native buttons and the command snapshots must be re-enabled
        // after success, cancellation or failure, without waiting for a cell edit.
        // Do not rebuild tabs here: that would discard native focus unnecessarily.
        _split.IsEnabled = !_busy;
        _formula.IsEnabled = !_busy;
        _tabs.IsEnabled = !_busy;
        foreach (var button in _tabs.Children.OfType<Button>()) button.IsEnabled = !_busy;
        _runtime.Refresh();
        _menu.Runtime.Refresh();
    }
    private static SpreadsheetSession CreateWorkbook()
    {
        var workbook = new Workbook(); var sheet = workbook.Worksheets[0]; workbook.RenameWorksheet(sheet, "Dự toán"); workbook.AddWorksheet("Ghi chú");
        sheet.Dimensions.SetColumnWidth(0, 220); sheet.SetValue(default, "NeraSpreadSheet — Avalonia Ribbon");
        sheet.SetValue(new CellAddress(2, 0), "Vật tư"); sheet.SetValue(new CellAddress(2, 1), "Khối lượng"); sheet.SetValue(new CellAddress(2, 2), "Đơn giá"); sheet.SetValue(new CellAddress(2, 3), "Thành tiền");
        for (var row = 3; row < 53; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), $"Vật tư {row - 2}"); sheet.SetValue(new CellAddress(row, 1), row - 1);
            sheet.SetValue(new CellAddress(row, 2), 12500d); sheet.SetFormula(new CellAddress(row, 3), $"=B{row + 1}*C{row + 1}");
        }
        var session = new SpreadsheetSession(workbook); session.Recalculate(); return session;
    }
    private void OnClosed(object? sender, EventArgs e) => ReleaseResources();
    private void ReleaseResources()
    {
        if (_closed) return; _closed = true; _smokeTimer?.Stop();
        foreach (var window in _dialogs.ToArray()) window.Close(); _dialogs.Clear();
        if (_subscribedSession is not null) { _subscribedSession.Selection.Changed -= OnSelectionChanged; _subscribedSession.ActiveWorksheetChanged -= OnWorksheetChanged; }
        if (_subscribedWorksheet is not null) _subscribedWorksheet.CellsChanged -= OnSelectionChanged;
        _split.ActivePaneChanged -= OnSelectionChanged; _split.SessionChanged -= OnSessionChanged; _split.InteractionFailed -= OnInteractionFailure;
        foreach (var pane in Enum.GetValues<SpreadsheetSplitViewPane>())
        {
            var sheet = _split.GetPane(pane); sheet.EditorDraftChanged -= OnDraftChanged; sheet.ZoomChanged -= OnSelectionChanged;
        }
        _formula.GotFocus -= OnFormulaFocus; _formula.TextChanged -= OnFormulaTextChanged; _formula.PropertyChanged -= OnFormulaSelectionChanged;
        _formula.RemoveHandler(InputElement.KeyDownEvent, OnFormulaKeyDown);
        _ribbon.CommandActivationFailed -= OnCommandFailure; _menu.CommandActivationFailed -= OnCommandFailure; _ribbon.CustomizationRequested -= OnCustomizationRequested;
        _ribbon.Dispose(); _menu.Dispose(); _split.Dispose();
    }
    public void Dispose()
    {
        VerifyAccess(); if (_disposing || _closed) return; _disposing = true;
        try { Close(); if (IsVisible && !_closed) throw new InvalidOperationException("Window closing was cancelled."); ReleaseResources(); }
        finally { _disposing = false; }
        GC.SuppressFinalize(this);
    }
}
