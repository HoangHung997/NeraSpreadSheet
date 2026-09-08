using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Input;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Styling;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Ribbon.Core;
using NeraSpreadSheet.Iconography;

namespace NeraSpreadSheet.Avalonia;

/// <summary>Native responsive Ribbon/QAT/backstage over the shared Ribbon runtime.
/// Caller-owned command, customization and workbook models are never copied.</summary>
public sealed partial class NeraRibbonControl : UserControl, IDisposable
{
    private readonly RibbonRuntimeController _runtime;
    private readonly RibbonResponsiveLayoutEngine _layoutEngine = new();
    private readonly NeraAvaloniaIconProvider _icons = new();
    private readonly DockPanel _root = new();
    private readonly StackPanel _top = new() { Orientation = Orientation.Horizontal, Spacing = 3 };
    private readonly TabControl _tabs = new();
    private readonly Grid _backstage = new();
    private readonly ThemeVariantScope _theme = new();
    private readonly List<IDisposable> _bindings = [];
    private readonly List<Popup> _popups = [];
    private bool _disposed;
    private bool _rebuilding;
    private int _refreshQueued;
    private string? _selectedTabId;
    private CommandId? _selectedBackstageId;
    private bool _backstageOpen;
    private bool _showTopBar = true;
    private NeraIconTheme _iconTheme = NeraIconTheme.Light;

    public NeraRibbonControl(RibbonRuntimeController runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        DockPanel.SetDock(_top, Dock.Top);
        _root.Children.Add(_top);
        var content = new Grid();
        content.Children.Add(_tabs);
        content.Children.Add(_backstage);
        _root.Children.Add(content);
        _theme.Child = _root;
        Content = _theme;
        UseLayoutRounding = true;
        FontSize = 12;
        SetIdentity(this, "nera-ribbon", "Ribbon NeraSpreadSheet");
        SetIdentity(_top, "ribbon-top-bar", "Thanh Tệp và truy cập nhanh");
        _tabs.SelectionChanged += OnSelectedTabChanged;
        _runtime.SnapshotChanged += OnSnapshotChanged;
        Rebuild();
    }

    public RibbonRuntimeController Runtime => _runtime;
    public RibbonLayoutSnapshot LayoutSnapshot { get; private set; } = null!;
    public TabControl NativeTabControl => _tabs;
    public string? SelectedTabId => _selectedTabId;
    public bool IsBackstageOpen => _backstageOpen;
    public bool ShowTopBar
    {
        get => _showTopBar;
        set
        {
            VerifyUsable();
            if (_showTopBar == value) return;
            _showTopBar = value;
            Rebuild();
        }
    }
    public bool IsMinimized { get => _runtime.IsMinimized; set { VerifyUsable(); _runtime.SetMinimized(value); } }
    public Func<CommandId, CommandContext>? CommandContextFactory { get; set; }
    public Func<NeraIconRequest, IImage?>? IconRequestResolver { get; set; }
    public event EventHandler<NeraAvaloniaCommandActivationFailedEventArgs>? CommandActivationFailed;
    public event EventHandler? CustomizationRequested;
    public NeraIconTheme IconTheme
    {
        get => _iconTheme;
        set { VerifyUsable(); if (_iconTheme != value) { _iconTheme = value; Rebuild(); } }
    }

    public IDisposable BindShortcuts(InputElement owner)
    {
        VerifyUsable();
        var binding = new RibbonInputBinding(this, owner);
        _bindings.Add(binding);
        return binding;
    }
    public ValueTask<bool> TryActivateShortcutAsync(string shortcut)
    {
        VerifyUsable();
        return _runtime.TryResolveShortcut(shortcut, out var id) ? ActivateCommandAsync(id) : ValueTask.FromResult(false);
    }
    public ValueTask<bool> ActivateCommandAsync(CommandId id) => ActivateAsync(id, null, false);
    public ValueTask<bool> ActivateChoiceAsync(CommandId id, string value) => ActivateAsync(id, value, true);
    private async ValueTask<bool> ActivateAsync(CommandId id, string? value, bool choice)
    {
        VerifyUsable();
        try
        {
            var context = CommandContextFactory?.Invoke(id) ?? default;
            var executed = choice ? await _runtime.TryActivateItemAsync(id, value, context) : await _runtime.TryActivateAsync(id, context);
            if (executed && !_disposed) ClosePopups();
            return executed;
        }
        catch (Exception exception)
        {
            if (CommandActivationFailed is not { } handler) throw;
            handler(this, new NeraAvaloniaCommandActivationFailedEventArgs(id, exception));
            return false;
        }
    }
    public bool SelectTab(string id)
    {
        VerifyUsable();
        if (!_runtime.Snapshot.Tabs.Any(tab => string.Equals(tab.Id, id, StringComparison.OrdinalIgnoreCase))) return false;
        _selectedTabId = id;
        _backstageOpen = false;
        Rebuild();
        return true;
    }
    public void OpenBackstage()
    {
        VerifyUsable();
        _backstageOpen = true;
        if (KeyTipScope != RibbonKeyTipScope.Inactive) _runtime.KeyTips.OpenBackstage();
        Rebuild();
    }
    public void CloseBackstage() { VerifyUsable(); _backstageOpen = false; Rebuild(); }

