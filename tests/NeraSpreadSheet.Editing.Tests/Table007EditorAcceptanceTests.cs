using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class Table007EditorAcceptanceTests
{
    [TestMethod]
    [DataRow(-1, 0)]
    [DataRow(0, 0)]
    [DataRow(6, 0)]
    [DataRow(9, 1)]
    [DataRow(int.MaxValue, 0)]
    public void AcceptanceShouldRejectMovedCaretOrSelectionWithoutChangingDraftOrHistory(int caret, int selectionLength)
    {
        var session = CreateSession();
        const string text = "=Sales[Am";
        var candidate = SpreadsheetFormulaEditingAssistant.GetStructuredReferenceSuggestions(text, text.Length,
            session.Workbook, session.ActiveWorksheet, default).Single();
        Assert.IsFalse(SpreadsheetFormulaEditingAssistant.TryApplyStructuredReferenceSuggestion(text, caret, selectionLength,
            session.Workbook, session.ActiveWorksheet, default, candidate, out var result));
        Assert.IsNull(result);
        Assert.AreEqual(0, session.History.UndoCount);
    }

    [TestMethod]
    public void AcceptanceShouldResolveRenameAndClosingBracketWithoutRecalculation()
    {
        var session = CreateSession();
        const string text = "=SUM(Sales[Am])";
        var candidate = SpreadsheetFormulaEditingAssistant.GetStructuredReferenceSuggestions(text, 13,
            session.Workbook, session.ActiveWorksheet, default).Single();
        session.Tables.RenameTable(candidate.TableId, "Orders");
        session.Tables.RenameColumn(candidate.TableId, candidate.ColumnId!.Value, "Net");
        var sentinel = new CellAddress(100, 100);
        session.ActiveWorksheet.SetCell(sentinel, new CellData(CellValue.FromNumber(999), "=1+1"));
        var history = session.History.UndoCount;
        Assert.IsTrue(SpreadsheetFormulaEditingAssistant.TryApplyStructuredReferenceSuggestion(text, 13, 0,
            session.Workbook, session.ActiveWorksheet, default, candidate, out var result));
        Assert.AreEqual("=SUM(Orders[[#Data],[Net]])", result!.Text);
        Assert.AreEqual(999d, session.ActiveWorksheet.GetValue(sentinel));
        Assert.AreEqual(history, session.History.UndoCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AcceptanceShouldRejectDeletedIdentity(bool deleteTable)
    {
        var session = CreateSession();
        const string text = "=Sales[Am";
        var candidate = SpreadsheetFormulaEditingAssistant.GetStructuredReferenceSuggestions(text, text.Length,
            session.Workbook, session.ActiveWorksheet, default).Single();
        if (deleteTable) session.Tables.Remove(candidate.TableId);
        else session.Tables.DeleteColumn(candidate.TableId, candidate.ColumnId!.Value);
        var history = session.History.UndoCount;
        Assert.IsFalse(SpreadsheetFormulaEditingAssistant.TryApplyStructuredReferenceSuggestion(text, text.Length, 0,
            session.Workbook, session.ActiveWorksheet, default, candidate, out _));
        Assert.AreEqual(history, session.History.UndoCount);
    }

    [TestMethod]
    public void AcceptanceShouldRejectOverflowSpanAndMovedCurrentRowContext()
    {
        var session = CreateSession();
        const string text = "=[@Am";
        var candidate = SpreadsheetFormulaEditingAssistant.GetStructuredReferenceSuggestions(text, text.Length,
            session.Workbook, session.ActiveWorksheet, new CellAddress(1, 0)).Single();
        Assert.IsFalse(SpreadsheetFormulaEditingAssistant.TryApplyStructuredReferenceSuggestion(text, text.Length, 0,
            session.Workbook, session.ActiveWorksheet, default, candidate, out _));
        Assert.IsFalse(SpreadsheetFormulaEditingAssistant.TryApplyStructuredReferenceSuggestion(text, text.Length, 0,
            session.Workbook, session.ActiveWorksheet, new CellAddress(1, 0),
            candidate with { ReplacementSpan = new FormulaTextSpan(1, int.MaxValue) }, out _));
    }

    private static SpreadsheetSession CreateSession()
    {
        var workbook = new Workbook();
        workbook.Worksheets[0].AddTable(new SpreadsheetTable(Guid.NewGuid(), "Sales",
            new CellRange(default, new CellAddress(3, 1)),
            [new SpreadsheetTableColumn(Guid.NewGuid(), "Item"), new SpreadsheetTableColumn(Guid.NewGuid(), "Amount")]));
        return new SpreadsheetSession(workbook);
    }
}
