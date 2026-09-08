using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Input;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;
using NeraSpreadSheet.Bars.Core;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Iconography;

namespace NeraSpreadSheet.Avalonia;

/// <summary>Native toolbar, menu and context-menu projection of a shared Bar runtime.
/// The presenter owns its native controls and images, but never the supplied runtime.</summary>
public sealed class NeraBarPresenter : IDisposable
{
    private readonly BarRuntimeController _runtime;
    private readonly NeraAvaloniaIconProvider _icons = new();
    private readonly List<IDisposable> _shortcutBindings = [];
    private int _refreshQueued;
    private bool _disposed;
    private NeraIconTheme _iconTheme = NeraIconTheme.Light;

    public NeraBarPresenter(BarRuntimeController runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        NativeControl = runtime.Snapshot.Kind switch
        {
            BarKind.Toolbar => new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 },
            BarKind.MainMenu => new Menu(),
            BarKind.ContextMenu => new ContextMenu(),
            _ => throw new ArgumentOutOfRangeException(nameof(runtime)),
        };
        _runtime.SnapshotChanged += OnSnapshotChanged;
        Rebuild();
    }

    public Control NativeControl { get; }
    public BarRuntimeController Runtime => _runtime;
    public Func<CommandId, CommandContext>? CommandContextFactory { get; set; }
    public Func<NeraIconRequest, IImage?>? IconRequestResolver { get; set; }
    /// <summary>Returning null cancels activation before the shared command dispatcher.</summary>
    public Func<CommandId, CommandContext, ValueTask<CommandContext?>>? ActivationContextProvider { get; set; }
    public event EventHandler<NeraAvaloniaCommandActivationFailedEventArgs>? CommandActivationFailed;

    public NeraIconTheme IconTheme
    {
        get => _iconTheme;
        set { VerifyUsable(); if (_iconTheme != value) { _iconTheme = value; Rebuild(); } }
    }

    public IDisposable BindShortcuts(InputElement owner)
    {
        VerifyUsable();
        var binding = new NeraAvaloniaShortcutBinding(owner, _runtime.TryResolveShortcut, ActivateCommandAsync);
        _shortcutBindings.Add(binding);
        return binding;
    }

    public ValueTask<bool> TryActivateShortcutAsync(string shortcut)
    {
        VerifyUsable();
        return _runtime.TryResolveShortcut(shortcut, out var id)
            ? ActivateCommandAsync(id) : ValueTask.FromResult(false);
    }

    public async ValueTask<bool> ActivateCommandAsync(CommandId commandId)
    {
        VerifyUsable();
        try
        {
            var context = CommandContextFactory?.Invoke(commandId) ?? default;
            if (ActivationContextProvider is { } prepare)
            {
                var prepared = await prepare(commandId, context);
                if (_disposed || prepared is null) return false;
                context = prepared.Value;
            }
            return await _runtime.TryActivateAsync(commandId, context);
        }
        catch (Exception exception)
        {
            if (CommandActivationFailed is not { } handler) throw;
            handler(this, new NeraAvaloniaCommandActivationFailedEventArgs(commandId, exception));
            return false;
        }
    }

    public void Rebuild()
    {
        VerifyUsable();
        Interlocked.Exchange(ref _refreshQueued, 0);
        var focused = TopLevel.GetTopLevel(NativeControl)?.FocusManager?.GetFocusedElement();
        var focusId = focused is Control control && NativeControl.GetVisualDescendants().Contains(control)
            ? AutomationProperties.GetAutomationId(control) : null;
        CloseMenus();
        if (NativeControl is StackPanel panel)
        {
            panel.Children.Clear();
            foreach (var item in _runtime.Snapshot.Items) panel.Children.Add(CreateItem(item, true));
        }
        else if (NativeControl is ItemsControl items)
        {
            items.Items.Clear();
            foreach (var item in _runtime.Snapshot.Items) items.Items.Add(CreateItem(item, false));
        }
        if (!string.IsNullOrEmpty(focusId))
            NativeControl.GetVisualDescendants().OfType<Control>()
                .FirstOrDefault(item => AutomationProperties.GetAutomationId(item) == focusId)?.Focus();
    }

    private Control CreateItem(BarItemPresentation item, bool toolbar)
    {
        if (item.Kind == BarItemKind.Separator)
            return toolbar ? new Border { Width = 1, Background = Brushes.Gray, Margin = new Thickness(3) } : new Separator();
        if (item.Kind == BarItemKind.Submenu)
        {
            var menu = new MenuItem { Header = item.Caption, IsEnabled = item.IsEnabled };
            foreach (var child in item.Children) menu.Items.Add(CreateItem(child, false));
            if (!toolbar) return menu;
            var button = new Button { Content = item.Caption + " ⌄", IsEnabled = item.IsEnabled };
            var popup = new ContextMenu();
            foreach (var child in item.Children) popup.Items.Add(CreateItem(child, false));
            button.ContextMenu = popup;
            button.Click += (_, _) => popup.Open(button);
            AutomationProperties.SetName(button, item.Caption);
            return button;
        }
        var command = item.Command!;
        if (!toolbar)
        {
            var native = new MenuItem
            {
                Header = command.Caption,
                IsEnabled = command.IsEnabled,
                ToggleType = command.IsChecked.HasValue ? MenuItemToggleType.CheckBox : MenuItemToggleType.None,
                IsChecked = command.IsChecked ?? false,
                Icon = CreateIcon(command),
            };
            native.Click += async (_, e) => { e.Handled = true; await ActivateCommandAsync(command.CommandId); };
            Configure(native, command);
            return native;
        }
        Button action = command.IsChecked.HasValue ? new ToggleButton { IsChecked = command.IsChecked.Value } : new Button();
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        if (CreateIcon(command) is { } image) content.Children.Add(image);
        content.Children.Add(new TextBlock { Text = command.Caption });
        action.Content = content;
        action.IsEnabled = command.IsEnabled;
        action.Click += async (_, _) => await ActivateCommandAsync(command.CommandId);
        Configure(action, command);
        return action;
    }

    private Image? CreateIcon(CommandPresentation command)
    {
        if (command.IconKey is not { } key) return null;
        var request = new NeraIconRequest(key, 16, _iconTheme);
        var image = IconRequestResolver?.Invoke(request) ?? _icons.Resolve(request);
        return image is null ? null : new Image { Source = image, Width = 16, Height = 16 };
    }

    private static void Configure(Control control, CommandPresentation command)
    {
        AutomationProperties.SetAutomationId(control, "bar-command-" + command.CommandId.Value);
        AutomationProperties.SetName(control, command.Caption);
        ToolTip.SetTip(control, (command.Tooltip ?? command.Caption) +
            (string.IsNullOrWhiteSpace(command.Shortcut) ? string.Empty : $" ({command.Shortcut})"));
    }

    private void OnSnapshotChanged(object? sender, EventArgs e)
    {
        if (Volatile.Read(ref _disposed) || Interlocked.Exchange(ref _refreshQueued, 1) != 0) return;
        Dispatcher.UIThread.Post(() => { if (!_disposed && Volatile.Read(ref _refreshQueued) != 0) Rebuild(); });
    }

    private void CloseMenus()
    {
        if (NativeControl is ContextMenu root) root.Close();
        foreach (var control in NativeControl.GetVisualDescendants().OfType<Control>()) control.ContextMenu?.Close();
    }

    private void VerifyUsable() { NativeControl.VerifyAccess(); ObjectDisposedException.ThrowIf(_disposed, this); }

    public void Dispose()
    {
        NativeControl.VerifyAccess();
        if (_disposed) return;
        _disposed = true;
        _runtime.SnapshotChanged -= OnSnapshotChanged;
        foreach (var binding in _shortcutBindings) binding.Dispose();
        _shortcutBindings.Clear();
        CloseMenus();
        if (NativeControl is StackPanel panel) panel.Children.Clear();
        else if (NativeControl is ItemsControl items) items.Items.Clear();
        _icons.Dispose();
    }
}

public sealed class NeraAvaloniaCommandActivationFailedEventArgs : EventArgs
{
    public NeraAvaloniaCommandActivationFailedEventArgs(CommandId commandId, Exception exception)
    {
        CommandId = commandId;
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
    }
    public CommandId CommandId { get; }
    public Exception Exception { get; }
}
