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
    private PresentationLocalization Localization => _runtime.Localization;

    private readonly RibbonRuntimeController _runtime;
    private readonly RibbonResponsiveLayoutEngine _layoutEngine = new();
    private readonly FlowLayoutPanel _topBar = new()
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        WrapContents = false,
    };
    private readonly Panel _backstage = new() { Dock = DockStyle.Fill };
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly ToolTip _toolTip = new();
    private readonly List<ContextMenuStrip> _overflowMenus = [];
    private readonly List<IDisposable> _shortcutBindings = [];
    private readonly List<Image> _galleryImages = [];
    private readonly Font _backstageHeadingFont = new("Segoe UI", 18f);
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
    private CommandId? _backstageSelection;
    private Control? _focusBeforeKeyTips;
    private string? _focusBeforeKeyTipsControlName;
    private bool _disposed;

    public NeraRibbonControl(RibbonRuntimeController runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        Name = "NeraRibbon";
        AccessibleName = Localization.Get("Thanh Ribbon NeraSpreadSheet");
        Font = new Font("Segoe UI", 9f);
        DoubleBuffered = true;
        _topBar.Margin = Padding.Empty;
        _topBar.Padding = new Padding(4, 2, 4, 2);
        _tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        _tabs.DrawItem += OnDrawTab;
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

    private string? _fileCaption;
    [Localizable(true)]
    [DefaultValue("Tệp")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public string FileCaption { get => _fileCaption ?? Localization.Get("Tệp"); set => _fileCaption = value; }

    private string? _localizedFileAutomationName;
    [Localizable(true)]
    [DefaultValue("Mở khu vực Tệp")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public string FileAutomationName { get => _localizedFileAutomationName ?? Localization.Get("Mở khu vực Tệp"); set => _localizedFileAutomationName = value; }

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
        var form = FindForm();
        var externalFocus = form is null ? null : FindDescendants<Control>(form)
            .FirstOrDefault(control => control.Focused && !ReferenceEquals(control, this) && !Contains(control));
        CaptureIdentities();
        var scale = DeviceDpi / 96d;
        LayoutSnapshot = _layoutEngine.Layout(
            _runtime.Snapshot,
            new RibbonLayoutRequest(
                ClientSize.Width > 0 ? Math.Max(0d, ClientSize.Width - (8d * scale)) : double.PositiveInfinity,
                scale,
                _selectedTabId,
                _focusedCommandId)
            {
                IsIconAvailable = key => ResolveIcon(key, 16) is not null,
            });
        var selectedTabId = LayoutSnapshot.SelectedTabId;
        _focusedCommandId = LayoutSnapshot.FocusedCommandId;
        BackColor = Palette.Surface;
        ForeColor = Palette.Text;
        _topBar.BackColor = Palette.Chrome;
        _tabs.BackColor = Palette.Chrome;
        _tabs.ForeColor = Palette.Text;
        _tabs.ItemSize = new Size(0, ScalePixel(28));
        _toolTip.RemoveAll();
        var oldPages = _tabs.TabPages.Cast<TabPage>().ToArray();
        _tabs.TabPages.Clear();
        foreach (var menu in _overflowMenus)
        {
            menu.Dispose();
        }
        _overflowMenus.Clear();
        foreach (var page in oldPages)
        {
            page.Dispose();
        }
        foreach (var image in _galleryImages)
        {
            image.Dispose();
        }
        _galleryImages.Clear();
        RebuildTopBar();
        RebuildBackstage();
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
                BackColor = Palette.Surface,
                ForeColor = Palette.Text,
                Padding = Padding.Empty,
            };
            var groups = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = false,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Visible = !_runtime.IsMinimized,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                BackColor = Palette.Surface,
            };
            foreach (var group in tab.Groups.Where(static group =>
                         group.Mode != RibbonGroupLayoutMode.Overflow))
            {
                var box = new Panel
                {
                    Name = $"ribbon-group-{group.Presentation.Id}",
                    AccessibleName = group.Presentation.Caption,
                    Size = new Size(Pixel(group.Width), Pixel(group.Height)),
                    Margin = new Padding(0, 0, ScalePixel(2), 0),
                    BackColor = Palette.Surface,
                };
                var caption = new Label
                {
                    Name = $"ribbon-group-caption-{group.Presentation.Id}",
                    Text = group.Presentation.Caption,
                    AccessibleName = group.Presentation.Caption,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Bounds = new Rectangle(0, Pixel(group.CaptionY), Pixel(group.Width), Pixel(group.CaptionHeight)),
                    ForeColor = Palette.Muted,
                    AutoEllipsis = true,
                };
                foreach (var item in group.Items)
                {
                    var control = CreateRibbonItem(item);
                    control.Bounds = new Rectangle(Pixel(item.X), Pixel(item.Y), Pixel(item.Width), Pixel(item.Height));
                    control.Margin = Padding.Empty;
                    box.Controls.Add(control);
                }
                box.Controls.Add(caption);
                box.Paint += (_, args) =>
                {
                    using var pen = new Pen(Palette.Separator);
                    args.Graphics.DrawLine(pen, box.Width - 1, ScalePixel(5), box.Width - 1, box.Height - ScalePixel(5));
                };
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
        if (externalFocus is { IsDisposed: false, CanFocus: true })
        {
            externalFocus.Focus();
        }
        else
        {
            RestoreFocus();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _runtime.SnapshotChanged -= OnSnapshotChanged;
            _tabs.SelectedIndexChanged -= OnSelectedIndexChanged;
            _tabs.DrawItem -= OnDrawTab;
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
            foreach (var image in _galleryImages)
            {
                image.Dispose();
            }
            _galleryImages.Clear();
            _backstageHeadingFont.Dispose();
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
            Size = new Size(ScalePixel(54), ScalePixel(28)),
        };
        StyleButton(file);
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
            var icon = command.IconKey is { Length: > 0 } key ? ResolveIcon(key, 16) : null;
            var button = new Button
            {
                Text = _runtime.KeyTips.Scope switch
                {
                    RibbonKeyTipScope.Tabs => $"{command.Caption} [Q→{FindSurfaceTip(_runtime.Definition.QuickAccessToolbar, command.CommandId)}]",
                    RibbonKeyTipScope.QuickAccessToolbar => $"{command.Caption} [{FindSurfaceTip(_runtime.Definition.QuickAccessToolbar, command.CommandId)}]",
                    _ => icon is null ? command.Caption : string.Empty,
                },
                Name = $"ribbon-qat-{command.CommandId.Value}",
                AccessibleName = command.Caption,
                Tag = command.CommandId,
                Enabled = command.IsEnabled,
                Image = icon,
                Size = new Size(ScalePixel(icon is null || _runtime.KeyTips.Scope != RibbonKeyTipScope.Inactive ? 100 : 28), ScalePixel(28)),
            };
            StyleButton(button);
            _toolTip.SetToolTip(button, BuildToolTip(command));
            button.Click += OnCommandClick;
            _topBar.Controls.Add(button);
        }
    }

    private void RebuildBackstage()
    {
        DisposeChildren(_backstage);
        _backstage.Controls.Clear();
        _backstage.BackColor = Palette.Surface;
        var selection = _runtime.Snapshot.Backstage.FirstOrDefault(command => command.CommandId == _backstageSelection)
            ?? (_runtime.Snapshot.Backstage.Count > 0 ? _runtime.Snapshot.Backstage[0] : null);
        _backstageSelection = selection?.CommandId;
        var rail = new FlowLayoutPanel
        {
            Name = "ribbon-backstage-navigation",
            AccessibleName = Localization.Get("Điều hướng Tệp"),
            Dock = DockStyle.Left,
            Width = ScalePixel(196),
            Padding = new Padding(ScalePixel(8)),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Palette.Chrome,
        };
        var content = new TableLayoutPanel
        {
            Name = "ribbon-backstage-content",
            Dock = DockStyle.Fill,
            Padding = new Padding(ScalePixel(24), ScalePixel(14), ScalePixel(24), ScalePixel(14)),
            ColumnCount = 1,
            RowCount = 3,
        };
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, ScalePixel(34)));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, ScalePixel(38)));
        var title = new Label { Text = selection?.Caption ?? FileCaption, Dock = DockStyle.Fill, ForeColor = Palette.Text, Font = _backstageHeadingFont, TextAlign = ContentAlignment.MiddleLeft };
        var detail = new Label { Text = selection is null ? Localization.Get("Chọn lệnh để làm việc với sổ tính.") : BuildToolTip(selection), Dock = DockStyle.Fill, ForeColor = Palette.Muted };
        content.Controls.Add(title, 0, 0);
        content.Controls.Add(detail, 0, 1);
        if (selection is not null)
        {
            var execute = new Button
            {
                Name = $"ribbon-backstage-{selection.CommandId.Value}-execute",
                Text = selection.Caption,
                AccessibleName = selection.Caption,
                AccessibleDescription = BuildToolTip(selection),
                Tag = selection.CommandId,
                Enabled = selection.IsEnabled,
                Size = new Size(ScalePixel(160), ScalePixel(34)),
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
            };
            StyleButton(execute);
            execute.BackColor = Palette.Checked;
            execute.FlatAppearance.BorderSize = 1;
            execute.Click += OnCommandClick;
            content.Controls.Add(execute, 0, 2);
        }
        _backstage.Controls.Add(content);
        _backstage.Controls.Add(rail);
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
                Size = new Size(ScalePixel(172), ScalePixel(34)),
                TextAlign = ContentAlignment.MiddleLeft,
                ImageAlign = ContentAlignment.MiddleLeft,
                TextImageRelation = TextImageRelation.ImageBeforeText,
            };
            StyleButton(button);
            if (command.IconKey is { Length: > 0 } iconKey)
            {
                button.Image = ResolveIcon(iconKey, 16);
            }
            if (command.CommandId == _backstageSelection)
            {
                button.BackColor = Palette.Checked;
                button.FlatAppearance.BorderSize = 1;
            }
            button.Click += (_, _) =>
            {
                _backstageSelection = command.CommandId;
                RebuildBackstage();
            };
            rail.Controls.Add(button);
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
        foreach (var child in parent.Controls.Cast<Control>().ToArray())
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
            Pixel(item.Width),
            Pixel(item.Height));
        button.Margin = Padding.Empty;
        button.TextAlign = item.Size == RibbonItemSize.Large ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft;
        button.ImageAlign = item.Size == RibbonItemSize.Large ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft;
        StyleButton(button);
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
        BackColor = Palette.Separator,
        Size = new Size(
            Pixel(item.Width),
            Pixel(item.Height)),
        Margin = Padding.Empty,
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
                Pixel(item.Width),
                Pixel(item.Height)),
            Margin = Padding.Empty,
            Tag = item.Presentation.Command.CommandId,
            Name = $"ribbon-command-{item.Presentation.Command.CommandId.Value}",
            AccessibleName = item.Presentation.AutomationName,
        };
        var primary = CreateCommandButton(item);
        primary.Name = $"ribbon-command-{item.Presentation.Command.CommandId.Value}-primary";
        primary.Margin = Padding.Empty;
        primary.Width = Math.Max(1, panel.Width - ScalePixel(18));
        var menuButton = CreateDropDownButton(item, "▾", ScalePixel(18), "menu");
        menuButton.Margin = Padding.Empty;
        panel.Controls.Add(primary);
        panel.Controls.Add(menuButton);
        return panel;
    }

    private Button CreateDropDown(RibbonItemLayout item) =>
        CreateDropDownButton(
            item,
            item.Presentation.Command.Caption,
            Pixel(item.Width));

    private Button CreateDropDownButton(
        RibbonItemLayout item,
        string text,
        int width,
        string? automationSuffix = null)
    {
        var command = item.Presentation.Command;
        var menu = CreateMenu();
        foreach (var choice in command.SelectableItems)
        {
            menu.Items.Add(CreateChoiceMenuItem(command.CommandId, choice, command.SelectedValue));
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
            Size = new Size(width, Pixel(item.Height)),
            Margin = Padding.Empty,
        };
        StyleButton(button);
        _toolTip.SetToolTip(button, BuildToolTip(command));
        if (automationSuffix is null && command.IconKey is { Length: > 0 } iconKey &&
            ResolveIcon(iconKey, item.Size == RibbonItemSize.Large ? 32 : 16) is Image image)
        {
            button.Image = image;
            button.TextImageRelation = item.Size == RibbonItemSize.Large ? TextImageRelation.ImageAboveText : TextImageRelation.ImageBeforeText;
            if (!item.CaptionVisible && _runtime.KeyTips.Scope != RibbonKeyTipScope.Tab)
            {
                button.Text = "▾";
            }
        }
        button.Click += (_, _) => menu.Show(button, new Point(0, button.Height));
        return button;
    }

    private ToolStripMenuItem CreateChoiceMenuItem(
        CommandId commandId,
        CommandItem choice,
        string? selectedValue = null)
    {
        var item = new ToolStripMenuItem(choice.Caption)
        {
            Tag = new RibbonChoiceTag(commandId, choice.Value),
            Enabled = choice.IsEnabled,
            Checked = choice.IsChecked ?? string.Equals(choice.Value, selectedValue, StringComparison.Ordinal),
            CheckOnClick = false,
            AccessibleName = choice.Caption,
            ToolTipText = choice.Tooltip ?? choice.Caption,
        };
        foreach (var child in choice.Children)
        {
            item.DropDownItems.Add(CreateChoiceMenuItem(commandId, child, selectedValue));
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
            FlatStyle = FlatStyle.Flat,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = Math.Max(1, Pixel(item.Height) - ScalePixel(8)),
            BackColor = Palette.Surface,
            ForeColor = Palette.Text,
            Size = new Size(
                Pixel(item.Width),
                Pixel(item.Height)),
            Margin = Padding.Empty,
        };
        if (command.SelectedValue is not null)
        {
            combo.SelectedValue = command.SelectedValue;
        }
        combo.DrawItem += (_, args) =>
        {
            if (args.Index < 0 || args.Index >= command.SelectableItems.Count)
            {
                return;
            }
            var choice = command.SelectableItems[args.Index];
            var selected = (args.State & DrawItemState.Selected) != 0;
            using var fill = new SolidBrush(selected ? Palette.Hover : Palette.Surface);
            args.Graphics.FillRectangle(fill, args.Bounds);
            var textBounds = args.Bounds;
            if (item.Presentation.Kind == RibbonItemKind.ColorPicker && TryResolveColor(choice.Value, out var color))
            {
                var swatch = new Rectangle(args.Bounds.Left + ScalePixel(3), args.Bounds.Top + ScalePixel(3), ScalePixel(16), Math.Max(1, args.Bounds.Height - ScalePixel(6)));
                using var brush = new SolidBrush(color);
                args.Graphics.FillRectangle(brush, swatch);
                using var border = new Pen(Palette.Separator);
                args.Graphics.DrawRectangle(border, swatch);
                textBounds.X += ScalePixel(23);
                textBounds.Width -= ScalePixel(23);
            }
            TextRenderer.DrawText(args.Graphics, choice.Caption, combo.Font, textBounds, choice.IsEnabled ? Palette.Text : Palette.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            args.DrawFocusRectangle();
        };
        _toolTip.SetToolTip(combo, BuildToolTip(command));
        combo.SelectedIndexChanged += OnChoiceSelectionCommitted;
        return combo;
    }

    private Panel CreateGallery(RibbonItemLayout item)
    {
        var command = item.Presentation.Command;
        var host = new Panel
        {
            Name = $"ribbon-gallery-{command.CommandId.Value}",
            Size = new Size(Pixel(item.Width), Pixel(item.Height)),
            Margin = Padding.Empty,
        };
        var panel = new FlowLayoutPanel
        {
            Name = $"ribbon-command-{command.CommandId.Value}",
            Tag = command.CommandId,
            AccessibleName = item.Presentation.AutomationName,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true,
            Size = new Size(
                Math.Max(1, Pixel(item.Width) - ScalePixel(18)),
                Pixel(item.Height)),
            Margin = Padding.Empty,
            Dock = DockStyle.Fill,
            BackColor = Palette.Surface,
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
                AutoSize = false,
                Size = new Size(ScalePixel(76), Math.Max(ScalePixel(24), Pixel(item.Height) - ScalePixel(18))),
                AccessibleName = choice.Caption,
                TextImageRelation = TextImageRelation.ImageAboveText,
                TextAlign = ContentAlignment.BottomCenter,
                Margin = new Padding(1),
            };
            StyleButton(button);
            if (item.Presentation.Definition.GalleryPreview?.Invoke(choice) is { } preview)
            {
                var thumbnail = NeraWinFormsRibbonChrome.CreatePreview(preview, ScalePixel(64), ScalePixel(32));
                _galleryImages.Add(thumbnail);
                button.Image = thumbnail;
            }
            else if (choice.IconKey is { Length: > 0 } iconKey &&
                ResolveIcon(iconKey, 16) is Image image)
            {
                button.Image = image;
            }
            _toolTip.SetToolTip(button, choice.Tooltip ?? choice.Caption);
            button.Click += async (_, _) =>
                await ActivateItemAsync(command.CommandId, choice.Value);
            panel.Controls.Add(button);
        }
        panel.AutoScrollMinSize = new Size(
            panel.Controls.Cast<Control>().Sum(control =>
                control.Width + control.Margin.Horizontal),
            0);
        var more = CreateDropDownButton(item, "▾", ScalePixel(18), "more");
        more.Dock = DockStyle.Right;
        more.AccessibleName = Localization.Format("{0}, thêm lựa chọn", item.Presentation.AutomationName);
        host.Controls.Add(panel);
        host.Controls.Add(more);
        return host;
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

        var menu = CreateMenu();
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
                            choice,
                            command.SelectedValue));
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
            Text = Localization.Get("Thêm"),
            AccessibleName = Localization.Get("Lệnh Ribbon bổ sung"),
            Size = new Size(ScalePixel(56), ScalePixel(76)),
            Margin = new Padding(0, ScalePixel(4), 0, 0),
        };
        StyleButton(overflow);
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

    private void OnDrawTab(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _tabs.TabPages.Count)
        {
            return;
        }
        var selected = e.Index == _tabs.SelectedIndex;
        using var fill = new SolidBrush(selected ? Palette.Surface : Palette.Chrome);
        e.Graphics.FillRectangle(fill, e.Bounds);
        TextRenderer.DrawText(e.Graphics, _tabs.TabPages[e.Index].Text, Font, e.Bounds, selected ? Palette.Accent : Palette.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        if (selected)
        {
            using var pen = new Pen(Palette.Accent, ScalePixel(2));
            e.Graphics.DrawLine(pen, e.Bounds.Left + ScalePixel(8), e.Bounds.Bottom - 2, e.Bounds.Right - ScalePixel(8), e.Bounds.Bottom - 2);
        }
        if (_tabs.Focused)
        {
            e.DrawFocusRectangle();
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

    private NeraWinFormsRibbonPalette Palette => NeraWinFormsRibbonPalette.For(IconTheme);

    private static int Pixel(double value) => Math.Max(0, (int)Math.Round(value));

    private int ScalePixel(double value) => Pixel(value * LayoutSnapshot.Scale);

    private void StyleButton(ButtonBase button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = button is CheckBox { Checked: true } ? 1 : 0;
        button.FlatAppearance.BorderColor = Palette.Accent;
        button.FlatAppearance.MouseOverBackColor = Palette.Hover;
        button.FlatAppearance.MouseDownBackColor = Palette.Pressed;
        button.FlatAppearance.CheckedBackColor = Palette.Checked;
        button.BackColor = button is CheckBox { Checked: true } ? Palette.Checked : Palette.Surface;
        button.ForeColor = Palette.Text;
        button.Padding = new Padding(ScalePixel(3), 0, ScalePixel(3), 0);
        button.UseVisualStyleBackColor = false;
        button.Margin = Padding.Empty;
    }

    private ContextMenuStrip CreateMenu() => new()
    {
        BackColor = Palette.Surface,
        ForeColor = Palette.Text,
        Renderer = new ToolStripProfessionalRenderer(new NeraWinFormsRibbonColorTable(Palette)),
        ShowImageMargin = true,
    };

    private static bool TryResolveColor(string value, out Color color)
    {
        if (value.StartsWith('#') && value.Length is 7 or 9 && uint.TryParse(value.AsSpan(1), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var argb))
        {
            color = Color.FromArgb(unchecked((int)(value.Length == 7 ? argb | 0xFF000000 : argb)));
            return true;
        }
        color = Color.FromName(value);
        return color.IsKnownColor;
    }

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
