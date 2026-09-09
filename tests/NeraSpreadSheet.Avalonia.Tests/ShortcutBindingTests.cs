using global::Avalonia.Controls;
using global::Avalonia.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Avalonia.Tests;

[TestClass]
public sealed class ShortcutBindingTests
{
    private static KeyModifiers PrimaryModifier => OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

    [TestMethod]
    public Task SharedSourceShouldResolveAndExecuteExactlyOnce() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var source = new Control();
        var resolutions = 0;
        var executions = 0;
        bool Resolve(string gesture, out CommandId id)
        {
            resolutions++;
            id = new CommandId("Test.Save");
            return gesture == "Ctrl+S";
        }
        ValueTask<bool> Activate(CommandId id)
        {
            Assert.AreEqual(new CommandId("Test.Save"), id);
            executions++;
            return ValueTask.FromResult(true);
        }
        using var ribbon = new NeraAvaloniaShortcutBinding(source, Resolve, Activate);
        using var bar = new NeraAvaloniaShortcutBinding(source, Resolve, Activate);
        var args = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.S, KeyModifiers = PrimaryModifier };
        source.RaiseEvent(args);
        Assert.IsTrue(args.Handled);
        Assert.AreEqual(1, resolutions);
        Assert.AreEqual(1, executions);
    });

    [TestMethod]
    public Task HandledShortcutShouldNotEvenResolve() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var source = new Control();
        bool Resolve(string gesture, out CommandId id)
        {
            id = default;
            Assert.Fail("An already handled event must never reach command resolution.");
            return false;
        }
        using var binding = new NeraAvaloniaShortcutBinding(source, Resolve, _ => ValueTask.FromResult(false));
        source.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.S, KeyModifiers = PrimaryModifier, Handled = true });
    });

    [TestMethod]
    public Task DisposedBindingShouldReleaseItsInputSubscription() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var source = new Control();
        var resolutions = 0;
        bool Resolve(string gesture, out CommandId id) { resolutions++; id = default; return false; }
        var binding = new NeraAvaloniaShortcutBinding(source, Resolve, _ => ValueTask.FromResult(false));
        binding.Dispose();
        binding.Dispose();
        source.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.S, KeyModifiers = PrimaryModifier });
        Assert.AreEqual(0, resolutions);
    });

    [TestMethod]
    public Task NativeTextBoxShortcutShouldNotReachWorkbookResolver() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var source = new TextBox();
        var resolutions = 0;
        bool Resolve(string gesture, out CommandId id) { resolutions++; id = default; return false; }
        using var binding = new NeraAvaloniaShortcutBinding(source, Resolve, _ => ValueTask.FromResult(false));
        source.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.C, KeyModifiers = PrimaryModifier });
        Assert.AreEqual(0, resolutions);
    });

    [TestMethod]
    public void PlainTextAndAltGrShouldNotBecomeWorkbookShortcuts()
    {
        Assert.IsFalse(NeraAvaloniaShortcutBinding.TryFormatGesture(Key.A, KeyModifiers.None, out _));
        Assert.IsFalse(NeraAvaloniaShortcutBinding.TryFormatGesture(Key.A, KeyModifiers.Control | KeyModifiers.Alt, out _));
        Assert.IsTrue(NeraAvaloniaShortcutBinding.TryFormatGesture(Key.S, PrimaryModifier | KeyModifiers.Shift, out var gesture));
        Assert.AreEqual("Ctrl+Shift+S", gesture);
    }
}
