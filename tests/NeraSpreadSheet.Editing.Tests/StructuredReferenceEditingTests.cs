using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Formulas;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class StructuredReferenceEditingTests
{
    [TestMethod]
    public void CompletionBeforeClosingBracketShouldReplaceTheWholeFragment()
    {
        var session = CreateSession();
        const string text = "=SUM(Sales[Am])";
        var item = SpreadsheetFormulaEditingAssistant.GetStructuredReferenceSuggestions(text, 13,
            session.Workbook, session.ActiveWorksheet, default).Single();
        var result = SpreadsheetFormulaEditingAssistant.ApplyStructuredReferenceSuggestion(text,
            session.Workbook, session.ActiveWorksheet, default, item);
        Assert.AreEqual("=SUM(Sales[[#Data],[Amount]])", result.Text);
    }

    [TestMethod]
    public void AddShouldRejectWorkbookDuplicateTableIdentity()
    {
        var session = CreateSession();
        var table = session.ActiveWorksheet.Tables.Single();
        var other = session.Workbook.AddWorksheet("Other");
        var otherSession = new SpreadsheetSession(session.Workbook, other);
        Assert.ThrowsExactly<InvalidOperationException>(() => otherSession.Tables.Add(new SpreadsheetTable(
            table.Id, "OtherTable", table.Range, table.Columns)));
        Assert.AreEqual(0, other.TableCount);
        Assert.AreEqual(0, otherSession.History.UndoCount);
    }

    [TestMethod]
    public void VisualTableChangesAndHistoryShouldNotRecalculateOrProjectCells()
    {
        var session = CreateSession();
        var sheet = session.ActiveWorksheet;
        var table = sheet.Tables.Single();
        var address = new CellAddress(1, 1);
        sheet.SetCell(address, new CellData(CellValue.FromNumber(999), "=1+1"));
        var beforeCells = sheet.EnumerateUsedCells().ToArray();
        session.Tables.SetFilterButtons(table.Id, false);
        session.Tables.SetStyle(table.Id, "TableStyleDark1");
        session.Tables.SetBandedColumns(table.Id, true);
        for (var index = 0; index < 3; index++) Assert.IsTrue(session.Undo());
        for (var index = 0; index < 3; index++) Assert.IsTrue(session.Redo());
        CollectionAssert.AreEqual(beforeCells, sheet.EnumerateUsedCells().ToArray());
        Assert.AreEqual(999d, sheet.GetValue(address));
        Assert.AreEqual(3, session.History.UndoCount);
    }

    [TestMethod]
    public void AddShouldRejectExistingSpillWithoutChangingCellsOrHistory()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        var session = new SpreadsheetSession(workbook);
        session.SetFormula(default, "=SEQUENCE(3,2)");
        var cells = sheet.EnumerateUsedCells().ToArray();
        var undo = session.History.UndoCount;
        Assert.ThrowsExactly<InvalidOperationException>(() => session.Tables.Add(new SpreadsheetTable(
            Guid.NewGuid(), "Blocked", new CellRange(default, new CellAddress(2, 1)),
            [new SpreadsheetTableColumn(Guid.NewGuid(), "A"), new SpreadsheetTableColumn(Guid.NewGuid(), "B")])));
        Assert.AreEqual(0, sheet.TableCount);
        Assert.AreEqual(undo, session.History.UndoCount);
        CollectionAssert.AreEqual(cells, sheet.EnumerateUsedCells().ToArray());
        Assert.AreEqual(1, sheet.GetFormulaSpills().Count);
    }

    [TestMethod]
    public void PointModeShouldUseExactTableAreaAndReplaceProvisionalSpan()
    {
        var session = CreateSession();
        var sheet = session.ActiveWorksheet;
        var first = SpreadsheetFormulaEditingAssistant.InsertReference("=SUM(", 5,
            session.Workbook, sheet, new CellAddress(6, 0), sheet,
            new CellRange(new CellAddress(1, 1), new CellAddress(3, 1)));
        Assert.AreEqual("=SUM(Sales[[#Data],[Amount]]", first.Text);
        var drag = SpreadsheetFormulaEditingAssistant.InsertReference(first.Text, first.CaretIndex,
            session.Workbook, sheet, new CellAddress(6, 0), sheet,
            new CellRange(new CellAddress(1, 0), new CellAddress(2, 1)), first.InsertedSpan);
        Assert.AreEqual("=SUM(A2:B3", drag.Text);
        Assert.AreEqual(0, session.History.UndoCount);
    }

    [TestMethod]
    public void PointModeShouldResolveOwningRowAndCrossSheetDependencies()
    {
        var session = CreateSession();
        var sheet = session.ActiveWorksheet;
        var row = SpreadsheetFormulaEditingAssistant.InsertReference("=", 1,
            session.Workbook, sheet, new CellAddress(2, 0), sheet,
            new CellRange(new CellAddress(2, 1), new CellAddress(2, 1)));
        Assert.AreEqual("=Sales[[#This Row],[Amount]]", row.Text);
        Assert.IsTrue(FormulaReferenceAnalyzer.TryGetReferences(row.Text, session.Workbook, sheet,
            new CellAddress(2, 0), out var local));
        Assert.AreEqual(new CellRange(new CellAddress(2, 1), new CellAddress(2, 1)), local.Single().Range);
        var summary = session.Workbook.AddWorksheet("Summary");
        var reference = SpreadsheetFormulaEditingAssistant.InsertReference("=SUM(", 5,
            session.Workbook, summary, default, sheet,
            new CellRange(new CellAddress(1, 1), new CellAddress(3, 1)));
        Assert.IsTrue(FormulaReferenceAnalyzer.TryGetReferences(reference.Text + ")", session.Workbook,
            summary, default, out var crossSheet));
        Assert.AreEqual(sheet.Name, crossSheet.Single().WorksheetName);
    }

    [TestMethod]
    public void CompletionShouldFollowRenamedIdentityAndRejectStaleTextOrDeletedColumn()
    {
        var session = CreateSession();
        var sheet = session.ActiveWorksheet;
        const string text = "=SUM(Sales[Am";
        var item = SpreadsheetFormulaEditingAssistant.GetStructuredReferenceSuggestions(text, text.Length,
            session.Workbook, sheet, new CellAddress(5, 0)).Single();
        session.Tables.RenameTable(item.TableId, "Orders");
        session.Tables.RenameColumn(item.TableId, item.ColumnId!.Value, "Net");
        var applied = SpreadsheetFormulaEditingAssistant.ApplyStructuredReferenceSuggestion(text,
            session.Workbook, sheet, new CellAddress(5, 0), item);
        Assert.AreEqual("=SUM(Orders[[#Data],[Net]]", applied.Text);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            SpreadsheetFormulaEditingAssistant.ApplyStructuredReferenceSuggestion(text + "x",
                session.Workbook, sheet, default, item));
        session.Tables.DeleteColumn(item.TableId, item.ColumnId.Value);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            SpreadsheetFormulaEditingAssistant.ApplyStructuredReferenceSuggestion(text,
                session.Workbook, sheet, default, item));
    }

    [TestMethod]
    [DataRow("=\"Sales[Am")]
    [DataRow("='Sales[Am")]
    [DataRow("=SUM(Sales[[#Data],[Am")]
    public void CompletionShouldIgnoreLiteralsAndUnsupportedNestedFragments(string text)
    {
        var session = CreateSession();
        Assert.AreEqual(0, SpreadsheetFormulaEditingAssistant.GetStructuredReferenceSuggestions(text, text.Length,
            session.Workbook, session.ActiveWorksheet, default).Count);
        Assert.AreEqual(0, new SpreadsheetFormulaEditingAssistant().GetSuggestions(text, text.Length).Count);
    }

    [TestMethod]
    public void CompletionShouldBeBoundedAndCurrentRowRequiresOwningData()
    {
        var session = CreateSession();
        var workbook = session.Workbook;
        var sheet = session.ActiveWorksheet;
        Assert.AreEqual(1, SpreadsheetFormulaEditingAssistant.GetStructuredReferenceSuggestions("=Sales[", 7,
            workbook, sheet, default, 1).Count);
        Assert.AreEqual(0, SpreadsheetFormulaEditingAssistant.GetStructuredReferenceSuggestions("=[@Am", 5,
            workbook, sheet, default).Count);
        Assert.AreEqual(1, SpreadsheetFormulaEditingAssistant.GetStructuredReferenceSuggestions("=[@Am", 5,
            workbook, sheet, new CellAddress(1, 0)).Count);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            SpreadsheetFormulaEditingAssistant.GetStructuredReferenceSuggestions("=Sa", 3, workbook, sheet, default, 257));
    }

    [TestMethod]
    public void MixedA1AndStructuredReferenceShouldRejectTableCompactAtomically()
    {
        var session = CreateSession();
        var sheet = session.ActiveWorksheet;
        var table = sheet.Tables.Single();
        var summary = session.Workbook.AddWorksheet("Summary");
        summary.SetFormula(default, "=Sheet1!A2+SUM(Sales[Amount])");
        var before = sheet.EnumerateUsedCells().ToArray();
        Assert.ThrowsExactly<InvalidOperationException>(() => session.Tables.InsertRow(table.Id, 1));
        CollectionAssert.AreEqual(before, sheet.EnumerateUsedCells().ToArray());
        Assert.AreEqual(table.Range, sheet.Tables.Single().Range);
        Assert.AreEqual(0, session.History.UndoCount);
    }

    [TestMethod]
    public void RenameShouldPreservePrefixedColumnValuesAcrossUndoAndRedo()
    {
        var session = CreateSession();
        var sheet = session.ActiveWorksheet;
        var table = sheet.Tables.Single();
        session.Tables.RenameColumn(table.Id, table.Columns[0].Id, "AmountTax");
        sheet.SetValue(new CellAddress(1, 0), 3d);
        sheet.SetFormula(new CellAddress(2, 0), "=[@Amount]+[@AmountTax]");
        session.Tables.RenameColumn(table.Id, table.Columns[1].Id, "Net");
        Assert.AreEqual("=[@Net]+[@AmountTax]", sheet.GetFormula(new CellAddress(2, 0)));
        Assert.IsTrue(session.Undo());
        Assert.AreEqual("=[@Amount]+[@AmountTax]", sheet.GetFormula(new CellAddress(2, 0)));
        Assert.IsTrue(session.Redo());
        Assert.AreEqual("=[@Net]+[@AmountTax]", sheet.GetFormula(new CellAddress(2, 0)));
    }

    private static SpreadsheetSession CreateSession()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(1, 1), 10d);
        sheet.SetValue(new CellAddress(2, 1), 20d);
        sheet.SetValue(new CellAddress(3, 1), 30d);
        sheet.AddTable(new SpreadsheetTable(Guid.NewGuid(), "Sales", new CellRange(default, new CellAddress(3, 1)),
            [new SpreadsheetTableColumn(Guid.NewGuid(), "Item"), new SpreadsheetTableColumn(Guid.NewGuid(), "Amount")]));
        return new SpreadsheetSession(workbook);
    }
}
