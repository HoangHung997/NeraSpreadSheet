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
    private readonly HorizontalStackLayout _tabStrip = new() { Spacing = 4d };
    private readonly HorizontalStackLayout _groups = new() { Spacing = 8d };
    private readonly VerticalStackLayout _overflowCommands = new()
    {
        Spacing = 4d,
    };
    private readonly ScrollView _overflowHost;
    private readonly List<Button> _commandButtons = [];
    private readonly List<VisualElement> _itemControls = [];
    private readonly Dictionary<VisualElement, CommandId> _focusIdentities = [];
    private readonly List<IDisposable> _shortcutBindings = [];
    private Func<string, ImageSource?>? _iconResolver;
    private Func<NeraIconRequest, ImageSource?>? _iconRequestResolver;
    private NeraIconTheme _iconTheme = NeraIconTheme.Light;
    private string? _selectedTabId;
    private CommandId? _focusedCommandId;
    private double _layoutScale = 1d;
    private bool _restoreCommandFocus;
    private bool _isRebuilding;
    private bool _resizeRebuildPending;
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
        };
        AutomationId = "NeraMauiRibbon";
        SemanticProperties.SetDescription(this, "Thanh Ribbon NeraSpreadSheet");
        _root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _root.Add(new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            Content = _tabStrip,
        }, 0, 0);
        _root.Add(_groups, 0, 1);
        _root.Add(_overflowHost, 0, 2);
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
                    _focusedCommandId));
            _selectedTabId = LayoutSnapshot.SelectedTabId;
            _focusedCommandId = LayoutSnapshot.FocusedCommandId;
            RebuildTabs(LayoutSnapshot);
            RebuildGroups(LayoutSnapshot);
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
        _groups.Children.Clear();
        _overflowCommands.Children.Clear();
        _commandButtons.Clear();
        _itemControls.Clear();
        _focusIdentities.Clear();
        GC.SuppressFinalize(this);
    }

    private void RebuildTabs(RibbonLayoutSnapshot snapshot)
    {
        _tabStrip.Children.Clear();
        for (var index = 0; index < snapshot.Tabs.Count; index++)
        {
            var tab = snapshot.Tabs[index];
            var button = new Button
            {
                Text = tab.Presentation.Caption,
                AutomationId = $"ribbon-tab-{tab.Presentation.Id}",
                CommandParameter = tab.Presentation.Id,
                IsEnabled = !string.Equals(
                    tab.Presentation.Id,
                    snapshot.SelectedTabId,
                    StringComparison.OrdinalIgnoreCase),
                Padding = new Thickness(12d, 6d),
            };
            SemanticProperties.SetDescription(button, tab.Presentation.Caption);
            button.Clicked += OnTabClicked;
            _tabStrip.Children.Add(button);
        }
    }

    private void RebuildGroups(RibbonLayoutSnapshot snapshot)
    {
        _groups.Children.Clear();
        _overflowCommands.Children.Clear();
        _overflowCommands.IsVisible = false;
        _commandButtons.Clear();
        _itemControls.Clear();
        _focusIdentities.Clear();
        if (snapshot.Tabs.Count == 0)
        {
            return;
        }

        var tab = snapshot.Tabs.First(tab => string.Equals(
            tab.Presentation.Id,
            snapshot.SelectedTabId,
            StringComparison.OrdinalIgnoreCase));
        foreach (var group in tab.Groups.Where(static group =>
                     group.Mode != RibbonGroupLayoutMode.Overflow))
        {
            var groupLayout = new VerticalStackLayout
            {
                Spacing = 4d,
                Padding = new Thickness(4d),
                AutomationId = $"ribbon-group-{group.Presentation.Id}",
            };
            groupLayout.Children.Add(new Label
            {
                Text = group.Presentation.Caption,
                FontSize = 12d,
            });
            var items = new HorizontalStackLayout { Spacing = 4d };
            foreach (var item in group.Items)
            {
                items.Children.Add(CreateRibbonItem(item));
            }
            groupLayout.Children.Add(items);
            _groups.Children.Add(groupLayout);
        }
        AddOverflow(tab);
    }

    private Button CreateCommandButton(RibbonItemLayout item)
    {
        var command = item.Presentation.Command;
        var button = new Button
        {
            Padding = new Thickness(10d, 6d),
            WidthRequest = item.Width / LayoutScale,
            MinimumHeightRequest = item.Size == RibbonItemSize.Large ? 56d : 36d,
        };
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
            item.Size == RibbonItemSize.Large);
        SemanticProperties.SetDescription(button, item.Presentation.AutomationName);
        if (item.Size == RibbonItemSize.Compact && resolvedIcon is not null)
        {
            button.Text = string.Empty;
        }
        button.Clicked += OnCommandClicked;
        TrackFocus(button, command.CommandId);
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
        _itemControls.Add(view);
        return view;
    }

    private BoxView CreateSeparator(RibbonItemLayout item) => new()
    {
        AutomationId = $"ribbon-command-{item.Presentation.Command.CommandId.Value}",
        WidthRequest = Math.Max(1d, item.Width / LayoutScale),
        HeightRequest = 36d,
        Color = Colors.Gray,
        Margin = new Thickness(4d, 6d),
    };

    private VerticalStackLayout CreateSplitButton(RibbonItemLayout item)
    {
        var choices = CreateChoiceStack(item);
        var menuButton = new Button
        {
            Text = "▼",
            AutomationId = $"ribbon-menu-{item.Presentation.Command.CommandId.Value}",
            IsEnabled = item.Presentation.Command.IsEnabled,
            WidthRequest = 36d,
            Padding = new Thickness(4d),
        };
        menuButton.Clicked += (_, _) => choices.IsVisible = !choices.IsVisible;
        TrackFocus(menuButton, item.Presentation.Command.CommandId);
        var row = new HorizontalStackLayout { Spacing = 0d };
        var primary = CreateCommandButton(item);
        primary.WidthRequest = Math.Max(1d, item.Width / LayoutScale - 36d);
        row.Children.Add(primary);
        row.Children.Add(menuButton);
        var stack = new VerticalStackLayout
        {
            Spacing = 2d,
            WidthRequest = item.Width / LayoutScale,
        };
        stack.Children.Add(row);
        stack.Children.Add(choices);
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
            Padding = new Thickness(8d, 6d),
            CommandParameter = command.CommandId,
        };
        SemanticProperties.SetDescription(button, item.Presentation.AutomationName);
        SemanticProperties.SetHint(button, BuildToolTip(command));
        if (command.IconKey is { Length: > 0 } iconKey &&
            ResolveIcon(iconKey, 16) is ImageSource source)
        {
            button.ImageSource = source;
            button.ContentLayout = new Button.ButtonContentLayout(
                Button.ButtonContentLayout.ImagePosition.Left,
                4d);
        }
        button.Clicked += (_, _) => choices.IsVisible = !choices.IsVisible;
        TrackFocus(button, command.CommandId);
        var stack = new VerticalStackLayout { Spacing = 2d };
        stack.Children.Add(button);
        stack.Children.Add(choices);
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
        AddChoiceButtons(choices, command.CommandId, command.SelectableItems, 0);
        return choices;
    }

    private void AddChoiceButtons(
        VerticalStackLayout target,
        CommandId commandId,
        IReadOnlyList<CommandItem> choices,
        int depth)
    {
        foreach (var choice in choices)
        {
            if (choice.Children.Count > 0)
            {
                target.Children.Add(new Label
                {
                    Text = choice.Caption,
                    Margin = new Thickness(depth * 12d, 2d, 0d, 0d),
                });
                AddChoiceButtons(target, commandId, choice.Children, depth + 1);
                continue;
            }
            var button = new Button
            {
                Text = choice.Caption,
                CommandParameter = new RibbonChoice(commandId, choice.Value),
                IsEnabled = choice.IsEnabled,
                Padding = new Thickness(8d, 4d),
                Margin = new Thickness(depth * 12d, 0d, 0d, 0d),
            };
            SemanticProperties.SetDescription(button, choice.Caption);
            SemanticProperties.SetHint(button, choice.Tooltip ?? choice.Caption);
            if (choice.IconKey is { Length: > 0 } iconKey &&
                ResolveIcon(iconKey, 16) is ImageSource source)
            {
                button.ImageSource = source;
            }
            button.Clicked += OnChoiceClicked;
            TrackFocus(button, commandId);
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
        };
        SemanticProperties.SetDescription(picker, item.Presentation.AutomationName);
        SemanticProperties.SetHint(picker, BuildToolTip(command));
        picker.SelectedIndexChanged += async (_, _) =>
        {
            if (!_isRebuilding && picker.SelectedIndex >= 0)
            {
                await ActivateItemAsync(
                    command.CommandId,
                    choices[picker.SelectedIndex].Value).ConfigureAwait(false);
            }
        };
        TrackFocus(picker, command.CommandId);
        return picker;
    }

    private HorizontalStackLayout CreateGallery(RibbonItemLayout item)
    {
        var command = item.Presentation.Command;
        var gallery = new HorizontalStackLayout
        {
            AutomationId = $"ribbon-command-{command.CommandId.Value}",
            Spacing = 2d,
            WidthRequest = item.Width / LayoutScale,
        };
        SemanticProperties.SetDescription(gallery, item.Presentation.AutomationName);
        foreach (var choice in command.SelectableItems)
        {
            var button = new Button
            {
                Text = choice.Caption,
                CommandParameter = new RibbonChoice(command.CommandId, choice.Value),
                IsEnabled = command.IsEnabled && choice.IsEnabled,
                Padding = new Thickness(6d, 4d),
                BorderWidth = string.Equals(
                    command.SelectedValue,
                    choice.Value,
                    StringComparison.Ordinal) ? 2d : 0d,
            };
            SemanticProperties.SetDescription(button, choice.Caption);
            if (choice.IconKey is { Length: > 0 } iconKey &&
                ResolveIcon(iconKey, 16) is ImageSource source)
            {
                button.ImageSource = source;
            }
            button.Clicked += OnChoiceClicked;
            TrackFocus(button, command.CommandId);
            gallery.Children.Add(button);
        }
        return gallery;
    }

    private void AddOverflow(RibbonTabLayout tab)
    {
        var overflowGroups = tab.Groups
            .Where(static group => group.Mode == RibbonGroupLayoutMode.Overflow)
            .ToArray();
        if (overflowGroups.Length == 0)
        {
            return;
        }

        var overflowButton = new Button
        {
            Text = "Thêm",
            AutomationId = "ribbon-overflow",
            WidthRequest = 56d,
            Padding = new Thickness(8d, 6d),
        };
        SemanticProperties.SetDescription(overflowButton, "Lệnh Ribbon bổ sung");
        overflowButton.Clicked += (_, _) =>
            _overflowHost.IsVisible = !_overflowHost.IsVisible;
        _groups.Children.Add(overflowButton);

        foreach (var group in overflowGroups)
        {
            _overflowCommands.Children.Add(new Label
            {
                Text = group.Presentation.Caption,
                FontSize = 12d,
            });
            foreach (var item in group.Items)
            {
                _overflowCommands.Children.Add(CreateRibbonItem(item));
            }
        }
    }

    private void CaptureFocus()
    {
        var focused = _focusIdentities.FirstOrDefault(static pair => pair.Key.IsFocused);
        if (focused.Key is not null)
        {
            _focusedCommandId = focused.Value;
            _restoreCommandFocus = true;
        }
    }

    private void RestoreFocus()
    {
        if (!_restoreCommandFocus || _focusedCommandId is not { } commandId)
        {
            return;
        }
        _focusIdentities.FirstOrDefault(pair => pair.Value == commandId).Key?.Focus();
    }

    private void OnRibbonSizeChanged(object? sender, EventArgs e)
    {
        ScheduleResizeRebuild();
    }

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
            if (!_disposed)
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
            _focusIdentities.TryGetValue(element, out var commandId))
        {
            _focusedCommandId = commandId;
            _restoreCommandFocus = true;
        }
    }

    private void TrackFocus(VisualElement element, CommandId commandId)
    {
        _focusIdentities[element] = commandId;
        element.Focused += OnCommandFocused;
        element.Unfocused += OnCommandUnfocused;
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

    private sealed record RibbonChoice(CommandId CommandId, string Value);
}
