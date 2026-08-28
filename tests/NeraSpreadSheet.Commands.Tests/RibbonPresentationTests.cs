using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Commands.Tests;

[TestClass]
public sealed class RibbonPresentationTests
{
    [TestMethod]
    public void ProjectShouldCacheStateAndKeepEachSnapshotImmutable()
    {
        var registry = new CommandRegistry();
        var handler = new MutableStateHandler(
            new CommandState(true, IsChecked: false, DisplayText: "Sao chép"));
        registry.Register(
            new CommandDescriptor("edit.copy", "Copy", iconKey: "copy"),
            handler);
        var definition = new RibbonDefinition(
        [
            new RibbonTabDefinition(
                "home",
                "Trang đầu",
                [
                    new RibbonGroupDefinition(
                        "clipboard",
                        "Bảng tạm",
                        [
                            new RibbonItemDefinition("edit.copy", IsLarge: true),
                            new RibbonItemDefinition("module.optional", Order: 1),
                        ]),
                    new RibbonGroupDefinition(
                        "quick",
                        "Nhanh",
                        [new RibbonItemDefinition("edit.copy")],
                        order: 1),
                ]),
        ]);
        var projector = new RibbonPresentationProjector(registry);

        var first = projector.Project(definition);
        handler.State = new CommandState(false, IsChecked: true);
        var second = projector.Project(definition);

        Assert.AreEqual(2, handler.QueryCount);
        var firstCopy = first.Tabs[0].Groups[0].Items[0];
        Assert.AreEqual("Sao chép", firstCopy.Command.Caption);
        Assert.IsTrue(firstCopy.Command.IsEnabled);
        Assert.IsFalse(firstCopy.Command.IsChecked);
        Assert.IsTrue(firstCopy.IsLarge);
        Assert.AreSame(
            firstCopy.Command,
            first.Tabs[0].Groups[1].Items[0].Command);

        var unknown = first.Tabs[0].Groups[0].Items[1].Command;
        Assert.IsFalse(unknown.IsRegistered);
        Assert.AreEqual("module.optional", unknown.Caption);
        Assert.IsFalse(unknown.IsEnabled);

        var secondCopy = second.Tabs[0].Groups[0].Items[0].Command;
        Assert.IsFalse(secondCopy.IsEnabled);
        Assert.IsTrue(secondCopy.IsChecked);
        Assert.IsTrue(firstCopy.Command.IsEnabled);
    }

    private sealed class MutableStateHandler : IStatefulCommandHandler
    {
        public MutableStateHandler(CommandState state)
        {
            State = state;
        }

        public CommandState State { get; set; }

        public int QueryCount { get; private set; }

        public bool CanExecute(CommandContext context) => State.IsEnabled;

        public CommandState GetState(CommandContext context)
        {
            QueryCount++;
            return State;
        }

        public ValueTask ExecuteAsync(CommandContext context) =>
            ValueTask.CompletedTask;
    }
}
