using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Iconography;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Wpf;

/// <summary>
/// Native WPF ribbon chrome backed by a host-neutral ribbon runtime.
/// </summary>
public sealed class NeraRibbonControl : UserControl, IDisposable
{
    private readonly RibbonRuntimeController _runtime;
    private readonly RibbonResponsiveLayoutEngine _layoutEngine = new();
    private readonly DockPanel _root = new();
    private readonly StackPanel _topBar = new() { Orientation = Orientation.Horizontal };
    private readonly Grid _backstage = new();
    private readonly Grid _contentHost = new();
    private readonly TabControl _tabs = new();
    private readonly List<IDisposable> _shortcutBindings = [];
    private readonly List<IDisposable> _tableDesignBindings = [];
    private Func<string, ImageSource?>? _iconResolver;
    private Func<NeraIconRequest, ImageSource?>? _iconRequestResolver;
    private NeraIconTheme _iconTheme = NeraIconTheme.Light;
    private string? _selectedTabId;
    private string? _focusedAutomationId;
    private CommandId? _focusedCommandId;
    private bool _restoreCommandFocus;
    private bool _suppressChoiceActivation;
    private bool _resizeRebuildPending;
    private bool _isBackstageOpen;
    private CommandId? _backstageSelection;
    private IInputElement? _focusBeforeKeyTips;
    private string? _focusBeforeKeyTipsAutomationId;
    private bool _disposed;

    public NeraRibbonControl(RibbonRuntimeController runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        NeraRibbonChrome.Install(this);
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 12d;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        SetResourceReference(ForegroundProperty, "RibbonForeground");
        SetResourceReference(BackgroundProperty, "RibbonSurface");
        _topBar.SetResourceReference(Panel.BackgroundProperty, "RibbonTopSurface");
        DockPanel.SetDock(_topBar, Dock.Top);
        _root.Children.Add(_topBar);
        _contentHost.Children.Add(_tabs);
        _contentHost.Children.Add(_backstage);
        _root.Children.Add(_contentHost);
        Content = _root;
        AutomationProperties.SetAutomationId(this, "NeraRibbon");
        AutomationProperties.SetName(this, "Thanh Ribbon NeraSpreadSheet");
        _runtime.SnapshotChanged += OnSnapshotChanged;
        _tabs.SelectionChanged += OnTabSelectionChanged;
        SizeChanged += OnRibbonSizeChanged;
        PreviewKeyDown += OnRibbonPreviewKeyDown;
        Rebuild();
    }

    /// <summary>
    /// Resolves an optional WPF image for a command icon key.
    /// </summary>
    public Func<string, ImageSource?>? IconResolver
    {
        get => _iconResolver;
        set
        {
            if (ReferenceEquals(_iconResolver, value))
            {
                return;
            }
            _iconResolver = value;
            RebuildIfAlive();
        }
    }

    /// <summary>
    /// Resolves an icon with its requested size and theme. The legacy resolver takes precedence.
    /// </summary>
    public Func<NeraIconRequest, ImageSource?>? IconRequestResolver
    {
        get => _iconRequestResolver;
        set
        {
            if (ReferenceEquals(_iconRequestResolver, value))
            {
                return;
            }
            _iconRequestResolver = value;
            RebuildIfAlive();
        }
    }

    /// <summary>
    /// Gets or sets the theme used by the built-in icon provider.
    /// </summary>
    public NeraIconTheme IconTheme
    {
        get => _iconTheme;
        set
        {
            if (_iconTheme == value)
            {
                return;
            }
            _iconTheme = value;
            RebuildIfAlive();
        }
    }

    /// <summary>
    /// Supplies command context at activation time.
    /// </summary>
    public Func<CommandId, CommandContext>? CommandContextFactory { get; set; }

    /// <summary>
    /// Reports an activation failure at the platform boundary.
    /// </summary>
    public event EventHandler<NeraWpfCommandActivationFailedEventArgs>? CommandActivationFailed;

    /// <summary>
    /// Gets the latest host-neutral responsive layout consumed by this presenter.
    /// </summary>
    public RibbonLayoutSnapshot LayoutSnapshot { get; private set; } = null!;

    /// <summary>Gets or sets the localizable File-surface caption.</summary>
    public string FileCaption { get; set; } = "Tệp";

    /// <summary>Gets or sets the localizable File-surface automation name.</summary>
    public string FileAutomationName { get; set; } = "Mở khu vực Tệp";

    /// <summary>Gets the native tab control used by the presenter.</summary>
    public TabControl NativeTabControl => _tabs;

    public bool IsMinimized
    {
        get => _runtime.IsMinimized;
        set => _runtime.SetMinimized(value);
    }

    public bool IsBackstageOpen => _isBackstageOpen;

    public RibbonKeyTipScope KeyTipScope => _runtime.KeyTips.Scope;

    public void EnterKeyTipMode()
    {
        CaptureKeyTipOrigin();
        _runtime.KeyTips.Enter();
        _isBackstageOpen = false;
        Rebuild();
    }

    public async ValueTask<bool> ProcessKeyTipAsync(string keyTip)
    {
        var result = _runtime.KeyTips.Process(keyTip);
        return await ApplyKeyTipResultAsync(result).ConfigureAwait(true);
    }

    /// <summary>Consumes one native key-tip character.</summary>
    public ValueTask<bool> ProcessKeyTipCharacterAsync(char character) =>
        ApplyKeyTipResultAsync(_runtime.KeyTips.ProcessCharacter(character));

    private async ValueTask<bool> ApplyKeyTipResultAsync(RibbonKeyTipResult result)
    {
        if (result.TabId is { } tabId)
        {
            _isBackstageOpen = false;
            _selectedTabId = tabId;
            Rebuild();
            return true;
        }
        if (result.CommandId is { } commandId)
        {
            try
            {
                return await ActivateCommandAsync(commandId).ConfigureAwait(true);
            }
            finally
            {
                _isBackstageOpen = false;
                Rebuild();
                RestoreKeyTipOrigin();
            }
        }
        if (result.Action == RibbonKeyTipAction.ScopeChanged)
        {
            _isBackstageOpen = _runtime.KeyTips.Scope == RibbonKeyTipScope.Backstage;
            Rebuild();
            return true;
        }
        return false;
    }

