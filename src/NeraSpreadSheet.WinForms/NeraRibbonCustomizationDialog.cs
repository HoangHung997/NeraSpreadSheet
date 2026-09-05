using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.WinForms;

/// <summary>
/// Native WinForms editor for a <see cref="RibbonRuntimeController"/> customization.
/// </summary>
public sealed class NeraRibbonCustomizationDialog : Form
{
    private readonly RibbonRuntimeController _runtime;
    private readonly ListBox _entries = new() { Dock = DockStyle.Fill };
    private readonly CheckBox _visible = new() { Text = "Hiển thị", AutoSize = true };
    private readonly CheckBox _large = new() { Text = "Nút lớn", AutoSize = true };
    private bool _refreshing;
    private bool _accepted;

    public NeraRibbonCustomizationDialog(RibbonRuntimeController runtime)
        : this(runtime, null)
    {
    }

    public NeraRibbonCustomizationDialog(
        RibbonRuntimeController runtime,
        RibbonCustomizationPolicy? policy)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        Session = new RibbonCustomizationSession(
            runtime.Definition,
            runtime.CommandCatalog,
            runtime.Customization,
            CreateCaptionResolver(runtime.Snapshot),
            policy);
        Text = "Tùy biến Ribbon";
        Name = "NeraRibbonCustomizationDialog";
        AccessibleName = Text;
        MinimumSize = new Size(420, 360);
        ClientSize = new Size(500, 520);
        StartPosition = FormStartPosition.CenterParent;
        BuildLayout();
        _entries.SelectedIndexChanged += (_, _) => RefreshOptions();
        _visible.Click += (_, _) => SetSelectedVisible(_visible.Checked);
        _large.Click += (_, _) => SetSelectedLarge(_large.Checked);
        RefreshEntries();
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public RibbonCustomizationSession Session { get; }

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
            Padding = new Padding(10),
            ColumnCount = 1,
            RowCount = 3,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(_entries, 0, 0);

        var options = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0),
        };
        options.Controls.Add(_visible);
        options.Controls.Add(_large);
        root.Controls.Add(options, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 8, 0, 0),
        };
        buttons.Controls.Add(CreateButton("Áp dụng", "RibbonCustomizationApply", ApplyCustomization));
        buttons.Controls.Add(CreateButton("Hủy", "RibbonCustomizationCancel", () => { CancelCustomization(); Close(); }));
        buttons.Controls.Add(CreateButton(
            "Mặc định",
            "RibbonCustomizationReset",
            ResetCustomization));
        buttons.Controls.Add(CreateButton(
            "Xuống",
            "RibbonCustomizationMoveDown",
            () => MoveSelected(1)));
        buttons.Controls.Add(CreateButton(
            "Lên",
            "RibbonCustomizationMoveUp",
            () => MoveSelected(-1)));
        root.Controls.Add(buttons, 0, 2);
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
        };
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
        if (!_accepted) CancelCustomization();
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
            foreach (var entry in Session.Entries)
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
            .ToDictionary(
                item => item.Command.CommandId.Value,
                item => item.Command.Caption,
                StringComparer.OrdinalIgnoreCase);
        return commandId => captions.GetValueOrDefault(commandId.Value, commandId.Value);
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
