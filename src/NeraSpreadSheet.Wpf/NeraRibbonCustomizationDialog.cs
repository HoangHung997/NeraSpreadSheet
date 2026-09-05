using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Iconography;
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
    private readonly ListBox _catalog = new();
    private readonly TextBox _search = new();
    private readonly TextBlock _selectionCaption = new();
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
        Session = new RibbonCustomizationSession(
            runtime.Definition,
            runtime.CommandCatalog,
            runtime.Customization,
            CreateCaptionResolver(runtime.Snapshot),
            policy);
        Title = "Tùy biến Ribbon";
        Width = 900d;
        Height = 640d;
        MinWidth = 740d;
        MinHeight = 460d;
        NeraRibbonChrome.Install(this);
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 13d;
        SetResourceReference(BackgroundProperty, "RibbonSurface");
        SetResourceReference(ForegroundProperty, "RibbonForeground");
        _visible.SetResourceReference(Control.ForegroundProperty, "RibbonForeground");
        _large.SetResourceReference(Control.ForegroundProperty, "RibbonForeground");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        AutomationProperties.SetAutomationId(this, "NeraRibbonCustomizationDialog");
        AutomationProperties.SetName(this, Title);
        Content = CreateContent();
        _entries.SelectionChanged += OnSelectionChanged;
        _visible.Click += (_, _) => SetSelectedVisible(_visible.IsChecked == true);
        _large.Click += (_, _) => SetSelectedLarge(_large.IsChecked == true);
        _search.TextChanged += (_, _) => RefreshCatalog();
        RefreshEntries();
        RefreshCatalog();
        _initialized = true;
    }

    public RibbonCustomizationSession Session { get; }

    /// <summary>Gets or sets the shared Ribbon chrome and icon palette used by this dialog.</summary>
    public NeraIconTheme IconTheme
    {
        get => _iconTheme;
        set
        {
            if (_iconTheme == value) return;
            _iconTheme = value;
            NeraRibbonChrome.ApplyTheme(this, value);
        }
    }

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
        var root = new DockPanel { Margin = new Thickness(24d) };
        var heading = new StackPanel { Margin = new Thickness(0d, 0d, 0d, 22d) };
        heading.Children.Add(new TextBlock { Text = Title, FontSize = 24d, FontWeight = FontWeights.SemiBold });
        heading.Children.Add(CreateMutedText("Sắp xếp lệnh theo cách bạn làm việc. Thay đổi được xem trước trên Ribbon.", new Thickness(0d, 8d, 0d, 0d)));
        DockPanel.SetDock(heading, Dock.Top);
        root.Children.Add(heading);
        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0d, 18d, 0d, 0d),
        };
        footer.Children.Add(CreateButton("Mặc định", "Reset", ResetCustomization));
        var apply = CreateButton("Áp dụng", "Apply", ApplyCustomization);
        apply.SetResourceReference(Control.BackgroundProperty, "RibbonChecked");
        apply.SetResourceReference(Control.BorderBrushProperty, "RibbonAccent");
        footer.Children.Add(apply);
        footer.Children.Add(CreateButton("Hủy", "Cancel", () => { CancelCustomization(); Close(); }));
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition());
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24d) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.25d, GridUnitType.Star) });
        root.Children.Add(body);

        var available = new DockPanel();
        var availableHeader = new StackPanel();
        availableHeader.Children.Add(new TextBlock { Text = "Lệnh có sẵn", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0d, 0d, 0d, 10d) });
        _search.Height = 32d;
        _search.Padding = new Thickness(8d, 5d, 8d, 5d);
        _search.ToolTip = "Tìm lệnh theo tên hoặc danh mục";
        AutomationProperties.SetAutomationId(_search, "RibbonCustomizationSearch");
        AutomationProperties.SetName(_search, "Tìm lệnh");
        availableHeader.Children.Add(_search);
        availableHeader.Children.Add(CreateMutedText("Tìm kiếm theo tên lệnh hoặc tab", new Thickness(0d, 5d, 0d, 10d), 11d));
        DockPanel.SetDock(availableHeader, Dock.Top);
        available.Children.Add(availableHeader);
        _catalog.DisplayMemberPath = nameof(RibbonCommandCatalogEntry.Caption);
        _catalog.ItemContainerStyle = CreateCatalogRowStyle();
        _catalog.SetResourceReference(Control.BorderBrushProperty, "RibbonDivider");
        _catalog.SetResourceReference(Control.BackgroundProperty, "RibbonSurface");
        _catalog.SetResourceReference(Control.ForegroundProperty, "RibbonForeground");
        AutomationProperties.SetAutomationId(_catalog, "RibbonCustomizationCatalog");
        AutomationProperties.SetName(_catalog, "Lệnh có sẵn");
        available.Children.Add(_catalog);
        body.Children.Add(available);

        var current = new DockPanel();
        Grid.SetColumn(current, 2);
        body.Children.Add(current);
        var currentHeader = new TextBlock { Text = "Ribbon hiện tại", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0d, 0d, 0d, 12d) };
        DockPanel.SetDock(currentHeader, Dock.Top);
        current.Children.Add(currentHeader);
        var details = new StackPanel { Margin = new Thickness(0d, 12d, 0d, 0d) };
        _selectionCaption.FontWeight = FontWeights.SemiBold;
        _selectionCaption.Margin = new Thickness(0d, 0d, 0d, 8d);
        details.Children.Add(_selectionCaption);
        var options = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0d, 8d, 0d, 0d),
        };
        options.Children.Add(_visible);
        _large.Margin = new Thickness(16d, 0d, 0d, 0d);
        options.Children.Add(_large);
        details.Children.Add(options);
        var ordering = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0d, 12d, 0d, 0d) };
        ordering.Children.Add(CreateButton("↑ Lên", "MoveUp", () => MoveSelected(-1)));
        ordering.Children.Add(CreateButton("↓ Xuống", "MoveDown", () => MoveSelected(1)));
        details.Children.Add(ordering);
        DockPanel.SetDock(details, Dock.Bottom);
        current.Children.Add(details);

        _entries.SelectionMode = SelectionMode.Single;
        _entries.SetResourceReference(Control.BorderBrushProperty, "RibbonDivider");
        _entries.SetResourceReference(Control.BackgroundProperty, "RibbonSurface");
        _entries.SetResourceReference(Control.ForegroundProperty, "RibbonForeground");
        _entries.ItemTemplate = CreateEntryTemplate();
        _entries.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        AutomationProperties.SetAutomationId(_entries, "RibbonCustomizationEntries");
        AutomationProperties.SetName(_entries, "Cấu trúc Ribbon hiện tại");
        current.Children.Add(_entries);
        return root;
    }

    private Style CreateCatalogRowStyle()
    {
        var style = new Style(typeof(ListBoxItem), (Style)FindResource(typeof(ListBoxItem)));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8d, 6d, 8d, 6d)));
        style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new Binding(nameof(RibbonCommandCatalogEntry.CategoryCaption))));
        return style;
    }

    private static TextBlock CreateMutedText(string caption, Thickness margin, double fontSize = 13d)
    {
        var text = new TextBlock { Text = caption, Margin = margin, FontSize = fontSize, TextWrapping = TextWrapping.Wrap };
        text.SetResourceReference(TextBlock.ForegroundProperty, "RibbonMuted");
        return text;
    }

    private static DataTemplate CreateEntryTemplate()
    {
        var template = new DataTemplate(typeof(EditorRow));
        var row = new FrameworkElementFactory(typeof(TextBlock));
        row.SetBinding(TextBlock.TextProperty, new Binding(nameof(EditorRow.Caption)));
        row.SetBinding(FrameworkElement.MarginProperty, new Binding(nameof(EditorRow.Indent)));
        row.SetBinding(TextBlock.FontWeightProperty, new Binding(nameof(EditorRow.Weight)));
        row.SetBinding(UIElement.OpacityProperty, new Binding(nameof(EditorRow.Opacity)));
        template.VisualTree = row;
        return template;
    }

    private void RefreshCatalog()
    {
        var query = _search.Text.Trim();
        _catalog.ItemsSource = _runtime.CommandCatalog.Entries.Where(entry =>
            query.Length == 0 || entry.Caption.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            entry.CategoryCaption.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToArray();
    }

    private static Button CreateButton(string caption, string automationId, Action action)
    {
        var button = new Button
        {
            Content = caption,
            MinWidth = 72d,
            Height = 32d,
            Margin = new Thickness(4d, 0d, 0d, 0d),
            Padding = new Thickness(8d, 4d, 8d, 4d),
        };
        AutomationProperties.SetAutomationId(button, $"RibbonCustomization{automationId}");
        AutomationProperties.SetName(button, caption);
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
            _selectionCaption.Text = entry?.Caption ?? "Chọn tab, nhóm hoặc lệnh";
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
        if (_initialized && !_accepted) CancelCustomization();
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
            .DistinctBy(static item => item.Command.CommandId.Value, StringComparer.OrdinalIgnoreCase)
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

        public string Caption => Entry.Caption;

        public Thickness Indent => new(8d + (Entry.Depth * 18d), 5d, 8d, 5d);

        public FontWeight Weight => Entry.Target.Kind == RibbonCustomizationTargetKind.Command ? FontWeights.Normal : FontWeights.SemiBold;

        public double Opacity => Entry.IsVisible ? 1d : 0.45d;

        public override string ToString() =>
            $"{new string(' ', Entry.Depth * 4)}{Entry.Caption}";
    }
}
