using System.Text.Json;
using Microsoft.Maui.Controls;
using NeraSpreadSheet.Bars.Core;
using NeraSpreadSheet.Commands;
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

            var ribbonRuntime = new RibbonRuntimeController(
                CreateRibbonDefinition(),
                registry);
            var barRuntime = new BarRuntimeController(
                CreateBarDefinition(),
                registry);
            using var shortcutSource = new SmokeShortcutSource();
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
            for (var index = 0; index < 12; index++)
            {
                var id = $"overflow.command-{index}";
                overflowRegistry.Register(
                    new CommandDescriptor(
                        id,
                        $"Lệnh {index}",
                        iconKey: "missing.icon.key"),
                    new ToggleHandler());
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
                var handler = new SelectionHandler();
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
            using var ribbonShortcut = ribbon.BindShortcuts(shortcutSource);
            using var barShortcut = bar.BindShortcuts(shortcutSource);

            _host.Children.Add(ribbon);
            _host.Children.Add(bar);
            _host.Children.Add(overflowRibbon);
            _host.Children.Add(complexRibbon);
            await Task.Delay(500).ConfigureAwait(true);
            overflowRibbon.Rebuild();
            complexRibbon.Rebuild();

            Require(ribbon.Handler?.PlatformView is not null,
                "The MAUI Ribbon view did not receive a native platform view.");
            Require(ribbon.LayoutSnapshot.SelectedTabId == "view" &&
                    ribbon.LayoutSnapshot.Tabs.Count == 1,
                "The MAUI Ribbon did not consume the shared responsive layout snapshot.");
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
            Require(complexRibbon.ItemControls.Count ==
                    Enum.GetValues<RibbonItemKind>().Length,
                "The MAUI Ribbon did not render every complex item kind.");
            Require(complexRibbon.ItemControls.All(static control =>
                    control.Handler?.PlatformView is not null),
                "A complex MAUI Ribbon item did not receive a native platform view.");
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

            Require(shortcutSource.Raise("Ctrl+S"),
                "The MAUI shortcut source did not mark the Bar shortcut handled.");
            Require(save.ExecutionCount == 1 &&
                    Equals(save.LastParameter, "bar:file.save"),
                "The MAUI Bar shortcut did not activate through runtime.");
            Require(!bar.CommandButtons[0].IsEnabled,
                "The MAUI Bar did not refresh enabled state after shortcut activation.");

            var customization = new NeraMauiRibbonCustomizationBinding(
                ribbonRuntime);
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
            customization = "hide-reset",
            overflow = "bounded-scroll",
            complexItems = "all-kinds-selection",
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
        ]);

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
        public string? SelectedValue { get; private set; } = "one";

        public int ExecutionCount { get; private set; }

        public bool CanExecute(CommandContext context) => true;

        public CommandState GetState(CommandContext context) => new(
            true,
            SelectedValue: SelectedValue,
            ItemsSource:
            [
                new CommandItem("one", "Một"),
                new CommandItem("two", "Hai"),
            ]);

        public ValueTask ExecuteAsync(CommandContext context)
        {
            ExecutionCount++;
            SelectedValue = ((RibbonItemActivation)context.Parameter!).SelectedValue;
            return ValueTask.CompletedTask;
        }
    }
}
