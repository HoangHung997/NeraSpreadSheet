using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Commands.Tests;

[TestClass]
public sealed class CommandDispatcherTests
{
    [TestMethod]
    public async Task TryExecuteAsyncRunsRegisteredEnabledHandler()
    {
        var registry = new CommandRegistry();
        var handler = new TestHandler();
        var id = new CommandId("Edit.Copy");
        registry.Register(new CommandDescriptor(id, "Copy"), handler);
        var dispatcher = new CommandDispatcher(registry);
        var executed = await dispatcher.TryExecuteAsync(id);
        Assert.IsTrue(executed);
        Assert.AreEqual(1, handler.ExecutionCount);
    }

    private sealed class TestHandler : ICommandHandler
    {
        public int ExecutionCount { get; private set; }
        public bool CanExecute(CommandContext context) => true;
        public ValueTask ExecuteAsync(CommandContext context) { ExecutionCount++; return ValueTask.CompletedTask; }
    }
}
