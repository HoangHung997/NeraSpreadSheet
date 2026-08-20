using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class DataValidationAlertPolicyTests
{
    [TestMethod]
    public void DisabledErrorAlertAllowsInvalidCommitButDiagnosticsRemain()
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
            showErrorMessage: false,
            errorStyle: DataValidationErrorStyle.Stop));
        var session = new SpreadsheetSession(workbook);
        var failureEvents = 0;
        session.Editor.ValidationFailed += (_, _) => failureEvents++;

        session.Editor.BeginEdit();
        Assert.IsTrue(session.Editor.Commit("20"));
        Assert.AreEqual(20d, worksheet.GetValue(default));
        Assert.AreEqual(0, failureEvents);
        Assert.AreEqual(
            1,
            session.Validation.GetInvalidCells(
                new CellRange(default, default))
                .Count);
        Assert.IsTrue(session.Undo());
        Assert.IsTrue(worksheet.GetCell(default).Value.IsBlank);
    }
}