    public void Rebuild()
    {
        VerifyUsable();
        if (_rebuilding) return;
        _rebuilding = true;
        Interlocked.Exchange(ref _refreshQueued, 0);
        var focused = CaptureFocusId();
        try
        {
            ClosePopups();
            var dark = _iconTheme is NeraIconTheme.Dark or NeraIconTheme.HighContrastDark;
            _theme.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            Background = dark ? new SolidColorBrush(Color.FromRgb(32, 35, 41)) : Brushes.White;
            Foreground = dark ? Brushes.White : Brushes.Black;
            _root.Background = Background;
            var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
            LayoutSnapshot = _layoutEngine.Layout(_runtime.Snapshot,
                new RibbonLayoutRequest(Bounds.Width > 0 ? Bounds.Width * scale : double.PositiveInfinity, scale, _selectedTabId)
                { IsIconAvailable = key => ResolveIcon(key, 16) is not null });
            _selectedTabId = LayoutSnapshot.SelectedTabId;
            BuildTopBar();
            _tabs.Items.Clear();
            foreach (var tab in LayoutSnapshot.Tabs)
            {
                var caption = tab.Presentation.Caption;
                if (KeyTipScope == RibbonKeyTipScope.Tabs && _runtime.KeyTips.TabTips.TryGetValue(tab.Presentation.Id, out var tip)) caption += $" [{tip}]";
                var native = new TabItem
                {
                    Header = caption, Tag = tab.Presentation.Id,
                    Content = IsMinimized ? null : BuildGroups(tab),
                    Padding = new Thickness(12, 5),
                };
                SetIdentity(native, "ribbon-tab-" + tab.Presentation.Id, tab.Presentation.Caption);
                native.PointerPressed += (_, _) =>
                {
                    if (IsMinimized) Dispatcher.UIThread.Post(() => { if (!_disposed) ShowMinimizedTab(); });
                };
                _tabs.Items.Add(native);
                if (string.Equals(_selectedTabId, tab.Presentation.Id, StringComparison.OrdinalIgnoreCase)) _tabs.SelectedItem = native;
            }
            BuildBackstage();
            _backstage.IsVisible = _backstageOpen;
            _tabs.IsVisible = !_backstageOpen;
        }
        finally { _rebuilding = false; }
        RestoreFocusId(focused);
    }

