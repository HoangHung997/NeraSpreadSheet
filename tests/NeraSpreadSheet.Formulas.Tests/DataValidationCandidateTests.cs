using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class DataValidationCandidateTests
{
    [TestMethod]
    public void CustomRuleUsesCandidateValueAtTargetAddress()
    {
        var worksheet = new Workbook().Worksheets[0];
        var target = new CellAddress(1, 1);
        worksheet.AddDataValidationRule(new DataValidationRule(
            Guid.NewGuid(),
            [new CellRange(target, target)],
            DataValidationType.Custom,
            null,
            "=B2>0"));
        var snapshot = WorksheetSnapshot.Capture(worksheet);

        Assert.IsTrue(DataValidationEvaluator.Evaluate(
            snapshot,
            target,
            CellValue.FromNumber(5d)).IsValid);
        Assert.IsFalse(DataValidationEvaluator.Evaluate(
            snapshot,
            target,
            CellValue.FromNumber(-1d)).IsValid);
    }

    [TestMethod]
    public void RelativeCustomRuleUsesCandidateAndNeighbor()
    {
        var worksheet = new Workbook().Worksheets[0];
        worksheet.SetValue(new CellAddress(1, 0), 10d);
        worksheet.SetValue(new CellAddress(2, 0), 20d);
        worksheet.AddDataValidationRule(new DataValidationRule(
            Guid.NewGuid(),
            [new CellRange(
                new CellAddress(1, 1),
                new CellAddress(2, 1))],
            DataValidationType.Custom,
            null,
            "=B2<A2"));
        var snapshot = WorksheetSnapshot.Capture(worksheet);

        Assert.IsTrue(DataValidationEvaluator.Evaluate(
            snapshot,
            new CellAddress(1, 1),
            CellValue.FromNumber(9d)).IsValid);
        Assert.IsFalse(DataValidationEvaluator.Evaluate(
            snapshot,
            new CellAddress(2, 1),
            CellValue.FromNumber(21d)).IsValid);
    }
}
