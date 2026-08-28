using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Wpf;

/// <summary>
/// Native WPF ribbon chrome backed by a host-neutral ribbon runtime.
/// </summary>
public sealed class NeraRibbonControl : UserControl, IDisposable
{
    private readonly RibbonRuntimeController _runtime;
    private readonly TabControl _tabs = new();
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
    public Func<string, ImageSource?>? IconResolver { get; set; }

    /// <summary>
    /// Supplies command context at activation time.
    /// </summary>
    public Func<CommandId, CommandContext>? CommandContextFactory { get; set; }

    /// <summary>
    /// Reports an activation failure at the platform boundary.
    /// </summary>
    public event EventHandler<NeraWpfCommandActivationFailedEventArgs>? CommandActivationFailed;

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
        button.Content = CreateCommandContent(command);
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

    private StackPanel CreateCommandContent(CommandPresentation command)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        if (command.IconKey is { Length: > 0 } iconKey &&
            IconResolver?.Invoke(iconKey) is ImageSource source)
        {
            panel.Children.Add(new Image
            {
                Source = source,
                Width = 16d,
                Height = 16d,
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

    private async void OnCommandClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ButtonBase { CommandParameter: CommandId commandId })
        {
            return;
        }

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
