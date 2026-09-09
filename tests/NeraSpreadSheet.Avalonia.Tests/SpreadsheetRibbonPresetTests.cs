using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Avalonia.Tests;

[TestClass]
public sealed class SpreadsheetRibbonPresetTests
{
    private static readonly string[] OptionalHomeIds = ["Ui.FontFamily", "Ui.FontSize", "Ui.Borders", "Ui.Underline", "Ui.Fill", "Ui.FontColor",
        "Ui.Align.Left", "Ui.Align.Center", "Ui.Align.Right", "Ui.Wrap", "Ui.Number", "Ui.Percent", "Ui.Decimal"];
    private static readonly string[] HomeGroups = ["clipboard", "font", "alignment", "number", "cells", "editing"];

    [TestMethod]
    public void EmptyRegistryShouldNotAdvertiseInventedCapabilities()
    {
        var definition = NeraSpreadsheetRibbonPreset.Create(new CommandRegistry());
        Assert.HasCount(0, definition.Tabs); Assert.HasCount(0, definition.QuickAccessToolbar);
        Assert.HasCount(0, definition.Backstage); Assert.HasCount(0, definition.ContextualTabs);
    }

    [TestMethod]
    public void PresetShouldOnlyReferenceRegisteredCommands()
    {
        var session = new SpreadsheetSession(new Workbook());
        var definition = NeraSpreadsheetRibbonPreset.Create(session.Commands);
        foreach (var item in definition.Tabs.SelectMany(tab => tab.Groups).SelectMany(group => group.Items))
            Assert.IsTrue(session.Commands.TryResolve(item.CommandId, out _, out _), item.CommandId.Value);
        foreach (var item in definition.Backstage.Concat(definition.QuickAccessToolbar))
            Assert.IsTrue(session.Commands.TryResolve(item.CommandId, out _, out _), item.CommandId.Value);
        Assert.IsFalse(definition.Tabs.Any(tab => tab.Id == "page-layout"));
    }

    [TestMethod]
    public void FullHomeCapabilitiesShouldUseTheWpfReferenceGroupOrder()
    {
        var session = new SpreadsheetSession(new Workbook()); var handler = new RecordingHandler();
        foreach (var id in OptionalHomeIds) session.Commands.Register(new CommandDescriptor(id, id), handler);
        var definition = NeraSpreadsheetRibbonPreset.Create(session.Commands);
        CollectionAssert.AreEqual(HomeGroups, definition.Tabs.Single(tab => tab.Id == "home").Groups.Select(group => group.Id).ToArray());
        Assert.AreEqual(0, handler.Executions); Assert.AreEqual(0, handler.StateQueries);
    }

    [TestMethod]
    public void CanonicalToggleIdsShouldRemainCanonical()
    {
        var session = new SpreadsheetSession(new Workbook());
        var definition = NeraSpreadsheetRibbonPreset.Create(session.Commands);
        var bold = definition.Tabs.SelectMany(tab => tab.Groups).SelectMany(group => group.Items)
            .Single(item => item.CommandId.Value == "Cell.Format.Bold");
        Assert.AreEqual(RibbonItemKind.Toggle, bold.Kind);
        Assert.IsFalse(definition.Tabs.SelectMany(tab => tab.Groups).SelectMany(group => group.Items).Any(item => item.CommandId.Value == "Cell.Bold"));
    }

    [TestMethod]
    public void ExistingAvaloniaAliasesShouldRemainUsable()
    {
        var registry = new CommandRegistry(); var handler = new RecordingHandler();
        registry.Register(new CommandDescriptor("Cell.Bold", "Đậm"), handler);
        registry.Register(new CommandDescriptor("Formula.Calculate", "Tính lại"), handler);
        var definition = NeraSpreadsheetRibbonPreset.Create(registry);
        Assert.AreEqual("Cell.Bold", definition.Tabs[0].Groups[0].Items[0].CommandId.Value);
        Assert.AreEqual(RibbonItemKind.Toggle, definition.Tabs[0].Groups[0].Items[0].Kind);
        Assert.AreEqual("formulas", definition.Tabs[1].Id);
    }

    [TestMethod]
    public void UnregisteredTableCommandsShouldNotCreateEmptyContextualTabs()
    {
        var registry = new CommandRegistry(); registry.Register(new CommandDescriptor("Edit.Copy", "Sao chép"), new RecordingHandler());
        var definition = NeraSpreadsheetRibbonPreset.Create(registry);
        Assert.HasCount(1, definition.Tabs); Assert.HasCount(0, definition.ContextualTabs);
        Assert.IsFalse(definition.Tabs.Any(tab => tab.Id == "table-design"));
    }

    [TestMethod]
    public void PresetShouldNotMutateRegistrySelectionOrWorkbook()
    {
        var session = new SpreadsheetSession(new Workbook()); session.Selection.SetActiveCell(new CellAddress(5, 4));
        var count = session.Commands.Count; var version = session.Workbook.Version;
        _ = NeraSpreadsheetRibbonPreset.Create(session.Commands);
        Assert.AreEqual(count, session.Commands.Count); Assert.AreEqual(version, session.Workbook.Version);
        Assert.AreEqual(new CellAddress(5, 4), session.Selection.ActiveCell); Assert.IsFalse(session.Editor.IsEditing);
    }

    private sealed class RecordingHandler : IStatefulCommandHandler
    {
        public int Executions { get; private set; }
        public int StateQueries { get; private set; }
        public bool CanExecute(CommandContext context) => false;
        public CommandState GetState(CommandContext context) { StateQueries++; return CommandState.Disabled; }
        public ValueTask ExecuteAsync(CommandContext context) { Executions++; return ValueTask.CompletedTask; }
    }
}
