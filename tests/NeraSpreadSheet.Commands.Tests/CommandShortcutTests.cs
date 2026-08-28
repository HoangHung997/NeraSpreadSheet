using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeraSpreadSheet.Commands.Tests;

[TestClass]
public sealed class CommandShortcutTests
{
    [TestMethod]
    [DataRow("Shift+Control+s", "Ctrl+Shift+S")]
    [DataRow("cmd+alt+p", "Alt+Meta+P")]
    [DataRow("F12", "F12")]
    public void ParseShouldCanonicalizeModifierOrderAndAliases(
        string source,
        string expected)
    {
        Assert.AreEqual(expected, CommandShortcut.Parse(source).CanonicalText);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("Ctrl")]
    [DataRow("Ctrl+S+P")]
    [DataRow("Ctrl+Control+S")]
    public void TryParseShouldRejectInvalidChord(string source)
    {
        Assert.IsFalse(CommandShortcut.TryParse(source, out _));
    }

    [TestMethod]
    public void MapShouldRejectAmbiguousShortcutAndAllowRepeatedCommand()
    {
        var repeated = CommandShortcutMap.Create(
        [
            CreatePresentation("file.save", "Ctrl+S"),
            CreatePresentation("file.save", "Control+s"),
        ]);

        Assert.IsTrue(repeated.TryResolve("control+s", out var id));
        Assert.AreEqual("file.save", id.Value);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CommandShortcutMap.Create(
            [
                CreatePresentation("file.save", "Ctrl+S"),
                CreatePresentation("file.save-as", "Control+s"),
            ]));
    }

    private static CommandPresentation CreatePresentation(
        CommandId id,
        string shortcut) =>
        new(
            id,
            IsRegistered: true,
            id.Value,
            Tooltip: null,
            IconKey: null,
            shortcut,
            IsEnabled: true,
            IsChecked: null);
}
