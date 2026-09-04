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
    private readonly TabControl _tabs = new();
    private readonly List<IDisposable> _shortcutBindings = [];
    private Func<string, ImageSource?>? _iconResolver;
    private Func<NeraIconRequest, ImageSource?>? _iconRequestResolver;
    private NeraIconTheme _iconTheme = NeraIconTheme.Light;
    private bool _disposed;

    public NeraRibbonControl(RibbonRuntimeController runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        Content = _tabs;
        AutomationProperties.SetAutomationId(this, "NeraRibbon");
        AutomationProperties.SetName(this, "Thanh Ribbon NeraSpreadSheet");
        _runtime.SnapshotChanged += OnSnapshotChanged;
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
        var selectedIndex = Math.Max(0, _tabs.SelectedIndex);
        _tabs.Items.Clear();
        foreach (var tab in _runtime.Snapshot.Tabs)
        {
            var groups = new WrapPanel
            {
                Margin = new Thickness(4d),
            };
            foreach (var group in tab.Groups)
            {
                var items = new WrapPanel
                {
                    Margin = new Thickness(2d),
                    VerticalAlignment = VerticalAlignment.Stretch,
                };
                foreach (var item in group.Items)
                {
                    items.Children.Add(CreateCommandButton(item));
                }

                groups.Children.Add(new GroupBox
                {
                    Header = group.Caption,
                    Content = items,
                    Margin = new Thickness(2d),
                    Padding = new Thickness(3d),
                });
            }

            var tabItem = new TabItem
            {
                Header = tab.Caption,
                Content = new ScrollViewer
                {
                    Content = groups,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                },
            };
            AutomationProperties.SetAutomationId(tabItem, $"ribbon-tab-{tab.Id}");
            AutomationProperties.SetName(tabItem, tab.Caption);
            _tabs.Items.Add(tabItem);
        }
        if (_tabs.Items.Count > 0)
        {
            _tabs.SelectedIndex = Math.Min(selectedIndex, _tabs.Items.Count - 1);
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
        foreach (var binding in _shortcutBindings)
        {
            binding.Dispose();
        }
        _shortcutBindings.Clear();
        _tabs.Items.Clear();
        GC.SuppressFinalize(this);
    }

    private ButtonBase CreateCommandButton(RibbonItemPresentation item)
    {
        var command = item.Command;
        ButtonBase button = command.IsChecked.HasValue
            ? new ToggleButton
            {
                IsChecked = command.IsChecked.Value,
            }
            : new Button();
        button.CommandParameter = command.CommandId;
        button.Content = CreateCommandContent(item);
        button.IsEnabled = command.IsEnabled;
        button.MinWidth = item.IsLarge ? 72d : 42d;
        button.MinHeight = item.IsLarge ? 58d : 30d;
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

    private StackPanel CreateCommandContent(RibbonItemPresentation item)
    {
        var command = item.Command;
        var panel = new StackPanel
        {
            Orientation = item.IsLarge ? Orientation.Vertical : Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (command.IconKey is { Length: > 0 } iconKey &&
            ResolveIcon(iconKey, item.IsLarge ? 32 : 16) is ImageSource source)
        {
            panel.Children.Add(new Image
            {
                Source = source,
                Width = item.IsLarge ? 32d : 16d,
                Height = item.IsLarge ? 32d : 16d,
                Margin = item.IsLarge
                    ? new Thickness(0d, 0d, 0d, 3d)
                    : new Thickness(0d, 0d, 4d, 0d),
                Stretch = Stretch.Uniform,
            });
        }
        panel.Children.Add(new TextBlock
        {
            Text = command.Caption,
            TextAlignment = TextAlignment.Center,
        });
        return panel;
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
