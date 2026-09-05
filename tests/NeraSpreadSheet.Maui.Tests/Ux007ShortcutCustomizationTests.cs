using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Maui;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Maui.Tests;

[TestClass]
public sealed class Ux007ShortcutCustomizationTests
{
    [TestMethod]
    public async Task MultipleBindingsShouldClaimEventBeforeAwaitingActivation()
    {
        var source = new Source();
        var pending = new TaskCompletionSource<bool>();
        var count = 0;
        bool Resolve(string text, out CommandId id) { id = "test.run"; return text == "F6"; }
        ValueTask<bool> Activate(CommandId id) { count++; return new ValueTask<bool>(pending.Task); }
        using var first = new NeraMauiShortcutBinding(source, Resolve, Activate);
        using var second = new NeraMauiShortcutBinding(source, Resolve, Activate);
        Assert.IsTrue(source.Raise("F6").Handled);
        Assert.AreEqual(1, count, "The multicast event must be claimed before the first await.");
        Assert.IsFalse(source.Raise("F7").Handled);
        pending.SetResult(true);
        await pending.Task;
        first.Dispose();
        Assert.IsTrue(source.Raise("F6").Handled);
        Assert.AreEqual(2, count);
        second.Dispose();
        Assert.IsFalse(source.Raise("F6").Handled);
    }

    [TestMethod]
    public void FailedApplyShouldPreserveLastSuccessfulRollbackPoint()
    {
        var registry = new CommandRegistry();
        var handler = new Handler();
        registry.Register(new CommandDescriptor("test.run", "Chạy"), handler);
        var definition = new RibbonDefinition([new RibbonTabDefinition("home", "Trang đầu", [
            new RibbonGroupDefinition("tools", "Công cụ", [new RibbonItemDefinition("test.run")])])]);
        var runtime = new RibbonRuntimeController(definition, registry);
        var binding = new NeraMauiRibbonCustomizationBinding(runtime);
        var target = RibbonCustomizationTarget.Tab("home");
        binding.Rename(target, "Đã áp dụng");
        binding.Apply();
        binding.Rename(target, "Chưa áp dụng");
        handler.Throw = true;
        Assert.ThrowsExactly<InvalidOperationException>(() => binding.Apply());
        handler.Throw = false;
        binding.Cancel();
        Assert.AreEqual("Đã áp dụng", runtime.Snapshot.Tabs[0].Caption);
    }

    private sealed class Source : INeraMauiShortcutSource
    {
        public event EventHandler<NeraMauiShortcutEventArgs>? ShortcutPressed;
        internal NeraMauiShortcutEventArgs Raise(string text)
        {
            var args = new NeraMauiShortcutEventArgs(text);
            ShortcutPressed?.Invoke(this, args);
            return args;
        }
    }

    private sealed class Handler : ICommandHandler
    {
        internal bool Throw { get; set; }
        public bool CanExecute(CommandContext context) => Throw ? throw new InvalidOperationException("State failed.") : true;
        public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
    }
}
