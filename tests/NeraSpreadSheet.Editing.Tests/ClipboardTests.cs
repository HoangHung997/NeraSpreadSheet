using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class ClipboardTests
{
    [TestMethod]
    public void FormulaReferenceTranslatorPreservesAbsoluteReferenceParts()
    {
        var translated = FormulaReferenceTranslator.Translate(
            "=A1+$A1+A$1+$A$1+\"A1\"",
            new CellAddress(0, 0),
            new CellAddress(2, 3));

        Assert.AreEqual("=D3+$A3+D$1+$A$1+\"A1\"", translated);
    }

    [TestMethod]
    public void PasteCopiesBlankCellsAndTranslatesRelativeFormula()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(0, 0), 10d);
        sheet.SetFormula(new CellAddress(0, 1), "=A1*2");
        sheet.SetValue(new CellAddress(4, 4), "must be cleared");
        var session = new SpreadsheetSession(workbook);
        session.Selection.Select(new CellRange(new CellAddress(0, 0), new CellAddress(1, 1)));
        var clipboard = new SpreadsheetClipboardController(session);
        clipboard.CopyPrimarySelection();

        Assert.IsTrue(clipboard.Paste(new CellAddress(3, 3)));

        Assert.AreEqual(10d, sheet.GetCell(new CellAddress(3, 3)).Value.RawValue);
        Assert.AreEqual("=D4*2", sheet.GetCell(new CellAddress(3, 4)).Formula);
        Assert.IsTrue(sheet.GetCell(new CellAddress(4, 4)).IsEmpty);
    }

    [TestMethod]
    public void PasteIsUndoable()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(default, "source");
        var session = new SpreadsheetSession(workbook);
        var clipboard = new SpreadsheetClipboardController(session);
        clipboard.CopyPrimarySelection();
        clipboard.Paste(new CellAddress(3, 3));

        Assert.AreEqual("source", sheet.GetCell(new CellAddress(3, 3)).Value.RawValue);
        Assert.IsTrue(session.Undo());
        Assert.IsTrue(sheet.GetCell(new CellAddress(3, 3)).IsEmpty);
    }

    [TestMethod]
    public async Task ClipboardCommandsUseNeraNativeCommandIds()
    {
        var workbook = new Workbook();
        workbook.Worksheets[0].SetValue(default, "source");
        var session = new SpreadsheetSession(workbook);
        var clipboard = new SpreadsheetClipboardController(session);
        var registry = new CommandRegistry();
        SpreadsheetClipboardCommandCatalog.Register(registry, clipboard);
        var dispatcher = new CommandDispatcher(registry);

        Assert.IsTrue(await dispatcher.TryExecuteAsync(SpreadsheetClipboardCommandIds.Copy));
        session.Selection.SetActiveCell(new CellAddress(2, 2));
        Assert.IsTrue(await dispatcher.TryExecuteAsync(SpreadsheetClipboardCommandIds.Paste));
        Assert.AreEqual("source", workbook.Worksheets[0].GetCell(new CellAddress(2, 2)).Value.RawValue);
    }

    [TestMethod]
    public void OversizedClipboardRangeIsRejectedBeforeMaterialization()
    {
        var session = new SpreadsheetSession(new Workbook());
        session.Selection.Select(new CellRange(new CellAddress(0, 0), new CellAddress(100, 100)));
        var clipboard = new SpreadsheetClipboardController(session, maximumMaterializedCells: 100);
        Assert.ThrowsExactly<InvalidOperationException>(() => clipboard.CopyPrimarySelection());
    }
}
