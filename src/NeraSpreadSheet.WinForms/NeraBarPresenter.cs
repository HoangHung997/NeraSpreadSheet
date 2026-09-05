using System.Drawing;
using System.Windows.Forms;
using NeraSpreadSheet.Bars.Core;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Iconography;

namespace NeraSpreadSheet.WinForms;

/// <summary>
/// Builds a native WinForms toolbar, menu or context menu from a bar runtime.
/// </summary>
public sealed class NeraBarPresenter : IDisposable
{
    private PresentationLocalization Localization => _runtime.Localization;

    private readonly BarRuntimeController _runtime;
    private readonly List<IDisposable> _shortcutBindings = [];
    private Func<string, Image?>? _iconResolver;
    private Func<NeraIconRequest, Image?>? _iconRequestResolver;
    private NeraIconTheme _iconTheme = NeraIconTheme.Light;
    private bool _disposed;

    public NeraBarPresenter(BarRuntimeController runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        NativeControl = CreateRoot(runtime.Snapshot.Kind);
        _runtime.SnapshotChanged += OnSnapshotChanged;
        Rebuild();
    }

    public ToolStrip NativeControl { get; }

    public Func<CommandId, CommandContext>? CommandContextFactory { get; set; }

    public Func<string, Image?>? IconResolver
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

    public Func<NeraIconRequest, Image?>? IconRequestResolver
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

    public event EventHandler<NeraWinFormsCommandActivationFailedEventArgs>? CommandActivationFailed;

    public IDisposable BindShortcuts(Control owner)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var binding = new NeraWinFormsShortcutBinding(
            owner,
            _runtime.TryResolveShortcut,
            ActivateCommandAsync);
        _shortcutBindings.Add(binding);
        return binding;
    }

    public ValueTask<bool> TryActivateShortcutAsync(string shortcut) =>
        _runtime.TryActivateShortcutAsync(shortcut);

    public void Rebuild()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var palette = NeraWinFormsRibbonPalette.For(IconTheme);
        NativeControl.BackColor = palette.Surface;
        NativeControl.ForeColor = palette.Text;
        NativeControl.Renderer = new ToolStripProfessionalRenderer(new NeraWinFormsRibbonColorTable(palette));
        var oldItems = NativeControl.Items.Cast<ToolStripItem>().ToArray();
        NativeControl.Items.Clear();
        foreach (var item in oldItems)
        {
            item.Dispose();
        }
        foreach (var item in _runtime.Snapshot.Items)
        {
            NativeControl.Items.Add(CreateItem(item, NativeControl is not MenuStrip and not ContextMenuStrip));
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
        NativeControl.Dispose();
        GC.SuppressFinalize(this);
    }

    private ToolStripItem CreateItem(BarItemPresentation item, bool toolbar)
    {
        if (item.Kind == BarItemKind.Separator)
        {
            return new ToolStripSeparator { Name = item.Id };
        }
        if (item.Kind == BarItemKind.Submenu)
        {
            var submenu = new ToolStripMenuItem(item.Caption)
            {
                Name = $"bar-submenu-{item.Id ?? item.Caption}",
                Enabled = item.IsEnabled,
                AccessibleName = item.Caption,
            };
            foreach (var child in item.Children)
            {
                submenu.DropDownItems.Add(CreateItem(child, toolbar: false));
            }
            return submenu;
        }

        var command = item.Command!;
        ToolStripItem control = toolbar
            ? new ToolStripButton(command.Caption)
            {
                CheckOnClick = false,
                Checked = command.IsChecked ?? false,
            }
            : new ToolStripMenuItem(command.Caption)
            {
                CheckOnClick = false,
                Checked = command.IsChecked ?? false,
                ShortcutKeyDisplayString = command.Shortcut ?? string.Empty,
            };
        if (control is ToolStripMenuItem menuItem &&
            NeraWinFormsShortcutBinding.TryConvertKeys(command.Shortcut, out var keys))
        {
            menuItem.ShortcutKeys = keys;
        }
        control.Name = $"bar-command-{command.CommandId.Value}";
        control.Tag = command.CommandId;
        control.Enabled = command.IsEnabled;
        control.ToolTipText = command.Tooltip;
        control.AccessibleName = command.Caption;
        control.AccessibleDescription = command.Tooltip;
        if (command.IconKey is { Length: > 0 } iconKey)
        {
            control.Image = ResolveIcon(iconKey);
        }
        control.Click += OnCommandClick;
        return control;
    }

    private Image? ResolveIcon(string iconKey)
    {
        var legacy = IconResolver?.Invoke(iconKey);
        if (legacy is not null)
        {
            return legacy;
        }

        var request = new NeraIconRequest(iconKey, 16, IconTheme);
        return IconRequestResolver?.Invoke(request) ?? NeraWinFormsIconProvider.Resolve(request);
    }

    private void RebuildIfAlive()
    {
        if (!_disposed && !NativeControl.IsDisposed)
        {
            Rebuild();
        }
    }

    private static ToolStrip CreateRoot(BarKind kind) => kind switch
    {
        BarKind.Toolbar => new ToolStrip(),
        BarKind.MainMenu => new MenuStrip(),
        BarKind.ContextMenu => new ContextMenuStrip(),
        _ => throw new InvalidOperationException($"Unsupported bar kind '{kind}'."),
    };

    private async void OnCommandClick(object? sender, EventArgs e)
    {
        if (sender is not ToolStripItem { Tag: CommandId commandId })
        {
            return;
        }
        await ActivateCommandAsync(commandId);
    }

    private async ValueTask ActivateCommandAsync(CommandId commandId)
    {
        try
        {
            await _runtime.TryActivateAsync(
                commandId,
                CommandContextFactory?.Invoke(commandId) ?? default);
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
                new NeraWinFormsCommandActivationFailedEventArgs(commandId, exception));
        }
    }

    private void OnSnapshotChanged(object? sender, EventArgs e)
    {
        if (_disposed || NativeControl.IsDisposed)
        {
            return;
        }
        if (NativeControl.IsHandleCreated)
        {
            NativeControl.BeginInvoke((Action)(() =>
            {
                if (!_disposed && !NativeControl.IsDisposed)
                {
                    Rebuild();
                }
            }));
        }
        else
        {
            Rebuild();
        }
    }
}
