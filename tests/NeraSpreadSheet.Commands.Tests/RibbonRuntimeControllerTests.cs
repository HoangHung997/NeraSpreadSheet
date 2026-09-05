using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Commands.Tests;

[TestClass]
public sealed class RibbonRuntimeControllerTests
{
    [TestMethod]
    public async Task TryActivateAsyncShouldExecuteVisibleCommandAndRefreshState()
    {
        var registry = new CommandRegistry();
        var handler = new ToggleHandler();
        registry.Register(new CommandDescriptor("view.gridlines", "Đường lưới"), handler);
        var definition = CreateDefinition();
        var runtime = new RibbonRuntimeController(definition, registry);
        var changeCount = 0;
        runtime.SnapshotChanged += (_, _) => changeCount++;

        var executed = await runtime.TryActivateAsync("view.gridlines");

        Assert.IsTrue(executed);
        Assert.AreEqual(1, handler.ExecutionCount);
        Assert.AreEqual(1, changeCount);
        Assert.IsTrue(runtime.Snapshot.Tabs[0].Groups[0].Items[0].Command.IsChecked);
    }

    [TestMethod]
    public async Task CustomizationShouldPreventHiddenActivationAndRestoreSource()
    {
        var registry = new CommandRegistry();
        var handler = new ToggleHandler();
        registry.Register(new CommandDescriptor("view.gridlines", "Đường lưới"), handler);
        var definition = CreateDefinition();
        var customization = new RibbonCustomization(
        [
            new RibbonTabCustomization(
                "view",
                groups:
                [
                    new RibbonGroupCustomization(
                        "display",
                        items:
                        [new RibbonItemCustomization("view.gridlines", IsVisible: false)]),
                ]),
        ]);
        var runtime = new RibbonRuntimeController(definition, registry, customization);

        var hiddenExecution = await runtime.TryActivateAsync("view.gridlines");
        var restored = runtime.SetCustomization(customization: null);
        var restoredExecution = await runtime.TryActivateAsync("view.gridlines");

        Assert.IsFalse(hiddenExecution);
        Assert.AreEqual(0, runtime.Customization?.Tabs.Count ?? 0);
        Assert.AreSame(definition, runtime.EffectiveDefinition);
        Assert.AreEqual(1, restored.Tabs[0].Groups[0].Items.Count);
        Assert.IsTrue(restoredExecution);
        Assert.AreEqual(1, handler.ExecutionCount);
    }

    [TestMethod]
    public void RefreshShouldReplaceSnapshotAndPublishExternalStateChange()
    {
        var registry = new CommandRegistry();
        var handler = new ToggleHandler();
        registry.Register(new CommandDescriptor("view.gridlines", "Đường lưới"), handler);
        var runtime = new RibbonRuntimeController(CreateDefinition(), registry);
        var original = runtime.Snapshot;
        var changeCount = 0;
        runtime.SnapshotChanged += (_, _) => changeCount++;
        handler.IsChecked = true;

        var refreshed = runtime.Refresh();

        Assert.AreNotSame(original, refreshed);
        Assert.IsFalse(original.Tabs[0].Groups[0].Items[0].Command.IsChecked);
        Assert.IsTrue(refreshed.Tabs[0].Groups[0].Items[0].Command.IsChecked);
        Assert.AreEqual(1, changeCount);
    }

    [TestMethod]
    public async Task ShortcutShouldActivateVisibleCommandAndDisappearWhenHidden()
    {
        var registry = new CommandRegistry();
        var handler = new ToggleHandler();
        registry.Register(
            new CommandDescriptor(
                "view.gridlines",
                "Đường lưới",
                shortcut: "Ctrl+G"),
            handler);
        var runtime = new RibbonRuntimeController(CreateDefinition(), registry);

        Assert.IsTrue(runtime.TryResolveShortcut("control+g", out var commandId));
        Assert.AreEqual("view.gridlines", commandId.Value);
        Assert.IsTrue(await runtime.TryActivateShortcutAsync("CTRL+G"));
        runtime.SetCustomization(new RibbonCustomization(
        [
            new RibbonTabCustomization("view", isVisible: false),
        ]));

        Assert.IsFalse(runtime.TryResolveShortcut("Ctrl+G", out _));
        Assert.IsFalse(await runtime.TryActivateShortcutAsync("Ctrl+G"));
        Assert.AreEqual(1, handler.ExecutionCount);
    }

    private static RibbonDefinition CreateDefinition() =>
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

    private sealed class ToggleHandler : IStatefulCommandHandler
    {
        public bool IsChecked { get; set; }

        public int ExecutionCount { get; private set; }

        public bool CanExecute(CommandContext context) => true;

        public CommandState GetState(CommandContext context) =>
            new(true, IsChecked);

        public ValueTask ExecuteAsync(CommandContext context)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            ExecutionCount++;
            IsChecked = !IsChecked;
            return ValueTask.CompletedTask;
        }
    }
}
