using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Iconography;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.WinForms;

/// <summary>
/// Native WinForms editor for a <see cref="RibbonRuntimeController"/> customization.
/// </summary>
public sealed class NeraRibbonCustomizationDialog : Form
{
    private PresentationLocalization Localization => _runtime.Localization;

    private readonly RibbonRuntimeController _runtime;
    private readonly ListBox _entries = new() { Dock = DockStyle.Fill };
    private readonly CheckBox _visible = new() { AutoSize = true };
    private readonly CheckBox _large = new() { AutoSize = true };
    private readonly ListBox _catalog = new() { Dock = DockStyle.Fill, DisplayMember = nameof(RibbonCommandCatalogEntry.Caption), BorderStyle = BorderStyle.FixedSingle };
    private readonly TextBox _search = new() { Dock = DockStyle.Fill, Name = "RibbonCustomizationSearch" };
    private readonly Label _selectionDetails = new() { AutoSize = true, ForeColor = Color.FromArgb(98, 109, 119), Padding = new Padding(0, 6, 0, 6) };
    private bool _refreshing;
    private bool _accepted;
    private bool _initialized;
    private NeraIconTheme _iconTheme;

    public NeraRibbonCustomizationDialog(RibbonRuntimeController runtime)
        : this(runtime, null)
    {
    }

    public NeraRibbonCustomizationDialog(
        RibbonRuntimeController runtime,
        RibbonCustomizationPolicy? policy)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _visible.Text = Localization.Get("Hiển thị");
        _search.PlaceholderText = Localization.Get("Tìm lệnh…");
        _search.AccessibleName = Localization.Get("Tìm trong danh mục lệnh");
        _large.Text = Localization.Get("Nút lớn");
        Session = new RibbonCustomizationSession(
            runtime.Definition,
            runtime.CommandCatalog,
            runtime.Customization,
            CreateCaptionResolver(runtime.Snapshot),
            policy);
        Text = Localization.Get("Tùy biến Ribbon");
        Name = "NeraRibbonCustomizationDialog";
        AccessibleName = Text;
        MinimumSize = new Size(760, 440);
        ClientSize = new Size(920, 580);
        Font = new Font("Segoe UI", 9f);
        BackColor = Color.FromArgb(247, 249, 250);
        _entries.BorderStyle = BorderStyle.FixedSingle;
        _entries.ItemHeight = 24;
        _catalog.ItemHeight = 24;
        StartPosition = FormStartPosition.CenterParent;
        BuildLayout();
        _entries.SelectedIndexChanged += (_, _) => RefreshOptions();
        _visible.Click += (_, _) => SetSelectedVisible(_visible.Checked);
        _large.Click += (_, _) => SetSelectedLarge(_large.Checked);
        RefreshEntries();
        _search.TextChanged += (_, _) => RefreshCatalog();
        RefreshCatalog();
        ApplyTheme();
        _initialized = true;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public RibbonCustomizationSession Session { get; }

