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
using NeraSpreadSheet.Iconography;
using NeraSpreadSheet.OpenXml;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Avalonia.Sample;

/// <summary>Runnable native shell using one workbook/session and the SDK's
/// existing canonical draft. The sample does not own a separate editor model.</summary>
public sealed partial class FullShellWindow : Window, IDisposable
{
    private static readonly string[] ExcelPatterns = ["*.xlsx"];
    private static readonly IReadOnlyList<FilePickerFileType> ExcelTypes = Array.AsReadOnly<FilePickerFileType>([new FilePickerFileType("Excel") { Patterns = ExcelPatterns }]);
    private readonly NeraSpreadsheetSplitControl _split = new();
    private readonly CommandRegistry _registry = new();
    private readonly RibbonRuntimeController _runtime;
    private readonly NeraRibbonControl _ribbon;
    private readonly NeraBarPresenter _menu;
    private readonly TextBox _formula = new() { AcceptsReturn = true, MinHeight = 30, MaxHeight = 100 };
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
        Title = "NeraSpreadSheet — Avalonia Ribbon"; Width = 1280; Height = 800; MinWidth = 740; MinHeight = 480;
        _split.Session = CreateWorkbook(); RegisterCommands();
        _runtime = new RibbonRuntimeController(CreateDefinition(), _registry); _ribbon = new NeraRibbonControl(_runtime);
        _menu = new NeraBarPresenter(new BarRuntimeController(new BarDefinition("main-menu", BarKind.MainMenu,
            [BarItemDefinition.Submenu("Tệp", [BarItemDefinition.Command("Shell.Open"), BarItemDefinition.Command("Shell.Save")], "file"),
             BarItemDefinition.Submenu("Chỉnh sửa", [BarItemDefinition.Command("Edit.Undo"), BarItemDefinition.Command("Edit.Redo"), BarItemDefinition.Command("Edit.Copy"), BarItemDefinition.Command("Edit.Paste")], "edit")]), _registry));
        _ribbon.BindShortcuts(this); _menu.BindShortcuts(this);
        _ribbon.CommandActivationFailed += OnCommandFailure; _menu.CommandActivationFailed += OnCommandFailure; _ribbon.CustomizationRequested += OnCustomizationRequested;
        var root = new DockPanel(); DockPanel.SetDock(_menu.NativeControl, Dock.Top); root.Children.Add(_menu.NativeControl);
        DockPanel.SetDock(_ribbon, Dock.Top); root.Children.Add(_ribbon);
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(6) };
        bar.Children.Add(_address); Grid.SetColumn(_formula, 1); bar.Children.Add(_formula); DockPanel.SetDock(bar, Dock.Top); root.Children.Add(bar);
        DockPanel.SetDock(_status, Dock.Bottom); root.Children.Add(_status); DockPanel.SetDock(_tabs, Dock.Bottom); root.Children.Add(_tabs);
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

    private void RegisterCommands()
    {
        AddAsync("Shell.Open", "Mở XLSX", OpenAsync, "Ctrl+O", "file.open"); AddAsync("Shell.Save", "Lưu XLSX", SaveAsync, "Ctrl+S", "file.save");
        Add("Edit.Undo", "Hoàn tác", () => Session.Undo(), "Ctrl+Z"); Add("Edit.Redo", "Làm lại", () => Session.Redo(), "Ctrl+Y");
        AddAsync("Edit.Copy", "Sao chép", async () => { await _split.ActiveSpreadsheet.CopyToClipboardAsync(); }, "Ctrl+C");
        AddAsync("Edit.Cut", "Cắt", async () => { await _split.ActiveSpreadsheet.CopyToClipboardAsync(true); }, "Ctrl+X");
        AddAsync("Edit.Paste", "Dán", async () => { await _split.ActiveSpreadsheet.PasteFromClipboardAsync(); }, "Ctrl+V");
        Add("Cell.Bold", "Đậm", () => Session.Styles.ToggleBold(), "Ctrl+B"); Add("Cell.Italic", "Nghiêng", () => Session.Styles.ToggleItalic(), "Ctrl+I");
        Add("Cell.Clear", "Xóa nội dung", () => Session.ClearSelection()); Add("Formula.Calculate", "Tính lại", () => Session.Recalculate(), "F9");
        Add("View.Freeze", "Cố định khung", () => Session.View.FreezeAtActiveCell()); Add("View.Unfreeze", "Bỏ cố định", () => Session.View.Unfreeze());
        Add("View.Split.None", "Bỏ chia", () => _split.SetMode(SpreadsheetSplitViewMode.None)); Add("View.Split.Vertical", "Chia dọc", () => _split.SetMode(SpreadsheetSplitViewMode.Vertical));
        Add("View.Split.Horizontal", "Chia ngang", () => _split.SetMode(SpreadsheetSplitViewMode.Horizontal)); Add("View.Split.Both", "Chia bốn", () => _split.SetMode(SpreadsheetSplitViewMode.Both));
        Add("View.Split.Undo", "Hoàn tác chia khung", () => _split.UndoSplit()); Add("View.Split.Redo", "Làm lại chia khung", () => _split.RedoSplit());
        Add("View.ZoomIn", "Phóng to", () => _split.SetZoom(Math.Min(4, _split.ActiveSpreadsheet.Zoom + 0.1)));
        Add("View.ZoomOut", "Thu nhỏ", () => _split.SetZoom(Math.Max(0.25, _split.ActiveSpreadsheet.Zoom - 0.1))); Add("View.ZoomReset", "100%", () => _split.SetZoom(1));
        Add("View.Theme", "Sáng/tối Ribbon", () =>
        {
            _ribbon.IconTheme = _ribbon.IconTheme == NeraIconTheme.Light ? NeraIconTheme.Dark : NeraIconTheme.Light; _menu.IconTheme = _ribbon.IconTheme;
        });
    }
    private void Add(string id, string caption, Action action, string? shortcut = null, string? icon = null) => AddAsync(id, caption, () => { action(); return Task.CompletedTask; }, shortcut, icon);
    private void AddAsync(string id, string caption, Func<Task> action, string? shortcut = null, string? icon = null) =>
        _registry.Register(new CommandDescriptor(id, caption, iconKey: icon, shortcut: shortcut), new ShellCommand(this, action));
    private static RibbonDefinition CreateDefinition() => new(
        [new RibbonTabDefinition("home", "Trang đầu",
            [new RibbonGroupDefinition("clipboard", "Bảng tạm", [new RibbonItemDefinition("Edit.Paste", IsLarge: true), new RibbonItemDefinition("Edit.Cut"), new RibbonItemDefinition("Edit.Copy")]),
             new RibbonGroupDefinition("edit", "Chỉnh sửa", [new RibbonItemDefinition("Edit.Undo"), new RibbonItemDefinition("Edit.Redo"), new RibbonItemDefinition("Cell.Clear")]),
             new RibbonGroupDefinition("font", "Phông chữ", [new RibbonItemDefinition("Cell.Bold"), new RibbonItemDefinition("Cell.Italic")])]),
         new RibbonTabDefinition("formulas", "Công thức", [new RibbonGroupDefinition("calculation", "Tính toán", [new RibbonItemDefinition("Formula.Calculate", IsLarge: true)])]),
         new RibbonTabDefinition("view", "Xem",
            [new RibbonGroupDefinition("split", "Chia cửa sổ", [new RibbonItemDefinition("View.Split.Both", IsLarge: true), new RibbonItemDefinition("View.Split.Vertical"), new RibbonItemDefinition("View.Split.Horizontal"), new RibbonItemDefinition("View.Split.None"), new RibbonItemDefinition("View.Split.Undo"), new RibbonItemDefinition("View.Split.Redo")]),
             new RibbonGroupDefinition("freeze", "Cố định", [new RibbonItemDefinition("View.Freeze"), new RibbonItemDefinition("View.Unfreeze")]),
             new RibbonGroupDefinition("zoom", "Thu phóng", [new RibbonItemDefinition("View.ZoomIn"), new RibbonItemDefinition("View.ZoomOut"), new RibbonItemDefinition("View.ZoomReset"), new RibbonItemDefinition("View.Theme")])])],
        [], [new RibbonCommandSurfaceItem("Shell.Save", "1"), new RibbonCommandSurfaceItem("Edit.Undo", "2"), new RibbonCommandSurfaceItem("Edit.Redo", "3")],
        [new RibbonCommandSurfaceItem("Shell.Open", "O"), new RibbonCommandSurfaceItem("Shell.Save", "S")]);

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
        var text = _formula.Text ?? string.Empty; editor.UpdateEditorDraft(text, Math.Clamp(_formula.SelectionStart, 0, text.Length), Math.Clamp(_formula.SelectionEnd, 0, text.Length));
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
            var button = new Button { Content = worksheet.Name, IsEnabled = !_busy };
            button.Click += (_, _) => { if (!_busy && !_closed) Session.ActivateWorksheet(worksheet); }; _tabs.Children.Add(button);
        }
    }
    private void OnCommandFailure(object? sender, NeraAvaloniaCommandActivationFailedEventArgs e) => _status.Text = e.Exception.Message;
    private void OnInteractionFailure(object? sender, SpreadsheetInteractionFailedEventArgs e) => _status.Text = e.Exception.Message;
    private void OnCustomizationRequested(object? sender, EventArgs e)
    {
        if (_busy || _closed) return;
        var editor = new NeraRibbonCustomizationControl(_runtime);
        var window = new Window { Title = "Tùy biến Ribbon và QAT", Width = 1050, Height = 700, Content = editor };
        editor.Cancelled += (_, _) => window.Close(); _dialogs.Add(window); window.Closed += (_, _) => _dialogs.Remove(window); window.Show(this);
    }
    private async Task OpenAsync() => await RunIo(async () =>
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Mở bảng tính", FileTypeFilter = ExcelTypes, AllowMultiple = false });
        if (_closed || files.Count == 0) return;
        await using var stream = await files[0].OpenReadAsync(); var loaded = await _serializer.LoadSessionAsync(stream, new OpenXmlImportOptions());
        if (!_closed) _split.Session = loaded;
    });
    private async Task SaveAsync() => await RunIo(async () =>
    {
        var session = Session;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Lưu bảng tính", SuggestedFileName = "NeraSpreadSheet.xlsx", DefaultExtension = "xlsx", FileTypeChoices = ExcelTypes });
        if (_closed || file is null) return;
        using var staging = new MemoryStream(); await _serializer.SaveSessionAsync(session, staging, new OpenXmlExportOptions()); if (_closed) return; staging.Position = 0;
        await using var destination = await file.OpenWriteAsync(); if (!destination.CanSeek) throw new NotSupportedException("Thiết bị lưu phải hỗ trợ seek.");
        destination.Position = 0; await staging.CopyToAsync(destination); destination.SetLength(staging.Length); await destination.FlushAsync();
    });
    private async Task RunIo(Func<Task> operation)
    {
        if (_busy || _closed) return; _busy = true; _split.IsEnabled = false; _formula.IsEnabled = false;
        try { await operation(); } finally { _busy = false; if (!_closed) { _split.IsEnabled = true; _formula.IsEnabled = true; } }
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
    private sealed class ShellCommand : ICommandHandler
    {
        private readonly FullShellWindow _owner;
        private readonly Func<Task> _action;
        public ShellCommand(FullShellWindow owner, Func<Task> action) { _owner = owner; _action = action; }
        public bool CanExecute(CommandContext context) => !_owner._busy && !_owner._closed;
        public async ValueTask ExecuteAsync(CommandContext context)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (_owner._split.EditingSpreadsheet is { } editor && !editor.CommitEditor()) throw new InvalidOperationException("Dữ liệu chưa hợp lệ; bản nháp vẫn được giữ.");
            await _action(); _owner._commandExecutions++;
            if (!_owner._closed) { _owner.RefreshSelection(); _owner._menu.Runtime.Refresh(); }
        }
    }
}
