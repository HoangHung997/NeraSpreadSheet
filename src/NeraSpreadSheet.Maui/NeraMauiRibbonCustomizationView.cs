using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Iconography;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Maui;

/// <summary>
/// Native, embeddable Ribbon customization shell. The host owns its window and
/// profile storage; all working state and persistence use the existing binding.
/// Dispose or Cancel rolls preview back to the most recent successful Apply.
/// </summary>
public sealed partial class NeraMauiRibbonCustomizationView : ContentView, IDisposable
{
    private readonly RibbonRuntimeController _runtime;
    private readonly RibbonCustomizationPolicy _policy;
    private readonly VisualElement? _focusOrigin;
    private readonly Grid _columns = new() { ColumnSpacing = 16d, RowSpacing = 16d };
    private readonly VerticalStackLayout _structure = new() { Spacing = 8d };
    private readonly VerticalStackLayout _catalogPanel = new() { Spacing = 8d };
    private readonly Picker _targets = new() { AutomationId = "ribbon-customization-targets" };
    private readonly Picker _catalog = new() { AutomationId = "ribbon-customization-catalog" };
    private readonly Picker _destination = new() { AutomationId = "ribbon-customization-destination" };
    private readonly Picker _qat = new() { AutomationId = "ribbon-customization-qat" };
    private readonly Entry _caption = new() { AutomationId = "ribbon-customization-caption" };
    private readonly Entry _search = new() { AutomationId = "ribbon-customization-search" };
    private readonly CheckBox _visible = new() { AutomationId = "ribbon-customization-visible" };
    private readonly CheckBox _large = new() { AutomationId = "ribbon-customization-large" };
    private readonly Editor _json = new() { AutomationId = "ribbon-customization-json", HeightRequest = 120d, IsVisible = false };
    private readonly Label _status = new() { AutomationId = "ribbon-customization-status" };
    private readonly List<(VisualElement Element, string Key)> _labels = [];
    private readonly Dictionary<string, Button> _buttons = new(StringComparer.Ordinal);
    private IReadOnlyList<RibbonCustomizationEntry> _entries = [];
    private IReadOnlyList<RibbonCustomizationEntry> _groups = [];
    private IReadOnlyList<RibbonCommandCatalogEntry> _commands = [];
    private IReadOnlyList<CommandId> _quickAccess = [];
    private bool _refreshing;
    private bool _disposed;
    private string? _statusKey;

