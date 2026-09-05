using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Wpf;

/// <summary>
/// Native WPF editor for a <see cref="RibbonRuntimeController"/> customization.
/// Changes are applied live through the existing runtime controller.
/// </summary>
public sealed class NeraRibbonCustomizationDialog : Window
{
    private readonly RibbonRuntimeController _runtime;
    private readonly ListBox _entries = new();
    private readonly CheckBox _visible = new() { Content = "Hiển thị" };
    private readonly CheckBox _large = new() { Content = "Nút lớn" };
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
        Title = "Tùy biến Ribbon";
        Width = 520d;
        Height = 560d;
        MinWidth = 420d;
        MinHeight = 360d;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        AutomationProperties.SetAutomationId(this, "NeraRibbonCustomizationDialog");
        AutomationProperties.SetName(this, Title);
        Content = CreateContent();
        _entries.SelectionChanged += OnSelectionChanged;
        _visible.Click += (_, _) => SetSelectedVisible(_visible.IsChecked == true);
        _large.Click += (_, _) => SetSelectedLarge(_large.IsChecked == true);
        RefreshEntries();
    }

    public RibbonCustomizationSession Session { get; }

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

    private DockPanel CreateContent()
    {
        var root = new DockPanel { Margin = new Thickness(10d) };
        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0d, 8d, 0d, 0d),
        };
        footer.Children.Add(CreateButton("Lên", "MoveUp", () => MoveSelected(-1)));
        footer.Children.Add(CreateButton("Xuống", "MoveDown", () => MoveSelected(1)));
        footer.Children.Add(CreateButton("Mặc định", "Reset", ResetCustomization));
        footer.Children.Add(CreateButton("Áp dụng", "Apply", ApplyCustomization));
        footer.Children.Add(CreateButton("Hủy", "Cancel", () => { CancelCustomization(); Close(); }));
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        var options = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0d, 8d, 0d, 0d),
        };
        options.Children.Add(_visible);
        _large.Margin = new Thickness(16d, 0d, 0d, 0d);
        options.Children.Add(_large);
        DockPanel.SetDock(options, Dock.Bottom);
        root.Children.Add(options);

        _entries.SelectionMode = SelectionMode.Single;
        AutomationProperties.SetAutomationId(_entries, "RibbonCustomizationEntries");
        root.Children.Add(_entries);
        return root;
    }

    private static Button CreateButton(string caption, string automationId, Action action)
    {
        var button = new Button
        {
            Content = caption,
            MinWidth = 72d,
            Margin = new Thickness(4d, 0d, 0d, 0d),
            Padding = new Thickness(8d, 4d, 8d, 4d),
        };
        AutomationProperties.SetAutomationId(button, $"RibbonCustomization{automationId}");
        button.Click += (_, _) => action();
        return button;
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshOptions();
    }

    private void RefreshOptions()
    {
        _refreshing = true;
        try
        {
            var entry = (_entries.SelectedItem as EditorRow)?.Entry;
            _visible.IsEnabled = entry is not null && !entry.IsLocked;
            _visible.IsChecked = entry?.IsVisible;
            _large.IsEnabled = entry?.Target.Kind == RibbonCustomizationTargetKind.Command && !entry.IsLocked;
            _large.IsChecked = entry?.IsLarge;
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void ApplyAndRefresh(RibbonCustomizationTarget? selected)
    {
        _runtime.SetCustomization(Session.CreateCustomization());
        RefreshEntries(selected);
        CustomizationApplied?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (!_accepted) CancelCustomization();
        base.OnClosed(e);
    }

    private void RefreshEntries(RibbonCustomizationTarget? selected = null)
    {
        selected ??= SelectedTarget;
        _refreshing = true;
        try
        {
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
            _refreshing = false;
        }
        RefreshOptions();
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