    /// <summary>Gets or sets the shared Ribbon chrome palette used by this dialog.</summary>
    [DefaultValue(NeraIconTheme.Light)]
    public NeraIconTheme IconTheme
    {
        get => _iconTheme;
        set
        {
            if (_iconTheme == value) return;
            _iconTheme = value;
            ApplyTheme();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public RibbonCustomizationTarget? SelectedTarget
    {
        get => (_entries.SelectedItem as EditorRow)?.Entry.Target;
        set
        {
            _entries.SelectedItem = value is null
                ? null
                : _entries.Items.Cast<EditorRow>().FirstOrDefault(
                    row => row.Entry.Target == value);
        }
    }

    public event EventHandler? CustomizationApplied;

    public RibbonDefinition PreviewCustomization()
    {
        _runtime.SetCustomization(Session.CreateCustomization());
        return Session.Preview();
    }

    public void ApplyCustomization()
    {
        _runtime.SetCustomization(Session.Commit());
        _accepted = true;
        CustomizationApplied?.Invoke(this, EventArgs.Empty);
    }

    public void CancelCustomization()
    {
        Session.Cancel();
        _runtime.SetCustomization(Session.CreateCustomization());
        _accepted = true;
    }

    public RibbonCustomizationTarget AddCustomTab(string tabId, string caption) => Session.AddTab(tabId, caption);
    public RibbonCustomizationTarget AddCustomGroup(string tabId, string groupId, string caption) => Session.AddGroup(tabId, groupId, caption);
    public RibbonCustomizationTarget MoveCommand(RibbonCustomizationTarget source, string tabId, string groupId, int index = int.MaxValue) => Session.MoveCommand(source, tabId, groupId, index);
    public bool AddToQuickAccessToolbar(CommandId commandId) => Session.AddToQuickAccessToolbar(commandId);
    public bool RemoveFromQuickAccessToolbar(CommandId commandId) => Session.RemoveFromQuickAccessToolbar(commandId);

    public bool MoveSelected(int offset)
    {
        var target = SelectedTarget;
        if (target is null || !Session.Move(target, offset))
        {
            return false;
        }
        ApplyAndRefresh(target);
        return true;
    }

    public bool SetSelectedVisible(bool isVisible)
    {
        if (_refreshing || SelectedTarget is not { } target ||
            !Session.SetVisible(target, isVisible))
        {
            return false;
        }
        ApplyAndRefresh(target);
        return true;
    }

    public bool SetSelectedLarge(bool isLarge)
    {
        if (_refreshing || SelectedTarget is not { } target ||
            target.Kind != RibbonCustomizationTargetKind.Command ||
            !Session.SetLarge(target, isLarge))
        {
            return false;
        }
        ApplyAndRefresh(target);
        return true;
    }

    public void ResetCustomization()
    {
        Session.Reset();
        _runtime.SetCustomization(customization: null);
        RefreshEntries();
        CustomizationApplied?.Invoke(this, EventArgs.Empty);
    }

    public string SaveCustomizationJson() =>
        RibbonCustomizationJsonSerializer.Serialize(Session.CreateCustomization());

    public void LoadCustomizationJson(string json)
    {
        var customization = RibbonCustomizationJsonSerializer.Deserialize(json);
        var selected = SelectedTarget;
        Session.ReplaceCustomization(customization);
        ApplyAndRefresh(selected);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            ColumnCount = 1,
            RowCount = 4,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(new Label
        {
            Text = Localization.Get("Sắp xếp Ribbon theo cách làm việc của bạn"),
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 16),
            ForeColor = Color.FromArgb(37, 45, 51),
        }, 0, 0);
        var columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43f));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57f));
        var available = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, Padding = new Padding(0, 0, 16, 0) };
        available.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        available.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
        available.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        available.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        available.Controls.Add(new Label { Text = Localization.Get("Danh mục lệnh"), AutoSize = true, Padding = new Padding(0, 0, 0, 8) }, 0, 0);
        available.Controls.Add(_search, 0, 1);
        available.Controls.Add(_catalog, 0, 2);
        available.Controls.Add(new Label { Text = Localization.Get("Các lệnh được nhóm theo tab nguồn.\nChọn một mục bên phải để đổi hiển thị hoặc kích thước."), AutoSize = true, Padding = new Padding(0, 8, 0, 0), ForeColor = Color.FromArgb(98, 109, 119) }, 0, 3);
        var current = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        current.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        current.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        current.Controls.Add(new Label { Text = Localization.Get("Tab và nhóm hiện tại"), AutoSize = true, Padding = new Padding(0, 0, 0, 8) }, 0, 0);
        current.Controls.Add(_entries, 0, 1);
        columns.Controls.Add(available, 0, 0);
        columns.Controls.Add(current, 1, 0);
        root.Controls.Add(columns, 0, 1);

        var options = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0),
        };
        options.Controls.Add(_visible);
        options.Controls.Add(_large);
        options.Controls.Add(_selectionDetails);
        root.Controls.Add(options, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 8, 0, 0),
        };
        buttons.Controls.Add(CreateButton(Localization.Get("Áp dụng"), "RibbonCustomizationApply", ApplyCustomization));
        buttons.Controls.Add(CreateButton(Localization.Get("Hủy"), "RibbonCustomizationCancel", () => { CancelCustomization(); Close(); }));
        buttons.Controls.Add(CreateButton(
            Localization.Get("Mặc định"),
            "RibbonCustomizationReset",
            ResetCustomization));
        buttons.Controls.Add(CreateButton(
            Localization.Get("Xuống"),
            "RibbonCustomizationMoveDown",
            () => MoveSelected(1)));
        buttons.Controls.Add(CreateButton(
            Localization.Get("Lên"),
            "RibbonCustomizationMoveUp",
            () => MoveSelected(-1)));
        root.Controls.Add(buttons, 0, 3);
        Controls.Add(root);
    }

    private static Button CreateButton(string caption, string name, Action action)
    {
        var button = new Button
        {
            Text = caption,
            Name = name,
            AutoSize = true,
            MinimumSize = new Size(72, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Padding = new Padding(10, 4, 10, 4),
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(210, 218, 224);
        button.Click += (_, _) => action();
        return button;
    }

    private void ApplyAndRefresh(RibbonCustomizationTarget? selected)
    {
        _runtime.SetCustomization(Session.CreateCustomization());
        RefreshEntries(selected);
        CustomizationApplied?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (_initialized && !_accepted) CancelCustomization();
        base.OnFormClosed(e);
    }

    private void RefreshEntries(RibbonCustomizationTarget? selected = null)
    {
        selected ??= SelectedTarget;
        _refreshing = true;
        try
        {
            _entries.BeginUpdate();
            _entries.Items.Clear();
            foreach (var entry in Session.GetLocalizedEntries(Localization))
            {
                _entries.Items.Add(new EditorRow(entry));
            }
            SelectedTarget = selected;
            if (_entries.SelectedIndex < 0 && _entries.Items.Count > 0)
            {
                _entries.SelectedIndex = 0;
            }
        }
        finally
        {
            _entries.EndUpdate();
            _refreshing = false;
        }
        RefreshOptions();
    }

    private void RefreshOptions()
    {
        _refreshing = true;
        try
        {
            var entry = (_entries.SelectedItem as EditorRow)?.Entry;
            _visible.Enabled = entry is not null && !entry.IsLocked;
            _visible.Checked = entry?.IsVisible == true;
            _large.Enabled = entry?.Target.Kind == RibbonCustomizationTargetKind.Command && !entry.IsLocked;
            _large.Checked = entry?.IsLarge == true;
            _selectionDetails.Text = entry is null ? string.Empty : $"{entry.Caption}{(entry.IsLocked ? Localization.Get(" · Đã khóa bởi ứng dụng") : string.Empty)}";
        }
        finally
        {
            _refreshing = false;
        }
    }

    private static Func<CommandId, string> CreateCaptionResolver(
        RibbonPresentationSnapshot snapshot)
    {
        var captions = snapshot.Tabs
            .SelectMany(static tab => tab.Groups)
            .SelectMany(static group => group.Items)
            .DistinctBy(static item => item.Command.CommandId.Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                item => item.Command.CommandId.Value,
                item => item.Command.Caption,
                StringComparer.OrdinalIgnoreCase);
        return commandId => captions.GetValueOrDefault(commandId.Value, commandId.Value);
    }

    private void RefreshCatalog()
    {
        _catalog.BeginUpdate();
        try
        {
            _catalog.Items.Clear();
            foreach (var entry in _runtime.CommandCatalog.Entries.Where(entry =>
                         entry.Caption.Contains(_search.Text, StringComparison.CurrentCultureIgnoreCase) ||
                         entry.CategoryCaption.Contains(_search.Text, StringComparison.CurrentCultureIgnoreCase)))
            {
                _catalog.Items.Add(entry);
            }
        }
        finally
        {
            _catalog.EndUpdate();
        }
    }

    private void ApplyTheme()
    {
        var palette = NeraWinFormsRibbonPalette.For(IconTheme);
        Apply(this);
        _selectionDetails.ForeColor = palette.Muted;

        void Apply(Control control)
        {
            control.BackColor = control is TextBoxBase or ListBox ? palette.Surface : palette.Chrome;
            control.ForeColor = palette.Text;
            if (control is Button button)
            {
                button.FlatAppearance.BorderColor = palette.Separator;
                button.FlatAppearance.MouseOverBackColor = palette.Hover;
                button.FlatAppearance.MouseDownBackColor = palette.Pressed;
            }
            foreach (Control child in control.Controls) Apply(child);
        }
    }

    private sealed class EditorRow
    {
        public EditorRow(RibbonCustomizationEntry entry)
        {
            Entry = entry;
        }

        public RibbonCustomizationEntry Entry { get; }

        public override string ToString() =>
            $"{new string(' ', Entry.Depth * 4)}{Entry.Caption}";
    }
}
