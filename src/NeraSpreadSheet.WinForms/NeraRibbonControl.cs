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
    private readonly RibbonResponsiveLayoutEngine _layoutEngine = new();
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly ToolTip _toolTip = new();
    private readonly List<ContextMenuStrip> _overflowMenus = [];
    private readonly List<IDisposable> _shortcutBindings = [];
    private Func<string, Image?>? _iconResolver;
    private Func<NeraIconRequest, Image?>? _iconRequestResolver;
    private NeraIconTheme _iconTheme = NeraIconTheme.Light;
    private string? _selectedTabId;
    private CommandId? _focusedCommandId;
    private bool _disposed;

    public NeraRibbonControl(RibbonRuntimeController runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        Name = "NeraRibbon";
        AccessibleName = "Thanh Ribbon NeraSpreadSheet";
        Controls.Add(_tabs);
        _runtime.SnapshotChanged += OnSnapshotChanged;
        _tabs.SelectedIndexChanged += OnSelectedIndexChanged;
        Resize += OnRibbonResize;
        DpiChangedAfterParent += OnRibbonDpiChanged;
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

    /// <summary>
    /// Gets the latest host-neutral responsive layout consumed by this presenter.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public RibbonLayoutSnapshot LayoutSnapshot { get; private set; } = null!;

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
        CaptureIdentities();
        var scale = DeviceDpi / 96d;
        LayoutSnapshot = _layoutEngine.Layout(
            _runtime.Snapshot,
            new RibbonLayoutRequest(
                ClientSize.Width > 0 ? ClientSize.Width : double.PositiveInfinity,
                scale,
                _selectedTabId,
                _focusedCommandId));
        _selectedTabId = LayoutSnapshot.SelectedTabId;
        _focusedCommandId = LayoutSnapshot.FocusedCommandId;
        var oldPages = _tabs.TabPages.Cast<TabPage>().ToArray();
        _tabs.TabPages.Clear();
        _toolTip.RemoveAll();
        foreach (var menu in _overflowMenus)
        {
            menu.Dispose();
        }
        _overflowMenus.Clear();
        foreach (var page in oldPages)
        {
            page.Dispose();
        }
        foreach (var tab in LayoutSnapshot.Tabs)
        {
            var page = new TabPage(tab.Presentation.Caption)
            {
                Name = $"ribbon-tab-{tab.Presentation.Id}",
                Tag = tab.Presentation.Id,
                AutoScroll = false,
            };
            var groups = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = false,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
            };
            foreach (var group in tab.Groups.Where(static group =>
                         group.Mode != RibbonGroupLayoutMode.Overflow))
            {
                var box = new GroupBox
                {
                    Text = group.Presentation.Caption,
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
            AddOverflowButton(groups, tab);
            page.Controls.Add(groups);
            _tabs.TabPages.Add(page);
        }
        if (_selectedTabId is not null)
        {
            var selected = _tabs.TabPages.Cast<TabPage>().FirstOrDefault(page =>
                string.Equals(
                    page.Tag as string,
                    _selectedTabId,
                    StringComparison.OrdinalIgnoreCase));
            if (selected is not null)
            {
                _tabs.SelectedTab = selected;
            }
        }
        RestoreFocus();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _runtime.SnapshotChanged -= OnSnapshotChanged;
            _tabs.SelectedIndexChanged -= OnSelectedIndexChanged;
            Resize -= OnRibbonResize;
            DpiChangedAfterParent -= OnRibbonDpiChanged;
            foreach (var binding in _shortcutBindings)
            {
                binding.Dispose();
            }
            _shortcutBindings.Clear();
            foreach (var menu in _overflowMenus)
            {
                menu.Dispose();
            }
            _overflowMenus.Clear();
            _toolTip.Dispose();
        }
        base.Dispose(disposing);
    }

    private ButtonBase CreateCommandButton(RibbonItemLayout item)
    {
        var command = item.Presentation.Command;
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
        button.Text = item.Size == RibbonItemSize.Compact && command.IconKey is not null
            ? string.Empty
            : command.Caption;
        button.Tag = command.CommandId;
        button.Enabled = command.IsEnabled;
        button.AutoSize = false;
        button.Size = new Size(
            Math.Max(1, (int)Math.Round(item.Width / LayoutSnapshot.Scale) - 4),
            item.Size == RibbonItemSize.Large ? 58 : 30);
        button.Margin = new Padding(2);
        button.AccessibleName = command.Caption;
        button.AccessibleDescription = command.Tooltip;
        if (command.IconKey is { Length: > 0 } iconKey &&
            ResolveIcon(iconKey, item.Size == RibbonItemSize.Large ? 32 : 16) is Image image)
        {
            button.Image = image;
            button.TextImageRelation = item.Size == RibbonItemSize.Large
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

    private void AddOverflowButton(FlowLayoutPanel groups, RibbonTabLayout tab)
    {
        var overflowGroups = tab.Groups
            .Where(static group => group.Mode == RibbonGroupLayoutMode.Overflow)
            .ToArray();
        if (overflowGroups.Length == 0)
        {
            return;
        }

        var menu = new ContextMenuStrip();
        foreach (var group in overflowGroups)
        {
            var groupItem = new ToolStripMenuItem(group.Presentation.Caption);
            foreach (var item in group.Items)
            {
                var command = item.Presentation.Command;
                var commandItem = new ToolStripMenuItem(command.Caption)
                {
                    Tag = command.CommandId,
                    Enabled = command.IsEnabled,
                    Checked = command.IsChecked ?? false,
                    CheckOnClick = false,
                    AccessibleName = command.Caption,
                    ToolTipText = command.Tooltip,
                };
                commandItem.Click += OnOverflowCommandClick;
                groupItem.DropDownItems.Add(commandItem);
            }
            menu.Items.Add(groupItem);
        }
        _overflowMenus.Add(menu);
        var overflow = new Button
        {
            Name = "ribbon-overflow",
            Text = "Thêm",
            AccessibleName = "Lệnh Ribbon bổ sung",
            Size = new Size(56, 30),
            Margin = new Padding(2),
        };
        overflow.Click += (_, _) => menu.Show(overflow, new Point(0, overflow.Height));
        groups.Controls.Add(overflow);
    }

    private void CaptureIdentities()
    {
        if (_tabs.SelectedTab?.Tag is string selectedId)
        {
            _selectedTabId = selectedId;
        }
        var focused = FindDescendants<ButtonBase>(this)
            .FirstOrDefault(static button => button.Focused && button.Tag is CommandId);
        if (focused?.Tag is CommandId focusedId)
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
        FindDescendants<ButtonBase>(this)
            .FirstOrDefault(button => button.Tag is CommandId id && id == commandId)
            ?.Focus();
    }

    private static IEnumerable<T> FindDescendants<T>(Control root)
        where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T match)
            {
                yield return match;
            }
            foreach (var descendant in FindDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private async void OnOverflowCommandClick(object? sender, EventArgs e)
    {
        if (sender is ToolStripMenuItem { Tag: CommandId commandId })
        {
            await ActivateCommandAsync(commandId);
        }
    }

    private void OnSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_tabs.SelectedTab?.Tag is string selectedId)
        {
            _selectedTabId = selectedId;
        }
    }

    private void OnRibbonResize(object? sender, EventArgs e)
    {
        if (!_disposed)
        {
            Rebuild();
        }
    }

    private void OnRibbonDpiChanged(object? sender, EventArgs e)
    {
        if (!_disposed)
        {
            Rebuild();
        }
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
