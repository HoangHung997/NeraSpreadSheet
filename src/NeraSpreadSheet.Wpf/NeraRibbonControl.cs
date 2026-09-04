using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using NeraSpreadSheet.Commands;
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
    private readonly StackPanel _backstage = new();
    private readonly Grid _contentHost = new();
    private readonly TabControl _tabs = new();
    private readonly List<IDisposable> _shortcutBindings = [];
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
    private IInputElement? _focusBeforeKeyTips;
    private bool _disposed;

    public NeraRibbonControl(RibbonRuntimeController runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
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
        _focusBeforeKeyTips = System.Windows.Input.Keyboard.FocusedElement;
        _runtime.KeyTips.Enter();
        Rebuild();
    }

    public async ValueTask<bool> ProcessKeyTipAsync(string keyTip)
    {
        var result = _runtime.KeyTips.Process(keyTip);
        return await ApplyKeyTipResultAsync(result).ConfigureAwait(true);
    }

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
            await ActivateCommandAsync(commandId).ConfigureAwait(true);
            RestoreKeyTipOrigin();
            return true;
        }
        if (result.Action == RibbonKeyTipAction.ScopeChanged)
        {
            Rebuild();
            return true;
        }
        return false;
    }

    public void EscapeKeyTipMode()
    {
        var result = _runtime.KeyTips.Escape();
        if (result.Action == RibbonKeyTipAction.Exit)
        {
            RestoreKeyTipOrigin();
        }
        if (_runtime.KeyTips.Scope == RibbonKeyTipScope.Tabs)
        {
            _isBackstageOpen = false;
        }
        Rebuild();
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
            ActivateCommandAsync);
        _shortcutBindings.Add(binding);
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
                _focusedCommandId));
        var selectedTabId = LayoutSnapshot.SelectedTabId;
        _focusedCommandId = LayoutSnapshot.FocusedCommandId;
        _tabs.Items.Clear();
        RebuildTopBar();
        RebuildBackstage();
        foreach (var tab in LayoutSnapshot.Tabs)
        {
            var groups = new StackPanel
            {
                Margin = new Thickness(4d),
                Orientation = Orientation.Horizontal,
            };
            foreach (var group in tab.Groups.Where(static group =>
                         group.Mode != RibbonGroupLayoutMode.Overflow))
            {
                var items = new StackPanel
                {
                    Margin = new Thickness(2d),
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Visibility = _runtime.IsMinimized
                        ? Visibility.Collapsed
                        : Visibility.Visible,
                };
                foreach (var item in group.Items)
                {
                    items.Children.Add(CreateRibbonItem(item));
                }

                groups.Children.Add(new GroupBox
                {
                    Header = group.Presentation.Caption,
                    Content = items,
                    Margin = new Thickness(2d),
                    Padding = new Thickness(3d),
                });
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
            Margin = new Thickness(2d),
        };
        AutomationProperties.SetAutomationId(file, "ribbon-file");
        AutomationProperties.SetName(file, FileAutomationName);
        file.Click += (_, _) =>
        {
            _isBackstageOpen = !_isBackstageOpen;
            if (_isBackstageOpen)
            {
                _runtime.KeyTips.OpenBackstage();
            }
            Rebuild();
        };
        _topBar.Children.Add(file);
        foreach (var command in _runtime.Snapshot.QuickAccessToolbar)
        {
            var button = new Button
            {
                Content = _runtime.KeyTips.Scope switch
                {
                    RibbonKeyTipScope.Tabs => $"{command.Caption} [Q→{FindSurfaceTip(_runtime.Definition.QuickAccessToolbar, command.CommandId)}]",
                    RibbonKeyTipScope.QuickAccessToolbar => $"{command.Caption} [{FindSurfaceTip(_runtime.Definition.QuickAccessToolbar, command.CommandId)}]",
                    _ => command.Caption,
                },
                CommandParameter = command.CommandId,
                IsEnabled = command.IsEnabled,
                Margin = new Thickness(2d),
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
        foreach (var command in _runtime.Snapshot.Backstage)
        {
            var button = new Button
            {
                Content = _runtime.KeyTips.Scope == RibbonKeyTipScope.Backstage
                    ? $"{command.Caption} [{FindSurfaceTip(_runtime.Definition.Backstage, command.CommandId)}]"
                    : command.Caption,
                CommandParameter = command.CommandId,
                IsEnabled = command.IsEnabled,
                Margin = new Thickness(4d),
            };
            AutomationProperties.SetAutomationId(button, $"ribbon-backstage-{command.CommandId.Value}");
            AutomationProperties.SetName(button, command.Caption);
            button.Click += OnCommandClick;
            _backstage.Children.Add(button);
        }
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
        _focusBeforeKeyTips?.Focus();
        _focusBeforeKeyTips = null;
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
        button.Width = Math.Max(1d, item.Width / LayoutSnapshot.Scale - 4d);
        button.MinHeight = item.Size == RibbonItemSize.Large ? 58d : 30d;
        button.Margin = new Thickness(2d);
        button.Padding = new Thickness(6d, 3d, 6d, 3d);
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
            Margin = new Thickness(0d, 6d, 0d, 6d),
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
            Width = Math.Max(1d, item.Width / LayoutSnapshot.Scale - 4d),
            Margin = new Thickness(2d),
            Tag = item.Presentation.Command.CommandId,
        };
        var menu = CreateChoiceMenu(
            item,
            header: "▼",
            compactHeader: false,
            automationSuffix: "menu");
        menu.Margin = new Thickness(0d);
        DockPanel.SetDock(menu, Dock.Right);
        panel.Children.Add(menu);
        var primary = CreateCommandButton(item);
        AutomationProperties.SetAutomationId(
            primary,
            $"ribbon-command-{item.Presentation.Command.CommandId.Value}-primary");
        primary.Width = double.NaN;
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
            Header = header,
            Tag = command.CommandId,
            IsEnabled = command.IsEnabled,
            ToolTip = BuildToolTip(command),
            MinWidth = compactHeader
                ? Math.Max(1d, item.Width / LayoutSnapshot.Scale - 4d)
                : 20d,
        };
        AutomationProperties.SetAutomationId(
            root,
            $"ribbon-command-{command.CommandId.Value}{(automationSuffix is null ? string.Empty : $"-{automationSuffix}")}");
        AutomationProperties.SetName(root, item.Presentation.AutomationName);
        if (command.IconKey is { Length: > 0 } iconKey &&
            ResolveIcon(iconKey, 16) is ImageSource rootIcon)
        {
            root.Icon = new Image
            {
                Source = rootIcon,
                Width = 16d,
                Height = 16d,
            };
        }
        foreach (var choice in command.SelectableItems)
        {
            root.Items.Add(CreateChoiceMenuItem(command.CommandId, choice));
        }
        return new Menu
        {
            Margin = new Thickness(2d),
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
            Width = Math.Max(1d, item.Width / LayoutSnapshot.Scale - 4d),
            Margin = new Thickness(2d),
            ToolTip = BuildToolTip(command),
        };
        var itemStyle = new Style(typeof(ComboBoxItem));
        itemStyle.Setters.Add(new Setter(
            UIElement.IsEnabledProperty,
            new Binding(nameof(CommandItem.IsEnabled))));
        combo.ItemContainerStyle = itemStyle;
        AutomationProperties.SetAutomationId(combo, $"ribbon-command-{command.CommandId.Value}");
        AutomationProperties.SetName(combo, item.Presentation.AutomationName);
        combo.SelectionChanged += OnChoiceSelectionChanged;
        return combo;
    }

    private ScrollViewer CreateGallery(RibbonItemLayout item)
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
                Content = CreateGalleryChoiceContent(choice),
                Tag = command.CommandId,
                CommandParameter = choice.Value,
                IsEnabled = command.IsEnabled && choice.IsEnabled,
                IsChecked = string.Equals(
                    command.SelectedValue,
                    choice.Value,
                    StringComparison.Ordinal),
                ToolTip = choice.Tooltip ?? choice.Caption,
                Margin = new Thickness(1d),
                Padding = new Thickness(4d, 2d, 4d, 2d),
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
            Width = Math.Max(1d, item.Width / LayoutSnapshot.Scale - 4d),
            Margin = new Thickness(2d),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Tag = command.CommandId,
        };
        AutomationProperties.SetAutomationId(scroll, $"ribbon-command-{command.CommandId.Value}");
        AutomationProperties.SetName(scroll, item.Presentation.AutomationName);
        return scroll;
    }

    private object CreateGalleryChoiceContent(CommandItem choice)
    {
        if (choice.IconKey is not { Length: > 0 } iconKey ||
            ResolveIcon(iconKey, 16) is not ImageSource source)
        {
            return choice.Caption;
        }
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new Image
        {
            Source = source,
            Width = 16d,
            Height = 16d,
            Margin = new Thickness(0d, 0d, 4d, 0d),
        });
        panel.Children.Add(new TextBlock { Text = choice.Caption });
        return panel;
    }

    private StackPanel CreateCommandContent(RibbonItemLayout item)
    {
        var command = item.Presentation.Command;
        var isLarge = item.Size == RibbonItemSize.Large;
        var panel = new StackPanel
        {
            Orientation = isLarge ? Orientation.Vertical : Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
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
        panel.Children.Add(new TextBlock
        {
            Text = item.Size == RibbonItemSize.Compact && resolvedIcon is not null
                ? string.Empty
                : DecorateCommandCaption(command),
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        return panel;
    }

    private string DecorateCommandCaption(CommandPresentation command)
    {
        if (_runtime.KeyTips.Scope != RibbonKeyTipScope.Tab)
        {
            return command.Caption;
        }
        return _runtime.KeyTips.GetCommandTips()
            .FirstOrDefault(pair => pair.Value == command.CommandId) is { Key: { Length: > 0 } key }
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

        var root = new MenuItem { Header = "Thêm", Width = 56d };
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
        groups.Children.Add(new Menu { Items = { root } });
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
            await ActivateItemAsync(commandId, selectedValue);
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
            await ActivateItemAsync(commandId, selectedValue);
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

        await ActivateCommandAsync(commandId);
    }

    private async ValueTask ActivateCommandAsync(CommandId commandId)
    {
        try
        {
            var context = CommandContextFactory?.Invoke(commandId) ?? default;
            await _runtime.TryActivateAsync(commandId, context);
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
