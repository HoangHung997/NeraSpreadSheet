using System.Text.Json;
using Microsoft.Maui.Controls;
using NeraSpreadSheet.Bars.Core;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Maui;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Maui.Windows.RibbonSmoke;

internal sealed class SmokePage : ContentPage
{
    private static readonly JsonSerializerOptions ResultJsonOptions = new()
    {
        WriteIndented = true,
    };
    private static readonly TimeSpan SmokeTimeout = TimeSpan.FromSeconds(30d);

    private readonly VerticalStackLayout _host = new() { Spacing = 8d };
    private int _finished;

    public SmokePage()
    {
        Title = "NeraSpreadSheet MAUI Ribbon smoke";
        Content = _host;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        _ = MonitorTimeoutAsync();
        Dispatcher.Dispatch(() => _ = RunSmokeAsync());
    }

    private async Task RunSmokeAsync()
    {
        try
        {
            var registry = new CommandRegistry();
            var gridlines = new ToggleHandler();
            var save = new OneShotHandler();
            var open = new OneShotHandler();
            registry.Register(
                new CommandDescriptor(
                    "view.gridlines",
                    "Đường lưới",
                    tooltip: "Bật tắt đường lưới",
                    iconKey: "view.gridlines",
                    shortcut: "Ctrl+G"),
                gridlines);
            registry.Register(
                new CommandDescriptor(
                    "file.save",
                    "Lưu",
                    tooltip: "Lưu sổ tính",
                    iconKey: "file.save",
                    shortcut: "Ctrl+S"),
                save);
            registry.Register(
                new CommandDescriptor(
                    "file.open",
                    "Mở",
                    tooltip: "Mở sổ tính",
                    iconKey: "file.open",
                    shortcut: "Ctrl+O"),
                open);

            var ribbonRuntime = new RibbonRuntimeController(
                CreateRibbonDefinition(),
                registry);
            var tableWorkbook = new Workbook();
            tableWorkbook.Worksheets[0].AddTable(new SpreadsheetTable(
                Guid.NewGuid(),
                "Sales",
                new CellRange(default, new CellAddress(2, 0)),
                [new SpreadsheetTableColumn(Guid.NewGuid(), "Item")]));
            var tableSession = new SpreadsheetSession(tableWorkbook);
            tableSession.Selection.SetActiveCell(new CellAddress(4, 4));
            var barRuntime = new BarRuntimeController(
                CreateBarDefinition(),
                registry);
            using var ribbonShortcutSource = new SmokeShortcutSource();
            using var barShortcutSource = new SmokeShortcutSource();
            var ribbon = new NeraMauiRibbonView(ribbonRuntime)
            {
                CommandContextFactory = id => new CommandContext(
                    Parameter: $"ribbon:{id.Value}"),
            };
            var bar = new NeraMauiBarPresenter(barRuntime)
            {
                CommandContextFactory = id => new CommandContext(
                    Parameter: $"bar:{id.Value}"),
            };
            var overflowRegistry = new CommandRegistry();
            var overflowItems = new List<RibbonItemDefinition>();
            var overflowHandlers = new List<ToggleHandler>();
            for (var index = 0; index < 12; index++)
            {
                var id = $"overflow.command-{index}";
                var handler = new ToggleHandler();
                overflowHandlers.Add(handler);
                overflowRegistry.Register(
                    new CommandDescriptor(
                        id,
                        $"Lệnh {index}",
                        iconKey: "missing.icon.key"),
                    handler);
                overflowItems.Add(new RibbonItemDefinition(id));
            }
            using var overflowRibbon = new NeraMauiRibbonView(
                new RibbonRuntimeController(
                    new RibbonDefinition([
                        new RibbonTabDefinition("overflow", "Thu gọn", [
                            new RibbonGroupDefinition(
                                "commands",
                                "Lệnh",
                                overflowItems),
                        ]),
                    ]),
                    overflowRegistry))
            {
                WidthRequest = 70d,
                MaximumWidthRequest = 70d,
                HorizontalOptions = LayoutOptions.Start,
            };
            var complexRegistry = new CommandRegistry();
            var complexHandlers = new Dictionary<RibbonItemKind, SelectionHandler>();
            var complexItems = new List<RibbonItemDefinition>();
            var complexOrder = 0;
            foreach (var kind in Enum.GetValues<RibbonItemKind>()
                         .Where(static kind => kind != RibbonItemKind.Separator))
            {
                var handler = new SelectionHandler(
                    kind == RibbonItemKind.Button
                        ? true
                        : kind == RibbonItemKind.Toggle ? false : null,
                    kind == RibbonItemKind.Gallery
                        ? 12
                        : kind == RibbonItemKind.ComboBox ? 3 : 2,
                    kind == RibbonItemKind.ComboBox ? 3 : null);
                complexHandlers.Add(kind, handler);
                complexRegistry.Register(
                    new CommandDescriptor(
                        $"complex.{kind}",
                        kind.ToString(),
                        tooltip: $"Tooltip {kind}",
                        iconKey: "missing.icon.key"),
                    handler);
                complexItems.Add(new RibbonItemDefinition(
                    $"complex.{kind}",
                    kind,
                    order: complexOrder++,
                    automationName: $"Automation {kind}",
                    measurement: static context => context.Kind switch
                    {
                        RibbonItemKind.Gallery => 160d,
                        RibbonItemKind.ComboBox or RibbonItemKind.ColorPicker => 120d,
                        _ => context.DefaultWidth,
                    }));
            }
            complexItems.Add(RibbonItemDefinition.Separator("primary", complexOrder));
            using var complexRibbon = new NeraMauiRibbonView(
                new RibbonRuntimeController(
                    new RibbonDefinition([
                        new RibbonTabDefinition("complex", "Phức hợp", [
                            new RibbonGroupDefinition("items", "Mục", complexItems),
                        ]),
                    ]),
                    complexRegistry))
            {
                WidthRequest = 1_600d,
            };
            var compactRuntime = new RibbonRuntimeController(
                new RibbonDefinition([
                    new RibbonTabDefinition("compact", "Thu gọn", [
                        new RibbonGroupDefinition("display", "Hiển thị", [
                            new RibbonItemDefinition("view.gridlines"),
                        ]),
                    ]),
                ]),
                registry);
            using var compactRibbon = new NeraMauiRibbonView(compactRuntime)
            {
                WidthRequest = 70d,
                MaximumWidthRequest = 70d,
                HorizontalOptions = LayoutOptions.Start,
            };
            using var ribbonShortcut = ribbon.BindShortcuts(ribbonShortcutSource);
            using var tableDesignBinding = ribbon.BindTableDesign(tableSession);
            using var barShortcut = bar.BindShortcuts(barShortcutSource);
            var focusOrigin = new Button
            {
                Text = "Ô trang tính giả lập",
                AutomationId = "worksheet-focus-origin",
            };

            _host.Children.Add(ribbon);
            _host.Children.Add(bar);
            _host.Children.Add(overflowRibbon);
            _host.Children.Add(complexRibbon);
            _host.Children.Add(compactRibbon);
            _host.Children.Add(focusOrigin);
            await Task.Delay(500).ConfigureAwait(true);
            overflowRibbon.Rebuild();
            complexRibbon.Rebuild();
            compactRibbon.Rebuild();

            Require(ribbon.Handler?.PlatformView is not null,
                "The MAUI Ribbon view did not receive a native platform view.");
            Require(ribbon.LayoutSnapshot.SelectedTabId == "view" &&
                    ribbon.LayoutSnapshot.Tabs.Count == 1,
                "The MAUI Ribbon did not consume the shared responsive layout snapshot.");
            tableSession.Selection.SetActiveCell(new CellAddress(1, 0));
            await Task.Delay(100).ConfigureAwait(true);
            Require(ribbon.LayoutSnapshot.Tabs.Count == 2,
                "The MAUI contextual Table Design tab did not follow table selection state.");
            tableSession.Selection.SetActiveCell(new CellAddress(4, 4));
            await Task.Delay(100).ConfigureAwait(true);
            Require(ribbon.LayoutSnapshot.Tabs.Count == 1,
                "The MAUI contextual Table Design tab did not hide outside the Table.");
            tableSession.Selection.SetActiveCell(new CellAddress(1, 0));
            await Task.Delay(100).ConfigureAwait(true);
            Require(((Grid)ribbon.Content).Children.OfType<HorizontalStackLayout>()
                    .SelectMany(static layout => layout.Children.OfType<Button>())
                    .Any(static button => button.AutomationId == "ribbon-qat-view.gridlines"),
                "The MAUI Ribbon did not render its QAT command.");
            var fileButton = ((Grid)ribbon.Content).Children.OfType<HorizontalStackLayout>()
                .SelectMany(static layout => layout.Children.OfType<Button>())
                .Single(static button => button.AutomationId == "ribbon-file");
            fileButton.SendClicked();
            await Task.Delay(100).ConfigureAwait(true);
            Require(ribbon.IsBackstageOpen && ((Grid)ribbon.Content).Children
                    .OfType<VerticalStackLayout>()
                    .Single().Children.OfType<Button>()
                    .Any(static button => button.AutomationId == "ribbon-backstage-file.open"),
                "The MAUI Ribbon did not open its accessible backstage surface.");
            ((Grid)ribbon.Content).Children.OfType<HorizontalStackLayout>()
                .SelectMany(static layout => layout.Children.OfType<Button>())
                .Single(static button => button.AutomationId == "ribbon-file")
                .SendClicked();
            await Task.Delay(100).ConfigureAwait(true);
            ribbon.IsMinimized = true;
            await Task.Delay(100).ConfigureAwait(true);
            var minimizedRoot = (Grid)ribbon.Content;
            Require(ribbon.IsMinimized && minimizedRoot.Children
                    .OfType<VisualElement>()
                    .Where(child => minimizedRoot.GetRow(child) is 2 or 3)
                    .All(static child => !child.IsVisible),
                "The minimized MAUI Ribbon left groups or overflow visible.");
            Require(focusOrigin.Focus(),
                "The MAUI worksheet focus origin could not receive focus.");
            await Task.Delay(100).ConfigureAwait(true);
            ribbon.EnterKeyTipModeWithFocusOrigin(focusOrigin);
            Require(await ribbon.ProcessKeyTipCharacterAsync('F') &&
                    ribbon.IsBackstageOpen &&
                    ((Grid)ribbon.Content).Children.OfType<VerticalStackLayout>()
                        .Single().IsVisible,
                "The MAUI F key tip did not reveal the backstage surface.");
            ribbon.EscapeKeyTipMode();
            ribbon.EscapeKeyTipMode();
            await Task.Delay(100).ConfigureAwait(true);
            Require(ribbon.KeyTipScope == RibbonKeyTipScope.Inactive,
                "The MAUI Ribbon did not unwind key-tip scopes with Escape.");
            Require(focusOrigin.IsFocused,
                "The MAUI Ribbon did not restore external focus after rebuilding Key Tips.");
            ribbon.IsMinimized = false;
            Require(bar.Handler?.PlatformView is not null,
                "The MAUI Bar presenter did not receive a native platform view.");
            Require(ribbon.CommandButtons.Count == 1,
                "The MAUI Ribbon did not render the expected command.");
            Require(bar.CommandButtons.Count == 1,
                "The MAUI Bar did not render the expected command.");
            Require(ribbon.CommandButtons[0].ImageSource is not null,
                "The MAUI Ribbon did not resolve its default command icon.");
            Require(bar.CommandButtons[0].ImageSource is not null,
                "The MAUI Bar did not resolve its default command icon.");
            compactRibbon.EnterKeyTipMode();
            Require(await compactRibbon.ProcessKeyTipAsync(
                    compactRuntime.KeyTips.TabTips["compact"]),
                "The compact MAUI Ribbon did not enter its command key-tip scope.");
            Require(compactRibbon.LayoutSnapshot.Tabs[0].Groups[0].Items[0].Size ==
                    RibbonItemSize.Compact &&
                    compactRibbon.CommandButtons.Single().Text.Contains('[') &&
                    compactRibbon.CommandButtons.Single().ImageSource is not null,
                "The compact MAUI command did not keep both its icon and textual key tip.");
            Require(overflowRibbon.LayoutSnapshot.Tabs[0].HasOverflow,
                "The narrow MAUI Ribbon did not expose its overflow surface.");
            Require(overflowRibbon.CommandButtons.Count == 12 &&
                    overflowRibbon.CommandButtons.All(static button =>
                        !string.IsNullOrWhiteSpace(button.Text)),
                "A missing compact icon hid its MAUI command caption.");
            var overflowRoot = (Grid)overflowRibbon.Content;
            var overflowHost = overflowRoot.Children.OfType<ScrollView>()
                .Single(static scroll =>
                    scroll.Orientation == ScrollOrientation.Vertical);
            Require(overflowHost.MaximumHeightRequest == 360d &&
                    overflowHost.Content is VerticalStackLayout,
                "The MAUI overflow surface is not bounded and scrollable.");
            var overflowButton = overflowRoot.Children
                .OfType<HorizontalStackLayout>()
                .SelectMany(static layout => layout.Children.OfType<Button>())
                .Single(static button => button.AutomationId == "ribbon-overflow");
            overflowButton.SendClicked();
            await Task.Delay(100).ConfigureAwait(true);
            Require(overflowHost.IsVisible &&
                    ((VerticalStackLayout)overflowHost.Content).IsVisible,
                "The MAUI overflow button did not reveal usable command content.");
            overflowRibbon.CommandButtons[0].SendClicked();
            await Task.Delay(100).ConfigureAwait(true);
            Require(overflowHandlers[0].ExecutionCount == 1,
                "A loaded command inside the MAUI overflow did not activate.");
            Require(complexRibbon.ItemControls.Count ==
                    Enum.GetValues<RibbonItemKind>().Length,
                "The MAUI Ribbon did not render every complex item kind.");
            Require(complexRibbon.ItemControls.All(static control =>
                    control.Handler?.PlatformView is not null),
                "A complex MAUI Ribbon item did not receive a native platform view.");
            var separator = complexRibbon.ItemControls.OfType<BoxView>().Single();
            Require(separator.WidthRequest == 8d &&
                    separator.Margin.Left + separator.Margin.Right == 0d,
                "The MAUI separator did not occupy its measured logical width.");
            var gallery = complexRibbon.ItemControls.OfType<ScrollView>()
                .Single(static scroll =>
                    scroll.AutomationId == "ribbon-command-complex.Gallery");
            Require(gallery.Orientation == ScrollOrientation.Horizontal &&
                    gallery.Content is HorizontalStackLayout galleryItems &&
                    galleryItems.Children.Count == 12 &&
                    galleryItems.Children.OfType<Button>().All(static button =>
                        button.ImageSource is not null),
                "The MAUI gallery is not horizontally scrollable with item icons.");
            var toggleButton = complexRibbon.CommandButtons.Single(static button =>
                button.AutomationId == "ribbon-command-complex.Toggle");
            Require(toggleButton.BorderWidth == 1d &&
                    SemanticProperties.GetDescription(toggleButton).Contains(
                        "Đang tắt",
                        StringComparison.Ordinal) &&
                    SemanticProperties.GetHint(toggleButton).Contains(
                        "Tooltip Toggle",
                        StringComparison.Ordinal),
                "The MAUI toggle did not expose visible and accessible unchecked state.");
            var split = complexRibbon.ItemControls.OfType<VerticalStackLayout>()
                .Single(static stack => stack.Children
                    .OfType<HorizontalStackLayout>()
                    .SelectMany(static row => row.Children.OfType<Button>())
                    .Any(static button => button.AutomationId ==
                        "ribbon-command-complex.SplitButton-primary"));
            var splitButtons = split.Children.OfType<HorizontalStackLayout>()
                .Single().Children.OfType<Button>().ToArray();
            Require(splitButtons.Select(static button => button.AutomationId)
                    .Order(StringComparer.Ordinal)
                    .SequenceEqual(
                    [
                        "ribbon-command-complex.SplitButton-menu",
                        "ribbon-command-complex.SplitButton-primary",
                    ],
                    StringComparer.Ordinal),
                "The MAUI split-button subparts do not have stable unique identities.");
            Require(splitButtons.Single(static button =>
                    button.AutomationId.EndsWith("-menu", StringComparison.Ordinal)).Focus(),
                "The MAUI split disclosure could not receive focus.");
            await Task.Delay(100).ConfigureAwait(true);
            Require(splitButtons.Single(static button =>
                    button.AutomationId.EndsWith("-menu", StringComparison.Ordinal)).IsFocused,
                "The loaded MAUI split disclosure did not acquire focus.");
            complexRibbon.Rebuild();
            await Task.Delay(100).ConfigureAwait(true);
            var rebuiltSplit = complexRibbon.ItemControls.OfType<VerticalStackLayout>()
                .Single(static stack => stack.Children
                    .OfType<HorizontalStackLayout>()
                    .SelectMany(static row => row.Children.OfType<Button>())
                    .Any(static button => button.AutomationId ==
                        "ribbon-command-complex.SplitButton-primary"));
            Require(rebuiltSplit.Children.OfType<HorizontalStackLayout>()
                    .Single().Children.OfType<Button>()
                    .Single(static button =>
                        button.AutomationId.EndsWith("-menu", StringComparison.Ordinal))
                    .IsFocused,
                "The MAUI split-button did not restore the focused subpart.");
            var combo = complexRibbon.ItemControls.OfType<Picker>().First();
            combo.SelectedIndex = 1;
            await Task.Delay(100).ConfigureAwait(true);
            Require(complexHandlers[RibbonItemKind.ComboBox].SelectedValue == "two" &&
                    complexHandlers[RibbonItemKind.ComboBox].ExecutionCount == 1,
                "The MAUI combo item did not activate its selected value.");

            Require(await ribbon.TryActivateCommandAsync("view.gridlines"),
                "The MAUI Ribbon command did not activate through runtime.");
            Require(gridlines.ExecutionCount == 1 &&
                    Equals(gridlines.LastParameter, "ribbon:view.gridlines"),
                "The MAUI Ribbon command context was not supplied.");
            Require(NeraMauiCommandChrome.GetIsCommandChecked(
                    ribbon.CommandButtons[0]) == true,
                "The MAUI Ribbon did not refresh checked state after activation.");

            Require(ribbonShortcutSource.Raise("Ctrl+O"),
                "The MAUI shortcut source did not mark a backstage shortcut handled.");
            await Task.Delay(100).ConfigureAwait(true);
            Require(open.ExecutionCount == 1 &&
                    Equals(open.LastParameter, "ribbon:file.open"),
                "The MAUI backstage shortcut did not activate through Ribbon runtime.");

            Require(barShortcutSource.Raise("Ctrl+S"),
                "The MAUI shortcut source did not mark the Bar shortcut handled.");
            await Task.Delay(100).ConfigureAwait(true);
            Require(save.ExecutionCount == 1 &&
                    Equals(save.LastParameter, "bar:file.save"),
                "The MAUI Bar shortcut did not activate through runtime.");
            Require(!bar.CommandButtons[0].IsEnabled,
                "The MAUI Bar did not refresh enabled state after shortcut activation.");

            var customization = new NeraMauiRibbonCustomizationBinding(
                ribbonRuntime);
            var customTab = customization.AddCustomTab("custom", "Cá nhân");
            var customGroup = customization.AddCustomGroup(customTab.TabId, "quick", "Lệnh nhanh");
            customization.MoveCommand(
                RibbonCustomizationTarget.Command("view", "display", "view.gridlines"),
                customTab.TabId,
                customGroup.GroupId!);
            customization.Preview();
            Require(ribbonRuntime.Snapshot.Tabs.Any(static tab => tab.Id == "custom"),
                "The loaded MAUI Ribbon did not preview a custom tab/group and moved command.");
            customization.Cancel();
            Require(ribbonRuntime.Snapshot.Tabs.All(static tab => tab.Id != "custom"),
                "The loaded MAUI Ribbon did not roll back a structural preview.");
            Require(customization.SetVisible(
                    RibbonCustomizationTarget.Command(
                        "view",
                        "display",
                        "view.gridlines"),
                    false),
                "The MAUI Ribbon customization did not hide the command.");
            Require(ribbon.CommandButtons.Count == 0,
                "The MAUI Ribbon did not rebuild after customization.");
            customization.Reset();
            Require(ribbon.CommandButtons.Count == 1,
                "The MAUI Ribbon did not rebuild after customization reset.");

            CompleteSuccessfully();
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private void CompleteSuccessfully()
    {
        if (Interlocked.Exchange(ref _finished, 1) != 0)
        {
            return;
        }

        WriteResult(new
        {
            status = "success",
            frameCount = 3,
            ribbonCommand = "view.gridlines",
            barCommand = "file.save",
            shortcut = "Ctrl+S",
            customization = "structural-preview-cancel-hide-reset",
            overflow = "bounded-scroll",
            complexItems = "all-kinds-selection",
            tableDesign = "selection-context-binding",
        });
        Environment.Exit(0);
    }

    private void Fail(Exception exception)
    {
        if (Interlocked.Exchange(ref _finished, 1) != 0)
        {
            return;
        }

        try
        {
            WriteResult(new
            {
                status = "failure",
                frameCount = 0,
                error = exception.ToString(),
            });
        }
        finally
        {
            Environment.Exit(1);
        }
    }

    private async Task MonitorTimeoutAsync()
    {
        await Task.Delay(SmokeTimeout).ConfigureAwait(false);
        if (Volatile.Read(ref _finished) != 0)
        {
            return;
        }

        Dispatcher.Dispatch(() =>
        {
            if (Volatile.Read(ref _finished) == 0)
            {
                Fail(new TimeoutException(
                    $"The loaded Ribbon smoke did not complete within {SmokeTimeout}."));
            }
        });
    }

    private static RibbonDefinition CreateRibbonDefinition() =>
        new(
        [
            new RibbonTabDefinition(
                "view",
                "Xem",
                [
                    new RibbonGroupDefinition(
                        "display",
                        "Hiển thị",
                        [new RibbonItemDefinition("view.gridlines")]),
                ]),
            new RibbonTabDefinition(
                "table-design",
                "Thiết kế Bảng",
                [
                    new RibbonGroupDefinition(
                        "table",
                        "Bảng",
                        [new RibbonItemDefinition("view.gridlines")]),
                ]),
        ],
        [new RibbonContextualTabRule("table-design", RibbonContextRequirement.Table, "TB")],
        [new RibbonCommandSurfaceItem("view.gridlines", "1")],
        [new RibbonCommandSurfaceItem("file.open", "O")]);

    private static BarDefinition CreateBarDefinition() =>
        new(
            "main",
            BarKind.MainMenu,
            [
                BarItemDefinition.Command("file.save"),
            ]);

    private static void WriteResult(object result)
    {
        var path = Environment.GetEnvironmentVariable("NERA_MAUI_SMOKE_RESULT");
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException(
                "NERA_MAUI_SMOKE_RESULT must identify the smoke result file.");
        }

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException(
                "The Ribbon smoke result file has no parent directory."));
        File.WriteAllText(
            fullPath,
            JsonSerializer.Serialize(result, ResultJsonOptions));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class SmokeShortcutSource :
        INeraMauiShortcutSource,
        IDisposable
    {
        public event EventHandler<NeraMauiShortcutEventArgs>? ShortcutPressed;

        public bool Raise(string shortcut)
        {
            var args = new NeraMauiShortcutEventArgs(shortcut);
            ShortcutPressed?.Invoke(this, args);
            return args.Handled;
        }

        public void Dispose()
        {
            ShortcutPressed = null;
        }
    }

    private sealed class ToggleHandler : IStatefulCommandHandler
    {
        private bool _isChecked;

        public int ExecutionCount { get; private set; }

        public object? LastParameter { get; private set; }

        public bool CanExecute(CommandContext context) => true;

        public CommandState GetState(CommandContext context) =>
            new(true, _isChecked);

        public ValueTask ExecuteAsync(CommandContext context)
        {
            ExecutionCount++;
            LastParameter = context.Parameter;
            _isChecked = !_isChecked;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class OneShotHandler : IStatefulCommandHandler
    {
        public int ExecutionCount { get; private set; }

        public object? LastParameter { get; private set; }

        public bool CanExecute(CommandContext context) => ExecutionCount == 0;

        public CommandState GetState(CommandContext context) =>
            new(CanExecute(context));

        public ValueTask ExecuteAsync(CommandContext context)
        {
            ExecutionCount++;
            LastParameter = context.Parameter;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SelectionHandler : IStatefulCommandHandler
    {
        private readonly bool? _isChecked;
        private readonly IReadOnlyList<CommandItem> _items;

        public SelectionHandler(
            bool? isChecked,
            int itemCount,
            int? disabledIndex)
        {
            _isChecked = isChecked;
            _items = Enumerable.Range(1, itemCount)
                .Select(index => new CommandItem(
                    index == disabledIndex
                        ? "disabled"
                        : index == 1 ? "one" : index == 2 ? "two" : $"choice-{index}",
                    $"Mục {index}",
                    isEnabled: index != disabledIndex,
                    iconKey: "file.new"))
                .ToArray();
        }

        public string? SelectedValue { get; private set; } = "one";

        public int ExecutionCount { get; private set; }

        public bool CanExecute(CommandContext context) => true;

        public CommandState GetState(CommandContext context) => new(
            true,
            _isChecked,
            null,
            SelectedValue,
            _items);

        public ValueTask ExecuteAsync(CommandContext context)
        {
            ExecutionCount++;
            if (context.Parameter is RibbonItemActivation activation)
            {
                SelectedValue = activation.SelectedValue;
            }
            return ValueTask.CompletedTask;
        }
    }
}
