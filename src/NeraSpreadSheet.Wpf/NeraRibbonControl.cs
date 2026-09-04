using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    private readonly TabControl _tabs = new();
    private readonly List<IDisposable> _shortcutBindings = [];
    private Func<string, ImageSource?>? _iconResolver;
    private Func<NeraIconRequest, ImageSource?>? _iconRequestResolver;
    private NeraIconTheme _iconTheme = NeraIconTheme.Light;
    private string? _selectedTabId;
    private CommandId? _focusedCommandId;
    private bool _disposed;

    public NeraRibbonControl(RibbonRuntimeController runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        Content = _tabs;
        AutomationProperties.SetAutomationId(this, "NeraRibbon");
        AutomationProperties.SetName(this, "Thanh Ribbon NeraSpreadSheet");
        _runtime.SnapshotChanged += OnSnapshotChanged;
        _tabs.SelectionChanged += OnTabSelectionChanged;
        SizeChanged += OnRibbonSizeChanged;
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
        _selectedTabId = LayoutSnapshot.SelectedTabId;
        _focusedCommandId = LayoutSnapshot.FocusedCommandId;
        _tabs.Items.Clear();
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
                };
                foreach (var item in group.Items)
                {
                    items.Children.Add(CreateCommandButton(item));
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
                Header = tab.Presentation.Caption,
                Content = groups,
                Tag = tab.Presentation.Id,
            };
            AutomationProperties.SetAutomationId(
                tabItem,
                $"ribbon-tab-{tab.Presentation.Id}");
            AutomationProperties.SetName(tabItem, tab.Presentation.Caption);
            _tabs.Items.Add(tabItem);
        }
        if (_tabs.Items.Count > 0 && _selectedTabId is not null)
        {
            _tabs.SelectedItem = _tabs.Items.OfType<TabItem>().First(item =>
                string.Equals(
                    item.Tag as string,
                    _selectedTabId,
                    StringComparison.OrdinalIgnoreCase));
        }
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
        foreach (var binding in _shortcutBindings)
        {
            binding.Dispose();
        }
        _shortcutBindings.Clear();
        _tabs.Items.Clear();
        GC.SuppressFinalize(this);
    }

    private ButtonBase CreateCommandButton(RibbonItemLayout item)
    {
        var command = item.Presentation.Command;
        ButtonBase button = command.IsChecked.HasValue
            ? new ToggleButton
            {
                IsChecked = command.IsChecked.Value,
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
        AutomationProperties.SetName(button, command.Caption);
        if (!string.IsNullOrWhiteSpace(command.Tooltip))
        {
            AutomationProperties.SetHelpText(button, command.Tooltip);
        }
        button.Click += OnCommandClick;
        return button;
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
        if (command.IconKey is { Length: > 0 } iconKey &&
            ResolveIcon(iconKey, isLarge ? 32 : 16) is ImageSource source)
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
            Text = item.Size == RibbonItemSize.Compact && command.IconKey is not null
                ? string.Empty
                : command.Caption,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        return panel;
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
                var commandItem = new MenuItem
                {
                    Header = command.Caption,
                    CommandParameter = command.CommandId,
                    Tag = command.CommandId,
                    IsEnabled = command.IsEnabled,
                    IsCheckable = command.IsChecked.HasValue,
                    IsChecked = command.IsChecked ?? false,
                    ToolTip = BuildToolTip(command),
                };
                AutomationProperties.SetAutomationId(
                    commandItem,
                    $"ribbon-command-{command.CommandId.Value}");
                AutomationProperties.SetName(commandItem, command.Caption);
                commandItem.Click += OnOverflowCommandClick;
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
        if (System.Windows.Input.Keyboard.FocusedElement is FrameworkElement
            { Tag: CommandId focusedId })
        {
            _focusedCommandId = focusedId;
        }
    }

    private void RestoreFocus()
    {
        if (_focusedCommandId is not { } commandId)
        {
            return;
        }
        var target = FindVisualDescendants<FrameworkElement>(_tabs)
            .FirstOrDefault(element => element.Tag is CommandId id && id == commandId);
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
            Rebuild();
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
