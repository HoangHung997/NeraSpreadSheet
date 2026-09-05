using Microsoft.Maui.Controls;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Iconography;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Maui;

/// <summary>
/// MAUI command chrome backed by the host-neutral ribbon runtime.
/// </summary>
public sealed class NeraMauiRibbonView : ContentView, IDisposable
{
    private readonly RibbonRuntimeController _runtime;
    private readonly RibbonResponsiveLayoutEngine _layoutEngine = new();
    private readonly Grid _root = new();
    private readonly HorizontalStackLayout _topBar = new() { Spacing = 2d, Padding = new Thickness(4d, 2d) };
    private readonly Grid _backstage = new() { AutomationId = "ribbon-backstage" };
    private readonly HorizontalStackLayout _tabStrip = new() { Spacing = 2d };
    private readonly HorizontalStackLayout _groups = new() { Spacing = 2d };
    private readonly List<VerticalStackLayout> _choiceMenus = [];
    private readonly VerticalStackLayout _groupOverflowCommands = new() { Spacing = 4d };
    private readonly VerticalStackLayout _overflowCommands = new()
    {
        Spacing = 4d,
    };
    private readonly ScrollView _overflowHost;
    private readonly List<Button> _commandButtons = [];
    private readonly List<VisualElement> _itemControls = [];
    private readonly Dictionary<VisualElement, RibbonFocusIdentity> _focusIdentities = [];
    private readonly Dictionary<VisualElement, string> _keyTipFocusElements = [];
    private readonly List<IDisposable> _shortcutBindings = [];
    private Func<string, ImageSource?>? _iconResolver;
    private Func<NeraIconRequest, ImageSource?>? _iconRequestResolver;
    private NeraIconTheme _iconTheme = NeraIconTheme.Light;
    private string? _selectedTabId;
    private string? _focusedSubpartId;
    private CommandId? _focusedCommandId;
    private double _layoutScale = 1d;
    private bool _restoreCommandFocus;
    private bool _isRebuilding;
    private bool _isOverflowOpen;
    private bool _resizeRebuildPending;
    private bool _isBackstageOpen;
    private CommandId? _backstageSelection;
    private VisualElement? _focusBeforeKeyTips;
    private string? _focusBeforeKeyTipsAutomationId;
    private bool _disposed;

