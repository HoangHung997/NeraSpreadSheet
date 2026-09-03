using Microsoft.Maui.Controls;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Maui;

/// <summary>
/// MAUI command chrome backed by the host-neutral ribbon runtime.
/// </summary>
public sealed class NeraMauiRibbonView : ContentView, IDisposable
{
    private readonly RibbonRuntimeController _runtime;
    private readonly Grid _root = new();
    private readonly HorizontalStackLayout _tabStrip = new() { Spacing = 4d };
    private readonly HorizontalStackLayout _groups = new() { Spacing = 8d };
    private readonly List<Button> _commandButtons = [];
    private readonly List<IDisposable> _shortcutBindings = [];
    private int _selectedIndex;
    private bool _disposed;

    public NeraMauiRibbonView(RibbonRuntimeController runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        AutomationId = "NeraMauiRibbon";
        SemanticProperties.SetDescription(this, "Thanh Ribbon NeraSpreadSheet");
        _root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _root.Add(new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            Content = _tabStrip,
        }, 0, 0);
        _root.Add(new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            Content = _groups,
        }, 0, 1);
        Content = _root;
        _runtime.SnapshotChanged += OnSnapshotChanged;
        Rebuild();
    }

    public Func<string, ImageSource?>? IconResolver { get; set; }

    public Func<CommandId, CommandContext>? CommandContextFactory { get; set; }

    public IReadOnlyList<Button> CommandButtons => _commandButtons;

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
        var snapshot = _runtime.Snapshot;
        if (snapshot.Tabs.Count == 0)
        {
            _selectedIndex = 0;
        }
        else
        {
            _selectedIndex = Math.Clamp(_selectedIndex, 0, snapshot.Tabs.Count - 1);
        }
        RebuildTabs(snapshot);
        RebuildGroups(snapshot);
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
        _tabStrip.Children.Clear();
        _groups.Children.Clear();
        _commandButtons.Clear();
        GC.SuppressFinalize(this);
    }

    private void RebuildTabs(RibbonPresentationSnapshot snapshot)
    {
        _tabStrip.Children.Clear();
        for (var index = 0; index < snapshot.Tabs.Count; index++)
        {
            var tab = snapshot.Tabs[index];
            var tabIndex = index;
            var button = new Button
            {
                Text = tab.Caption,
                AutomationId = $"ribbon-tab-{tab.Id}",
                CommandParameter = tabIndex,
                IsEnabled = tabIndex != _selectedIndex,
                Padding = new Thickness(12d, 6d),
            };
            SemanticProperties.SetDescription(button, tab.Caption);
            button.Clicked += OnTabClicked;
            _tabStrip.Children.Add(button);
        }
    }

    private void RebuildGroups(RibbonPresentationSnapshot snapshot)
    {
        _groups.Children.Clear();
        _commandButtons.Clear();
        if (snapshot.Tabs.Count == 0)
        {
            return;
        }

        var tab = snapshot.Tabs[_selectedIndex];
        foreach (var group in tab.Groups)
        {
            var groupLayout = new VerticalStackLayout
            {
                Spacing = 4d,
                Padding = new Thickness(4d),
                AutomationId = $"ribbon-group-{group.Id}",
            };
            groupLayout.Children.Add(new Label
            {
                Text = group.Caption,
                FontSize = 12d,
            });
            var items = new HorizontalStackLayout { Spacing = 4d };
            foreach (var item in group.Items)
            {
                items.Children.Add(CreateCommandButton(item));
            }
            groupLayout.Children.Add(items);
            _groups.Children.Add(groupLayout);
        }
    }

    private Button CreateCommandButton(RibbonItemPresentation item)
    {
        var command = item.Command;
        var button = new Button
        {
            Padding = new Thickness(10d, 6d),
            MinimumWidthRequest = item.IsLarge ? 84d : 64d,
            MinimumHeightRequest = item.IsLarge ? 56d : 36d,
        };
        if (command.IconKey is { Length: > 0 } iconKey &&
            IconResolver?.Invoke(iconKey) is ImageSource source)
        {
            button.ImageSource = source;
            button.ContentLayout = item.IsLarge
                ? new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Top, 4d)
                : new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Left, 4d);
        }
        NeraMauiCommandChrome.Configure(
            button,
            command,
            "ribbon-command",
            item.IsLarge);
        button.Clicked += OnCommandClicked;
        _commandButtons.Add(button);
        return button;
    }

    private void OnTabClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: int index } ||
            index == _selectedIndex)
        {
            return;
        }

        _selectedIndex = index;
        Rebuild();
    }

    private async void OnCommandClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: CommandId commandId })
        {
            await ActivateCommandAsync(commandId).ConfigureAwait(false);
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
}
