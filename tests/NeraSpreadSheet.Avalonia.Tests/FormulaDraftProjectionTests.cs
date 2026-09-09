using global::Avalonia.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Formulas;

namespace NeraSpreadSheet.Avalonia.Tests;

[TestClass]
public sealed class FormulaDraftProjectionTests
{
    [TestMethod]
    public void UnfinishedArgumentsShouldRetainAllCompletedRanges()
    {
        var workbook = new Workbook();
        var result = Project(workbook, "=SUM(A1:A3,B1:B3,");
        Assert.HasCount(2, result);
        Assert.AreEqual(Range(0, 0, 2, 0), result[0].Range);
        Assert.AreEqual(Range(0, 1, 2, 1), result[1].Range);
    }
    [TestMethod]
    public void UnfinishedNestedFunctionShouldRetainItsArgumentReferences()
    {
        var result = Project(new Workbook(), "=SUM(A1:A3,IF(B2>0,C2");
        Assert.HasCount(3, result);
        Assert.AreEqual(Range(0, 0, 2, 0), result[0].Range);
        Assert.AreEqual(Range(1, 1, 1, 1), result[1].Range);
        Assert.AreEqual(Range(1, 2, 1, 2), result[2].Range);
    }
    [TestMethod]
    public void UnfinishedStringShouldNotCreateAReferenceFromItsContents()
    {
        var result = Project(new Workbook(), "=SUM(A1,\"B2");
        Assert.HasCount(1, result);
        Assert.AreEqual(Range(0, 0, 0, 0), result[0].Range);
    }
    [TestMethod]
    public void IncompleteFunctionNameShouldNotRemoveEarlierRanges()
    {
        var result = Project(new Workbook(), "=SUM(A1:A3,XLO");
        Assert.HasCount(1, result);
        Assert.AreEqual(Range(0, 0, 2, 0), result[0].Range);
    }
    [TestMethod]
    public void EscapedSheetNamesShouldBeResolvedByTheSharedAnalyzer()
    {
        var workbook = new Workbook();
        workbook.AddWorksheet("O'Brien");
        var result = Project(workbook, "=SUM(A1,'O''Brien'!B2,");
        Assert.HasCount(2, result);
        Assert.AreEqual("O'Brien", result[1].WorksheetName);
        Assert.AreEqual(Range(1, 1, 1, 1), result[1].Range);
    }
    [TestMethod]
    public void EscapedQuotedStringsShouldNotProduceFalseRangeOutlines()
    {
        var result = Project(new Workbook(), "=SUM(A1,\"B2\"\"C3\",");
        Assert.HasCount(1, result);
        Assert.AreEqual(Range(0, 0, 0, 0), result[0].Range);
    }
    [TestMethod]
    public void ProvisionalReferenceShouldNotDuplicateAnImplicitCurrentSheetReference()
    {
        var workbook = new Workbook();
        var range = Range(0, 0, 2, 0);
        var result = FormulaDraftReferenceProjection.GetReferences("=SUM(A1:A3", workbook,
            workbook.Worksheets[0], default, new FormulaDependency(workbook.Worksheets[0].Name, range));
        Assert.HasCount(1, result);
        Assert.AreEqual(range, result[0].Range);
    }
    [TestMethod]
    public Task ProvisionalDragShouldKeepEarlierReferenceAndItsColor() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var session = new SpreadsheetSession(new Workbook());
        using var control = new NeraSpreadsheetControl { Session = session };
        var window = new Window { Width = 800, Height = 500, Content = control };
        window.Show(); window.UpdateLayout();
        try
        {
            control.BeginEdit("=SUM(A1:A3,");
            Assert.HasCount(1, control.CurrentFormulaReferenceHighlights);
            var firstColor = control.CurrentFormulaReferenceHighlights[0].Color;
            Assert.IsTrue(control.InsertFormulaReference(Range(1, 2, 3, 2)));
            Assert.HasCount(2, control.CurrentFormulaReferenceHighlights);
            Assert.AreEqual(firstColor, control.CurrentFormulaReferenceHighlights[0].Color);
            Assert.AreEqual(Range(1, 2, 3, 2), control.CurrentFormulaReferenceHighlights[1].Range);
            Assert.IsTrue(control.InsertFormulaReference(Range(1, 2, 7, 2)));
            Assert.HasCount(2, control.CurrentFormulaReferenceHighlights);
            Assert.AreEqual(firstColor, control.CurrentFormulaReferenceHighlights[0].Color);
            Assert.AreEqual("=SUM(A1:A3,C2:C8", control.EditorText);
        }
        finally { window.Content = null; window.Close(); }
    });
    [TestMethod]
    public Task PreviewBalancingShouldNeverCompleteDraftOrChangeHistory() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var session = new SpreadsheetSession(new Workbook());
        using var control = new NeraSpreadsheetControl { Session = session };
        var version = session.Workbook.Version;
        const string text = "=SUM(A1:A3,B1:B3,";
        control.BeginEdit(text, false);
        Assert.AreEqual(text, control.EditorText);
        Assert.HasCount(2, control.CurrentFormulaReferenceHighlights);
        Assert.AreEqual(version, session.Workbook.Version);
        Assert.IsTrue(session.ActiveWorksheet.GetCell(default).IsEmpty);
        Assert.IsFalse(session.Undo());
    });
    private static IReadOnlyList<FormulaDependency> Project(Workbook workbook, string text) =>
        FormulaDraftReferenceProjection.GetReferences(text, workbook, workbook.Worksheets[0], default, null);
    private static CellRange Range(int row, int column, int lastRow, int lastColumn) =>
        new(new CellAddress(row, column), new CellAddress(lastRow, lastColumn));
}
