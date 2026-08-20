using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class DataValidationBlankPolicyTests
{
    [TestMethod]
    public void BlankIsControlledOnlyByAllowBlankPolicy()
    {
        foreach (var type in new[]
                 {
                     DataValidationType.Whole,
                     DataValidationType.Decimal,
                     DataValidationType.Date,
                     DataValidationType.Time,
                     DataValidationType.TextLength,
                 })
        {
            var blockedWorksheet = new Workbook().Worksheets[0];
            blockedWorksheet.AddDataValidationRule(new DataValidationRule(
                Guid.NewGuid(),
                [new CellRange(default, default)],
                type,
                DataValidationOperator.GreaterThanOrEqual,
                "=-1",
                allowBlank: false));
            Assert.IsFalse(DataValidationEvaluator.Evaluate(
                WorksheetSnapshot.Capture(blockedWorksheet),
                default,
                CellValue.Blank).IsValid);

            var allowedWorksheet = new Workbook().Worksheets[0];
            allowedWorksheet.AddDataValidationRule(new DataValidationRule(
                Guid.NewGuid(),
                [new CellRange(default, default)],
                type,
                DataValidationOperator.GreaterThanOrEqual,
                "=999999",
                allowBlank: true));
            Assert.IsTrue(DataValidationEvaluator.Evaluate(
                WorksheetSnapshot.Capture(allowedWorksheet),
                default,
                CellValue.Blank).IsValid);
        }
    }

    [TestMethod]
    public void BlankListAndCustomCandidatesRespectAllowBlankBeforeFormulaEvaluation()
    {
        foreach (var type in new[]
                 {
                     DataValidationType.List,
                     DataValidationType.Custom,
                 })
        {
            var formula = type == DataValidationType.List
                ? "=\"A,B\""
                : "=TRUE";
            var worksheet = new Workbook().Worksheets[0];
            worksheet.AddDataValidationRule(new DataValidationRule(
                Guid.NewGuid(),
                [new CellRange(default, default)],
                type,
                null,
                formula,
                allowBlank: false));
            Assert.IsFalse(DataValidationEvaluator.Evaluate(
                WorksheetSnapshot.Capture(worksheet),
                default,
                CellValue.Blank).IsValid);
        }
    }
}
