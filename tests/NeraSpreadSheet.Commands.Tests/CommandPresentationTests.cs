using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Commands.Tests;

[TestClass]
public sealed class CommandPresentationTests
{
    [TestMethod]
    public void ResolveShouldCombineDescriptorAndStatefulRuntimeMetadata()
    {
        var registry = new CommandRegistry();
        var handler = new StatefulHandler(
            new CommandState(
                IsEnabled: true,
                IsChecked: true,
                DisplayText: "Sao chép vùng"));
        registry.Register(
            new CommandDescriptor(
                "edit.copy",
                "Sao chép",
                "Sao chép lựa chọn",
                "copy",
                "Ctrl+C"),
            handler);
        var resolver = new CommandPresentationResolver(registry);
        var context = new CommandContext(Parameter: "selection");

        var presentation = resolver.Resolve("edit.copy", context);

        Assert.IsTrue(presentation.IsRegistered);
        Assert.AreEqual("Sao chép vùng", presentation.Caption);
        Assert.AreEqual("Sao chép lựa chọn", presentation.Tooltip);
        Assert.AreEqual("copy", presentation.IconKey);
        Assert.AreEqual("Ctrl+C", presentation.Shortcut);
        Assert.IsTrue(presentation.IsEnabled);
        Assert.IsTrue(presentation.IsChecked);
        Assert.AreEqual("selection", handler.LastContext.Parameter);
    }

    [TestMethod]
    public void ResolveShouldUseCanExecuteForStatelessHandler()
    {
        var registry = new CommandRegistry();
        registry.Register(
            new CommandDescriptor("file.save", "Lưu"),
            new StatelessHandler(canExecute: false));
        var resolver = new CommandPresentationResolver(registry);

        var presentation = resolver.Resolve("file.save");

        Assert.IsTrue(presentation.IsRegistered);
        Assert.AreEqual("Lưu", presentation.Caption);
        Assert.IsFalse(presentation.IsEnabled);
        Assert.IsNull(presentation.IsChecked);
    }

    [TestMethod]
    public void ResolveShouldKeepUnknownCommandAsDisabledFallback()
    {
        var resolver = new CommandPresentationResolver(new CommandRegistry());

        var presentation = resolver.Resolve("module.optional");

        Assert.IsFalse(presentation.IsRegistered);
        Assert.AreEqual("module.optional", presentation.Caption);
        Assert.IsFalse(presentation.IsEnabled);
        Assert.IsNull(presentation.Tooltip);
        Assert.IsNull(presentation.IconKey);
        Assert.IsNull(presentation.Shortcut);
    }

    private sealed class StatefulHandler : IStatefulCommandHandler
    {
        public StatefulHandler(CommandState state)
        {
            State = state;
        }

        public CommandState State { get; set; }

        public CommandContext LastContext { get; private set; }

        public int QueryCount { get; private set; }

        public bool CanExecute(CommandContext context) => State.IsEnabled;

        public CommandState GetState(CommandContext context)
        {
            LastContext = context;
            QueryCount++;
            return State;
        }

        public ValueTask ExecuteAsync(CommandContext context) =>
            ValueTask.CompletedTask;
    }

    private sealed class StatelessHandler : ICommandHandler
    {
        private readonly bool _canExecute;

        public StatelessHandler(bool canExecute)
        {
            _canExecute = canExecute;
        }

        public bool CanExecute(CommandContext context) => _canExecute;

        public ValueTask ExecuteAsync(CommandContext context) =>
            ValueTask.CompletedTask;
    }
}