    private void BuildTopBar()
    {
        _top.Children.Clear();
        _top.IsVisible = _showTopBar;
        if (!_showTopBar) return;
        var file = new Button { Content = KeyTipScope == RibbonKeyTipScope.Tabs ? Localize("Tệp") + " [F]" : Localize("Tệp"), MinWidth = 54, Height = 28 };
        SetIdentity(file, "ribbon-file", Localize("Tệp"));
        file.Click += (_, _) => { if (_backstageOpen) CloseBackstage(); else OpenBackstage(); };
        _top.Children.Add(file);
        foreach (var command in _runtime.Snapshot.QuickAccessToolbar)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
            if (command.IconKey is { } key && ResolveIcon(key, 16) is { } image) row.Children.Add(new Image { Source = image, Width = 16, Height = 16 });
            else row.Children.Add(new TextBlock { Text = command.Caption });
            var tip = _runtime.EffectiveDefinition.QuickAccessToolbar.FirstOrDefault(item => item.CommandId == command.CommandId)?.KeyTip;
            if (KeyTipScope is RibbonKeyTipScope.Tabs or RibbonKeyTipScope.QuickAccessToolbar && tip is not null)
                row.Children.Add(new TextBlock { Text = $"[{(KeyTipScope == RibbonKeyTipScope.Tabs ? "Q→" : "")}{tip}]", FontSize = 10 });
            var button = new Button { Content = row, MinWidth = 28, Height = 28, IsEnabled = command.IsEnabled, Padding = new Thickness(4, 2) };
            SetIdentity(button, "ribbon-qat-" + command.CommandId.Value, command.Caption);
            ToolTip.SetTip(button, ToolTipText(command));
            button.Click += async (_, _) => await ActivateCommandAsync(command.CommandId);
            _top.Children.Add(button);
        }
        var minimize = new Button { Content = IsMinimized ? "⌄" : "⌃", MinWidth = 28, Height = 28 };
        SetIdentity(minimize, "ribbon-minimize", Localize("Thu gọn Ribbon"));
        minimize.Click += (_, _) => IsMinimized = !IsMinimized;
        _top.Children.Add(minimize);
        var customize = new Button { Content = "⚙", MinWidth = 28, Height = 28 };
        SetIdentity(customize, "ribbon-customize", Localize("Tùy biến Ribbon"));
        ToolTip.SetTip(customize, Localize("Tùy biến Ribbon"));
        customize.Click += (_, _) => CustomizationRequested?.Invoke(this, EventArgs.Empty);
        _top.Children.Add(customize);
    }

    private StackPanel BuildGroups(RibbonTabLayout tab)
    {
        var groups = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, ClipToBounds = true };
        foreach (var group in tab.Groups.Where(static item => item.Mode != RibbonGroupLayoutMode.Overflow))
        {
            var canvas = new Canvas { Width = group.Width / LayoutSnapshot.Scale, Height = group.Height / LayoutSnapshot.Scale };
            foreach (var item in group.Items)
            {
                var control = BuildItem(item);
                control.Width = Math.Max(1, item.Width / LayoutSnapshot.Scale);
                control.Height = Math.Max(1, item.Height / LayoutSnapshot.Scale);
                Canvas.SetLeft(control, item.X / LayoutSnapshot.Scale);
                Canvas.SetTop(control, item.Y / LayoutSnapshot.Scale);
                canvas.Children.Add(control);
            }
            var caption = new TextBlock
            {
                Text = group.Presentation.Caption, FontSize = 10.5, TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis, Width = canvas.Width,
                Height = group.CaptionHeight / LayoutSnapshot.Scale,
            };
            Canvas.SetTop(caption, group.CaptionY / LayoutSnapshot.Scale);
            canvas.Children.Add(caption);
            var border = new Border { Child = canvas, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0, 0, 1, 0) };
            SetIdentity(border, "ribbon-group-" + group.Presentation.Id, group.Presentation.Caption);
            groups.Children.Add(border);
        }
        if (tab.HasOverflow)
        {
            var button = new Button { Content = Localize("Thêm") + " ⌄", Width = 58, Height = 76, VerticalAlignment = VerticalAlignment.Top };
            SetIdentity(button, "ribbon-overflow", Localize("Lệnh Ribbon bổ sung"));
            var menu = new ContextMenu();
            foreach (var group in tab.Groups.Where(static item => item.Mode == RibbonGroupLayoutMode.Overflow))
            {
                var submenu = new MenuItem { Header = group.Presentation.Caption };
                foreach (var item in group.Items) submenu.Items.Add(BuildOverflowItem(item));
                menu.Items.Add(submenu);
            }
            button.ContextMenu = menu;
            button.Click += (_, _) => menu.Open(button);
            groups.Children.Add(button);
        }
        return groups;
    }

    private void BuildBackstage()
    {
        _backstage.Children.Clear();
        _backstage.ColumnDefinitions = new ColumnDefinitions("190,*");
        _backstage.MinHeight = 260;
        var rail = new StackPanel { Spacing = 4, Margin = new Thickness(8) };
        _backstage.Children.Add(rail);
        var entries = _runtime.Snapshot.Backstage;
        var selected = entries.FirstOrDefault(item => item.CommandId == _selectedBackstageId) ?? (entries.Count > 0 ? entries[0] : null);
        _selectedBackstageId = selected?.CommandId;
        foreach (var command in entries)
        {
            var caption = command.Caption;
            if (KeyTipScope == RibbonKeyTipScope.Backstage)
            {
                var tip = _runtime.EffectiveDefinition.Backstage.FirstOrDefault(item => item.CommandId == command.CommandId)?.KeyTip;
                if (tip is not null) caption += $" [{tip}]";
            }
            var button = new Button { Content = caption, IsEnabled = command.IsEnabled, HorizontalAlignment = HorizontalAlignment.Stretch, Height = 36 };
            SetIdentity(button, "ribbon-backstage-" + command.CommandId.Value, command.Caption);
            button.Click += (_, _) => { _selectedBackstageId = command.CommandId; BuildBackstage(); };
            rail.Children.Add(button);
        }
        var pane = new StackPanel { Margin = new Thickness(24), Spacing = 16 };
        Grid.SetColumn(pane, 1);
        _backstage.Children.Add(pane);
        pane.Children.Add(new TextBlock { Text = selected?.Caption ?? Localize("Tệp"), FontSize = 25 });
        if (selected is null) return;
        pane.Children.Add(new TextBlock { Text = selected.Tooltip ?? selected.Caption, TextWrapping = TextWrapping.Wrap, MaxWidth = 480 });
        var action = new Button { Content = selected.Caption, IsEnabled = selected.IsEnabled, MinWidth = 140, Height = 36 };
        SetIdentity(action, "ribbon-backstage-execute-" + selected.CommandId.Value, selected.Caption);
        action.Click += async (_, _) => { if (await ActivateCommandAsync(selected.CommandId) && !_disposed) CloseBackstage(); };
        pane.Children.Add(action);
    }

    private void ShowMinimizedTab()
    {
        if (_disposed || !IsMinimized) return;
        var tab = LayoutSnapshot.Tabs.FirstOrDefault(item => item.Presentation.Id == _selectedTabId);
        if (tab is null) return;
        ClosePopups();
        CreatePopup(_tabs, BuildGroups(tab)).IsOpen = true;
    }
    private Popup CreatePopup(Control target, Control content)
    {
        var popup = new Popup
        {
            PlacementTarget = target, Placement = PlacementMode.Bottom, IsLightDismissEnabled = true,
            Child = new Border { Child = content, Background = Background, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Padding = new Thickness(6) },
        };
        LogicalChildren.Add(popup);
        _popups.Add(popup);
        return popup;
    }
    private void ClosePopups()
    {
        foreach (var popup in _popups)
        {
            popup.IsOpen = false; popup.Child = null; popup.PlacementTarget = null; LogicalChildren.Remove(popup);
        }
        _popups.Clear();
        foreach (var control in this.GetVisualDescendants().OfType<Control>()) control.ContextMenu?.Close();
    }
    private void OnSelectedTabChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_rebuilding && ReferenceEquals(e.Source, _tabs) && _tabs.SelectedItem is TabItem { Tag: string id }) _selectedTabId = id;
    }
    private void OnSnapshotChanged(object? sender, EventArgs e) => ScheduleRebuild();
    private void ScheduleRebuild()
    {
        if (Volatile.Read(ref _disposed) || Interlocked.Exchange(ref _refreshQueued, 1) != 0) return;
        Dispatcher.UIThread.Post(() => { if (!_disposed && Volatile.Read(ref _refreshQueued) != 0) Rebuild(); });
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (_runtime is not null && !_disposed && change.Property == BoundsProperty && change.GetOldValue<Rect>().Width != change.GetNewValue<Rect>().Width) ScheduleRebuild();
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { base.OnAttachedToVisualTree(e); ScheduleRebuild(); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { ClosePopups(); base.OnDetachedFromVisualTree(e); }
    private string? CaptureFocusId()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        return focused is Control control && this.GetVisualDescendants().Contains(control) ? AutomationProperties.GetAutomationId(control) : null;
    }
    private void RestoreFocusId(string? id)
    {
        if (!string.IsNullOrEmpty(id)) this.GetVisualDescendants().OfType<Control>().FirstOrDefault(control => AutomationProperties.GetAutomationId(control) == id)?.Focus();
    }
    private IImage? ResolveIcon(string key, int size)
    {
        var request = new NeraIconRequest(key, size, _iconTheme);
        return IconRequestResolver?.Invoke(request) ?? _icons.Resolve(request);
    }
    private string Localize(string value) => _runtime.Localization.Get(value);
    private static string ToolTipText(CommandPresentation command) => (command.Tooltip ?? command.Caption) + (string.IsNullOrWhiteSpace(command.Shortcut) ? string.Empty : $" ({command.Shortcut})");
    private static void SetIdentity(Control control, string id, string name)
    {
        AutomationProperties.SetAutomationId(control, id); AutomationProperties.SetName(control, name);
    }
    private void VerifyUsable() { VerifyAccess(); ObjectDisposedException.ThrowIf(_disposed, this); }
    public void Dispose()
    {
        VerifyAccess(); if (_disposed) return; _disposed = true;
        _runtime.SnapshotChanged -= OnSnapshotChanged; _tabs.SelectionChanged -= OnSelectedTabChanged;
        foreach (var binding in _bindings) binding.Dispose(); _bindings.Clear();
        ClosePopups(); _tabs.Items.Clear(); _top.Children.Clear(); _backstage.Children.Clear(); _icons.Dispose();
        _keyTipOrigin = null;
    }
}