    public void EscapeKeyTipMode()
    {
        var result = _runtime.KeyTips.Escape();
        if (_runtime.KeyTips.Scope == RibbonKeyTipScope.Tabs)
        {
            _isBackstageOpen = false;
        }
        Rebuild();
        if (result.Action == RibbonKeyTipAction.Exit)
        {
            RestoreKeyTipOrigin();
        }
    }

    /// <summary>
    /// Binds this ribbon's shortcuts to a window or another WPF input root.
    /// </summary>
    public IDisposable BindShortcuts(UIElement owner)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var binding = new NeraWpfShortcutBinding(
            owner,
            _runtime.TryResolveShortcut,
            ActivateCommandAsync,
            EnterKeyTipMode,
            () => _runtime.KeyTips.Scope != RibbonKeyTipScope.Inactive,
            ProcessKeyTipCharacterAsync,
            EscapeKeyTipMode);
        _shortcutBindings.Add(binding);
        return binding;
    }

    /// <summary>Binds contextual Table Design visibility to a spreadsheet session.</summary>
    public IDisposable BindTableDesign(SpreadsheetSession session)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var binding = new NeraWpfTableDesignRibbonBinding(
            session,
            _runtime,
            Dispatcher);
        _tableDesignBindings.Add(binding);
        return binding;
    }

    public ValueTask<bool> TryActivateShortcutAsync(string shortcut) =>
        _runtime.TryActivateShortcutAsync(shortcut);

    /// <summary>
    /// Rebuilds native controls from the current runtime snapshot.
    /// </summary>
    public void Rebuild()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        CaptureIdentities();
        NeraRibbonChrome.ApplyTheme(this, IconTheme);
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var physicalWidth = ActualWidth > 0d
            ? ActualWidth * scale
            : double.PositiveInfinity;
        LayoutSnapshot = _layoutEngine.Layout(
            _runtime.Snapshot,
            new RibbonLayoutRequest(
                physicalWidth,
                scale,
                _selectedTabId,
                _focusedCommandId)
            {
                IsIconAvailable = iconKey => ResolveIcon(iconKey, 16) is not null,
            });
        var selectedTabId = LayoutSnapshot.SelectedTabId;
        _focusedCommandId = LayoutSnapshot.FocusedCommandId;
        _tabs.Items.Clear();
        RebuildTopBar();
        RebuildBackstage();
        foreach (var tab in LayoutSnapshot.Tabs)
        {
            var groups = new StackPanel
            {
                Margin = new Thickness(0d),
                Orientation = Orientation.Horizontal,
                Visibility = _runtime.IsMinimized
                    ? Visibility.Collapsed
                    : Visibility.Visible,
            };
            foreach (var group in tab.Groups.Where(static group =>
                         group.Mode != RibbonGroupLayoutMode.Overflow))
            {
                var items = new Canvas
                {
                    Width = group.Width / scale,
                    Height = group.Height / scale,
                };
                foreach (var item in group.Items)
                {
                    var control = CreateRibbonItem(item);
                    control.Width = Math.Max(1d, item.Width / scale);
                    control.Height = Math.Max(1d, item.Height / scale);
                    Canvas.SetLeft(control, item.X / scale);
                    Canvas.SetTop(control, item.Y / scale);
                    items.Children.Add(control);
                }
                var caption = new TextBlock
                {
                    Text = group.Presentation.Caption,
                    Width = group.Width / scale,
                    Height = group.CaptionHeight / scale,
                    FontSize = 11d,
                    TextAlignment = TextAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    ToolTip = group.Presentation.Caption,
                    IsHitTestVisible = false,
                };
                caption.SetResourceReference(TextBlock.ForegroundProperty, "RibbonMuted");
                AutomationProperties.SetAutomationId(caption, $"ribbon-group-{group.Presentation.Id}-caption");
                Canvas.SetTop(caption, group.CaptionY / scale);
                items.Children.Add(caption);
                var nativeGroup = new GroupBox
                {
                    Header = group.Presentation.Caption,
                    Content = items,
                    Width = group.Width / scale,
                    Height = group.Height / scale,
                    Margin = new Thickness(groups.Children.Count == 0 ? 0d : RibbonLayoutMetrics.Default.Spacing, 0d, 0d, 0d),
                };
                AutomationProperties.SetAutomationId(nativeGroup, $"ribbon-group-{group.Presentation.Id}");
                AutomationProperties.SetName(nativeGroup, group.Presentation.Caption);
                groups.Children.Add(nativeGroup);
            }
            AddOverflowMenu(groups, tab);

            var tabItem = new TabItem
            {
                Header = _runtime.KeyTips.Scope == RibbonKeyTipScope.Tabs
                    ? $"{tab.Presentation.Caption} [{_runtime.KeyTips.TabTips[tab.Presentation.Id]}]"
                    : tab.Presentation.Caption,
                Content = groups,
                Tag = tab.Presentation.Id,
            };
            AutomationProperties.SetAutomationId(
                tabItem,
                $"ribbon-tab-{tab.Presentation.Id}");
            AutomationProperties.SetName(tabItem, tab.Presentation.Caption);
            _tabs.Items.Add(tabItem);
        }
        if (_tabs.Items.Count > 0 && selectedTabId is not null)
        {
            _tabs.SelectedItem = _tabs.Items.OfType<TabItem>().First(item =>
                string.Equals(
                    item.Tag as string,
                    selectedTabId,
                    StringComparison.OrdinalIgnoreCase));
        }
        _selectedTabId = selectedTabId;
        _tabs.Visibility = _isBackstageOpen ? Visibility.Collapsed : Visibility.Visible;
        _backstage.Visibility = _isBackstageOpen ? Visibility.Visible : Visibility.Collapsed;
        RestoreFocus();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _runtime.SnapshotChanged -= OnSnapshotChanged;
        _tabs.SelectionChanged -= OnTabSelectionChanged;
        SizeChanged -= OnRibbonSizeChanged;
        PreviewKeyDown -= OnRibbonPreviewKeyDown;
        foreach (var binding in _shortcutBindings)
        {
            binding.Dispose();
        }
        _shortcutBindings.Clear();
        foreach (var binding in _tableDesignBindings)
        {
            binding.Dispose();
        }
        _tableDesignBindings.Clear();
        _tabs.Items.Clear();
        GC.SuppressFinalize(this);
    }

    private void RebuildTopBar()
    {
        _topBar.Children.Clear();
        var file = new Button
        {
            Content = _runtime.KeyTips.Scope == RibbonKeyTipScope.Tabs
                ? $"{FileCaption} [F]"
                : FileCaption,
            Height = 28d,
            MinWidth = 56d,
            Margin = new Thickness(6d, 2d, 8d, 2d),
            FontWeight = FontWeights.SemiBold,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        AutomationProperties.SetAutomationId(file, "ribbon-file");
        AutomationProperties.SetName(file, FileAutomationName);
        file.Click += (_, _) =>
        {
            _isBackstageOpen = !_isBackstageOpen;
            var restoreFocus = false;
            if (_isBackstageOpen)
            {
                if (_runtime.KeyTips.Scope != RibbonKeyTipScope.Inactive)
                {
                    _runtime.KeyTips.OpenBackstage();
                }
            }
            else
            {
                ExitKeyTipMode();
                restoreFocus = true;
            }
            Rebuild();
            if (restoreFocus)
            {
                RestoreKeyTipOrigin();
            }
        };
        _topBar.Children.Add(file);
        foreach (var command in _runtime.Snapshot.QuickAccessToolbar)
        {
            var tip = _runtime.KeyTips.Scope switch
            {
                RibbonKeyTipScope.Tabs => $"Q→{FindSurfaceTip(_runtime.Definition.QuickAccessToolbar, command.CommandId)}",
                RibbonKeyTipScope.QuickAccessToolbar => FindSurfaceTip(_runtime.Definition.QuickAccessToolbar, command.CommandId),
                _ => null,
            };
            var icon = command.IconKey is { Length: > 0 } key ? ResolveIcon(key, 16) : null;
            var content = new Grid();
            if (icon is not null)
            {
                content.Children.Add(new Image { Source = icon, Width = 16d, Height = 16d });
            }
            else
            {
                content.Children.Add(new TextBlock { Text = command.Caption, VerticalAlignment = VerticalAlignment.Center });
            }
            if (tip is not null)
            {
                content.Children.Add(CreateKeyTipBadge(tip));
            }
            var button = new Button
            {
                Content = content,
                CommandParameter = command.CommandId,
                Tag = command.CommandId,
                IsEnabled = command.IsEnabled,
                Width = icon is null ? double.NaN : 28d,
                Height = 28d,
                Margin = new Thickness(1d, 2d, 1d, 2d),
                ToolTip = BuildToolTip(command),
            };
            AutomationProperties.SetAutomationId(button, $"ribbon-qat-{command.CommandId.Value}");
            AutomationProperties.SetName(button, command.Caption);
            button.Click += OnCommandClick;
            _topBar.Children.Add(button);
        }
    }

    private void RebuildBackstage()
    {
        _backstage.Children.Clear();
        _backstage.ColumnDefinitions.Clear();
        _backstage.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190d) });
        _backstage.ColumnDefinitions.Add(new ColumnDefinition());
        _backstage.MinHeight = 310d;
        var rail = new StackPanel { Margin = new Thickness(0d), Background = (Brush)FindResource("RibbonRail") };
        var railTitle = new TextBlock
        {
            Text = FileCaption,
            FontSize = 23d,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(20d, 20d, 12d, 18d),
        };
        rail.Children.Add(railTitle);
        _backstage.Children.Add(rail);
        var selection = _runtime.Snapshot.Backstage.FirstOrDefault(command => command.CommandId == _backstageSelection)
            ?? (_runtime.Snapshot.Backstage.Count > 0 ? _runtime.Snapshot.Backstage[0] : null);
        _backstageSelection = selection?.CommandId;
        foreach (var command in _runtime.Snapshot.Backstage)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            if (command.IconKey is { Length: > 0 } iconKey && ResolveIcon(iconKey, 16) is { } icon)
            {
                content.Children.Add(new Image { Source = icon, Width = 16d, Height = 16d, Margin = new Thickness(0d, 0d, 12d, 0d) });
            }
            content.Children.Add(new TextBlock
            {
                Text = _runtime.KeyTips.Scope == RibbonKeyTipScope.Backstage
                    ? $"{command.Caption} [{FindSurfaceTip(_runtime.Definition.Backstage, command.CommandId)}]"
                    : command.Caption,
                VerticalAlignment = VerticalAlignment.Center,
            });
            var button = new Button
            {
                Content = content,
                CommandParameter = command.CommandId,
                Tag = command.CommandId,
                IsEnabled = command.IsEnabled,
                Height = 38d,
                Padding = new Thickness(12d, 4d, 8d, 4d),
                Margin = new Thickness(8d, 1d, 8d, 1d),
                Background = command.CommandId == _backstageSelection
                    ? (Brush)FindResource("RibbonChecked") : Brushes.Transparent,
            };
            AutomationProperties.SetAutomationId(button, $"ribbon-backstage-{command.CommandId.Value}");
            AutomationProperties.SetName(button, command.Caption);
            button.Click += (_, _) =>
            {
                _backstageSelection = command.CommandId;
                RebuildBackstage();
            };
            rail.Children.Add(button);
        }
        var pane = new StackPanel { Margin = new Thickness(32d, 26d, 32d, 24d), MaxWidth = 640d, HorizontalAlignment = HorizontalAlignment.Left };
        Grid.SetColumn(pane, 1);
        _backstage.Children.Add(pane);
        pane.Children.Add(new TextBlock { Text = selection?.Caption ?? FileCaption, FontSize = 26d, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0d, 0d, 0d, 16d) });
        if (selection is not null)
        {
            pane.Children.Add(new TextBlock { Text = selection.Tooltip ?? selection.Caption, TextWrapping = TextWrapping.Wrap, MaxWidth = 520d, Margin = new Thickness(0d, 0d, 0d, 24d) });
            var action = new Button
            {
                Content = selection.Caption,
                CommandParameter = selection.CommandId,
                IsEnabled = selection.IsEnabled,
                MinWidth = 160d,
                Height = 38d,
                HorizontalAlignment = HorizontalAlignment.Left,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(18d, 6d, 18d, 6d),
                Background = (Brush)FindResource("RibbonChecked"),
                BorderBrush = (Brush)FindResource("RibbonAccent"),
                ToolTip = BuildToolTip(selection),
            };
            AutomationProperties.SetAutomationId(action, $"ribbon-backstage-{selection.CommandId.Value}-execute");
            AutomationProperties.SetName(action, selection.Caption);
            action.Click += OnCommandClick;
            pane.Children.Add(action);
        }
    }

    private Border CreateKeyTipBadge(string text)
    {
        return new Border
        {
            Child = new TextBlock { Text = text, FontSize = 10d, Foreground = (Brush)FindResource("RibbonForeground") },
            Background = (Brush)FindResource("RibbonSurface"),
            BorderBrush = (Brush)FindResource("RibbonAccent"),
            BorderThickness = new Thickness(1d),
            Padding = new Thickness(2d, 0d, 2d, 0d),
            CornerRadius = new CornerRadius(2d),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            IsHitTestVisible = false,
        };
    }

    private async void OnRibbonPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.System &&
            e.SystemKey is System.Windows.Input.Key.LeftAlt or System.Windows.Input.Key.RightAlt)
        {
            EnterKeyTipMode();
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Escape &&
                 _runtime.KeyTips.Scope != RibbonKeyTipScope.Inactive)
        {
            EscapeKeyTipMode();
            e.Handled = true;
        }
        else if (_runtime.KeyTips.Scope != RibbonKeyTipScope.Inactive &&
                 TryGetKeyTipCharacter(e.Key, out var character))
        {
            e.Handled = await ApplyKeyTipResultAsync(
                _runtime.KeyTips.ProcessCharacter(character)).ConfigureAwait(true);
        }
    }

    private static bool TryGetKeyTipCharacter(System.Windows.Input.Key key, out char character)
    {
        var text = key.ToString();
        if (text.Length == 1 && char.IsLetterOrDigit(text[0]))
        {
            character = text[0];
            return true;
        }
        if (text.Length == 2 && text[0] is 'D' or 'd' && char.IsDigit(text[1]))
        {
            character = text[1];
            return true;
        }
        character = default;
        return false;
    }

    private void RestoreKeyTipOrigin()
    {
        if (_focusBeforeKeyTipsAutomationId is { } automationId)
        {
            FindVisualDescendants<FrameworkElement>(this)
                .FirstOrDefault(element => string.Equals(
                    AutomationProperties.GetAutomationId(element),
                    automationId,
                    StringComparison.Ordinal))
                ?.Focus();
        }
        else
        {
            _focusBeforeKeyTips?.Focus();
        }
        _focusBeforeKeyTips = null;
        _focusBeforeKeyTipsAutomationId = null;
    }

    private void CaptureKeyTipOrigin()
    {
        var focused = System.Windows.Input.Keyboard.FocusedElement;
        if (focused is FrameworkElement element &&
            FindVisualDescendants<FrameworkElement>(this).Contains(element))
        {
            var automationId = AutomationProperties.GetAutomationId(element);
            if (!string.IsNullOrEmpty(automationId))
            {
                _focusBeforeKeyTipsAutomationId = automationId;
                _focusBeforeKeyTips = null;
                return;
            }
        }
        _focusBeforeKeyTips = focused;
        _focusBeforeKeyTipsAutomationId = null;
    }

    private void ExitKeyTipMode()
    {
        while (_runtime.KeyTips.Scope != RibbonKeyTipScope.Inactive)
        {
            _runtime.KeyTips.Escape();
        }
    }

    private static string FindSurfaceTip(
        IReadOnlyList<RibbonCommandSurfaceItem> items,
        CommandId commandId) =>
        items.First(item => item.CommandId == commandId).KeyTip;

    private FrameworkElement CreateRibbonItem(RibbonItemLayout item)
    {
        return item.Presentation.Kind switch
        {
            RibbonItemKind.Separator => CreateSeparator(item),
            RibbonItemKind.SplitButton => CreateSplitButton(item),
            RibbonItemKind.DropDown or RibbonItemKind.Menu => CreateDropDown(item),
            RibbonItemKind.ComboBox or RibbonItemKind.ColorPicker =>
                CreateComboBox(item),
            RibbonItemKind.Gallery => CreateGallery(item),
            _ => CreateCommandButton(item),
        };
    }

    private ButtonBase CreateCommandButton(RibbonItemLayout item)
    {
        var command = item.Presentation.Command;
        ButtonBase button = item.Presentation.IsToggle
            ? new ToggleButton
            {
                IsChecked = command.IsChecked ?? false,
            }
            : new Button();
        button.CommandParameter = command.CommandId;
        button.Tag = command.CommandId;
        button.Content = CreateCommandContent(item);
        button.IsEnabled = command.IsEnabled;
        button.Width = Math.Max(1d, item.Width / LayoutSnapshot.Scale);
        button.Height = Math.Max(1d, item.Height / LayoutSnapshot.Scale);
        button.Margin = new Thickness(0d);
        button.Padding = new Thickness(3d, 1d, 3d, 1d);
        button.ToolTip = BuildToolTip(command);
        AutomationProperties.SetAutomationId(
            button,
            $"ribbon-command-{command.CommandId.Value}");
        AutomationProperties.SetName(button, item.Presentation.AutomationName);
        if (!string.IsNullOrWhiteSpace(command.Tooltip))
        {
            AutomationProperties.SetHelpText(button, command.Tooltip);
        }
        button.Click += OnCommandClick;
        return button;
    }

    private Separator CreateSeparator(RibbonItemLayout item)
    {
        var separator = new Separator
        {
            Tag = item.Presentation.Command.CommandId,
            Margin = new Thickness(0d),
            Width = Math.Max(1d, item.Width / LayoutSnapshot.Scale),
        };
        AutomationProperties.SetAutomationId(
            separator,
            $"ribbon-command-{item.Presentation.Command.CommandId.Value}");
        AutomationProperties.SetName(separator, item.Presentation.AutomationName);
        return separator;
    }

    private DockPanel CreateSplitButton(RibbonItemLayout item)
    {
        var panel = new DockPanel
        {
            Width = Math.Max(1d, item.Width / LayoutSnapshot.Scale),
            Margin = new Thickness(0d),
            Tag = item.Presentation.Command.CommandId,
        };
        var menu = CreateChoiceMenu(
            item,
            header: "⌄",
            compactHeader: false,
            automationSuffix: "menu");
        menu.Margin = new Thickness(0d);
        menu.Width = 18d;
        menu.VerticalAlignment = VerticalAlignment.Stretch;
        DockPanel.SetDock(menu, Dock.Right);
        panel.Children.Add(menu);
        var primary = CreateCommandButton(item with { Width = Math.Max(1d, item.Width - (18d * LayoutSnapshot.Scale)) });
        AutomationProperties.SetAutomationId(
            primary,
            $"ribbon-command-{item.Presentation.Command.CommandId.Value}-primary");
        primary.Width = double.NaN;
        primary.Height = double.NaN;
        primary.Margin = new Thickness(0d);
        panel.Children.Add(primary);
        return panel;
    }

    private Menu CreateDropDown(RibbonItemLayout item) =>
        CreateChoiceMenu(item, item.Presentation.Command.Caption, compactHeader: true);

    private Menu CreateChoiceMenu(
        RibbonItemLayout item,
        string header,
        bool compactHeader,
        string? automationSuffix = null)
    {
        var command = item.Presentation.Command;
        var root = new MenuItem
        {
            Header = compactHeader ? CreateCommandContent(item, showArrow: true) : header,
            Tag = command.CommandId,
            IsEnabled = command.IsEnabled,
            ToolTip = BuildToolTip(command),
            Width = compactHeader ? Math.Max(1d, item.Width / LayoutSnapshot.Scale) : 18d,
            Height = item.Height / LayoutSnapshot.Scale,
            Padding = compactHeader ? new Thickness(3d, 1d, 3d, 1d) : new Thickness(1d),
        };
        AutomationProperties.SetAutomationId(
            root,
            $"ribbon-command-{command.CommandId.Value}{(automationSuffix is null ? string.Empty : $"-{automationSuffix}")}");
        AutomationProperties.SetName(root, item.Presentation.AutomationName);
        foreach (var choice in command.SelectableItems)
        {
            root.Items.Add(CreateChoiceMenuItem(command.CommandId, choice));
        }
        return new Menu
        {
            Margin = new Thickness(0d),
            Items = { root },
        };
    }

    private MenuItem CreateChoiceMenuItem(CommandId commandId, CommandItem choice)
    {
        var menuItem = new MenuItem
        {
            Header = choice.Caption,
            Tag = commandId,
            CommandParameter = choice.Value,
            IsEnabled = choice.IsEnabled,
            IsCheckable = choice.IsChecked.HasValue,
            IsChecked = choice.IsChecked ?? false,
            ToolTip = choice.Tooltip ?? choice.Caption,
        };
        AutomationProperties.SetName(menuItem, choice.Caption);
        if (choice.IconKey is { Length: > 0 } iconKey &&
            ResolveIcon(iconKey, 16) is ImageSource choiceIcon)
        {
            menuItem.Icon = new Image
            {
                Source = choiceIcon,
                Width = 16d,
                Height = 16d,
            };
        }
        foreach (var child in choice.Children)
        {
            menuItem.Items.Add(CreateChoiceMenuItem(commandId, child));
        }
        if (choice.Children.Count == 0)
        {
            menuItem.Click += OnChoiceMenuItemClick;
        }
        return menuItem;
    }

    private ComboBox CreateComboBox(RibbonItemLayout item)
    {
        var command = item.Presentation.Command;
        var combo = new ComboBox
        {
            ItemsSource = command.SelectableItems,
            DisplayMemberPath = nameof(CommandItem.Caption),
            SelectedValuePath = nameof(CommandItem.Value),
            SelectedValue = command.SelectedValue,
            IsEnabled = command.IsEnabled,
            Tag = command.CommandId,
            Width = Math.Max(1d, item.Width / LayoutSnapshot.Scale),
            Margin = new Thickness(0d),
            ToolTip = BuildToolTip(command),
        };
        var itemStyle = new Style(typeof(ComboBoxItem), (Style)FindResource(typeof(ComboBoxItem)));
        itemStyle.Setters.Add(new Setter(
            UIElement.IsEnabledProperty,
            new Binding(nameof(CommandItem.IsEnabled))));
        combo.ItemContainerStyle = itemStyle;
        if (item.Presentation.Kind == RibbonItemKind.ColorPicker)
        {
            combo.DisplayMemberPath = string.Empty;
            var template = new DataTemplate(typeof(CommandItem));
            var content = new FrameworkElementFactory(typeof(StackPanel));
            content.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            var swatch = new FrameworkElementFactory(typeof(Border));
            swatch.SetValue(FrameworkElement.WidthProperty, 13d);
            swatch.SetValue(FrameworkElement.HeightProperty, 13d);
            swatch.SetValue(FrameworkElement.MarginProperty, new Thickness(0d, 0d, 5d, 0d));
            swatch.SetValue(Border.BorderBrushProperty, (Brush)FindResource("RibbonFieldBorder"));
            swatch.SetValue(Border.BorderThicknessProperty, new Thickness(1d));
            swatch.SetBinding(Border.BackgroundProperty, new Binding(nameof(CommandItem.Value)) { Converter = NeraRibbonColorConverter.Instance });
            content.AppendChild(swatch);
            var label = new FrameworkElementFactory(typeof(TextBlock));
            label.SetBinding(TextBlock.TextProperty, new Binding(nameof(CommandItem.Caption)));
            content.AppendChild(label);
            template.VisualTree = content;
            combo.ItemTemplate = template;
        }
        AutomationProperties.SetAutomationId(combo, $"ribbon-command-{command.CommandId.Value}");
        AutomationProperties.SetName(combo, item.Presentation.AutomationName);
        combo.SelectionChanged += OnChoiceSelectionChanged;
        return combo;
    }

    private DockPanel CreateGallery(RibbonItemLayout item)
    {
        var command = item.Presentation.Command;
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Tag = command.CommandId,
        };
        foreach (var choice in command.SelectableItems)
        {
            var button = new ToggleButton
            {
                Content = CreateGalleryChoiceContent(item, choice),
                Tag = command.CommandId,
                CommandParameter = choice.Value,
                IsEnabled = command.IsEnabled && choice.IsEnabled,
                IsChecked = string.Equals(
                    command.SelectedValue,
                    choice.Value,
                    StringComparison.Ordinal),
                ToolTip = choice.Tooltip ?? choice.Caption,
                Width = 72d,
                Height = Math.Max(24d, item.Height / LayoutSnapshot.Scale - 4d),
                Margin = new Thickness(1d),
                Padding = new Thickness(3d),
            };
            AutomationProperties.SetName(button, choice.Caption);
            AutomationProperties.SetAutomationId(
                button,
                $"ribbon-command-{command.CommandId.Value}-choice-{choice.Value}");
            button.Click += OnChoiceButtonClick;
            panel.Children.Add(button);
        }
        var scroll = new ScrollViewer
        {
            Content = panel,
            Margin = new Thickness(0d),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Tag = command.CommandId,
        };
        AutomationProperties.SetAutomationId(scroll, $"ribbon-command-{command.CommandId.Value}-viewport");
        AutomationProperties.SetName(scroll, item.Presentation.AutomationName);
        var gallery = new DockPanel { Tag = command.CommandId, Background = (Brush)FindResource("RibbonFieldSurface") };
        AutomationProperties.SetAutomationId(gallery, $"ribbon-command-{command.CommandId.Value}");
        AutomationProperties.SetName(gallery, item.Presentation.AutomationName);
        var controls = new UniformGrid { Rows = 3, Width = 18d };
        DockPanel.SetDock(controls, Dock.Right);
        var previous = new Button { Content = "⌃", Padding = new Thickness(0d), ToolTip = "Kiểu trước", IsEnabled = false };
        previous.Click += (_, _) => scroll.ScrollToHorizontalOffset(Math.Max(0d, scroll.HorizontalOffset - 74d));
        var next = new Button { Content = "⌄", Padding = new Thickness(0d), ToolTip = "Kiểu tiếp theo", IsEnabled = command.IsEnabled };
        next.Click += (_, _) => scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset + 74d);
        var more = new Button { Content = "⌄", Padding = new Thickness(0d), ToolTip = "Tất cả kiểu", Tag = command.CommandId, IsEnabled = command.IsEnabled };
        AutomationProperties.SetAutomationId(previous, $"ribbon-command-{command.CommandId.Value}-previous");
        AutomationProperties.SetAutomationId(next, $"ribbon-command-{command.CommandId.Value}-next");
        AutomationProperties.SetAutomationId(more, $"ribbon-command-{command.CommandId.Value}-more");
        AutomationProperties.SetName(previous, "Kiểu trước");
        AutomationProperties.SetName(next, "Kiểu tiếp theo");
        AutomationProperties.SetName(more, "Tất cả kiểu");
        controls.Children.Add(previous);
        controls.Children.Add(next);
        controls.Children.Add(more);
        gallery.Children.Add(controls);
        gallery.Children.Add(scroll);
        scroll.ScrollChanged += (_, _) =>
        {
            previous.IsEnabled = command.IsEnabled && scroll.HorizontalOffset > 0d;
            next.IsEnabled = command.IsEnabled && scroll.HorizontalOffset + scroll.ViewportWidth < scroll.ExtentWidth;
        };
        scroll.Loaded += (_, _) =>
        {
            var selectedIndex = command.SelectableItems.ToList().FindIndex(choice =>
                string.Equals(choice.Value, command.SelectedValue, StringComparison.Ordinal));
            if (selectedIndex > 0)
            {
                scroll.ScrollToHorizontalOffset(selectedIndex * 74d);
            }
        };
        var popup = CreateGalleryPopup(item, more);
        more.Click += (_, _) => popup.IsOpen = !popup.IsOpen;
        gallery.Unloaded += (_, _) => popup.IsOpen = false;
        return gallery;
    }

    private Popup CreateGalleryPopup(RibbonItemLayout item, FrameworkElement placementTarget)
    {
        var tiles = new WrapPanel { Width = 370d };
        foreach (var choice in item.Presentation.Command.SelectableItems)
        {
            var tile = new ToggleButton
            {
                Width = 72d,
                Height = 74d,
                Margin = new Thickness(1d),
                Content = CreateGalleryChoiceContent(item, choice),
                Tag = item.Presentation.Command.CommandId,
                CommandParameter = choice.Value,
                IsEnabled = item.Presentation.Command.IsEnabled && choice.IsEnabled,
                IsChecked = string.Equals(item.Presentation.Command.SelectedValue, choice.Value, StringComparison.Ordinal),
                ToolTip = choice.Tooltip ?? choice.Caption,
            };
            AutomationProperties.SetName(tile, choice.Caption);
            AutomationProperties.SetAutomationId(tile, $"ribbon-command-{item.Presentation.Command.CommandId.Value}-popup-choice-{choice.Value}");
            tile.Click += OnChoiceButtonClick;
            tiles.Children.Add(tile);
        }
        var popup = new Popup
        {
            PlacementTarget = placementTarget,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true,
            Child = new Border
            {
                Background = (Brush)FindResource("RibbonSurface"),
                BorderBrush = (Brush)FindResource("RibbonFieldBorder"),
                BorderThickness = new Thickness(1d),
                CornerRadius = new CornerRadius(4d),
                Padding = new Thickness(6d),
                Child = new ScrollViewer { Content = tiles, MaxHeight = 320d, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            },
        };
        popup.Resources.MergedDictionaries.Add(Resources);
        return popup;
    }

    private StackPanel CreateGalleryChoiceContent(RibbonItemLayout item, CommandItem choice)
    {
        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var preview = item.Presentation.Definition.GalleryPreview?.Invoke(choice);
        if (preview is not null)
        {
            panel.Children.Add(new NeraRibbonGalleryThumbnail(preview) { Width = 58d, Height = 38d, Margin = new Thickness(0d, 0d, 0d, 4d) });
        }
        else if (choice.IconKey is { Length: > 0 } iconKey && ResolveIcon(iconKey, 32) is { } source)
        {
            panel.Children.Add(new Image { Source = source, Width = 32d, Height = 32d, Margin = new Thickness(0d, 0d, 0d, 4d) });
        }
        panel.Children.Add(new TextBlock { Text = choice.Caption, FontSize = 10d, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Width = 62d });
        return panel;
    }

    private Grid CreateCommandContent(RibbonItemLayout item, bool showArrow = false)
    {
        var command = item.Presentation.Command;
        var isLarge = item.Size == RibbonItemSize.Large;
        var wrapper = new Grid();
        var panel = new StackPanel
        {
            Orientation = isLarge ? Orientation.Vertical : Orientation.Horizontal,
            HorizontalAlignment = isLarge ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        wrapper.Children.Add(panel);
        var resolvedIcon = command.IconKey is { Length: > 0 } iconKey
            ? ResolveIcon(iconKey, isLarge ? 32 : 16)
            : null;
        if (resolvedIcon is ImageSource source)
        {
            panel.Children.Add(new Image
            {
                Source = source,
                Width = isLarge ? 32d : 16d,
                Height = isLarge ? 32d : 16d,
                Margin = isLarge
                    ? new Thickness(0d, 0d, 0d, 3d)
                    : new Thickness(0d, 0d, 4d, 0d),
                Stretch = Stretch.Uniform,
            });
        }
        var showCaption = item.CaptionVisible || resolvedIcon is null;
        if (showCaption)
        {
            panel.Children.Add(new TextBlock
            {
                Text = showArrow ? $"{command.Caption} ⌄" : command.Caption,
                TextAlignment = isLarge ? TextAlignment.Center : TextAlignment.Left,
                TextWrapping = isLarge ? TextWrapping.Wrap : TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                LineHeight = 14d,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                MaxHeight = isLarge ? item.CaptionMaxLines * 14d : 16d,
                MaxWidth = Math.Max(1d, item.Width / LayoutSnapshot.Scale - (isLarge || resolvedIcon is null ? 8d : 28d)),
                VerticalAlignment = VerticalAlignment.Center,
            });
        }
        else if (showArrow)
        {
            panel.Children.Add(new TextBlock { Text = "⌄", VerticalAlignment = VerticalAlignment.Center });
        }
        if (_runtime.KeyTips.Scope == RibbonKeyTipScope.Tab && _runtime.KeyTips.TryGetCommandTip(command.CommandId, out var tip))
        {
            wrapper.Children.Add(CreateKeyTipBadge(tip));
        }
        return wrapper;
    }

    private string DecorateCommandCaption(CommandPresentation command)
    {
        if (_runtime.KeyTips.Scope != RibbonKeyTipScope.Tab)
        {
            return command.Caption;
        }
        return _runtime.KeyTips.TryGetCommandTip(command.CommandId, out var key)
            ? $"{command.Caption} [{key}]"
            : command.Caption;
    }

    private void AddOverflowMenu(StackPanel groups, RibbonTabLayout tab)
    {
        var overflowGroups = tab.Groups
            .Where(static group => group.Mode == RibbonGroupLayoutMode.Overflow)
            .ToArray();
        if (overflowGroups.Length == 0)
        {
            return;
        }

        var root = new MenuItem { Header = "Thêm ⌄", Width = 56d, Height = 76d };
        AutomationProperties.SetAutomationId(root, "ribbon-overflow");
        AutomationProperties.SetName(root, "Lệnh Ribbon bổ sung");
        foreach (var group in overflowGroups)
        {
            var groupItem = new MenuItem { Header = group.Presentation.Caption };
            foreach (var item in group.Items)
            {
                var command = item.Presentation.Command;
                if (item.Presentation.Kind == RibbonItemKind.Separator)
                {
                    groupItem.Items.Add(new Separator());
                    continue;
                }
                var commandItem = new MenuItem
                {
                    Header = command.Caption,
                    CommandParameter = command.CommandId,
                    Tag = command.CommandId,
                    IsEnabled = command.IsEnabled,
                    IsCheckable = item.Presentation.IsToggle,
                    IsChecked = item.Presentation.IsToggle && command.IsChecked == true,
                    ToolTip = BuildToolTip(command),
                };
                AutomationProperties.SetAutomationId(
                    commandItem,
                    $"ribbon-command-{command.CommandId.Value}");
                AutomationProperties.SetName(
                    commandItem,
                    item.Presentation.AutomationName);
                if (item.Presentation.Kind is RibbonItemKind.Button or RibbonItemKind.Toggle)
                {
                    commandItem.Click += OnOverflowCommandClick;
                }
                else
                {
                    if (item.Presentation.Kind == RibbonItemKind.SplitButton)
                    {
                        var primary = new MenuItem
                        {
                            Header = command.Caption,
                            CommandParameter = command.CommandId,
                            Tag = command.CommandId,
                            IsEnabled = command.IsEnabled,
                            ToolTip = BuildToolTip(command),
                        };
                        AutomationProperties.SetAutomationId(
                            primary,
                            $"ribbon-command-{command.CommandId.Value}-primary");
                        AutomationProperties.SetName(primary, item.Presentation.AutomationName);
                        primary.Click += OnOverflowCommandClick;
                        commandItem.Items.Add(primary);
                        if (command.SelectableItems.Count > 0)
                        {
                            commandItem.Items.Add(new Separator());
                        }
                    }
                    foreach (var choice in command.SelectableItems)
                    {
                        commandItem.Items.Add(CreateChoiceMenuItem(command.CommandId, choice));
                    }
                }
                groupItem.Items.Add(commandItem);
            }
            root.Items.Add(groupItem);
        }
        groups.Children.Add(new Menu
        {
            Items = { root },
            Margin = new Thickness(groups.Children.Count == 0 ? 0d : RibbonLayoutMetrics.Default.Spacing, 4d, 0d, 0d),
        });
    }

    private void CaptureIdentities()
    {
        if (_tabs.SelectedItem is TabItem { Tag: string selectedId })
        {
            _selectedTabId = selectedId;
        }
        var focused = FindVisualDescendants<FrameworkElement>(_tabs)
            .FirstOrDefault(static element => element.IsKeyboardFocused);
        if (focused?.Tag is CommandId focusedId)
        {
            _focusedCommandId = focusedId;
            _focusedAutomationId = AutomationProperties.GetAutomationId(focused);
            _restoreCommandFocus = true;
        }
        else if (System.Windows.Input.Keyboard.FocusedElement is not null)
        {
            _restoreCommandFocus = false;
        }
    }

    private void RestoreFocus()
    {
        if (!_restoreCommandFocus || _focusedCommandId is not { } commandId)
        {
            return;
        }
        var candidates = FindVisualDescendants<FrameworkElement>(_tabs)
            .Where(element => element.Tag is CommandId id && id == commandId)
            .ToArray();
        var target = candidates.FirstOrDefault(element => string.Equals(
                AutomationProperties.GetAutomationId(element),
                _focusedAutomationId,
                StringComparison.Ordinal))
            ?? candidates.FirstOrDefault();
        target?.Focus();
    }

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }
            foreach (var descendant in FindVisualDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private async void OnOverflowCommandClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: CommandId commandId })
        {
            await ActivateCommandAsync(commandId);
        }
    }

    private async void OnChoiceMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem
            {
                Tag: CommandId commandId,
                CommandParameter: string selectedValue,
            })
        {
            if (!await ActivateItemAsync(commandId, selectedValue))
            {
                Rebuild();
            }
        }
    }

    private async void OnChoiceButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is ButtonBase
            {
                Tag: CommandId commandId,
                CommandParameter: string selectedValue,
            })
        {
            if (!await ActivateItemAsync(commandId, selectedValue))
            {
                Rebuild();
            }
        }
    }

    private async void OnChoiceSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressChoiceActivation ||
            sender is not ComboBox { Tag: CommandId commandId } combo)
        {
            return;
        }

        if (combo.SelectedValue is not string selectedValue ||
            !await ActivateItemAsync(commandId, selectedValue))
        {
            _suppressChoiceActivation = true;
            try
            {
                combo.SelectedValue = _runtime.Snapshot.Tabs
                    .SelectMany(static tab => tab.Groups)
                    .SelectMany(static group => group.Items)
                    .FirstOrDefault(item => item.Command.CommandId == commandId)
                    ?.Command.SelectedValue;
            }
            finally
            {
                _suppressChoiceActivation = false;
            }
        }
    }

    private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_tabs.SelectedItem is TabItem { Tag: string selectedId })
        {
            _selectedTabId = selectedId;
        }
    }

    private void OnRibbonSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_disposed && e.WidthChanged)
        {
            ScheduleResizeRebuild();
        }
    }

    private void ScheduleResizeRebuild()
    {
        if (_resizeRebuildPending)
        {
            return;
        }
        _resizeRebuildPending = true;
        _ = Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Render,
            new Action(() =>
            {
                _resizeRebuildPending = false;
                if (!_disposed)
                {
                    Rebuild();
                }
            }));
    }

    private ImageSource? ResolveIcon(string iconKey, int pixelSize)
    {
        var legacy = IconResolver?.Invoke(iconKey);
        if (legacy is not null)
        {
            return legacy;
        }

        var request = new NeraIconRequest(iconKey, pixelSize, IconTheme);
        return IconRequestResolver?.Invoke(request) ?? NeraWpfIconProvider.Resolve(request);
    }

    private void RebuildIfAlive()
    {
        if (!_disposed)
        {
            Rebuild();
        }
    }

    private async void OnCommandClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ButtonBase { CommandParameter: CommandId commandId })
        {
            return;
        }

        if (!await ActivateCommandAsync(commandId))
        {
            Rebuild();
        }
    }

    private async ValueTask<bool> ActivateCommandAsync(CommandId commandId)
    {
        try
        {
            var context = CommandContextFactory?.Invoke(commandId) ?? default;
            return await _runtime.TryActivateAsync(commandId, context);
        }
        catch (Exception exception)
        {
            var handler = CommandActivationFailed;
            if (handler is null)
            {
                throw;
            }
            handler(
                this,
                new NeraWpfCommandActivationFailedEventArgs(commandId, exception));
            return false;
        }
    }

    private async ValueTask<bool> ActivateItemAsync(
        CommandId commandId,
        string selectedValue)
    {
        try
        {
            var context = CommandContextFactory?.Invoke(commandId) ?? default;
            return await _runtime.TryActivateItemAsync(commandId, selectedValue, context);
        }
        catch (Exception exception)
        {
            var handler = CommandActivationFailed;
            if (handler is null)
            {
                throw;
            }
            handler(this, new NeraWpfCommandActivationFailedEventArgs(commandId, exception));
            return false;
        }
    }

    private void OnSnapshotChanged(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }
        _ = Dispatcher.BeginInvoke(() =>
        {
            if (!_disposed)
            {
                Rebuild();
            }
        });
    }

    private static string BuildToolTip(CommandPresentation command) =>
        (command.Tooltip, command.Shortcut) switch
        {
            ({ Length: > 0 } tooltip, { Length: > 0 } shortcut) =>
                $"{tooltip} ({shortcut})",
            ({ Length: > 0 } tooltip, _) => tooltip,
            (_, { Length: > 0 } shortcut) => shortcut,
            _ => command.Caption,
        };
}

public sealed class NeraWpfCommandActivationFailedEventArgs : EventArgs
{
    public NeraWpfCommandActivationFailedEventArgs(
        CommandId commandId,
        Exception exception)
    {
        CommandId = commandId;
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
    }

    public CommandId CommandId { get; }

    public Exception Exception { get; }
}
