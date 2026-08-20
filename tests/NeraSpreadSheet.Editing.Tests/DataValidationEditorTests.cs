using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class DataValidationEditorTests
{
    [TestMethod]
    public void StopStyleBlocksCommitWithoutMutationOrHistory()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.AddDataValidationRule(new DataValidationRule(
            Guid.NewGuid(),
            [new CellRange(default, default)],
            DataValidationType.Whole,
            DataValidationOperator.Between,
            "1",
            "10",
            allowBlank: false,
            showErrorMessage: true,
            errorTitle: "Invalid",
            error: "Enter 1 through 10."));
        var session = new SpreadsheetSession(workbook);
        var failureCount = 0;
        session.Editor.ValidationFailed += (_, args) =>
        {
            failureCount++;
            Assert.AreEqual(default(CellAddress), args.Address);
        };

        session.Editor.BeginEdit();
        Assert.IsFalse(session.Editor.Commit("20"));
        Assert.IsTrue(session.Editor.IsEditing);
        Assert.AreEqual(1, failureCount);
        Assert.IsTrue(worksheet.GetCell(default).Value.IsBlank);
        Assert.AreEqual(0, session.History.UndoCount);

        Assert.IsTrue(session.Editor.Commit("5"));
        Assert.IsFalse(session.Editor.IsEditing);
        Assert.AreEqual(5d, worksheet.GetValue(default));
        Assert.AreEqual(1, session.History.UndoCount);
        Assert.IsTrue(session.Undo());
        Assert.IsTrue(worksheet.GetCell(default).Value.IsBlank);
    }

    [TestMethod]
    public void WarningAndInformationRequireExplicitOverride()
    {
        foreach (var style in new[]
                 {
                     DataValidationErrorStyle.Warning,
                     DataValidationErrorStyle.Information,
                 })
        {
            var workbook = new Workbook();
            var worksheet = workbook.Worksheets[0];
            worksheet.AddDataValidationRule(new DataValidationRule(
                Guid.NewGuid(),
                [new CellRange(default, default)],
                DataValidationType.Decimal,
                DataValidationOperator.GreaterThan,
                "0",
                showErrorMessage: true,
                errorStyle: style));
            var session = new SpreadsheetSession(workbook);

            session.Editor.BeginEdit();
            Assert.IsFalse(session.Editor.Commit("-1"));
            Assert.IsTrue(session.Editor.Commit(
                "-1",
                acceptValidationWarning: true));
            Assert.AreEqual(-1d, worksheet.GetValue(default));
        }
    }

    [TestMethod]
    public void BeginEditExposesConfiguredInputMessage()
    {
        var workbook = new Workbook();
        workbook.Worksheets[0].AddDataValidationRule(
            new DataValidationRule(
                Guid.NewGuid(),
                [new CellRange(default, default)],
                DataValidationType.List,
                @operator: null,
                "=\"A,B\"",
                showInputMessage: true,
                promptTitle: "Choose",
                prompt: "Select A or B."));
        var session = new SpreadsheetSession(workbook);

        session.Editor.BeginEdit();
        Assert.AreEqual("Choose", session.Editor.CurrentInputMessage?.Title);
        Assert.AreEqual(
            "Select A or B.",
            session.Editor.CurrentInputMessage?.Message);
    }

    [TestMethod]
    public void DiagnosticsAreBoundedAndReturnInvalidCellsOnly()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.AddDataValidationRule(new DataValidationRule(
            Guid.NewGuid(),
            [new CellRange(
                new CellAddress(0, 0),
                new CellAddress(2, 0))],
            DataValidationType.Whole,
            DataValidationOperator.GreaterThan,
            "0",
            allowBlank: true));
        worksheet.SetValue(new CellAddress(0, 0), 1d);
        worksheet.SetValue(new CellAddress(1, 0), -1d);
        var session = new SpreadsheetSession(workbook);

        var diagnostics = session.Validation.GetInvalidCells(
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(2, 0)),
            maximumCells: 3);
        Assert.AreEqual(1, diagnostics.Count);
        Assert.AreEqual(new CellAddress(1, 0), diagnostics[0].Address);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.Validation.GetInvalidCells(
                new CellRange(
                    new CellAddress(0, 0),
                    new CellAddress(3, 0)),
                maximumCells: 3));
    }
}
