using Microsoft.Maui.Controls;
using NeraSpreadSheet.Bars.Core;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Iconography;

namespace NeraSpreadSheet.Maui;

/// <summary>
/// MAUI toolbar/menu chrome backed by the host-neutral bar runtime.
/// </summary>
public sealed class NeraMauiBarPresenter : ContentView, IDisposable
{
    private PresentationLocalization Localization => _runtime.Localization;

    private readonly BarRuntimeController _runtime;
    private readonly Microsoft.Maui.Controls.Layout _items;
    private readonly List<Button> _commandButtons = [];
    private readonly List<IDisposable> _shortcutBindings = [];
    private Func<string, ImageSource?>? _iconResolver;
    private Func<NeraIconRequest, ImageSource?>? _iconRequestResolver;
    private NeraIconTheme _iconTheme = NeraIconTheme.Light;
    private bool _disposed;

    public NeraMauiBarPresenter(BarRuntimeController runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        AutomationId = $"NeraMauiBar-{runtime.Snapshot.Id}";
        _items = CreateItemsLayout(runtime.Snapshot.Kind);
        Content = new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            Content = _items,
        };
        _runtime.SnapshotChanged += OnSnapshotChanged;
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
        BackgroundColor = NeraMauiRibbonPalette.For(IconTheme).Surface;
        _items.Children.Clear();
        _commandButtons.Clear();
        foreach (var item in _runtime.Snapshot.Items)
        {
            _items.Children.Add(CreateItem(item, level: 0));
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
        BackgroundColor = NeraMauiRibbonPalette.For(IconTheme).Surface;
        _items.Children.Clear();
        _commandButtons.Clear();
        GC.SuppressFinalize(this);
    }

    private View CreateItem(BarItemPresentation item, int level)
    {
        return item.Kind switch
        {
            BarItemKind.Separator => CreateSeparator(),
            BarItemKind.Submenu => CreateSubmenu(item, level),
            BarItemKind.Command => CreateCommandButton(item.Command!),
            _ => throw new InvalidOperationException(
                $"Unsupported bar item kind '{item.Kind}'."),
        };
    }

    private VerticalStackLayout CreateSubmenu(BarItemPresentation item, int level)
    {
        var layout = new VerticalStackLayout
        {
            Spacing = 4d,
            Padding = new Thickness(level == 0 ? 6d : 14d, 4d, 6d, 4d),
            AutomationId = $"bar-submenu-{item.Id ?? item.Caption}",
            IsEnabled = item.IsEnabled,
        };
        layout.Children.Add(new Label
        {
            Text = item.Caption,
            FontSize = 12d,
        });
        foreach (var child in item.Children)
        {
            layout.Children.Add(CreateItem(child, level + 1));
        }
        return layout;
    }

    private Button CreateCommandButton(CommandPresentation command)
    {
        var button = new Button
        {
            Padding = new Thickness(10d, 6d),
            MinimumHeightRequest = 34d,
        };
        if (command.IconKey is { Length: > 0 } iconKey &&
            ResolveIcon(iconKey) is ImageSource source)
        {
            button.ImageSource = source;
            button.ContentLayout =
                new Button.ButtonContentLayout(
                    Button.ButtonContentLayout.ImagePosition.Left,
                    4d);
        }
        NeraMauiCommandChrome.Configure(button, command, "bar-command");
        button.Clicked += OnCommandClicked;
        NeraMauiRibbonChrome.Configure(button, NeraMauiRibbonPalette.For(IconTheme), command.IsChecked == true);
        _commandButtons.Add(button);
        return button;
    }

    private ImageSource? ResolveIcon(string iconKey)
    {
        var legacy = IconResolver?.Invoke(iconKey);
        if (legacy is not null)
        {
            return legacy;
        }

        var request = new NeraIconRequest(iconKey, 16, IconTheme);
        return IconRequestResolver?.Invoke(request) ?? NeraMauiIconProvider.Resolve(request);
    }

    private void RebuildIfAlive()
    {
        if (!_disposed)
        {
            DispatchOrRun(Rebuild);
        }
    }

    private static BoxView CreateSeparator() =>
        new()
        {
            WidthRequest = 1d,
            HeightRequest = 24d,
            Color = Colors.Gray,
            Margin = new Thickness(4d, 2d),
        };

    private static Microsoft.Maui.Controls.Layout CreateItemsLayout(BarKind kind) =>
        kind == BarKind.ContextMenu
            ? new VerticalStackLayout { Spacing = 4d }
            : new HorizontalStackLayout { Spacing = 4d };

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
                "The MAUI dispatcher rejected the Bar rebuild.");
        }
    }
}
