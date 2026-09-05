using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Editing;
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
    private readonly FlowLayoutPanel _topBar = new()
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        WrapContents = false,
    };
    private readonly FlowLayoutPanel _backstage = new() { Dock = DockStyle.Fill };
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly ToolTip _toolTip = new();
    private readonly List<ContextMenuStrip> _overflowMenus = [];
    private readonly List<IDisposable> _shortcutBindings = [];
    private readonly List<IDisposable> _tableDesignBindings = [];
    private Func<string, Image?>? _iconResolver;
    private Func<NeraIconRequest, Image?>? _iconRequestResolver;
    private NeraIconTheme _iconTheme = NeraIconTheme.Light;
    private string? _selectedTabId;
    private string? _focusedControlName;
    private CommandId? _focusedCommandId;
    private bool _restoreCommandFocus;
    private bool _suppressChoiceActivation;
    private bool _resizeRebuildPending;
    private bool _isBackstageOpen;
    private Control? _focusBeforeKeyTips;
    private string? _focusBeforeKeyTipsControlName;
    private bool _disposed;

    public NeraRibbonControl(RibbonRuntimeController runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        Name = "NeraRibbon";
        AccessibleName = "Thanh Ribbon NeraSpreadSheet";
        Controls.Add(_tabs);
        Controls.Add(_backstage);
        Controls.Add(_topBar);
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

    [Localizable(true)]
    [DefaultValue("Tệp")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public string FileCaption { get; set; } = "Tệp";

    [Localizable(true)]
    [DefaultValue("Mở khu vực Tệp")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public string FileAutomationName { get; set; } = "Mở khu vực Tệp";

    [DefaultValue(false)]
    public bool IsMinimized
    {
        get => _runtime.IsMinimized;
        set => _runtime.SetMinimized(value);
    }

    [Browsable(false)]
    public bool IsBackstageOpen => _isBackstageOpen;

    [Browsable(false)]
    public RibbonKeyTipScope KeyTipScope => _runtime.KeyTips.Scope;

    public void EnterKeyTipMode()
    {
        CaptureKeyTipOrigin();
        _runtime.KeyTips.Enter();
        _isBackstageOpen = false;
        Rebuild();
    }

    public async ValueTask<bool> ProcessKeyTipAsync(string keyTip)
    {
        var result = _runtime.KeyTips.Process(keyTip);
        return await ApplyKeyTipResultAsync(result);
    }

    /// <summary>Consumes one native key-tip character.</summary>
    public ValueTask<bool> ProcessKeyTipCharacterAsync(char character) =>
        ApplyKeyTipResultAsync(_runtime.KeyTips.ProcessCharacter(character));

    private async ValueTask<bool> ApplyKeyTipResultAsync(RibbonKeyTipResult result)
    {
        if (result.TabId is { } tabId)
        {
            _isBackstageOpen = false;
            _selectedTabId = tabId;
            Rebuild();
            return true;
        }
        if (result.CommandId is { } commandId)
        {
            try
            {
                return await ActivateCommandAsync(commandId);
            }
            finally
            {
                _isBackstageOpen = false;
                Rebuild();
                RestoreKeyTipOrigin();
            }
        }
        if (result.Action == RibbonKeyTipAction.ScopeChanged)
        {
            _isBackstageOpen = _runtime.KeyTips.Scope == RibbonKeyTipScope.Backstage;
            Rebuild();
            return true;
        }
        return false;
    }

    public void EscapeKeyTipMode()
    {
        var result = _runtime.KeyTips.Escape();
        if (_runtime.KeyTips.Scope == RibbonKeyTipScope.Tabs)
        {
            _isBackstageOpen = false;
        }
        Rebuild();
        if (result.Action == RibbonKeyTipAction.Exit)
        {
            RestoreKeyTipOrigin();
        }
    }

    public IDisposable BindShortcuts(Control owner)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var binding = new NeraWinFormsShortcutBinding(
            owner,
            _runtime.TryResolveShortcut,
            ActivateCommandAsync,
            EnterKeyTipMode,
            () => _runtime.KeyTips.Scope != RibbonKeyTipScope.Inactive,
            ProcessKeyTipCharacterAsync,
            EscapeKeyTipMode);
        _shortcutBindings.Add(binding);
        return binding;
    }

    /// <summary>Binds contextual Table Design visibility to a spreadsheet session.</summary>
    public IDisposable BindTableDesign(SpreadsheetSession session)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var binding = new NeraWinFormsTableDesignRibbonBinding(
            session,
            _runtime,
            this);
        _tableDesignBindings.Add(binding);
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
        var selectedTabId = LayoutSnapshot.SelectedTabId;
        _focusedCommandId = LayoutSnapshot.FocusedCommandId;
        RebuildTopBar();
        RebuildBackstage();
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
            var page = new TabPage(
                _runtime.KeyTips.Scope == RibbonKeyTipScope.Tabs
                    ? $"{tab.Presentation.Caption} [{_runtime.KeyTips.TabTips[tab.Presentation.Id]}]"
                    : tab.Presentation.Caption)
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
                Visible = !_runtime.IsMinimized,
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
                    items.Controls.Add(CreateRibbonItem(item));
                }
                box.Controls.Add(items);
                groups.Controls.Add(box);
            }
            AddOverflowButton(groups, tab);
            page.Controls.Add(groups);
            _tabs.TabPages.Add(page);
        }
        if (selectedTabId is not null)
        {
            var selected = _tabs.TabPages.Cast<TabPage>().FirstOrDefault(page =>
                string.Equals(
                    page.Tag as string,
                    selectedTabId,
                    StringComparison.OrdinalIgnoreCase));
            if (selected is not null)
            {
                _tabs.SelectedTab = selected;
            }
        }
        _selectedTabId = selectedTabId;
        _tabs.Visible = !_isBackstageOpen;
        _backstage.Visible = _isBackstageOpen;
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
            foreach (var binding in _tableDesignBindings)
            {
                binding.Dispose();
            }
            _tableDesignBindings.Clear();
            foreach (var menu in _overflowMenus)
            {
                menu.Dispose();
            }
            _overflowMenus.Clear();
            _toolTip.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData is Keys.Menu or (Keys.Alt | Keys.Menu))
        {
            EnterKeyTipMode();
            return true;
        }
        if (keyData == Keys.Escape && _runtime.KeyTips.Scope != RibbonKeyTipScope.Inactive)
        {
            EscapeKeyTipMode();
            return true;
        }
        if (_runtime.KeyTips.Scope != RibbonKeyTipScope.Inactive &&
            TryGetKeyTipCharacter(keyData, out var character))
        {
            _ = ApplyKeyTipResultAsync(
                _runtime.KeyTips.ProcessCharacter(character)).AsTask();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private static bool TryGetKeyTipCharacter(Keys keyData, out char character)
    {
        var key = keyData & Keys.KeyCode;
        if (key is >= Keys.A and <= Keys.Z)
        {
            character = (char)('A' + (int)key - (int)Keys.A);
            return true;
        }
        if (key is >= Keys.D0 and <= Keys.D9)
        {
            character = (char)('0' + (int)key - (int)Keys.D0);
            return true;
        }
        character = default;
        return false;
    }

    private void RebuildTopBar()
    {
        DisposeChildren(_topBar);
        _topBar.Controls.Clear();
        var file = new Button
        {
            Text = _runtime.KeyTips.Scope == RibbonKeyTipScope.Tabs
                ? $"{FileCaption} [F]"
                : FileCaption,
            Name = "ribbon-file",
            AccessibleName = FileAutomationName,
            AutoSize = true,
        };
        file.Click += (_, _) =>
        {
            _isBackstageOpen = !_isBackstageOpen;
            var restoreFocus = false;
            if (_isBackstageOpen)
            {
                if (_runtime.KeyTips.Scope != RibbonKeyTipScope.Inactive)
                {
                    _runtime.KeyTips.OpenBackstage();
                }
            }
            else
            {
                ExitKeyTipMode();
                restoreFocus = true;
            }
            Rebuild();
            if (restoreFocus)
            {
                RestoreKeyTipOrigin();
            }
        };
        _topBar.Controls.Add(file);
        foreach (var command in _runtime.Snapshot.QuickAccessToolbar)
        {
            var button = new Button
            {
                Text = _runtime.KeyTips.Scope switch
                {
                    RibbonKeyTipScope.Tabs => $"{command.Caption} [Q→{FindSurfaceTip(_runtime.Definition.QuickAccessToolbar, command.CommandId)}]",
                    RibbonKeyTipScope.QuickAccessToolbar => $"{command.Caption} [{FindSurfaceTip(_runtime.Definition.QuickAccessToolbar, command.CommandId)}]",
                    _ => command.Caption,
                },
                Name = $"ribbon-qat-{command.CommandId.Value}",
                AccessibleName = command.Caption,
                Tag = command.CommandId,
                Enabled = command.IsEnabled,
                AutoSize = true,
            };
            button.Click += OnCommandClick;
            _topBar.Controls.Add(button);
        }
    }

    private void RebuildBackstage()
    {
        DisposeChildren(_backstage);
        _backstage.Controls.Clear();
        foreach (var command in _runtime.Snapshot.Backstage)
        {
            var button = new Button
            {
                Text = _runtime.KeyTips.Scope == RibbonKeyTipScope.Backstage
                    ? $"{command.Caption} [{FindSurfaceTip(_runtime.Definition.Backstage, command.CommandId)}]"
                    : command.Caption,
                Name = $"ribbon-backstage-{command.CommandId.Value}",
                AccessibleName = command.Caption,
                Tag = command.CommandId,
                Enabled = command.IsEnabled,
                AutoSize = true,
            };
            button.Click += OnCommandClick;
            _backstage.Controls.Add(button);
        }
    }

    private void RestoreKeyTipOrigin()
    {
        if (_focusBeforeKeyTipsControlName is { } name)
        {
            FindDescendants<Control>(this)
                .FirstOrDefault(control => string.Equals(
                    control.Name,
                    name,
                    StringComparison.Ordinal))
                ?.Focus();
        }
        else
        {
            _focusBeforeKeyTips?.Focus();
        }
        _focusBeforeKeyTips = null;
        _focusBeforeKeyTipsControlName = null;
    }

    private void CaptureKeyTipOrigin()
    {
        var form = FindForm();
        var focused = form is null
            ? null
            : FindDescendants<Control>(form).FirstOrDefault(static control => control.Focused) ??
              form.ActiveControl;
        if (focused is not null && FindDescendants<Control>(this).Contains(focused) &&
            !string.IsNullOrEmpty(focused.Name))
        {
            _focusBeforeKeyTipsControlName = focused.Name;
            _focusBeforeKeyTips = null;
            return;
        }
        _focusBeforeKeyTips = focused;
        _focusBeforeKeyTipsControlName = null;
    }

    private void ExitKeyTipMode()
    {
        while (_runtime.KeyTips.Scope != RibbonKeyTipScope.Inactive)
        {
            _runtime.KeyTips.Escape();
        }
    }

    private static void DisposeChildren(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            child.Dispose();
        }
    }

    private static string FindSurfaceTip(
        IReadOnlyList<RibbonCommandSurfaceItem> items,
        CommandId commandId) =>
        items.First(item => item.CommandId == commandId).KeyTip;

    private Control CreateRibbonItem(RibbonItemLayout item) =>
        item.Presentation.Kind switch
        {
            RibbonItemKind.Separator => CreateSeparator(item),
            RibbonItemKind.SplitButton => CreateSplitButton(item),
            RibbonItemKind.DropDown or RibbonItemKind.Menu => CreateDropDown(item),
            RibbonItemKind.ComboBox or RibbonItemKind.ColorPicker => CreateComboBox(item),
            RibbonItemKind.Gallery => CreateGallery(item),
            _ => CreateCommandButton(item),
        };

    private ButtonBase CreateCommandButton(RibbonItemLayout item)
    {
        var command = item.Presentation.Command;
        ButtonBase button = item.Presentation.IsToggle
            ? new CheckBox
            {
                Appearance = Appearance.Button,
                AutoCheck = false,
                Checked = command.IsChecked ?? false,
                TextAlign = ContentAlignment.MiddleCenter,
            }
            : new Button();
        button.Name = $"ribbon-command-{command.CommandId.Value}";
        var resolvedIcon = command.IconKey is { Length: > 0 } iconKey
            ? ResolveIcon(iconKey, item.Size == RibbonItemSize.Large ? 32 : 16)
            : null;
        button.Text = item.Size == RibbonItemSize.Compact && resolvedIcon is not null &&
                      _runtime.KeyTips.Scope != RibbonKeyTipScope.Tab
            ? string.Empty
            : DecorateCommandCaption(command);
        button.Tag = command.CommandId;
        button.Enabled = command.IsEnabled;
        button.AutoSize = false;
        button.Size = new Size(
            Math.Max(1, (int)Math.Round(item.Width / LayoutSnapshot.Scale) - 4),
            item.Size == RibbonItemSize.Large ? 58 : 30);
        button.Margin = new Padding(2);
        button.AccessibleName = item.Presentation.AutomationName;
        button.AccessibleDescription = command.Tooltip;
        if (resolvedIcon is Image image)
        {
            button.Image = image;
            button.TextImageRelation = item.Size == RibbonItemSize.Large
                ? TextImageRelation.ImageAboveText
                : TextImageRelation.ImageBeforeText;
        }
        var toolTip = string.Join(
            " ",
            new[] { command.Tooltip, command.Shortcut }
                .Where(static value => !string.IsNullOrWhiteSpace(value)));
        _toolTip.SetToolTip(
            button,
            string.IsNullOrWhiteSpace(toolTip) ? command.Caption : toolTip);
        button.Click += OnCommandClick;
        return button;
    }

    private string DecorateCommandCaption(CommandPresentation command)
    {
        if (_runtime.KeyTips.Scope != RibbonKeyTipScope.Tab)
        {
            return command.Caption;
        }
        return _runtime.KeyTips.TryGetCommandTip(command.CommandId, out var tip)
            ? $"{command.Caption} [{tip}]"
            : command.Caption;
    }

    private Panel CreateSeparator(RibbonItemLayout item) => new()
    {
        Name = $"ribbon-command-{item.Presentation.Command.CommandId.Value}",
        AccessibleName = item.Presentation.AutomationName,
        BackColor = SystemColors.ControlDark,
        Size = new Size(
            Math.Max(1, (int)Math.Round(item.Width / LayoutSnapshot.Scale)),
            42),
        Margin = new Padding(0, 6, 0, 6),
        Tag = item.Presentation.Command.CommandId,
    };

    private FlowLayoutPanel CreateSplitButton(RibbonItemLayout item)
    {
        var panel = new FlowLayoutPanel
        {
            AutoSize = false,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Size = new Size(
                Math.Max(1, (int)Math.Round(item.Width / LayoutSnapshot.Scale) - 4),
                item.Size == RibbonItemSize.Large ? 58 : 30),
            Margin = new Padding(2),
            Tag = item.Presentation.Command.CommandId,
            Name = $"ribbon-command-{item.Presentation.Command.CommandId.Value}",
            AccessibleName = item.Presentation.AutomationName,
        };
        var primary = CreateCommandButton(item);
        primary.Name = $"ribbon-command-{item.Presentation.Command.CommandId.Value}-primary";
        primary.Margin = Padding.Empty;
        primary.Width = Math.Max(1, panel.Width - 24);
        var menuButton = CreateDropDownButton(item, "▼", 24, "menu");
        menuButton.Margin = Padding.Empty;
        panel.Controls.Add(primary);
        panel.Controls.Add(menuButton);
        return panel;
    }

    private Button CreateDropDown(RibbonItemLayout item) =>
        CreateDropDownButton(
            item,
            item.Presentation.Command.Caption,
            Math.Max(1, (int)Math.Round(item.Width / LayoutSnapshot.Scale) - 4));

    private Button CreateDropDownButton(
        RibbonItemLayout item,
        string text,
        int width,
        string? automationSuffix = null)
    {
        var command = item.Presentation.Command;
        var menu = new ContextMenuStrip();
        foreach (var choice in command.SelectableItems)
        {
            menu.Items.Add(CreateChoiceMenuItem(command.CommandId, choice));
        }
        _overflowMenus.Add(menu);
        var button = new Button
        {
            Name = $"ribbon-command-{command.CommandId.Value}{(automationSuffix is null ? string.Empty : $"-{automationSuffix}")}",
            Text = text,
            Tag = command.CommandId,
            Enabled = command.IsEnabled,
            AccessibleName = item.Presentation.AutomationName,
            AccessibleDescription = command.Tooltip,
            Size = new Size(width, item.Size == RibbonItemSize.Large ? 58 : 30),
        };
        _toolTip.SetToolTip(button, BuildToolTip(command));
        if (command.IconKey is { Length: > 0 } iconKey &&
            ResolveIcon(iconKey, 16) is Image image)
        {
            button.Image = image;
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
        }
        button.Click += (_, _) => menu.Show(button, new Point(0, button.Height));
        return button;
    }

    private ToolStripMenuItem CreateChoiceMenuItem(
        CommandId commandId,
        CommandItem choice)
    {
        var item = new ToolStripMenuItem(choice.Caption)
        {
            Tag = new RibbonChoiceTag(commandId, choice.Value),
            Enabled = choice.IsEnabled,
            Checked = choice.IsChecked ?? false,
            CheckOnClick = false,
            AccessibleName = choice.Caption,
            ToolTipText = choice.Tooltip ?? choice.Caption,
        };
        foreach (var child in choice.Children)
        {
            item.DropDownItems.Add(CreateChoiceMenuItem(commandId, child));
        }
        if (choice.IconKey is { Length: > 0 } iconKey)
        {
            item.Image = ResolveIcon(iconKey, 16);
        }
        if (choice.Children.Count == 0)
        {
            item.Click += OnChoiceMenuItemClick;
        }
        return item;
    }

    private ComboBox CreateComboBox(RibbonItemLayout item)
    {
        var command = item.Presentation.Command;
        var combo = new ComboBox
        {
            Name = $"ribbon-command-{command.CommandId.Value}",
            Tag = command.CommandId,
            DataSource = command.SelectableItems.ToArray(),
            DisplayMember = nameof(CommandItem.Caption),
            ValueMember = nameof(CommandItem.Value),
            Enabled = command.IsEnabled,
            AccessibleName = item.Presentation.AutomationName,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Size = new Size(
                Math.Max(1, (int)Math.Round(item.Width / LayoutSnapshot.Scale) - 4),
                30),
            Margin = new Padding(2),
        };
        if (command.SelectedValue is not null)
        {
            combo.SelectedValue = command.SelectedValue;
        }
        _toolTip.SetToolTip(combo, BuildToolTip(command));
        combo.SelectedIndexChanged += OnChoiceSelectionCommitted;
        return combo;
    }

    private FlowLayoutPanel CreateGallery(RibbonItemLayout item)
    {
        var command = item.Presentation.Command;
        var panel = new FlowLayoutPanel
        {
            Name = $"ribbon-command-{command.CommandId.Value}",
            Tag = command.CommandId,
            AccessibleName = item.Presentation.AutomationName,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true,
            Size = new Size(
                Math.Max(1, (int)Math.Round(item.Width / LayoutSnapshot.Scale) - 4),
                item.Size == RibbonItemSize.Large ? 58 : 30),
            Margin = new Padding(2),
        };
        foreach (var choice in command.SelectableItems)
        {
            var button = new CheckBox
            {
                Name = $"ribbon-command-{command.CommandId.Value}-choice-{choice.Value}",
                Appearance = Appearance.Button,
                AutoCheck = false,
                Text = choice.Caption,
                Tag = command.CommandId,
                Enabled = command.IsEnabled && choice.IsEnabled,
                Checked = string.Equals(
                    command.SelectedValue,
                    choice.Value,
                    StringComparison.Ordinal),
                AutoSize = true,
                AccessibleName = choice.Caption,
            };
            if (choice.IconKey is { Length: > 0 } iconKey &&
                ResolveIcon(iconKey, 16) is Image image)
            {
                button.Image = image;
                button.TextImageRelation = TextImageRelation.ImageBeforeText;
            }
            _toolTip.SetToolTip(button, choice.Tooltip ?? choice.Caption);
            button.Click += async (_, _) =>
                await ActivateItemAsync(command.CommandId, choice.Value);
            panel.Controls.Add(button);
        }
        panel.AutoScrollMinSize = new Size(
            panel.Controls.Cast<Control>().Sum(control =>
                control.PreferredSize.Width + control.Margin.Horizontal),
            0);
        return panel;
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
                if (item.Presentation.Kind == RibbonItemKind.Separator)
                {
                    groupItem.DropDownItems.Add(new ToolStripSeparator());
                    continue;
                }
                var commandItem = new ToolStripMenuItem(command.Caption)
                {
                    Tag = command.CommandId,
                    Enabled = command.IsEnabled,
                    Checked = item.Presentation.IsToggle && command.IsChecked == true,
                    CheckOnClick = false,
                    AccessibleName = item.Presentation.AutomationName,
                    ToolTipText = command.Tooltip,
                };
                if (item.Presentation.IsToggle)
                {
                    commandItem.AccessibleRole = AccessibleRole.CheckButton;
                }
                if (item.Presentation.Kind is RibbonItemKind.Button or RibbonItemKind.Toggle)
                {
                    commandItem.Click += OnOverflowCommandClick;
                }
                else
                {
                    if (item.Presentation.Kind == RibbonItemKind.SplitButton)
                    {
                        var primary = new ToolStripMenuItem(command.Caption)
                        {
                            Name =
                                $"ribbon-command-{command.CommandId.Value}-primary",
                            Tag = command.CommandId,
                            Enabled = command.IsEnabled,
                            AccessibleName = item.Presentation.AutomationName,
                            ToolTipText = BuildToolTip(command),
                        };
                        primary.Click += OnOverflowCommandClick;
                        commandItem.DropDownItems.Add(primary);
                        if (command.SelectableItems.Count > 0)
                        {
                            commandItem.DropDownItems.Add(new ToolStripSeparator());
                        }
                    }
                    foreach (var choice in command.SelectableItems)
                    {
                        commandItem.DropDownItems.Add(CreateChoiceMenuItem(
                            command.CommandId,
                            choice));
                    }
                }
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
        var focused = FindDescendants<Control>(this)
            .FirstOrDefault(static control => control.Focused && control.Tag is CommandId);
        if (focused?.Tag is CommandId focusedId)
        {
            _focusedCommandId = focusedId;
            _focusedControlName = focused.Name;
            _restoreCommandFocus = true;
        }
        else if (FindForm()?.ActiveControl is not null)
        {
            _restoreCommandFocus = false;
        }
    }

    private void RestoreFocus()
    {
        if (!_restoreCommandFocus || _focusedCommandId is not { } commandId)
        {
            return;
        }
        var candidates = FindDescendants<Control>(this)
            .Where(control => control.Tag is CommandId id && id == commandId)
            .ToArray();
        (candidates.FirstOrDefault(control => string.Equals(
             control.Name,
             _focusedControlName,
             StringComparison.Ordinal))
         ?? candidates.FirstOrDefault())?.Focus();
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

    private async void OnChoiceMenuItemClick(object? sender, EventArgs e)
    {
        if (sender is ToolStripMenuItem { Tag: RibbonChoiceTag choice })
        {
            await ActivateItemAsync(choice.CommandId, choice.Value);
        }
    }

    private async void OnChoiceSelectionCommitted(object? sender, EventArgs e)
    {
        if (_suppressChoiceActivation ||
            sender is not ComboBox { Tag: CommandId commandId } combo)
        {
            return;
        }

        if (combo.SelectedValue is not string selectedValue ||
            !await ActivateItemAsync(commandId, selectedValue))
        {
            _suppressChoiceActivation = true;
            try
            {
                var previousValue = _runtime.Snapshot.Tabs
                    .SelectMany(static tab => tab.Groups)
                    .SelectMany(static group => group.Items)
                    .FirstOrDefault(item => item.Command.CommandId == commandId)
                    ?.Command.SelectedValue;
                combo.SelectedIndex = previousValue is null
                    ? -1
                    : combo.Items.Cast<CommandItem>().ToList().FindIndex(item =>
                        string.Equals(
                            item.Value,
                            previousValue,
                            StringComparison.Ordinal));
            }
            finally
            {
                _suppressChoiceActivation = false;
            }
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
            ScheduleResizeRebuild();
        }
    }

    private void ScheduleResizeRebuild()
    {
        if (_resizeRebuildPending)
        {
            return;
        }
        if (!IsHandleCreated)
        {
            Rebuild();
            return;
        }
        _resizeRebuildPending = true;
        BeginInvoke((Action)(() =>
        {
            _resizeRebuildPending = false;
            if (!_disposed && !IsDisposed)
            {
                Rebuild();
            }
        }));
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

    private async ValueTask<bool> ActivateCommandAsync(CommandId commandId)
    {
        try
        {
            return await _runtime.TryActivateAsync(
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
            return false;
        }
    }

    private async ValueTask<bool> ActivateItemAsync(
        CommandId commandId,
        string selectedValue)
    {
        try
        {
            var context = CommandContextFactory?.Invoke(commandId) ?? default;
            return await _runtime.TryActivateItemAsync(commandId, selectedValue, context);
        }
        catch (Exception exception)
        {
            var handler = CommandActivationFailed;
            if (handler is null)
            {
                throw;
            }
            handler(this, new NeraWinFormsCommandActivationFailedEventArgs(
                commandId,
                exception));
            return false;
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

    private static string BuildToolTip(CommandPresentation command) =>
        string.Join(
            " ",
            new[] { command.Tooltip, command.Shortcut }
                .Where(static value => !string.IsNullOrWhiteSpace(value))) is { Length: > 0 } value
            ? value
            : command.Caption;

    private sealed record RibbonChoiceTag(CommandId CommandId, string Value);
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
