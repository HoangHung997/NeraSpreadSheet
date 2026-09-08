using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Templates;
using global::Avalonia.Layout;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Avalonia;

/// <summary>Transactional native editor over RibbonCustomizationSession. Changes are
/// not published until Apply; Cancel never modifies the runtime or a workbook.</summary>
public sealed class NeraRibbonCustomizationControl : UserControl
{
    private readonly RibbonRuntimeController _runtime;
    private readonly RibbonCustomizationPolicy _policy;
    private readonly ListBox _catalog = new();
    private readonly ListBox _entries = new();
    private readonly ListBox _qat = new();
    private readonly TextBox _caption = new() { Watermark = "Tên tab hoặc nhóm" };
    private readonly TextBox _json = new() { AcceptsReturn = true, MinHeight = 90, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBlock _error = new() { TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
    private RibbonCustomizationSession _session;
    private RibbonCustomization? _baseline;
    private bool _changed;
    private bool _qatOverride;

    public NeraRibbonCustomizationControl(RibbonRuntimeController runtime, RibbonCustomizationPolicy? policy = null)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _policy = policy ?? RibbonCustomizationPolicy.Unrestricted;
        _baseline = runtime.Customization;
        _session = CreateSession(_baseline);
        _qatOverride = _baseline?.HasQuickAccessToolbarOverride == true;
        var root = new DockPanel { Margin = new Thickness(12) };
        var bottom = new StackPanel { Spacing = 6 };
        DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        actions.Children.Add(Button("Áp dụng", () => Apply()));
        actions.Children.Add(Button("Hủy thay đổi", Cancel));
        actions.Children.Add(Button("Khôi phục mặc định", Reset));
        actions.Children.Add(Button("Xuất JSON", () => _json.Text = ExportJson()));
        actions.Children.Add(Button("Nhập JSON", () => ImportJson(_json.Text ?? string.Empty)));
        bottom.Children.Add(_error); bottom.Children.Add(actions); bottom.Children.Add(_json);
        var header = new StackPanel { Spacing = 6 };
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        header.Children.Add(_caption);
        var tools = new WrapPanel { Orientation = Orientation.Horizontal };
        tools.Children.Add(Button("Thêm tab", () => AddTab("user-" + Guid.NewGuid().ToString("N"), RequiredCaption())));
        tools.Children.Add(Button("Thêm nhóm", () => AddGroup(Selected().Target.TabId, "user-" + Guid.NewGuid().ToString("N"), RequiredCaption())));
        tools.Children.Add(Button("Đổi tên", () => Rename(Selected().Target, RequiredCaption())));
        tools.Children.Add(Button("Ẩn/hiện", () => { var entry = Selected(); SetVisible(entry.Target, !entry.IsVisible); }));
        tools.Children.Add(Button("Lớn/nhỏ", () => { var entry = Selected(); SetLarge(entry.Target, entry.IsLarge != true); }));
        tools.Children.Add(Button("Lên", () => Move(Selected().Target, -1)));
        tools.Children.Add(Button("Xuống", () => Move(Selected().Target, 1)));
        tools.Children.Add(Button("Xóa", () => Remove(Selected().Target)));
        tools.Children.Add(Button("Thêm lệnh vào nhóm", () =>
        {
            var target = Selected().Target;
            if (target.GroupId is null) throw new InvalidOperationException("Chọn nhóm đích trước.");
            AddCommand(CatalogSelection(), target.TabId, target.GroupId);
        }));
        tools.Children.Add(Button("Thêm vào QAT", () => AddToQuickAccessToolbar(CatalogSelection())));
        tools.Children.Add(Button("Bỏ khỏi QAT", () => RemoveFromQuickAccessToolbar(QatSelection())));
        tools.Children.Add(Button("QAT ←", () => MoveQuickAccessToolbar(QatSelection(), -1)));
        tools.Children.Add(Button("QAT →", () => MoveQuickAccessToolbar(QatSelection(), 1)));
        header.Children.Add(tools);
        var columns = new Grid { ColumnDefinitions = new ColumnDefinitions("*,2*,*"), MinHeight = 230, Margin = new Thickness(0, 8) };
        _catalog.ItemTemplate = new FuncDataTemplate<RibbonCommandCatalogEntry>((entry, _) => new TextBlock { Text = entry?.CategoryCaption + " · " + entry?.Caption, Margin = new Thickness(4) });
        _entries.ItemTemplate = new FuncDataTemplate<RibbonCustomizationEntry>((entry, _) => new TextBlock
        {
            Text = entry is null ? string.Empty : new string(' ', entry.Depth * 4) + (entry.IsVisible ? "☑ " : "☐ ") + entry.Caption,
            Margin = new Thickness(4),
        });
        _qat.ItemTemplate = new FuncDataTemplate<CommandId>((id, _) => new TextBlock { Text = Caption(id), Margin = new Thickness(4) });
        _catalog.ItemsSource = runtime.CommandCatalog.Entries;
        columns.Children.Add(_catalog); Grid.SetColumn(_entries, 1); columns.Children.Add(_entries); Grid.SetColumn(_qat, 2); columns.Children.Add(_qat);
        AutomationProperties.SetName(_catalog, "Danh mục lệnh"); AutomationProperties.SetName(_entries, "Cấu trúc Ribbon"); AutomationProperties.SetName(_qat, "Thanh truy cập nhanh");
        AutomationProperties.SetAutomationId(this, "nera-ribbon-customization");
        root.Children.Add(columns); Content = root; Refresh();
    }

    public IReadOnlyList<RibbonCustomizationEntry> Entries => _session.GetLocalizedEntries(_runtime.Localization);
    public IReadOnlyList<CommandId> QuickAccessToolbar => _session.QuickAccessToolbar;
    public bool HasChanges => _changed;
    public event EventHandler? Applied;
    public event EventHandler? Cancelled;

    public bool SetVisible(RibbonCustomizationTarget target, bool visible) => Mutate(() => _session.SetVisible(target, visible));
    public bool SetLarge(RibbonCustomizationTarget target, bool large) => Mutate(() => _session.SetLarge(target, large));
    public bool Rename(RibbonCustomizationTarget target, string caption) => Mutate(() => _session.Rename(target, caption));
    public bool Move(RibbonCustomizationTarget target, int offset) => Mutate(() => _session.Move(target, offset));
    public bool Remove(RibbonCustomizationTarget target) => Mutate(() => _session.Remove(target));
    public RibbonCustomizationTarget AddTab(string id, string caption) => Add(() => _session.AddTab(id, caption));
    public RibbonCustomizationTarget AddGroup(string tabId, string groupId, string caption) => Add(() => _session.AddGroup(tabId, groupId, caption));
    public RibbonCustomizationTarget AddCommand(CommandId commandId, string tabId, string groupId, bool large = false) => Add(() => _session.AddCommand(commandId, tabId, groupId, large));
    public RibbonCustomizationTarget MoveCommand(RibbonCustomizationTarget command, string tabId, string groupId, int index = int.MaxValue) => Add(() => _session.MoveCommand(command, tabId, groupId, index));
    public bool AddToQuickAccessToolbar(CommandId id) => Mutate(() => _session.AddToQuickAccessToolbar(id), true);
    public bool RemoveFromQuickAccessToolbar(CommandId id) => Mutate(() => _session.RemoveFromQuickAccessToolbar(id), true);
    public bool MoveQuickAccessToolbar(CommandId id, int offset) => Mutate(() => _session.MoveQuickAccessToolbar(id, offset), true);

    /// <summary>Exports the shared versioned profile. An untouched QAT stays inherited,
    /// not a frozen copy of the application defaults. This does not patch other hosts.</summary>
    public string ExportJson()
    {
        VerifyAccess();
        return RibbonCustomizationJsonSerializer.Serialize(CreateProfile() ?? new RibbonCustomization([]));
    }
    public void ImportJson(string json)
    {
        VerifyAccess();
        var profile = RibbonCustomizationJsonSerializer.Deserialize(json);
        // Validate the complete candidate and application policy before touching the working editor.
        var candidate = CreateSession(_baseline);
        candidate.ReplaceCustomization(profile);
        _ = candidate.Preview();
        _session = candidate; _qatOverride = profile.HasQuickAccessToolbarOverride; _changed = true; Refresh();
    }
    public RibbonCustomization? Apply()
    {
        VerifyAccess();
        if (!ReferenceEquals(_runtime.Customization, _baseline)) throw new InvalidOperationException("Ribbon profile changed outside this editor. Cancel and reopen before applying.");
        var profile = CreateProfile();
        if (_changed) _runtime.SetCustomization(profile);
        _baseline = _runtime.Customization; _session = CreateSession(_baseline);
        _qatOverride = _baseline?.HasQuickAccessToolbarOverride == true; _changed = false; Refresh(); Applied?.Invoke(this, EventArgs.Empty);
        return _baseline;
    }
    public void Cancel()
    {
        VerifyAccess(); _baseline = _runtime.Customization; _session = CreateSession(_baseline);
        _qatOverride = _baseline?.HasQuickAccessToolbarOverride == true; _changed = false; Refresh(); Cancelled?.Invoke(this, EventArgs.Empty);
    }
    public void Reset()
    {
        VerifyAccess(); _session.Reset(); _qatOverride = false; _changed = true; Refresh();
    }
    private RibbonCustomization? CreateProfile()
    {
        if (!_changed) return _baseline;
        var profile = _session.CreateCustomization();
        return new RibbonCustomization(profile.Tabs, _qatOverride ? profile.QuickAccessToolbar : null);
    }
    private RibbonCustomizationSession CreateSession(RibbonCustomization? profile) => new(_runtime.Definition, _runtime.CommandCatalog, profile, Caption, _policy);
    private string Caption(CommandId id) => _runtime.CommandCatalog.Entries.FirstOrDefault(entry => entry.CommandId == id)?.Caption ?? id.Value;
    private bool Mutate(Func<bool> operation, bool qat = false)
    {
        VerifyAccess(); var changed = operation(); if (changed) { _changed = true; if (qat) _qatOverride = true; Refresh(); } return changed;
    }
    private RibbonCustomizationTarget Add(Func<RibbonCustomizationTarget> operation)
    {
        VerifyAccess(); var target = operation(); _changed = true; Refresh(); return target;
    }
    private RibbonCustomizationEntry Selected() => _entries.SelectedItem as RibbonCustomizationEntry ?? throw new InvalidOperationException("Chọn một tab, nhóm hoặc lệnh.");
    private CommandId CatalogSelection() => (_catalog.SelectedItem as RibbonCommandCatalogEntry)?.CommandId ?? throw new InvalidOperationException("Chọn lệnh trong danh mục.");
    private CommandId QatSelection() => _qat.SelectedItem is CommandId id ? id : throw new InvalidOperationException("Chọn lệnh trên QAT.");
    private string RequiredCaption() => string.IsNullOrWhiteSpace(_caption.Text) ? throw new InvalidOperationException("Nhập tên tab hoặc nhóm.") : _caption.Text;
    private Button Button(string caption, Action action)
    {
        var button = new Button { Content = _runtime.Localization.Get(caption), Margin = new Thickness(2) };
        button.Click += (_, _) =>
        {
            try { action(); _error.Text = string.Empty; }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.IO.InvalidDataException)
            { _error.Text = exception.Message; }
        };
        return button;
    }
    private void Refresh()
    {
        var selected = (_entries.SelectedItem as RibbonCustomizationEntry)?.Target;
        var selectedQat = _qat.SelectedItem;
        _entries.ItemsSource = Entries;
        _entries.SelectedItem = Entries.FirstOrDefault(entry => entry.Target == selected);
        _qat.ItemsSource = QuickAccessToolbar; _qat.SelectedItem = selectedQat;
    }
}
