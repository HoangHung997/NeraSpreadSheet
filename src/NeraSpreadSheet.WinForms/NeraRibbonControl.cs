using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Iconography;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.WinForms;

/// <summary>
/// Native WinForms ribbon chrome backed by a host-neutral ribbon runtime.
/// </summary>
public sealed class NeraRibbonControl : UserControl
{
    private readonly RibbonRuntimeController _runtime;
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly ToolTip _toolTip = new();
    private readonly List<IDisposable> _shortcutBindings = [];
    private Func<string, Image?>? _iconResolver;
    private Func<NeraIconRequest, Image?>? _iconRequestResolver;
    private NeraIconTheme _iconTheme = NeraIconTheme.Light;
    private bool _disposed;

    public NeraRibbonControl(RibbonRuntimeController runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        Name = "NeraRibbon";
        AccessibleName = "Thanh Ribbon NeraSpreadSheet";
        Controls.Add(_tabs);
        _runtime.SnapshotChanged += OnSnapshotChanged;
        Rebuild();
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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

    [DefaultValue(NeraIconTheme.Light)]
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

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<CommandId, CommandContext>? CommandContextFactory { get; set; }

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
        var oldPages = _tabs.TabPages.Cast<TabPage>().ToArray();
        _tabs.TabPages.Clear();
        _toolTip.RemoveAll();
        foreach (var page in oldPages)
        {
            page.Dispose();
        }
        foreach (var tab in _runtime.Snapshot.Tabs)
        {
            var page = new TabPage(tab.Caption)
            {
                Name = $"ribbon-tab-{tab.Id}",
                AutoScroll = true,
            };
            var groups = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
            };
            foreach (var group in tab.Groups)
            {
                var box = new GroupBox
                {
                    Text = group.Caption,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Padding = new Padding(6),
                    Margin = new Padding(3),
                };
                var items = new FlowLayoutPanel
                {
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    WrapContents = false,
                };
                foreach (var item in group.Items)
                {
                    items.Controls.Add(CreateCommandButton(item));
                }
                box.Controls.Add(items);
                groups.Controls.Add(box);
            }
            page.Controls.Add(groups);
            _tabs.TabPages.Add(page);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _runtime.SnapshotChanged -= OnSnapshotChanged;
            foreach (var binding in _shortcutBindings)
            {
                binding.Dispose();
            }
            _shortcutBindings.Clear();
            _toolTip.Dispose();
        }
        base.Dispose(disposing);
    }

    private ButtonBase CreateCommandButton(RibbonItemPresentation item)
    {
        var command = item.Command;
        ButtonBase button = command.IsChecked.HasValue
            ? new CheckBox
            {
                Appearance = Appearance.Button,
                AutoCheck = false,
                Checked = command.IsChecked.Value,
                TextAlign = ContentAlignment.MiddleCenter,
            }
            : new Button();
        button.Name = $"ribbon-command-{command.CommandId.Value}";
        button.Text = command.Caption;
        button.Tag = command.CommandId;
        button.Enabled = command.IsEnabled;
        button.AutoSize = false;
        button.Size = item.IsLarge ? new Size(84, 58) : new Size(72, 30);
        button.Margin = new Padding(2);
        button.AccessibleName = command.Caption;
        button.AccessibleDescription = command.Tooltip;
        if (command.IconKey is { Length: > 0 } iconKey &&
            ResolveIcon(iconKey, item.IsLarge ? 32 : 16) is Image image)
        {
            button.Image = image;
            button.TextImageRelation = item.IsLarge
                ? TextImageRelation.ImageAboveText
                : TextImageRelation.ImageBeforeText;
        }
        if (!string.IsNullOrWhiteSpace(command.Tooltip) ||
            !string.IsNullOrWhiteSpace(command.Shortcut))
        {
            _toolTip.SetToolTip(
                button,
                string.Join(
                    " ",
                    new[] { command.Tooltip, command.Shortcut }
                        .Where(static value => !string.IsNullOrWhiteSpace(value))));
        }
        button.Click += OnCommandClick;
        return button;
    }

    private Image? ResolveIcon(string iconKey, int pixelSize)
    {
        var legacy = IconResolver?.Invoke(iconKey);
        if (legacy is not null)
        {
            return legacy;
        }

        var request = new NeraIconRequest(iconKey, pixelSize, IconTheme);
        return IconRequestResolver?.Invoke(request) ?? NeraWinFormsIconProvider.Resolve(request);
    }

    private void RebuildIfAlive()
    {
        if (!_disposed && !IsDisposed)
        {
            Rebuild();
        }
    }

    private async void OnCommandClick(object? sender, EventArgs e)
    {
        if (sender is not ButtonBase { Tag: CommandId commandId })
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
        if (_disposed || IsDisposed)
        {
            return;
        }
        if (IsHandleCreated)
        {
            BeginInvoke((Action)(() =>
            {
                if (!_disposed && !IsDisposed)
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

public sealed class NeraWinFormsCommandActivationFailedEventArgs : EventArgs
{
    public NeraWinFormsCommandActivationFailedEventArgs(
        CommandId commandId,
        Exception exception)
    {
        CommandId = commandId;
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
    }

    public CommandId CommandId { get; }

    public Exception Exception { get; }
}
