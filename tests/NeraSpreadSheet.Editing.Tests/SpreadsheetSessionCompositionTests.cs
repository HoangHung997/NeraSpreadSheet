using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetSessionCompositionTests
{
    [TestMethod]
    public async Task SessionOwnsClipboardFormattingMergeAndEditorControllers()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        workbook.Worksheets[0].SetValue(default, "Nera");

        Assert.IsNotNull(session.Clipboard);
        Assert.IsNotNull(session.Styles);
        Assert.IsNotNull(session.Merge);
        Assert.IsNotNull(session.Editor);
        Assert.IsNotNull(session.Commands);
        Assert.IsNotNull(session.CommandDispatcher);

        Assert.IsTrue(await session.CommandDispatcher.TryExecuteAsync(SpreadsheetClipboardCommandIds.Copy));
        session.Selection.SetActiveCell(new CellAddress(2, 2));
        Assert.IsTrue(await session.CommandDispatcher.TryExecuteAsync(SpreadsheetClipboardCommandIds.Paste));
        Assert.AreEqual("Nera", workbook.Worksheets[0].GetCell(new CellAddress(2, 2)).Value.RawValue);

        Assert.IsTrue(await session.CommandDispatcher.TryExecuteAsync(SpreadsheetFormattingCommandIds.Bold));
        var pastedCell = workbook.Worksheets[0].GetCell(new CellAddress(2, 2));
        Assert.AreEqual(700, workbook.Styles.Get(pastedCell.StyleId).Font.Weight);
    }

    [TestMethod]
    public async Task SessionCommandRegistryTracksMergeStateFromSharedSelection()
    {
        var session = new SpreadsheetSession(new Workbook());
        session.Selection.Select(new CellRange(default, new CellAddress(0, 1)));

        Assert.IsTrue(session.CommandDispatcher.QueryState(SpreadsheetMergeCommandIds.Merge).IsEnabled);
        Assert.IsTrue(await session.CommandDispatcher.TryExecuteAsync(SpreadsheetMergeCommandIds.Merge));
        Assert.IsTrue(session.CommandDispatcher.QueryState(SpreadsheetMergeCommandIds.Unmerge).IsEnabled);
        Assert.AreEqual(1, session.ActiveWorksheet.MergedCells.Count);
    }

    [TestMethod]
    public void ActivatingWorksheetCancelsSharedEditorState()
    {
        var workbook = new Workbook();
        var second = workbook.AddWorksheet("Second");
        var session = new SpreadsheetSession(workbook);
        session.Editor.BeginEdit();

        session.ActivateWorksheet(second);

        Assert.IsFalse(session.Editor.IsEditing);
        Assert.AreSame(second, session.ActiveWorksheet);
    }
}
