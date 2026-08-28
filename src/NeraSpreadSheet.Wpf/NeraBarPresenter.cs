using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using NeraSpreadSheet.Bars.Core;
using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Wpf;

/// <summary>
/// Builds a native WPF toolbar, menu or context menu from a bar runtime.
/// </summary>
public sealed class NeraBarPresenter : IDisposable
{
    private readonly BarRuntimeController _runtime;
    private bool _disposed;

    public NeraBarPresenter(BarRuntimeController runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        NativeControl = CreateRoot(runtime.Snapshot.Kind);
        _runtime.SnapshotChanged += OnSnapshotChanged;
        Rebuild();
    }

    public ItemsControl NativeControl { get; }

    public Func<CommandId, CommandContext>? CommandContextFactory { get; set; }

    public Func<string, ImageSource?>? IconResolver { get; set; }

    public event EventHandler<NeraWpfCommandActivationFailedEventArgs>? CommandActivationFailed;

    public void Rebuild()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        NativeControl.Items.Clear();
        foreach (var item in _runtime.Snapshot.Items)
        {
            NativeControl.Items.Add(CreateItem(item, NativeControl is ToolBar));
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
        NativeControl.Items.Clear();
        GC.SuppressFinalize(this);
    }

    private object CreateItem(BarItemPresentation item, bool toolbar)
    {
        if (item.Kind == BarItemKind.Separator)
        {
            return new Separator();
        }
        if (item.Kind == BarItemKind.Submenu)
        {
            var submenu = new MenuItem
            {
                Header = item.Caption,
                IsEnabled = item.IsEnabled,
            };
            AutomationProperties.SetAutomationId(
                submenu,
                $"bar-submenu-{item.Id ?? item.Caption}");
            foreach (var child in item.Children)
            {
                submenu.Items.Add(CreateItem(child, toolbar: false));
            }
            return submenu;
        }

        var command = item.Command!;
        if (!toolbar)
        {
            var menuItem = new MenuItem
            {
                Header = command.Caption,
                CommandParameter = command.CommandId,
                IsEnabled = command.IsEnabled,
                IsCheckable = command.IsChecked.HasValue,
                IsChecked = command.IsChecked ?? false,
                InputGestureText = command.Shortcut ?? string.Empty,
                ToolTip = command.Tooltip,
            };
            menuItem.Icon = CreateIcon(command);
            ConfigureCommand(menuItem, command);
            menuItem.Click += OnCommandClick;
            return menuItem;
        }

        ButtonBase button = command.IsChecked.HasValue
            ? new ToggleButton { IsChecked = command.IsChecked.Value }
            : new Button();
        button.Content = CreateToolbarContent(command);
        button.CommandParameter = command.CommandId;
        button.IsEnabled = command.IsEnabled;
        button.ToolTip = command.Tooltip;
        ConfigureCommand(button, command);
        button.Click += OnCommandClick;
        return button;
    }

    private static ItemsControl CreateRoot(BarKind kind) => kind switch
    {
        BarKind.Toolbar => new ToolBar(),
        BarKind.MainMenu => new Menu(),
        BarKind.ContextMenu => new ContextMenu(),
        _ => throw new InvalidOperationException($"Unsupported bar kind '{kind}'."),
    };

    private static void ConfigureCommand(
        FrameworkElement element,
        CommandPresentation command)
    {
        AutomationProperties.SetAutomationId(
            element,
            $"bar-command-{command.CommandId.Value}");
        AutomationProperties.SetName(element, command.Caption);
        if (!string.IsNullOrWhiteSpace(command.Tooltip))
        {
            AutomationProperties.SetHelpText(element, command.Tooltip);
        }
    }

    private async void OnCommandClick(object sender, RoutedEventArgs e)
    {
        var commandId = sender switch
        {
            ButtonBase { CommandParameter: CommandId buttonCommandId } =>
                buttonCommandId,
            MenuItem { CommandParameter: CommandId menuCommandId } =>
                menuCommandId,
            _ => (CommandId?)null,
        };
        if (commandId is not CommandId id)
        {
            return;
        }
        try
        {
            await _runtime.TryActivateAsync(
                id,
                CommandContextFactory?.Invoke(id) ?? default);
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
                new NeraWpfCommandActivationFailedEventArgs(id, exception));
        }
    }

    private void OnSnapshotChanged(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }
        _ = NativeControl.Dispatcher.BeginInvoke(() =>
        {
            if (!_disposed)
            {
                Rebuild();
            }
        });
    }

    private Image? CreateIcon(CommandPresentation command)
    {
        if (command.IconKey is not { Length: > 0 } iconKey ||
            IconResolver?.Invoke(iconKey) is not ImageSource source)
        {
            return null;
        }
        return new Image
        {
            Source = source,
            Width = 16d,
            Height = 16d,
            Stretch = Stretch.Uniform,
        };
    }

    private StackPanel CreateToolbarContent(CommandPresentation command)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
        };
        if (CreateIcon(command) is Image icon)
        {
            icon.Margin = new Thickness(0d, 0d, 4d, 0d);
            panel.Children.Add(icon);
        }
        panel.Children.Add(new TextBlock { Text = command.Caption });
        return panel;
    }
}
