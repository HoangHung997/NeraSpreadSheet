using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class DataValidationTests
{
    [TestMethod]
    public void RuleNormalizesMetadataAndSnapshotIsIndependent()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var range = new CellRange(
            new CellAddress(1, 1),
            new CellAddress(4, 1));
        var id = Guid.NewGuid();
        worksheet.AddDataValidationRule(new DataValidationRule(
            id,
            [range],
            DataValidationType.Whole,
            DataValidationOperator.Between,
            "1",
            "10",
            allowBlank: false,
            showInputMessage: true,
            promptTitle: "Whole number",
            prompt: "Enter 1 through 10.",
            showErrorMessage: true,
            errorStyle: DataValidationErrorStyle.Stop,
            errorTitle: "Invalid",
            error: "Value is outside the allowed range."));

        var snapshot = WorksheetSnapshot.Capture(worksheet);
        var rule = worksheet.DataValidationRules.Single();
        Assert.AreEqual("=1", rule.Formula1);
        Assert.AreEqual("=10", rule.Formula2);
        Assert.AreEqual(1, snapshot.DataValidationRuleCount);
        Assert.IsTrue(snapshot.TryGetDataValidationRule(
            new CellAddress(3, 1),
            out var snapshotRule));
        Assert.AreEqual(id, snapshotRule?.Id);

        Assert.IsTrue(worksheet.RemoveDataValidationRule(id));
        Assert.AreEqual(0, worksheet.DataValidationRuleCount);
        Assert.AreEqual(1, snapshot.DataValidationRuleCount);
    }

    [TestMethod]
    public void OverlappingRulesAreRejectedWithoutChangingExistingState()
    {
        var worksheet = new Workbook().Worksheets[0];
        worksheet.AddDataValidationRule(new DataValidationRule(
            Guid.NewGuid(),
            [new CellRange(
                new CellAddress(1, 1),
                new CellAddress(3, 1))],
            DataValidationType.Decimal,
            DataValidationOperator.GreaterThan,
            "0"));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            worksheet.AddDataValidationRule(new DataValidationRule(
                Guid.NewGuid(),
                [new CellRange(
                    new CellAddress(3, 1),
                    new CellAddress(5, 1))],
                DataValidationType.Decimal,
                DataValidationOperator.LessThan,
                "100")));
        Assert.AreEqual(1, worksheet.DataValidationRuleCount);
    }

    [TestMethod]
    public void ListAndCustomRulesRejectNumericOperatorsAndSecondFormula()
    {
        var range = new CellRange(default, default);
        Assert.ThrowsExactly<ArgumentException>(() =>
            new DataValidationRule(
                Guid.NewGuid(),
                [range],
                DataValidationType.List,
                DataValidationOperator.Equal,
                "=\"A,B\""));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new DataValidationRule(
                Guid.NewGuid(),
                [range],
                DataValidationType.Custom,
                @operator: null,
                "=A1>0",
                "=B1>0"));
    }

    [TestMethod]
    public void RangesWithinOneRuleCannotOverlap()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new DataValidationRule(
                Guid.NewGuid(),
                [
                    new CellRange(
                        new CellAddress(1, 1),
                        new CellAddress(3, 1)),
                    new CellRange(
                        new CellAddress(2, 1),
                        new CellAddress(4, 1)),
                ],
                DataValidationType.List,
                @operator: null,
                "=\"A,B\""));
    }
}
