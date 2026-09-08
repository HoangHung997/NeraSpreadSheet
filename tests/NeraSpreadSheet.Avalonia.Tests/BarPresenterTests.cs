using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Bars.Core;
using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Avalonia.Tests;

[TestClass]
public sealed class BarPresenterTests
{
    [TestMethod]
    public Task ToolbarShouldDispatchNativeClickThroughSharedCommandRegistry() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var handler = new CountingHandler();
        using var presenter = CreatePresenter(BarKind.Toolbar, handler);
        var panel = (StackPanel)presenter.NativeControl;
        Assert.HasCount(1, panel.Children);
        var button = (Button)panel.Children[0];
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.AreEqual(1, handler.Count);
    });

    [TestMethod]
    public Task AllBarKindsShouldUseNativeControls() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var handler = new CountingHandler();
        using var toolbar = CreatePresenter(BarKind.Toolbar, handler);
        using var menu = CreatePresenter(BarKind.MainMenu, handler);
        using var context = CreatePresenter(BarKind.ContextMenu, handler);
        Assert.IsInstanceOfType<StackPanel>(toolbar.NativeControl);
        Assert.IsInstanceOfType<Menu>(menu.NativeControl);
        Assert.IsInstanceOfType<ContextMenu>(context.NativeControl);
        Assert.AreEqual(0, handler.Count);
    });

    [TestMethod]
    public Task DisabledCommandShouldNotExecuteEvenWhenInvokedProgrammatically() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var handler = new CountingHandler { Enabled = false };
        using var presenter = CreatePresenter(BarKind.Toolbar, handler);
        Assert.IsFalse(((StackPanel)presenter.NativeControl).Children[0].IsEnabled);
        Assert.IsFalse(presenter.ActivateCommandAsync(new CommandId("Test.Action")).GetAwaiter().GetResult());
        Assert.AreEqual(0, handler.Count);
    });

    [TestMethod]
    public Task CancelledParameterCollectionShouldNotExecute() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var handler = new CountingHandler();
        using var presenter = CreatePresenter(BarKind.Toolbar, handler);
        presenter.ActivationContextProvider = (_, _) => ValueTask.FromResult<CommandContext?>(null);
        Assert.IsFalse(presenter.ActivateCommandAsync(new CommandId("Test.Action")).GetAwaiter().GetResult());
        Assert.AreEqual(0, handler.Count);
    });

    [TestMethod]
    public Task ActivationFailureShouldPreserveTheOriginalException() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var expected = new InvalidOperationException("Intentional command failure");
        var handler = new CountingHandler { Failure = expected };
        using var presenter = CreatePresenter(BarKind.Toolbar, handler);
        Exception? actual = null;
        presenter.CommandActivationFailed += (_, e) => actual = e.Exception;
        Assert.IsFalse(presenter.ActivateCommandAsync(new CommandId("Test.Action")).GetAwaiter().GetResult());
        Assert.AreSame(expected, actual);
    });

    [TestMethod]
    public Task DisposedPresenterShouldIgnoreQueuedSnapshotNotifications() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var presenter = CreatePresenter(BarKind.Toolbar, new CountingHandler());
        presenter.Runtime.Refresh();
        presenter.Dispose();
        presenter.Runtime.Refresh();
        Assert.HasCount(0, ((StackPanel)presenter.NativeControl).Children);
    });

    private static NeraBarPresenter CreatePresenter(BarKind kind, CountingHandler handler)
    {
        var registry = new CommandRegistry();
        registry.Register(new CommandDescriptor("Test.Action", "Thực hiện", shortcut: "Ctrl+S"), handler);
        return new NeraBarPresenter(new BarRuntimeController(
            new BarDefinition("test", kind, [BarItemDefinition.Command("Test.Action")]), registry));
    }

    private sealed class CountingHandler : ICommandHandler
    {
        public int Count { get; private set; }
        public bool Enabled { get; init; } = true;
        public Exception? Failure { get; init; }
        public bool CanExecute(CommandContext context) => Enabled;
        public ValueTask ExecuteAsync(CommandContext context)
        {
            if (Failure is { } error) return ValueTask.FromException(error);
            Count++;
            return ValueTask.CompletedTask;
        }
    }
}
