using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Bars.Core;
using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Commands.Tests;

[TestClass]
public sealed class BarRuntimeControllerTests
{
    [TestMethod]
    public async Task TryActivateAsyncShouldExecuteNestedVisibleCommandAndRefresh()
    {
        var registry = new CommandRegistry();
        var handler = new SaveHandler();
        registry.Register(new CommandDescriptor("file.save", "Lưu"), handler);
        var runtime = new BarRuntimeController(CreateDefinition(), registry);
        var changeCount = 0;
        runtime.SnapshotChanged += (_, _) => changeCount++;

        var executed = await runtime.TryActivateAsync("file.save");

        Assert.IsTrue(executed);
        Assert.AreEqual(1, handler.ExecutionCount);
        Assert.AreEqual(1, changeCount);
        Assert.IsFalse(runtime.Snapshot.Items[0].Children[0].IsEnabled);
    }

    [TestMethod]
    public async Task CustomizationShouldHideNestedCommandAndRestoreDefinition()
    {
        var registry = new CommandRegistry();
        var handler = new SaveHandler();
        registry.Register(new CommandDescriptor("file.save", "Lưu"), handler);
        var definition = CreateDefinition();
        var customization = new BarCustomization(
            "main",
            [
                new BarItemCustomization(
                    "file",
                    children:
                    [new BarItemCustomization("file.save", isVisible: false)]),
            ]);
        var runtime = new BarRuntimeController(definition, registry, customization);

        var hiddenExecution = await runtime.TryActivateAsync("file.save");
        var restored = runtime.SetCustomization(customization: null);

        Assert.IsFalse(hiddenExecution);
        Assert.AreSame(definition, runtime.EffectiveDefinition);
        Assert.AreEqual(1, restored.Items[0].Children.Count);
        Assert.IsTrue(restored.Items[0].Children[0].IsEnabled);
    }

    [TestMethod]
    public async Task TryActivateAsyncShouldRejectCommandOutsideCurrentBar()
    {
        var registry = new CommandRegistry();
        var handler = new SaveHandler();
        registry.Register(new CommandDescriptor("file.save", "Lưu"), handler);
        registry.Register(new CommandDescriptor("file.close", "Đóng"), handler);
        var runtime = new BarRuntimeController(CreateDefinition(), registry);

        var executed = await runtime.TryActivateAsync("file.close");

        Assert.IsFalse(executed);
        Assert.AreEqual(0, handler.ExecutionCount);
    }

    private static BarDefinition CreateDefinition() =>
        new(
            "main",
            BarKind.MainMenu,
            [
                BarItemDefinition.Submenu(
                    "Tệp",
                    [BarItemDefinition.Command("file.save")],
                    id: "file"),
            ]);

    private sealed class SaveHandler : IStatefulCommandHandler
    {
        public int ExecutionCount { get; private set; }

        public bool CanExecute(CommandContext context) => ExecutionCount == 0;

        public CommandState GetState(CommandContext context) =>
            new(CanExecute(context));

        public ValueTask ExecuteAsync(CommandContext context)
        {
            ExecutionCount++;
            return ValueTask.CompletedTask;
        }
    }
}
