using Microsoft.Maui.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Bars.Core;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Maui;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Maui.Tests;

[TestClass]
public sealed class NeraMauiRibbonBarPresenterTests
{
    [TestMethod]
    public async Task RibbonMauiDescriptorShouldTrackRuntimeSnapshot()
    {
        var registry = new CommandRegistry();
        var handler = new ToggleHandler();
        registry.Register(
            new CommandDescriptor(
                "view.gridlines",
                "Đường lưới",
                tooltip: "Bật tắt đường lưới",
                shortcut: "Ctrl+G"),
            handler);
        var runtime = new RibbonRuntimeController(
            CreateRibbonDefinition(),
            registry);

        var descriptor = NeraMauiCommandChromeDescriptor.From(
            runtime.Snapshot.Tabs[0].Groups[0].Items[0].Command,
            "ribbon-command",
            isLarge: true);

        Assert.AreEqual("Đường lưới", descriptor.Caption);
        Assert.AreEqual("ribbon-command-view.gridlines", descriptor.AutomationId);
        Assert.AreEqual(
            "view.gridlines",
            descriptor.CommandId);
        Assert.AreEqual("Ctrl+G", descriptor.Shortcut);
        Assert.IsTrue(descriptor.IsLarge);
        Assert.IsFalse(descriptor.IsChecked);

        Assert.IsTrue(await runtime.TryActivateAsync(
            "view.gridlines",
            new CommandContext(Parameter: "maui:view.gridlines")));
        var refreshed = NeraMauiCommandChromeDescriptor.From(
            runtime.Snapshot.Tabs[0].Groups[0].Items[0].Command,
            "ribbon-command");

        Assert.AreEqual(1, handler.ExecutionCount);
        Assert.AreEqual("maui:view.gridlines", handler.LastParameter);
        Assert.IsTrue(refreshed.IsChecked);
    }

    [TestMethod]
    public async Task BarMauiDescriptorShouldTrackNestedRuntimeSnapshot()
    {
        var registry = new CommandRegistry();
        var handler = new OneShotHandler();
        registry.Register(
            new CommandDescriptor(
                "file.save",
                "Lưu",
                tooltip: "Lưu sổ tính",
                shortcut: "Ctrl+S"),
            handler);
        var runtime = new BarRuntimeController(CreateBarDefinition(), registry);

        var descriptor = NeraMauiCommandChromeDescriptor.From(
            runtime.Snapshot.Items[0].Children[0].Command!,
            "bar-command");

        Assert.AreEqual("Lưu", descriptor.Caption);
        Assert.AreEqual("bar-command-file.save", descriptor.AutomationId);
        Assert.IsTrue(descriptor.IsEnabled);
        Assert.AreEqual("Ctrl+S", descriptor.Shortcut);

        Assert.IsTrue(await runtime.TryActivateAsync(
            "file.save",
            new CommandContext(Parameter: "bar:file.save")));
        var refreshed = NeraMauiCommandChromeDescriptor.From(
            runtime.Snapshot.Items[0].Children[0].Command!,
            "bar-command");

        Assert.AreEqual(1, handler.ExecutionCount);
        Assert.AreEqual("bar:file.save", handler.LastParameter);
        Assert.IsFalse(refreshed.IsEnabled);
        Assert.IsFalse(await runtime.TryActivateShortcutAsync("Ctrl+S"));
    }

    [TestMethod]
    public void PresenterTypesShouldExposeMauiContractsWithoutOwningWorkbook()
    {
        Assert.IsTrue(typeof(ContentView).IsAssignableFrom(
            typeof(NeraMauiRibbonView)));
        Assert.IsTrue(typeof(ContentView).IsAssignableFrom(
            typeof(NeraMauiBarPresenter)));
        Assert.IsNull(typeof(NeraMauiRibbonView).GetProperty("Workbook"));
        Assert.IsNull(typeof(NeraMauiBarPresenter).GetProperty("Workbook"));
        Assert.IsNotNull(typeof(NeraMauiRibbonView).GetMethod(
            nameof(NeraMauiRibbonView.TryActivateCommandAsync)));
        Assert.IsNotNull(typeof(NeraMauiBarPresenter).GetMethod(
            nameof(NeraMauiBarPresenter.TryActivateCommandAsync)));
        Assert.IsNotNull(typeof(NeraMauiRibbonView).GetMethod(
            nameof(NeraMauiRibbonView.BindShortcuts)));
        Assert.IsNotNull(typeof(NeraMauiBarPresenter).GetMethod(
            nameof(NeraMauiBarPresenter.BindShortcuts)));
    }

    [TestMethod]
    public async Task ShortcutBindingShouldHandleVisibleShortcutOnce()
    {
        var source = new ShortcutSource();
        var commandId = new CommandId("file.save");
        var executionCount = 0;
        using var binding = new NeraMauiShortcutBinding(
            source,
            (string shortcut, out CommandId resolved) =>
            {
                resolved = commandId;
                return string.Equals(shortcut, "Ctrl+S", StringComparison.Ordinal);
            },
            id =>
            {
                Assert.AreEqual(commandId, id);
                executionCount++;
                return ValueTask.FromResult(true);
            });

        var handled = source.Raise("Ctrl+S");
        var ignored = source.Raise("Ctrl+P");

        Assert.IsTrue(handled);
        Assert.IsFalse(ignored);
        Assert.AreEqual(1, executionCount);
        Assert.IsTrue(await binding.TryProcessShortcutAsync("Ctrl+S"));
        Assert.AreEqual(2, executionCount);
    }

    [TestMethod]
    public void RibbonCustomizationBindingShouldPublishRuntimeCustomization()
    {
        var registry = new CommandRegistry();
        registry.Register(
            new CommandDescriptor("view.gridlines", "Đường lưới"),
            new ToggleHandler());
        var runtime = new RibbonRuntimeController(
            CreateRibbonDefinition(),
            registry);
        var binding = new NeraMauiRibbonCustomizationBinding(
            runtime,
            commandId => $"Caption {commandId.Value}");
        var changeCount = 0;
        binding.Changed += (_, _) => changeCount++;

        Assert.IsTrue(binding.SetVisible(
            RibbonCustomizationTarget.Command(
                "view",
                "display",
                "view.gridlines"),
            false));

        Assert.AreEqual(1, changeCount);
        Assert.AreEqual(0, runtime.Snapshot.Tabs[0].Groups[0].Items.Count);
        var json = binding.ExportJson();
        binding.Reset();
        Assert.AreEqual(1, runtime.Snapshot.Tabs[0].Groups[0].Items.Count);
        binding.LoadJson(json);

        Assert.AreEqual(0, runtime.Snapshot.Tabs[0].Groups[0].Items.Count);
        Assert.AreEqual(
            "Caption view.gridlines",
            binding.Entries.Single(entry =>
                entry.Target.CommandId == "view.gridlines").Caption);
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
                BarItemDefinition.Submenu(
                    "Tệp",
                    [BarItemDefinition.Command("file.save")],
                    id: "file"),
            ]);

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

    private sealed class ShortcutSource : INeraMauiShortcutSource
    {
        public event EventHandler<NeraMauiShortcutEventArgs>? ShortcutPressed;

        public bool Raise(string shortcut)
        {
            var args = new NeraMauiShortcutEventArgs(shortcut);
            ShortcutPressed?.Invoke(this, args);
            return args.Handled;
        }
    }
}