    public NeraMauiRibbonView(RibbonRuntimeController runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _overflowHost = new ScrollView
        {
            Content = _overflowCommands,
            IsVisible = false,
            Orientation = ScrollOrientation.Vertical,
            MaximumHeightRequest = 360d,
            AutomationId = "ribbon-popup-host",
        };
        AutomationId = "NeraMauiRibbon";
        SemanticProperties.SetDescription(this, "Thanh Ribbon NeraSpreadSheet");
        _root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _root.Add(_topBar, 0, 0);
        _root.Add(new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            Content = _tabStrip,
        }, 0, 1);
        _root.Add(_groups, 0, 2);
        _root.Add(_overflowHost, 0, 3);
        _root.Add(_backstage, 0, 1);
        Grid.SetRowSpan(_backstage, 3);
        Content = _root;
        _runtime.SnapshotChanged += OnSnapshotChanged;
        SizeChanged += OnRibbonSizeChanged;
        Rebuild();
    }

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

    public Func<CommandId, CommandContext>? CommandContextFactory { get; set; }

    /// <summary>
    /// Gets or sets the physical pixels per logical MAUI unit used for layout.
    /// Update this value when the containing window moves between displays.
    /// </summary>
    public double LayoutScale
    {
        get => _layoutScale;
        set
        {
            if (!double.IsFinite(value) || value <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            if (_layoutScale.Equals(value))
            {
                return;
            }
            _layoutScale = value;
            RebuildIfAlive();
        }
    }

    /// <summary>
    /// Gets the latest host-neutral responsive layout consumed by this presenter.
    /// </summary>
    public RibbonLayoutSnapshot LayoutSnapshot { get; private set; } = null!;

    public string FileCaption { get; set; } = "Tệp";

    public string FileAutomationName { get; set; } = "Mở khu vực Tệp";

    public bool IsMinimized
    {
        get => _runtime.IsMinimized;
        set => _runtime.SetMinimized(value);
    }

    public bool IsBackstageOpen => _isBackstageOpen;

    public RibbonKeyTipScope KeyTipScope => _runtime.KeyTips.Scope;

    public void EnterKeyTipMode() => EnterKeyTipModeWithFocusOrigin(null);

    /// <summary>
    /// Enters Key Tips while retaining an optional host focus origin outside this view.
    /// </summary>
    public void EnterKeyTipModeWithFocusOrigin(VisualElement? focusOrigin)
    {
        CaptureKeyTipOrigin(focusOrigin);
        _runtime.KeyTips.Enter();
        _isBackstageOpen = false;
        Rebuild();
    }

    public async ValueTask<bool> ProcessKeyTipAsync(string keyTip)
    {
        var result = _runtime.KeyTips.Process(keyTip);
        return await ApplyKeyTipResultAsync(result).ConfigureAwait(false);
    }

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
                return await ActivateCommandAsync(commandId).ConfigureAwait(false);
            }
            finally
            {
                DispatchOrRun(() =>
                {
                    _isBackstageOpen = false;
                    Rebuild();
                    RestoreKeyTipOriginCore();
                });
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

    public IReadOnlyList<Button> CommandButtons => _commandButtons;

    /// <summary>Gets the native MAUI root created for each visible Ribbon item.</summary>
    public IReadOnlyList<VisualElement> ItemControls => _itemControls;

    public event EventHandler<NeraMauiCommandActivationFailedEventArgs>?
        CommandActivationFailed;

    public ValueTask<bool> TryActivateShortcutAsync(string shortcut) =>
        _runtime.TryActivateShortcutAsync(shortcut);

    public ValueTask<bool> TryActivateCommandAsync(CommandId commandId) =>
        ActivateCommandAsync(commandId);

    public IDisposable BindShortcuts(INeraMauiShortcutSource source)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var binding = new NeraMauiShortcutBinding(
            source,
            _runtime.TryResolveShortcut,
            ActivateCommandAsync);
        _shortcutBindings.Add(binding);
        return binding;
    }

    public void Rebuild()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _isRebuilding = true;
        try
        {
            CaptureFocus();
            LayoutSnapshot = _layoutEngine.Layout(
                _runtime.Snapshot,
                new RibbonLayoutRequest(
                    Width > 0d ? Width * LayoutScale : double.PositiveInfinity,
                    LayoutScale,
                    _selectedTabId,
                    _focusedCommandId)
                {
                    IsIconAvailable = key => ResolveIcon(key, 16) is not null,
                });
            _selectedTabId = LayoutSnapshot.SelectedTabId;
            _focusedCommandId = LayoutSnapshot.FocusedCommandId;
            _keyTipFocusElements.Clear();
            _root.BackgroundColor = Palette.Surface;
            _topBar.BackgroundColor = Palette.Chrome;
            _overflowHost.BackgroundColor = Palette.Surface;
            RebuildTopBar();
            RebuildBackstage();
            RebuildTabs(LayoutSnapshot);
            RebuildGroups(LayoutSnapshot);
            _tabStrip.IsVisible = !_isBackstageOpen;
            _groups.IsVisible = !_isBackstageOpen && !_runtime.IsMinimized;
            _overflowHost.IsVisible = !_isBackstageOpen && !_runtime.IsMinimized &&
                _overflowHost.IsVisible;
            _backstage.IsVisible = _isBackstageOpen;
            RestoreFocus();
        }
        finally
        {
            _isRebuilding = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _runtime.SnapshotChanged -= OnSnapshotChanged;
        SizeChanged -= OnRibbonSizeChanged;
        foreach (var binding in _shortcutBindings)
        {
            binding.Dispose();
        }
        _shortcutBindings.Clear();
        _tabStrip.Children.Clear();
        _topBar.Children.Clear();
        _backstage.Children.Clear();
        _groups.Children.Clear();
        _overflowCommands.Children.Clear();
        _groupOverflowCommands.Children.Clear();
        _choiceMenus.Clear();
        _commandButtons.Clear();
        _itemControls.Clear();
        _focusIdentities.Clear();
        _keyTipFocusElements.Clear();
        GC.SuppressFinalize(this);
    }

    private void RebuildTopBar()
    {
        _topBar.Children.Clear();
        var file = new Button
        {
            Text = _runtime.KeyTips.Scope == RibbonKeyTipScope.Tabs
                ? $"{FileCaption} [F]"
                : FileCaption,
            AutomationId = "ribbon-file",
            Padding = new Thickness(10d, 4d),
            HeightRequest = 28d,
            WidthRequest = 54d,
        };
        StyleButton(file);
        SemanticProperties.SetDescription(file, FileAutomationName);
        TrackKeyTipFocus(file);
        file.Clicked += (_, _) =>
        {
            _isBackstageOpen = !_isBackstageOpen;
            var restoreKeyTipOrigin = false;
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
                restoreKeyTipOrigin = true;
            }
            Rebuild();
            if (restoreKeyTipOrigin)
            {
                RestoreKeyTipOrigin();
            }
        };
        _topBar.Children.Add(file);
        foreach (var command in _runtime.Snapshot.QuickAccessToolbar)
        {
            var source = command.IconKey is { Length: > 0 } key ? ResolveIcon(key, 16) : null;
            var button = new Button
            {
                Text = _runtime.KeyTips.Scope switch
                {
                    RibbonKeyTipScope.Tabs => $"{command.Caption} [Q→{FindSurfaceTip(_runtime.Definition.QuickAccessToolbar, command.CommandId)}]",
                    RibbonKeyTipScope.QuickAccessToolbar => $"{command.Caption} [{FindSurfaceTip(_runtime.Definition.QuickAccessToolbar, command.CommandId)}]",
                    _ => source is null ? command.Caption : string.Empty,
                },
                AutomationId = $"ribbon-qat-{command.CommandId.Value}",
                CommandParameter = command.CommandId,
                IsEnabled = command.IsEnabled,
                ImageSource = source,
                WidthRequest = source is null || _runtime.KeyTips.Scope != RibbonKeyTipScope.Inactive ? 100d : 28d,
                HeightRequest = 28d,
            };
            StyleButton(button);
            SemanticProperties.SetHint(button, BuildToolTip(command));
            SemanticProperties.SetDescription(button, command.Caption);
            TrackKeyTipFocus(button);
            button.Clicked += OnCommandClicked;
            _topBar.Children.Add(button);
        }
    }

    private void RebuildBackstage()
    {
        foreach (var element in _keyTipFocusElements.Where(static pair => pair.Value.StartsWith("ribbon-backstage-", StringComparison.Ordinal)).Select(static pair => pair.Key).ToArray())
        {
            _keyTipFocusElements.Remove(element);
        }
        _backstage.Children.Clear();
        var selection = _runtime.Snapshot.Backstage.FirstOrDefault(command => command.CommandId == _backstageSelection)
            ?? (_runtime.Snapshot.Backstage.Count > 0 ? _runtime.Snapshot.Backstage[0] : null);
        _backstageSelection = selection?.CommandId;
        _backstage.ColumnDefinitions.Clear();
        _backstage.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(196d)));
        _backstage.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        var rail = new VerticalStackLayout
        {
            AutomationId = "ribbon-backstage-navigation",
            BackgroundColor = Palette.Chrome,
            Padding = new Thickness(8d),
            Spacing = 2d,
        };
        var content = new VerticalStackLayout
        {
            AutomationId = "ribbon-backstage-content",
            Padding = new Thickness(24d, 14d),
            Spacing = 12d,
        };
        var title = new Label { Text = selection?.Caption ?? FileCaption, FontSize = 22d, TextColor = Palette.Text };
        var detail = new Label { Text = selection is null ? "Chọn lệnh để làm việc với sổ tính." : BuildToolTip(selection), FontSize = 13d, TextColor = Palette.Muted };
        content.Children.Add(title);
        content.Children.Add(detail);
        if (selection is not null)
        {
            var execute = new Button
            {
                Text = selection.Caption,
                AutomationId = $"ribbon-backstage-{selection.CommandId.Value}-execute",
                CommandParameter = selection.CommandId,
                IsEnabled = selection.IsEnabled,
                WidthRequest = 160d,
                HeightRequest = 38d,
                HorizontalOptions = LayoutOptions.Start,
            };
            StyleButton(execute, isChecked: true);
            SemanticProperties.SetDescription(execute, selection.Caption);
            SemanticProperties.SetHint(execute, BuildToolTip(selection));
            TrackKeyTipFocus(execute);
            execute.Clicked += OnCommandClicked;
            content.Children.Add(execute);
        }
        _backstage.Add(new ScrollView { Content = rail, Orientation = ScrollOrientation.Vertical, MaximumHeightRequest = 360d }, 0, 0);
        _backstage.Add(content, 1, 0);
        foreach (var command in _runtime.Snapshot.Backstage)
        {
            var button = new Button
            {
                Text = _runtime.KeyTips.Scope == RibbonKeyTipScope.Backstage
                    ? $"{command.Caption} [{FindSurfaceTip(_runtime.Definition.Backstage, command.CommandId)}]"
                    : command.Caption,
                AutomationId = $"ribbon-backstage-{command.CommandId.Value}",
                CommandParameter = command.CommandId,
                IsEnabled = command.IsEnabled,
                HeightRequest = 34d,
                HorizontalOptions = LayoutOptions.Fill,
            };
            StyleButton(button);
            if (command.IconKey is { Length: > 0 } iconKey)
            {
                button.ImageSource = ResolveIcon(iconKey, 16);
            }
            SemanticProperties.SetDescription(button, command.Caption);
            TrackKeyTipFocus(button);
            button.BackgroundColor = command.CommandId == _backstageSelection ? Palette.Checked : Palette.Surface;
            button.BorderWidth = command.CommandId == _backstageSelection ? 1d : 0d;
            button.Clicked += (_, _) =>
            {
                _backstageSelection = command.CommandId;
                RebuildBackstage();
            };
            rail.Children.Add(button);
        }
    }

    private void RestoreKeyTipOrigin()
    {
        DispatchOrRun(RestoreKeyTipOriginCore);
    }

    private void RestoreKeyTipOriginCore()
    {
        if (_focusBeforeKeyTipsAutomationId is { } automationId)
        {
            _keyTipFocusElements.FirstOrDefault(pair => string.Equals(
                pair.Value,
                automationId,
                StringComparison.Ordinal)).Key?.Focus();
        }
        else
        {
            _focusBeforeKeyTips?.Focus();
        }
        _focusBeforeKeyTips = null;
        _focusBeforeKeyTipsAutomationId = null;
    }

    private void CaptureKeyTipOrigin(VisualElement? focusOrigin)
    {
        var focused = focusOrigin ??
            _keyTipFocusElements.Keys.FirstOrDefault(static element => element.IsFocused);
        if (focused is not null && _keyTipFocusElements.TryGetValue(focused, out var automationId))
        {
            _focusBeforeKeyTipsAutomationId = automationId;
            _focusBeforeKeyTips = null;
            return;
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

    private void RebuildTabs(RibbonLayoutSnapshot snapshot)
    {
        _tabStrip.Children.Clear();
        for (var index = 0; index < snapshot.Tabs.Count; index++)
        {
            var tab = snapshot.Tabs[index];
            var button = new Button
            {
                Text = _runtime.KeyTips.Scope == RibbonKeyTipScope.Tabs
                    ? $"{tab.Presentation.Caption} [{_runtime.KeyTips.TabTips[tab.Presentation.Id]}]"
                    : tab.Presentation.Caption,
                AutomationId = $"ribbon-tab-{tab.Presentation.Id}",
                CommandParameter = tab.Presentation.Id,
                IsEnabled = !string.Equals(
                    tab.Presentation.Id,
                    snapshot.SelectedTabId,
                    StringComparison.OrdinalIgnoreCase),
                Padding = new Thickness(12d, 6d),
                HeightRequest = 30d,
            };
            StyleButton(button);
            if (!button.IsEnabled)
            {
                button.IsEnabled = true;
                button.BorderWidth = 1d;
                button.BorderColor = Palette.Accent;
                button.BackgroundColor = Palette.Checked;
            }
            SemanticProperties.SetDescription(button, tab.Presentation.Caption);
            TrackKeyTipFocus(button);
            button.Clicked += OnTabClicked;
            _tabStrip.Children.Add(button);
        }
    }

    private void RebuildGroups(RibbonLayoutSnapshot snapshot)
    {
        _groups.Children.Clear();
        _overflowCommands.Children.Clear();
        _choiceMenus.Clear();
        _groupOverflowCommands.Children.Clear();
        _groupOverflowCommands.IsVisible = true;
        _overflowCommands.Children.Add(_groupOverflowCommands);
        _overflowCommands.IsVisible = true;
        _overflowHost.IsVisible = false;
        _commandButtons.Clear();
        _itemControls.Clear();
        _focusIdentities.Clear();
        if (_runtime.IsMinimized)
        {
            _isOverflowOpen = false;
            return;
        }
        if (snapshot.Tabs.Count == 0)
        {
            _isOverflowOpen = false;
            return;
        }

        var tab = snapshot.Tabs.First(tab => string.Equals(
            tab.Presentation.Id,
            snapshot.SelectedTabId,
            StringComparison.OrdinalIgnoreCase));
        foreach (var group in tab.Groups.Where(static group =>
                     group.Mode != RibbonGroupLayoutMode.Overflow))
        {
            var groupLayout = new AbsoluteLayout
            {
                WidthRequest = group.Width / LayoutScale,
                HeightRequest = group.Height / LayoutScale,
                AutomationId = $"ribbon-group-{group.Presentation.Id}",
            };
            var caption = new Label
            {
                Text = group.Presentation.Caption,
                AutomationId = $"ribbon-group-caption-{group.Presentation.Id}",
                FontSize = 11d,
                TextColor = Palette.Muted,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                MaxLines = 1,
                LineBreakMode = LineBreakMode.TailTruncation,
            };
            groupLayout.Children.Add(caption);
            AbsoluteLayout.SetLayoutBounds(caption, new Rect(0d, group.CaptionY / LayoutScale, group.Width / LayoutScale, group.CaptionHeight / LayoutScale));
            foreach (var item in group.Items)
            {
                var view = CreateRibbonItem(item);
                groupLayout.Children.Add(view);
                AbsoluteLayout.SetLayoutBounds(view, new Rect(item.X / LayoutScale, item.Y / LayoutScale, item.Width / LayoutScale, item.Height / LayoutScale));
            }
            var separator = new BoxView { Color = Palette.Separator, InputTransparent = true };
            groupLayout.Children.Add(separator);
            AbsoluteLayout.SetLayoutBounds(separator, new Rect((group.Width / LayoutScale) - 1d, 5d, 1d, (group.Height / LayoutScale) - 10d));
            _groups.Children.Add(groupLayout);
        }
        AddOverflow(tab);
    }

    private Button CreateCommandButton(
        RibbonItemLayout item,
        string? automationSuffix = null)
    {
        var command = item.Presentation.Command;
        var button = new Button
        {
            Padding = new Thickness(3d, 0d),
            WidthRequest = item.Width / LayoutScale,
            HeightRequest = item.Height / LayoutScale,
            LineBreakMode = item.CaptionMaxLines > 1 ? LineBreakMode.WordWrap : LineBreakMode.NoWrap,
        };
        StyleButton(button, item.Presentation.IsToggle && command.IsChecked == true);
        var resolvedIcon = command.IconKey is { Length: > 0 } iconKey
            ? ResolveIcon(iconKey, item.Size == RibbonItemSize.Large ? 32 : 16)
            : null;
        if (resolvedIcon is ImageSource source)
        {
            button.ImageSource = source;
            button.ContentLayout = item.Size == RibbonItemSize.Large
                ? new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Top, 4d)
                : new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Left, 4d);
        }
        NeraMauiCommandChrome.Configure(
            button,
            command,
            "ribbon-command",
            item.Size == RibbonItemSize.Large,
            automationSuffix);
        if (_runtime.KeyTips.Scope == RibbonKeyTipScope.Tab &&
            _runtime.KeyTips.TryGetCommandTip(command.CommandId, out var keyTip))
        {
            button.Text = $"{command.Caption} [{keyTip}]";
        }
        var toggleState = item.Presentation.IsToggle
            ? command.IsChecked == true ? "Đang bật" : "Đang tắt"
            : null;
        SemanticProperties.SetDescription(
            button,
            toggleState is null
                ? item.Presentation.AutomationName
                : $"{item.Presentation.AutomationName}. {toggleState}");
        SemanticProperties.SetHint(button, BuildToolTip(command));
        if (item.Presentation.IsToggle)
        {
            button.BorderWidth = command.IsChecked == true ? 1d : 0d;
            button.BorderColor = Palette.Accent;
        }
        if (item.Size == RibbonItemSize.Compact && resolvedIcon is not null &&
            _runtime.KeyTips.Scope != RibbonKeyTipScope.Tab)
        {
            button.Text = string.Empty;
        }
        button.Clicked += OnCommandClicked;
        TrackFocus(button, command.CommandId, button.AutomationId);
        _commandButtons.Add(button);
        return button;
    }

    private View CreateRibbonItem(RibbonItemLayout item)
    {
        View view = item.Presentation.Kind switch
        {
            RibbonItemKind.Separator => CreateSeparator(item),
            RibbonItemKind.SplitButton => CreateSplitButton(item),
            RibbonItemKind.DropDown or RibbonItemKind.Menu => CreateDropDown(item),
            RibbonItemKind.ComboBox or RibbonItemKind.ColorPicker => CreatePicker(item),
            RibbonItemKind.Gallery => CreateGallery(item),
            _ => CreateCommandButton(item),
        };
        view.WidthRequest = item.Width / LayoutScale;
        view.HeightRequest = item.Height / LayoutScale;
        view.MinimumHeightRequest = 0d;
        view.MinimumWidthRequest = 0d;
        view.Margin = Thickness.Zero;
        _itemControls.Add(view);
        return view;
    }

    private BoxView CreateSeparator(RibbonItemLayout item) => new()
    {
        AutomationId = $"ribbon-command-{item.Presentation.Command.CommandId.Value}",
        WidthRequest = Math.Max(1d, item.Width / LayoutScale),
        HeightRequest = item.Height / LayoutScale,
        Color = Palette.Separator,
        Margin = Thickness.Zero,
    };

    private VerticalStackLayout CreateSplitButton(RibbonItemLayout item)
    {
        var choices = CreateChoiceStack(item);
        var menuButton = new Button
        {
            Text = "▼",
            AutomationId = $"ribbon-command-{item.Presentation.Command.CommandId.Value}-menu",
            IsEnabled = item.Presentation.Command.IsEnabled,
            WidthRequest = 18d,
            HeightRequest = item.Height / LayoutScale,
        };
        StyleButton(menuButton);
        SemanticProperties.SetDescription(
            menuButton,
            $"{item.Presentation.AutomationName}, mở danh sách");
        SemanticProperties.SetHint(
            menuButton,
            BuildToolTip(item.Presentation.Command));
        menuButton.Clicked += (_, _) => ShowChoices(choices);
        TrackFocus(
            menuButton,
            item.Presentation.Command.CommandId,
            menuButton.AutomationId);
        var row = new HorizontalStackLayout { Spacing = 0d };
        var primary = CreateCommandButton(item, "primary");
        primary.WidthRequest = Math.Max(1d, item.Width / LayoutScale - 18d);
        row.Children.Add(primary);
        row.Children.Add(menuButton);
        var stack = new VerticalStackLayout
        {
            Spacing = 0d,
            WidthRequest = item.Width / LayoutScale,
        };
        stack.Children.Add(row);
        return stack;
    }

    private VerticalStackLayout CreateDropDown(RibbonItemLayout item)
    {
        var choices = CreateChoiceStack(item);
        var command = item.Presentation.Command;
        var button = new Button
        {
            Text = command.Caption,
            AutomationId = $"ribbon-command-{command.CommandId.Value}",
            IsEnabled = command.IsEnabled,
            WidthRequest = item.Width / LayoutScale,
            HeightRequest = item.Height / LayoutScale,
            LineBreakMode = item.CaptionMaxLines > 1 ? LineBreakMode.WordWrap : LineBreakMode.NoWrap,
            CommandParameter = command.CommandId,
        };
        StyleButton(button);
        SemanticProperties.SetDescription(button, item.Presentation.AutomationName);
        SemanticProperties.SetHint(button, BuildToolTip(command));
        if (command.IconKey is { Length: > 0 } iconKey &&
            ResolveIcon(iconKey, item.Size == RibbonItemSize.Large ? 32 : 16) is ImageSource source)
        {
            button.ImageSource = source;
            button.ContentLayout = new Button.ButtonContentLayout(
                item.Size == RibbonItemSize.Large ? Button.ButtonContentLayout.ImagePosition.Top : Button.ButtonContentLayout.ImagePosition.Left,
                4d);
            if (!item.CaptionVisible && _runtime.KeyTips.Scope != RibbonKeyTipScope.Tab)
            {
                button.Text = "▾";
            }
        }
        button.Clicked += (_, _) => ShowChoices(choices);
        TrackFocus(button, command.CommandId);
        var stack = new VerticalStackLayout { Spacing = 0d };
        stack.Children.Add(button);
        return stack;
    }

    private VerticalStackLayout CreateChoiceStack(RibbonItemLayout item)
    {
        var command = item.Presentation.Command;
        var choices = new VerticalStackLayout
        {
            IsVisible = false,
            Spacing = 2d,
        };
        AddChoiceButtons(
            choices,
            command.CommandId,
            command.SelectableItems,
            0,
            command.IsEnabled,
            command.SelectedValue,
            item.Presentation.Definition.GalleryPreview);
        _choiceMenus.Add(choices);
        _overflowCommands.Children.Add(choices);
        return choices;
    }

    private void ShowChoices(VerticalStackLayout choices)
    {
        var open = !choices.IsVisible || !_overflowHost.IsVisible;
        foreach (var menu in _choiceMenus)
        {
            menu.IsVisible = open && ReferenceEquals(menu, choices);
        }
        _groupOverflowCommands.IsVisible = false;
        _isOverflowOpen = false;
        _overflowHost.IsVisible = open;
    }

    private void AddChoiceButtons(
        VerticalStackLayout target,
        CommandId commandId,
        IReadOnlyList<CommandItem> choices,
        int depth,
        bool ancestorsEnabled,
        string? selectedValue,
        Func<CommandItem, RibbonGalleryPreview?>? galleryPreview)
    {
        foreach (var choice in choices)
        {
            if (choice.Children.Count > 0)
            {
                target.Children.Add(new Label
                {
                    Text = choice.Caption,
                    TextColor = Palette.Muted,
                    Margin = new Thickness(depth * 12d, 2d, 0d, 0d),
                });
                AddChoiceButtons(
                    target,
                    commandId,
                    choice.Children,
                    depth + 1,
                    ancestorsEnabled && choice.IsEnabled,
                    selectedValue,
                    galleryPreview);
                continue;
            }
            var button = new Button
            {
                Text = choice.Caption,
                AutomationId = $"ribbon-command-{commandId.Value}-popup-choice-{choice.Value}",
                CommandParameter = new RibbonChoice(commandId, choice.Value),
                IsEnabled = ancestorsEnabled && choice.IsEnabled,
                Padding = new Thickness(8d, 4d),
                Margin = new Thickness(depth * 12d, 0d, 0d, 0d),
                HeightRequest = 28d,
            };
            StyleButton(button, choice.IsChecked ?? string.Equals(choice.Value, selectedValue, StringComparison.Ordinal));
            SemanticProperties.SetDescription(button, choice.Caption);
            SemanticProperties.SetHint(button, choice.Tooltip ?? choice.Caption);
            if (galleryPreview?.Invoke(choice) is { } preview)
            {
                button.ImageSource = NeraMauiRibbonChrome.CreatePreview(preview);
                button.HeightRequest = 44d;
            }
            else if (choice.IconKey is { Length: > 0 } iconKey &&
                ResolveIcon(iconKey, 16) is ImageSource source)
            {
                button.ImageSource = source;
            }
            button.Clicked += OnChoiceClicked;
            TrackFocus(button, commandId, button.AutomationId);
            target.Children.Add(button);
        }
    }

    private Picker CreatePicker(RibbonItemLayout item)
    {
        var command = item.Presentation.Command;
        var choices = command.SelectableItems.ToArray();
        var picker = new Picker
        {
            AutomationId = $"ribbon-command-{command.CommandId.Value}",
            Title = command.Caption,
            ItemsSource = choices.Select(static choice => choice.Caption).ToArray(),
            SelectedIndex = Array.FindIndex(choices, choice => string.Equals(
                choice.Value,
                command.SelectedValue,
                StringComparison.Ordinal)),
            IsEnabled = command.IsEnabled,
            WidthRequest = item.Width / LayoutScale,
            HeightRequest = item.Height / LayoutScale,
            MinimumHeightRequest = 0d,
            MinimumWidthRequest = 0d,
            FontSize = 12d,
            TextColor = Palette.Text,
            BackgroundColor = Palette.Surface,
        };
        NeraMauiRibbonChrome.RemoveNativeMinimums(picker);
        SemanticProperties.SetDescription(picker, item.Presentation.AutomationName);
        SemanticProperties.SetHint(picker, BuildToolTip(command));
        EventHandler? selectionChanged = null;
        selectionChanged = async (_, _) =>
        {
            if (!_isRebuilding && picker.SelectedIndex >= 0)
            {
                var activated = await ActivateItemAsync(
                    command.CommandId,
                    choices[picker.SelectedIndex].Value).ConfigureAwait(true);
                if (!activated)
                {
                    picker.SelectedIndexChanged -= selectionChanged;
                    try
                    {
                        picker.SelectedIndex = Array.FindIndex(
                            choices,
                            choice => string.Equals(
                                choice.Value,
                                command.SelectedValue,
                                StringComparison.Ordinal));
                    }
                    finally
                    {
                        picker.SelectedIndexChanged += selectionChanged;
                    }
                }
            }
        };
        picker.SelectedIndexChanged += selectionChanged;
        TrackFocus(picker, command.CommandId, picker.AutomationId);
        return picker;
    }

    private Grid CreateGallery(RibbonItemLayout item)
    {
        var command = item.Presentation.Command;
        var gallery = new HorizontalStackLayout
        {
            Spacing = 2d,
        };
        SemanticProperties.SetDescription(gallery, item.Presentation.AutomationName);
        foreach (var choice in command.SelectableItems)
        {
            var button = new Button
            {
                Text = choice.Caption,
                AutomationId =
                    $"ribbon-command-{command.CommandId.Value}-choice-{choice.Value}",
                CommandParameter = new RibbonChoice(command.CommandId, choice.Value),
                IsEnabled = command.IsEnabled && choice.IsEnabled,
                WidthRequest = 76d,
                HeightRequest = Math.Max(24d, (item.Height / LayoutScale) - 12d),
                LineBreakMode = LineBreakMode.TailTruncation,
                BorderWidth = string.Equals(
                    command.SelectedValue,
                    choice.Value,
                    StringComparison.Ordinal) ? 1d : 0d,
            };
            StyleButton(button, string.Equals(command.SelectedValue, choice.Value, StringComparison.Ordinal));
            button.ContentLayout = new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Top, 2d);
            SemanticProperties.SetDescription(button, choice.Caption);
            SemanticProperties.SetHint(button, choice.Tooltip ?? choice.Caption);
            if (item.Presentation.Definition.GalleryPreview?.Invoke(choice) is { } preview)
            {
                button.ImageSource = NeraMauiRibbonChrome.CreatePreview(preview);
            }
            else if (choice.IconKey is { Length: > 0 } iconKey &&
                ResolveIcon(iconKey, 16) is ImageSource source)
            {
                button.ImageSource = source;
            }
            button.Clicked += OnChoiceClicked;
            TrackFocus(button, command.CommandId, button.AutomationId);
            gallery.Children.Add(button);
        }
        var scroll = new ScrollView
        {
            AutomationId = $"ribbon-command-{command.CommandId.Value}",
            Orientation = ScrollOrientation.Horizontal,
            Content = gallery,
            WidthRequest = Math.Max(1d, (item.Width / LayoutScale) - 18d),
            HeightRequest = item.Height / LayoutScale,
        };
        SemanticProperties.SetDescription(scroll, item.Presentation.AutomationName);
        var more = new Button
        {
            Text = "▾",
            AutomationId = $"ribbon-command-{command.CommandId.Value}-more",
            WidthRequest = 18d,
            HeightRequest = item.Height / LayoutScale,
            IsEnabled = command.IsEnabled,
        };
        StyleButton(more);
        SemanticProperties.SetDescription(more, $"{item.Presentation.AutomationName}, thêm lựa chọn");
        var choices = CreateChoiceStack(item);
        more.Clicked += (_, _) => ShowChoices(choices);
        TrackFocus(more, command.CommandId, more.AutomationId);
        var root = new Grid
        {
            AutomationId = $"ribbon-gallery-{command.CommandId.Value}",
            ColumnSpacing = 0d,
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(18d))],
        };
        root.Add(scroll, 0, 0);
        root.Add(more, 1, 0);
        return root;
    }

    private void AddOverflow(RibbonTabLayout tab)
    {
        var overflowGroups = tab.Groups
            .Where(static group => group.Mode == RibbonGroupLayoutMode.Overflow)
            .ToArray();
        if (overflowGroups.Length == 0)
        {
            _isOverflowOpen = false;
            _overflowHost.IsVisible = false;
            return;
        }

        var overflowButton = new Button
        {
            Text = "Thêm",
            AutomationId = "ribbon-overflow",
            WidthRequest = 56d,
            HeightRequest = 76d,
            Margin = new Thickness(0d, 4d, 0d, 0d),
        };
        StyleButton(overflowButton);
        SemanticProperties.SetDescription(overflowButton, "Lệnh Ribbon bổ sung");
        overflowButton.Clicked += (_, _) =>
        {
            _isOverflowOpen = !_isOverflowOpen;
            foreach (var menu in _choiceMenus)
            {
                menu.IsVisible = false;
            }
            _groupOverflowCommands.IsVisible = true;
            _overflowHost.IsVisible = _isOverflowOpen;
        };
        _groups.Children.Add(overflowButton);

        foreach (var group in overflowGroups)
        {
            _groupOverflowCommands.Children.Add(new Label
            {
                Text = group.Presentation.Caption,
                FontSize = 12d,
                TextColor = Palette.Muted,
            });
            foreach (var item in group.Items)
            {
                _groupOverflowCommands.Children.Add(CreateRibbonItem(item));
            }
        }
        _overflowHost.IsVisible = _isOverflowOpen;
    }

    private void CaptureFocus()
    {
        var focused = _focusIdentities.FirstOrDefault(static pair => pair.Key.IsFocused);
        if (focused.Key is not null)
        {
            _focusedCommandId = focused.Value.CommandId;
            _focusedSubpartId = focused.Value.SubpartId;
            _restoreCommandFocus = true;
        }
        else
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
        var exact = _focusIdentities.FirstOrDefault(pair =>
            pair.Value.CommandId == commandId &&
            string.Equals(
                pair.Value.SubpartId,
                _focusedSubpartId,
                StringComparison.Ordinal));
        var target = exact.Key ?? _focusIdentities.FirstOrDefault(pair =>
            pair.Value.CommandId == commandId).Key;
        if (target is null)
        {
            return;
        }
        if (target.Handler?.PlatformView is not null && target.Focus())
        {
            return;
        }

        EventHandler? loaded = null;
        loaded = (_, _) =>
        {
            target.Loaded -= loaded;
            if (!_disposed && _restoreCommandFocus && _focusIdentities.ContainsKey(target))
            {
                target.Focus();
            }
        };
        target.Loaded += loaded;
    }

    private void OnRibbonSizeChanged(object? sender, EventArgs e)
    {
        if (NeedsResizeLayout())
        {
            ScheduleResizeRebuild();
        }
    }

    private bool NeedsResizeLayout() => LayoutSnapshot is null ||
        !LayoutSnapshot.AvailableWidth.Equals(Width > 0d ? Width * LayoutScale : double.PositiveInfinity) ||
        !LayoutSnapshot.Scale.Equals(LayoutScale);

    private void ScheduleResizeRebuild()
    {
        if (_disposed || _resizeRebuildPending)
        {
            return;
        }
        _resizeRebuildPending = true;
        void RebuildOnce()
        {
            _resizeRebuildPending = false;
            // Height-only native layout and superseded resize callbacks must not
            // replace controls whose width/scale snapshot is already current.
            if (!_disposed && NeedsResizeLayout())
            {
                Rebuild();
            }
        }
        var dispatcher = Dispatcher;
        if (dispatcher is null)
        {
            RebuildOnce();
        }
        else if (!dispatcher.Dispatch(RebuildOnce))
        {
            _resizeRebuildPending = false;
            throw new InvalidOperationException(
                "The MAUI dispatcher rejected the Ribbon resize rebuild.");
        }
    }

    private void OnCommandFocused(object? sender, FocusEventArgs e)
    {
        if (sender is VisualElement element &&
            _focusIdentities.TryGetValue(element, out var identity))
        {
            _focusedCommandId = identity.CommandId;
            _focusedSubpartId = identity.SubpartId;
            _restoreCommandFocus = true;
        }
    }

    private void TrackFocus(
        VisualElement element,
        CommandId commandId,
        string? subpartId = null)
    {
        _focusIdentities[element] = new RibbonFocusIdentity(
            commandId,
            subpartId ?? element.AutomationId);
        TrackKeyTipFocus(element);
        element.Focused += OnCommandFocused;
        element.Unfocused += OnCommandUnfocused;
    }

    private void TrackKeyTipFocus(VisualElement element)
    {
        if (element.AutomationId is { Length: > 0 } automationId)
        {
            _keyTipFocusElements[element] = automationId;
        }
    }

    private void OnCommandUnfocused(object? sender, FocusEventArgs e)
    {
        if (!_isRebuilding)
        {
            _restoreCommandFocus = false;
        }
    }

    private ImageSource? ResolveIcon(string iconKey, int pixelSize)
    {
        var legacy = IconResolver?.Invoke(iconKey);
        if (legacy is not null)
        {
            return legacy;
        }

        var request = new NeraIconRequest(iconKey, pixelSize, IconTheme);
        return IconRequestResolver?.Invoke(request) ?? NeraMauiIconProvider.Resolve(request);
    }

    private void RebuildIfAlive()
    {
        if (!_disposed)
        {
            DispatchOrRun(Rebuild);
        }
    }

    private void OnTabClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string tabId } ||
            string.Equals(tabId, _selectedTabId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _selectedTabId = tabId;
        _isOverflowOpen = false;
        Rebuild();
    }

    private async void OnCommandClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: CommandId commandId })
        {
            await ActivateCommandAsync(commandId).ConfigureAwait(false);
        }
    }

    private async void OnChoiceClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: RibbonChoice choice })
        {
            await ActivateItemAsync(choice.CommandId, choice.Value).ConfigureAwait(false);
        }
    }

    private async ValueTask<bool> ActivateCommandAsync(CommandId commandId)
    {
        try
        {
            return await _runtime.TryActivateAsync(
                commandId,
                CommandContextFactory?.Invoke(commandId) ?? default)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            var handler = CommandActivationFailed;
            if (handler is null)
            {
                throw;
            }
            handler(this, new NeraMauiCommandActivationFailedEventArgs(
                commandId,
                exception));
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
            return await _runtime.TryActivateItemAsync(commandId, selectedValue, context)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            var handler = CommandActivationFailed;
            if (handler is null)
            {
                throw;
            }
            handler(this, new NeraMauiCommandActivationFailedEventArgs(
                commandId,
                exception));
            return false;
        }
    }

    private void OnSnapshotChanged(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }
        DispatchOrRun(Rebuild);
    }

    private void DispatchOrRun(Action action)
    {
        var dispatcher = Dispatcher;
        if (dispatcher is null || !dispatcher.IsDispatchRequired)
        {
            action();
            return;
        }
        if (!dispatcher.Dispatch(action))
        {
            throw new InvalidOperationException(
                "The MAUI dispatcher rejected the Ribbon rebuild.");
        }
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

    private NeraMauiRibbonPalette Palette => NeraMauiRibbonPalette.For(IconTheme);

    private void StyleButton(Button button, bool isChecked = false) =>
        NeraMauiRibbonChrome.Configure(button, Palette, isChecked);

    private sealed record RibbonChoice(CommandId CommandId, string Value);

    private readonly record struct RibbonFocusIdentity(
        CommandId CommandId,
        string? SubpartId);
}
