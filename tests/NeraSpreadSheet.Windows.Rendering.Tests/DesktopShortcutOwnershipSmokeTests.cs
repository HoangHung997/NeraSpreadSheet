using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class DesktopShortcutOwnershipSmokeTests
{
    private static readonly bool[] OriginalPreviewValues = [false, true];
    [TestMethod]
    [Timeout(120_000)]
    public void WinFormsBindingsShouldClaimOnceAndRetainKeyPreviewUntilLastDispose()
    {
        RunInSta(() =>
        {
            foreach (var originallyEnabled in OriginalPreviewValues)
            {
                var handler = new Handler();
                using var form = new KeyboardForm { KeyPreview = originallyEnabled };
                using var menu = new NeraSpreadSheet.WinForms.NeraBarPresenter(CreateBarRuntime(handler));
                using var menuBinding = menu.BindShortcuts(form);
                using var first = new NeraSpreadSheet.WinForms.NeraRibbonControl(CreateRuntime(handler));
                using var second = new NeraSpreadSheet.WinForms.NeraRibbonControl(CreateRuntime(handler));
                using var binding1 = first.BindShortcuts(form);
                using var binding2 = second.BindShortcuts(form);
                Assert.IsTrue(form.Press(System.Windows.Forms.Keys.Menu).Handled);
                Assert.AreEqual(RibbonKeyTipScope.Tabs, first.KeyTipScope, "A Bar binding registered first must not consume Ribbon Alt.");
                first.EscapeKeyTipMode();
                menuBinding.Dispose();
                Assert.IsTrue(form.KeyPreview);
                var key = form.Press(System.Windows.Forms.Keys.F6);
                Assert.IsTrue(key.Handled);
                Assert.IsTrue(key.SuppressKeyPress);
                Assert.AreEqual(1, handler.Count);
                Assert.IsFalse(form.Press(System.Windows.Forms.Keys.F7).Handled);
                binding1.Dispose();
                Assert.IsTrue(form.KeyPreview, "A second live binding still requires KeyPreview.");
                form.Press(System.Windows.Forms.Keys.F6);
                Assert.AreEqual(2, handler.Count);
                binding2.Dispose();
                Assert.AreEqual(originallyEnabled, form.KeyPreview);
                Assert.IsFalse(form.Press(System.Windows.Forms.Keys.F6).Handled);
                Assert.AreEqual(2, handler.Count);
            }
        });
    }

    [TestMethod]
    [Timeout(120_000)]
    public void WpfBindingsShouldClaimOneRoutedEventAndDetachIndependently()
    {
        RunInSta(() =>
        {
            var handler = new Handler();
            using var first = new NeraSpreadSheet.Wpf.NeraRibbonControl(CreateRuntime(handler));
            using var second = new NeraSpreadSheet.Wpf.NeraRibbonControl(CreateRuntime(handler));
            var window = new System.Windows.Window
            {
                Content = first, ShowInTaskbar = false, Left = -32_000d, Top = -32_000d,
                WindowStartupLocation = System.Windows.WindowStartupLocation.Manual,
            };
            using var menu = new NeraSpreadSheet.Wpf.NeraBarPresenter(CreateBarRuntime(handler));
            using var menuBinding = menu.BindShortcuts(window);
            using var binding1 = first.BindShortcuts(window);
            using var binding2 = second.BindShortcuts(window);
            try
            {
                window.Show();
                window.UpdateLayout();
                var source = System.Windows.PresentationSource.FromVisual(window)!;
                var alt = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
                    source, Environment.TickCount, System.Windows.Input.Key.LeftAlt)
                { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent };
                window.RaiseEvent(alt);
                Assert.AreEqual(RibbonKeyTipScope.Tabs, first.KeyTipScope, "A Bar binding registered first must not consume Ribbon Alt.");
                first.EscapeKeyTipMode();
                menuBinding.Dispose();
                System.Windows.Input.KeyEventArgs Press()
                {
                    var args = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
                        source, Environment.TickCount, System.Windows.Input.Key.F6)
                    { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent };
                    window.RaiseEvent(args);
                    return args;
                }
                Assert.IsTrue(Press().Handled);
                Assert.AreEqual(1, handler.Count);
                binding1.Dispose();
                Assert.IsTrue(Press().Handled);
                Assert.AreEqual(2, handler.Count);
                binding2.Dispose();
                Assert.IsFalse(Press().Handled);
            }
            finally { window.Close(); }
        });
    }

    private static RibbonRuntimeController CreateRuntime(Handler handler)
    {
        var registry = new CommandRegistry();
        registry.Register(new CommandDescriptor("test.run", "Chạy", shortcut: "F6"), handler);
        return new RibbonRuntimeController(new RibbonDefinition([
            new RibbonTabDefinition("home", "Trang đầu", [
                new RibbonGroupDefinition("tools", "Công cụ", [new RibbonItemDefinition("test.run")])])]), registry);
    }

    private static NeraSpreadSheet.Bars.Core.BarRuntimeController CreateBarRuntime(Handler handler)
    {
        var registry = new CommandRegistry();
        registry.Register(new CommandDescriptor("test.run", "Chạy", shortcut: "F6"), handler);
        return new NeraSpreadSheet.Bars.Core.BarRuntimeController(new NeraSpreadSheet.Bars.Core.BarDefinition("bar",
            NeraSpreadSheet.Bars.Core.BarKind.Toolbar, [NeraSpreadSheet.Bars.Core.BarItemDefinition.Command("test.run")]), registry);
    }

    private static void RunInSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception error) { failure = error; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(90d)));
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class KeyboardForm : System.Windows.Forms.Form
    {
        internal System.Windows.Forms.KeyEventArgs Press(System.Windows.Forms.Keys key)
        {
            var args = new System.Windows.Forms.KeyEventArgs(key);
            OnKeyDown(args);
            return args;
        }
    }

    private sealed class Handler : ICommandHandler
    {
        internal int Count { get; private set; }
        public bool CanExecute(CommandContext context) => true;
        public ValueTask ExecuteAsync(CommandContext context) { Count++; return ValueTask.CompletedTask; }
    }
}