    /// <summary>Creates a shell over one runtime; call on the host UI thread.</summary>
    public NeraMauiRibbonCustomizationView(RibbonRuntimeController runtime,
        RibbonCustomizationPolicy? policy = null, VisualElement? focusOrigin = null)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _policy = policy ?? RibbonCustomizationPolicy.Unrestricted;
        _focusOrigin = focusOrigin;
        Binding = new NeraMauiRibbonCustomizationBinding(runtime, null, _policy);
        AutomationId = "ribbon-customization";
        var body = new VerticalStackLayout { Spacing = 12d, Padding = new Thickness(12d) };
        body.Add(Text("Tùy biến Ribbon"));
        body.Add(Text("Sắp xếp lệnh theo cách bạn làm việc. Thay đổi được xem trước trên Ribbon."));
        _structure.Add(Text("Ribbon hiện tại"));
        LabelControl(_targets, "Chọn tab, nhóm hoặc lệnh");
        _structure.Add(_targets);
        LabelControl(_caption, "Tên hiển thị");
        _structure.Add(_caption);
        _structure.Add(CheckRow(_visible, "Hiển thị"));
        _structure.Add(CheckRow(_large, "Nút lớn"));
        var edits = Actions();
        AddAction(edits, "rename", "Đổi tên", RenameSelected);
        AddAction(edits, "remove", "Xóa mục", RemoveSelected);
        AddAction(edits, "up", "Lên", () => MoveSelected(-1));
        AddAction(edits, "down", "Xuống", () => MoveSelected(1));
        AddAction(edits, "add-tab", "Thêm tab", AddTab);
        AddAction(edits, "add-group", "Thêm nhóm", AddGroup);
        _structure.Add(edits);
        _catalogPanel.Add(Text("Danh mục lệnh"));
        LabelControl(_search, "Tìm lệnh");
        _catalogPanel.Add(_search);
        LabelControl(_catalog, "Lệnh có sẵn");
        _catalogPanel.Add(_catalog);
        LabelControl(_destination, "Nhóm đích");
        _catalogPanel.Add(_destination);
        var placement = Actions();
        AddAction(placement, "add-command", "Thêm lệnh vào nhóm", AddCatalogCommand);
        AddAction(placement, "move-command", "Chuyển lệnh sang nhóm", MoveSelectedCommand);
        _catalogPanel.Add(placement);
        _catalogPanel.Add(Text("Thanh truy nhập nhanh"));
        LabelControl(_qat, "Lệnh truy nhập nhanh");
        _catalogPanel.Add(_qat);
        var qatActions = Actions();
        AddAction(qatActions, "qat-add", "Thêm vào truy nhập nhanh", () =>
        {
            if (CatalogCommand is { } command) { Binding.AddToQuickAccessToolbar(command.CommandId); Publish(); }
        });
        AddAction(qatActions, "qat-remove", "Bỏ khỏi truy nhập nhanh", () =>
        {
            if (QuickAccessCommand is { } id) { Binding.RemoveFromQuickAccessToolbar(id); Publish(); }
        });
        AddAction(qatActions, "qat-up", "Lên", () => MoveQuickAccess(-1));
        AddAction(qatActions, "qat-down", "Xuống", () => MoveQuickAccess(1));
        _catalogPanel.Add(qatActions);
        _columns.Add(_structure);
        _columns.Add(_catalogPanel);
        body.Add(_columns);
        var profileActions = Actions();
        AddAction(profileActions, "export", "Xuất cấu hình", () => { _json.Text = Binding.ExportJson(); _json.IsVisible = true; });
        AddAction(profileActions, "show-import", "Nhập cấu hình", () => { _json.IsVisible = true; _json.Focus(); });
        AddAction(profileActions, "import", "Nạp cấu hình", () => LoadJson(_json.Text ?? string.Empty));
        AddAction(profileActions, "reset", "Mặc định", () => { Binding.Reset(); Refresh(); });
        body.Add(profileActions);
        LabelControl(_json, "Cấu hình Ribbon JSON");
        body.Add(_json);
        var finish = Actions();
        AddAction(finish, "apply", "Áp dụng", ApplyCustomization);
        AddAction(finish, "cancel", "Hủy", CancelCustomization);
        var footer = new VerticalStackLayout { Spacing = 6d, Padding = new Thickness(12d, 4d), Children = { _status, finish } };
        var surface = new Grid { RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) } };
        surface.Add(new ScrollView { Content = body, Orientation = ScrollOrientation.Vertical });
        surface.Add(footer, 0, 1);
        Content = surface;
        _targets.SelectedIndexChanged += (_, _) => { if (!_refreshing) RefreshSelected(); };
        _catalog.SelectedIndexChanged += (_, _) => { if (!_refreshing) UpdateActions(); };
        _destination.SelectedIndexChanged += (_, _) => { if (!_refreshing) UpdateActions(); };
        _qat.SelectedIndexChanged += (_, _) => { if (!_refreshing) UpdateActions(); };
        _search.TextChanged += (_, _) => { if (!_refreshing) RefreshCatalog(); };
        _visible.CheckedChanged += (_, _) => Execute(() =>
        {
            if (!_refreshing && SelectedEntry is { } entry) { Binding.SetVisible(entry.Target, _visible.IsChecked); Refresh(); }
        });
        _large.CheckedChanged += (_, _) => Execute(() =>
        {
            if (!_refreshing && SelectedEntry is { } entry) { Binding.SetLarge(entry.Target, _large.IsChecked); Refresh(); }
        });
        SizeChanged += OnSizeChanged;
        InitializeKeyboard();
        Refresh();
        SetPresentation(NeraIconTheme.Light);
        UpdateColumns(0d);
    }

    /// <summary>Raised after Cancel restores the last applied profile; the host may close the shell.</summary>
    public event EventHandler? CloseRequested;
    /// <summary>Raised after a successful Apply; the host may persist <see cref="ExportJson"/>.</summary>
    public event EventHandler? Applied;
    /// <summary>Raised after an expected UI validation failure; <see cref="LastError"/> retains the original exception.</summary>
    public event EventHandler? CustomizationFailed;
    /// <summary>Gets the most recent UI validation exception without discarding its cause.</summary>
    public Exception? LastError { get; private set; }
    /// <summary>Gets the existing customization binding used by all native controls.</summary>
    public NeraMauiRibbonCustomizationBinding Binding { get; }
    /// <summary>Gets the selected stable target, independent of translated captions.</summary>
    public RibbonCustomizationTarget? SelectedTarget => SelectedEntry?.Target;
    /// <summary>Gets whether panels are stacked for a narrow window.</summary>
    public bool IsNarrow { get; private set; }

    /// <summary>Publishes and commits preview, retaining the shell for further edits.</summary>
    public void ApplyCustomization()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Binding.Apply();
        SetStatus("Đã áp dụng cấu hình Ribbon.");
        Applied?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Restores the last successful Apply and requests that the host close the shell.</summary>
    public void CancelCustomization()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Binding.Cancel();
        Refresh();
        var owner = Window;
        CloseRequested?.Invoke(this, EventArgs.Empty);
        if (_focusOrigin is { IsEnabled: true, IsVisible: true } origin && owner is not null && origin.Window == owner) origin.Focus();
    }

    /// <summary>Exports the working profile through the versioned shared serializer.</summary>
    public string ExportJson() => Binding.ExportJson();
    /// <summary>Loads and previews a validated profile. Apply remains explicit.</summary>
    public void LoadJson(string json) { Binding.LoadJson(json); Refresh(); }

    /// <summary>Refreshes host-scoped text and theme without replacing controls or committing a profile.</summary>
    public void SetPresentation(NeraIconTheme theme)
    {
        var draft = _caption.Text;
        var hasDraft = draft != SelectedEntry?.Caption;
        var cursor = _caption.CursorPosition;
        var selection = _caption.SelectionLength;
        foreach (var (element, key) in _labels)
        {
            var text = _runtime.Localization.Get(key);
            SemanticProperties.SetDescription(element, text);
            switch (element)
            {
                case Label label: label.Text = text; break;
                case Button button: button.Text = text; break;
                case Picker picker: picker.Title = text; break;
                case Entry entry: entry.Placeholder = text; break;
            }
        }
        SemanticProperties.SetDescription(this, _runtime.Localization.Get("Tùy biến Ribbon"));
        var palette = NeraMauiRibbonPalette.For(theme);
        NeraMauiRibbonChrome.ConfigureFilter(this, palette);
        foreach (var button in _buttons.Values)
        {
            NeraMauiRibbonChrome.Configure(button, palette, false, borderWidth: 1d);
            button.MinimumHeightRequest = 32d;
            button.BorderColor = palette.Separator;
            button.Padding = new Thickness(8d, 4d);
        }
        if (_statusKey is not null) SetStatus(_statusKey);
        Refresh(preserveCaption: hasDraft);
        if (hasDraft)
        {
            _caption.CursorPosition = Math.Clamp(cursor, 0, draft?.Length ?? 0);
            _caption.SelectionLength = Math.Clamp(selection, 0, (draft?.Length ?? 0) - _caption.CursorPosition);
        }
    }

    /// <summary>Detaches native keyboard input and rolls back any unapplied preview.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        Binding.Cancel();
        _disposed = true;
        SizeChanged -= OnSizeChanged;
        DisposeKeyboard();
        GC.SuppressFinalize(this);
    }

    private RibbonCustomizationEntry? SelectedEntry => At(_entries, _targets.SelectedIndex);
    private RibbonCommandCatalogEntry? CatalogCommand => At(_commands, _catalog.SelectedIndex);
    private RibbonCustomizationEntry? Destination => At(_groups, _destination.SelectedIndex);
    private CommandId? QuickAccessCommand => _qat.SelectedIndex >= 0 && _qat.SelectedIndex < _quickAccess.Count ? _quickAccess[_qat.SelectedIndex] : (CommandId?)null;
    private static T? At<T>(IReadOnlyList<T> items, int index) where T : class => index >= 0 && index < items.Count ? items[index] : null;
    private static FlexLayout Actions() => new() { Direction = FlexDirection.Row, Wrap = FlexWrap.Wrap, AlignItems = FlexAlignItems.Center };
    private Label Text(string key) { var label = new Label(); LabelControl(label, key); return label; }
    private void LabelControl(VisualElement element, string key) => _labels.Add((element, key));
    private HorizontalStackLayout CheckRow(CheckBox box, string key)
    {
        LabelControl(box, key);
        box.WidthRequest = 40d;
        box.MinimumWidthRequest = 0d;
        box.HorizontalOptions = LayoutOptions.Start;
        NeraMauiRibbonChrome.RemoveNativeMinimums(box);
        return new HorizontalStackLayout { Spacing = 6d, Children = { box, Text(key) } };
    }
    private void AddAction(FlexLayout parent, string id, string key, Action action)
    {
        var button = new Button { AutomationId = "ribbon-customization-" + id, Margin = new Thickness(2d), Padding = new Thickness(8d, 4d) };
        LabelControl(button, key);
        button.Clicked += (_, _) => Execute(action);
        _buttons.Add(id, button);
        parent.Add(button);
    }
    private void Execute(Action action)
    {
        if (_disposed) return;
        try { LastError = null; action(); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or InvalidDataException)
        {
            SetStatus("Không thể thay đổi cấu hình. Kiểm tra lựa chọn, tên và quyền tùy biến.");
            LastError = error;
            CustomizationFailed?.Invoke(this, EventArgs.Empty);
        }
    }
    private void SetStatus(string key) { _statusKey = key; _status.Text = _runtime.Localization.Get(key); }
    private void OnSizeChanged(object? sender, EventArgs args) => UpdateColumns(Width);
    private void UpdateColumns(double width)
    {
        var narrow = width < 720d;
        if (width > 24d)
        {
            var available = width - 24d;
            _structure.WidthRequest = _catalogPanel.WidthRequest = narrow ? available : (available - _columns.ColumnSpacing) / 2d;
            // An opened WinUI ComboBox can retain its previous native desired width.
            // Explicit picker widths keep its chevron inside the resized panel.
            _targets.WidthRequest = _catalog.WidthRequest = _destination.WidthRequest = _qat.WidthRequest = _structure.WidthRequest;
        }
        if (_columns.ColumnDefinitions.Count != 0 && IsNarrow == narrow) return;
        IsNarrow = narrow;
        _columns.ColumnDefinitions.Clear();
        _columns.RowDefinitions.Clear();
        _columns.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        _columns.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        if (narrow) _columns.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        else _columns.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        Grid.SetRow(_catalogPanel, narrow ? 1 : 0);
        Grid.SetColumn(_catalogPanel, narrow ? 0 : 1);
    }
    private void Refresh(bool preserveCaption = false)
    {
        var target = SelectedEntry?.Target;
        var destination = Destination?.Target;
        var qat = QuickAccessCommand;
        _refreshing = true;
        try
        {
            _entries = Binding.Entries;
            _groups = _entries.Where(static entry => entry.Target.Kind == RibbonCustomizationTargetKind.Group).ToArray();
            SetItems(_targets, _entries.Select(entry => new string(' ', entry.Depth * 2) + entry.Caption).ToArray(),
                Find(_entries, entry => entry.Target == target));
            SetItems(_destination, _groups.Select(entry => $"{_entries.First(tab => tab.Target.Kind == RibbonCustomizationTargetKind.Tab && tab.Target.TabId == entry.Target.TabId).Caption} / {entry.Caption}").ToArray(),
                Find(_groups, entry => entry.Target == destination));
            _quickAccess = Binding.QuickAccessToolbar;
            SetItems(_qat, _quickAccess.Select(id => Binding.CommandCatalog.Entries.FirstOrDefault(entry => entry.CommandId == id)?.Caption ?? id.Value).ToArray(),
                Find(_quickAccess, id => id == qat));
            RefreshCatalog();
        }
        finally { _refreshing = false; }
        RefreshSelected(preserveCaption);
    }
    private static int Find<T>(IReadOnlyList<T> entries, Func<T, bool> predicate)
    {
        for (var index = 0; index < entries.Count; index++) if (predicate(entries[index])) return index;
        return entries.Count == 0 ? -1 : 0;
    }
    private static void SetItems(Picker picker, string[] captions, int index)
    {
        if (picker.ItemsSource is not string[] current || !current.SequenceEqual(captions)) picker.ItemsSource = captions;
        picker.SelectedIndex = index;
    }
    private void RefreshCatalog()
    {
        var command = CatalogCommand?.CommandId;
        _commands = Binding.CommandCatalog.Entries.Where(entry => string.IsNullOrWhiteSpace(_search.Text) ||
            entry.Caption.Contains(_search.Text, StringComparison.CurrentCultureIgnoreCase)).ToArray();
        SetItems(_catalog, _commands.Select(static entry => entry.Caption).ToArray(), Find(_commands, entry => entry.CommandId == command));
        UpdateActions();
    }
    private void RefreshSelected(bool preserveCaption = false)
    {
        _refreshing = true;
        try
        {
            if (!preserveCaption) _caption.Text = SelectedEntry?.Caption ?? string.Empty;
            _visible.IsChecked = SelectedEntry?.IsVisible ?? false;
            _large.IsChecked = SelectedEntry?.IsLarge ?? false;
        }
        finally { _refreshing = false; }
        UpdateActions();
    }
    private bool Allowed(RibbonCustomizationOperation operation) => SelectedEntry is { } entry && _policy.IsAllowed(entry.Target, operation);
    private void UpdateActions()
    {
        _buttons["rename"].IsEnabled = SelectedEntry?.Target.Kind != RibbonCustomizationTargetKind.Command && Allowed(RibbonCustomizationOperation.Rename);
        _buttons["remove"].IsEnabled = Allowed(RibbonCustomizationOperation.Remove);
        _buttons["up"].IsEnabled = _buttons["down"].IsEnabled = Allowed(RibbonCustomizationOperation.Reorder);
        _buttons["add-tab"].IsEnabled = _policy.AllowCustomTabs;
        _buttons["add-group"].IsEnabled = _policy.AllowCustomGroups && SelectedEntry is { IsLocked: false };
        _buttons["add-command"].IsEnabled = CatalogCommand is not null && Destination is { IsLocked: false };
        _buttons["move-command"].IsEnabled = SelectedEntry?.Target.Kind == RibbonCustomizationTargetKind.Command && Destination is { IsLocked: false } && Allowed(RibbonCustomizationOperation.MoveCommand);
        _buttons["qat-add"].IsEnabled = _policy.AllowQuickAccessToolbar && CatalogCommand is not null;
        _buttons["qat-remove"].IsEnabled = _buttons["qat-up"].IsEnabled = _buttons["qat-down"].IsEnabled = _policy.AllowQuickAccessToolbar && QuickAccessCommand is not null;
        _buttons["reset"].IsEnabled = _policy.AllowReset;
        _buttons["show-import"].IsEnabled = _buttons["import"].IsEnabled = _policy.AllowImport;
        _visible.IsEnabled = Allowed(RibbonCustomizationOperation.Visibility);
        _large.IsEnabled = SelectedEntry?.Target.Kind == RibbonCustomizationTargetKind.Command && Allowed(RibbonCustomizationOperation.ResizeCommand);
    }
    private void Publish() { Binding.Preview(); Refresh(); }
    private void RenameSelected() { if (SelectedEntry is { } entry) { Binding.Rename(entry.Target, _caption.Text ?? string.Empty); Publish(); } }
    private void RemoveSelected() { if (SelectedEntry is { } entry) { Binding.Remove(entry.Target); Publish(); } }
    private void MoveSelected(int offset) { if (SelectedEntry is { } entry) { Binding.Move(entry.Target, offset); Refresh(); } }
    private void AddTab() { Binding.AddCustomTab("custom-" + Guid.NewGuid().ToString("N"), _caption.Text ?? string.Empty); Publish(); }
    private void AddGroup() { if (SelectedEntry is { } entry) { Binding.AddCustomGroup(entry.Target.TabId, "custom-" + Guid.NewGuid().ToString("N"), _caption.Text ?? string.Empty); Publish(); } }
    private void AddCatalogCommand() { if (CatalogCommand is { } command && Destination is { } group) { Binding.AddCommand(command.CommandId, group.Target.TabId, group.Target.GroupId!); Publish(); } }
    private void MoveSelectedCommand() { if (SelectedEntry is { } entry && Destination is { } group) { Binding.MoveCommand(entry.Target, group.Target.TabId, group.Target.GroupId!); Publish(); } }
    private void MoveQuickAccess(int offset) { if (QuickAccessCommand is { } id) { Binding.MoveQuickAccessToolbar(id, offset); Publish(); } }
    partial void InitializeKeyboard();
    partial void DisposeKeyboard();
}
