using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Commands.Tests;

[TestClass]
public sealed class ClipboardRibbonIntegrationTests
{
    [TestMethod]
    [DataRow("Edit.PasteValues")]
    [DataRow("Edit.PasteFormulas")]
    [DataRow("Edit.PasteFormats")]
    [DataRow("Edit.CancelCopyMode")]
    public void SharedClipboardCapabilityShouldHaveOneAuditedReachablePlacement(string id)
    {
        var session = new SpreadsheetSession(new Workbook());
        var definition = RibbonProductionCommandCatalog.CreateDefaultDefinition();
        RibbonCommandCatalogAudit.ValidateExact(session.Commands, definition,
            RibbonProductionCommandCatalog.CommandIds);
        Assert.AreEqual(1, RibbonProductionCommandCatalog.CommandIds.Count(command => command.Value == id));
        var placements = definition.Tabs.SelectMany(tab => tab.Groups)
            .SelectMany(group => group.Items).Where(item => item.CommandId.Value == id).ToArray();
        Assert.AreEqual(1, placements.Length);
        Assert.IsTrue(session.Commands.TryResolve(new CommandId(id), out var descriptor, out _));
        Assert.IsNotNull(descriptor);
        var english = new PresentationLocalization(CultureInfo.GetCultureInfo("en-US"));
        var expected = id switch
        {
            "Edit.PasteValues" => "Paste values",
            "Edit.PasteFormulas" => "Paste formulas",
            "Edit.PasteFormats" => "Paste formats",
            _ => "Cancel copy mode",
        };
        Assert.IsTrue(PresentationLocalization.ContainsKey(descriptor.Caption));
        Assert.AreEqual(expected, english.CommandCaption(descriptor.Id, descriptor.Caption));
        Assert.AreEqual("Host override", english.CommandCaption(descriptor.Id, "Host override"));
        // Cancel must not steal Esc from the active editor, IME or popup.
        if (id == "Edit.CancelCopyMode") Assert.IsNull(descriptor.Shortcut);
    }

    [TestMethod]
    [DataRow("Edit.PasteValues")]
    [DataRow("Edit.PasteFormulas")]
    [DataRow("Edit.PasteFormats")]
    public async Task ProductionRibbonShouldDispatchPasteModeThroughSharedHistory(string id)
    {
        var session = new SpreadsheetSession(new Workbook());
        var sheet = session.ActiveWorksheet;
        var style = session.Workbook.Styles.Intern(new CellStyle
        {
            Alignment = new CellAlignmentStyle { WrapText = true },
        });
        sheet.SetFormula(default, "=40+2");
        sheet.SetStyle(default, style);
        session.Recalculate();
        var destination = new CellAddress(3, 3);
        sheet.SetValue(destination, 9d);
        var before = sheet.GetCell(destination);
        var history = session.History.UndoCount;
        session.Clipboard.CopyPrimarySelection();
        session.Selection.SetActiveCell(destination);
        var runtime = new RibbonRuntimeController(
            RibbonProductionCommandCatalog.CreateDefaultDefinition(), session.Commands);

        Assert.IsTrue(await runtime.TryActivateAsync(new CommandId(id)));
        Assert.AreEqual(history + 1, session.History.UndoCount);
        Assert.AreEqual(id == "Edit.PasteFormats" ? 9d : 42d, sheet.GetValue(destination));
        Assert.AreEqual(id == "Edit.PasteFormulas" ? "=40+2" : null, sheet.GetFormula(destination));
        Assert.AreEqual(id == "Edit.PasteFormats" ? style : before.StyleId, sheet.GetCell(destination).StyleId);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(before, sheet.GetCell(destination));
        Assert.AreEqual(history, session.History.UndoCount);
    }

    [TestMethod]
    public async Task ProductionRibbonShouldCancelCopyModeWithoutEditingWorkbook()
    {
        var session = new SpreadsheetSession(new Workbook());
        session.ActiveWorksheet.SetValue(default, 12d);
        session.Clipboard.CopyPrimarySelection();
        var before = session.ActiveWorksheet.GetCell(default);
        var history = session.History.UndoCount;
        var runtime = new RibbonRuntimeController(
            RibbonProductionCommandCatalog.CreateDefaultDefinition(), session.Commands);
        Assert.IsTrue(session.Clipboard.CanCancelCopyMode);
        Assert.IsTrue(await runtime.TryActivateAsync(SpreadsheetClipboardCommandIds.CancelCopyMode));
        Assert.IsFalse(session.Clipboard.CanCancelCopyMode);
        Assert.AreEqual(before, session.ActiveWorksheet.GetCell(default));
        Assert.AreEqual(history, session.History.UndoCount);
    }
}
