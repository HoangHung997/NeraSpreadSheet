using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class DataValidationEvaluatorTests
{
    [TestMethod]
    public void WholeBetweenAndBlankPolicyAreEnforced()
    {
        var worksheet = CreateWorksheetWithRule(new DataValidationRule(
            Guid.NewGuid(),
            [new CellRange(default, default)],
            DataValidationType.Whole,
            DataValidationOperator.Between,
            "1",
            "10",
            allowBlank: false));
        var snapshot = WorksheetSnapshot.Capture(worksheet);

        Assert.IsTrue(DataValidationEvaluator.Evaluate(
            snapshot,
            default,
            CellValue.FromNumber(5d)).IsValid);
        Assert.IsFalse(DataValidationEvaluator.Evaluate(
            snapshot,
            default,
            CellValue.FromNumber(5.5d)).IsValid);
        Assert.IsFalse(DataValidationEvaluator.Evaluate(
            snapshot,
            default,
            CellValue.Blank).IsValid);
    }

    [TestMethod]
    public void DecimalDateTimeAndTextLengthOperatorsAreEvaluated()
    {
        AssertValidation(
            DataValidationType.Decimal,
            DataValidationOperator.GreaterThan,
            "10",
            formula2: null,
            CellValue.FromNumber(10.5d),
            expected: true);
        AssertValidation(
            DataValidationType.Date,
            DataValidationOperator.Between,
            "45000",
            "46000",
            CellValue.FromDateTime(DateTime.FromOADate(45500d)),
            expected: true);
        AssertValidation(
            DataValidationType.Time,
            DataValidationOperator.LessThan,
            "0.5",
            formula2: null,
            CellValue.FromDateTime(new DateTime(2026, 1, 1, 11, 0, 0)),
            expected: true);
        AssertValidation(
            DataValidationType.TextLength,
            DataValidationOperator.LessThanOrEqual,
            "4",
            formula2: null,
            CellValue.FromText("Nera"),
            expected: true);
    }

    [TestMethod]
    public void LiteralListAndRangeListAreSupported()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), "Red");
        worksheet.SetValue(new CellAddress(1, 0), "Green");
        worksheet.SetValue(new CellAddress(2, 0), "Blue");
        worksheet.AddDataValidationRule(new DataValidationRule(
            Guid.NewGuid(),
            [new CellRange(
                new CellAddress(0, 1),
                new CellAddress(0, 1))],
            DataValidationType.List,
            @operator: null,
            "=\"Small,Medium,Large\""));
        worksheet.AddDataValidationRule(new DataValidationRule(
            Guid.NewGuid(),
            [new CellRange(
                new CellAddress(1, 1),
                new CellAddress(1, 1))],
            DataValidationType.List,
            @operator: null,
            "=$A$1:$A$3"));
        var snapshot = WorksheetSnapshot.Capture(worksheet);

        Assert.IsTrue(DataValidationEvaluator.Evaluate(
            snapshot,
            new CellAddress(0, 1),
            CellValue.FromText("medium")).IsValid);
        Assert.IsFalse(DataValidationEvaluator.Evaluate(
            snapshot,
            new CellAddress(0, 1),
            CellValue.FromText("XL")).IsValid);
        Assert.IsTrue(DataValidationEvaluator.Evaluate(
            snapshot,
            new CellAddress(1, 1),
            CellValue.FromText("Blue")).IsValid);
    }

    [TestMethod]
    public void CustomFormulaTranslatesFromRuleAnchor()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(1, 0), 1d);
        worksheet.SetValue(new CellAddress(2, 0), -1d);
        worksheet.AddDataValidationRule(new DataValidationRule(
            Guid.NewGuid(),
            [new CellRange(
                new CellAddress(1, 1),
                new CellAddress(2, 1))],
            DataValidationType.Custom,
            @operator: null,
            "=A2>0"));
        var snapshot = WorksheetSnapshot.Capture(worksheet);

        Assert.IsTrue(DataValidationEvaluator.Evaluate(
            snapshot,
            new CellAddress(1, 1),
            CellValue.FromText("anything")).IsValid);
        Assert.IsFalse(DataValidationEvaluator.Evaluate(
            snapshot,
            new CellAddress(2, 1),
            CellValue.FromText("anything")).IsValid);
    }

    [TestMethod]
    public void FailureCarriesConfiguredErrorMetadata()
    {
        var worksheet = CreateWorksheetWithRule(new DataValidationRule(
            Guid.NewGuid(),
            [new CellRange(default, default)],
            DataValidationType.Decimal,
            DataValidationOperator.GreaterThan,
            "0",
            showErrorMessage: true,
            errorStyle: DataValidationErrorStyle.Warning,
            errorTitle: "Check value",
            error: "A positive number is required."));

        var result = DataValidationEvaluator.Evaluate(
            WorksheetSnapshot.Capture(worksheet),
            default,
            CellValue.FromNumber(-1d));
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(DataValidationErrorStyle.Warning, result.ErrorStyle);
        Assert.AreEqual("Check value", result.ErrorTitle);
        Assert.AreEqual("A positive number is required.", result.ErrorMessage);
    }

    private static Worksheet CreateWorksheetWithRule(DataValidationRule rule)
    {
        var worksheet = new Workbook().Worksheets[0];
        worksheet.AddDataValidationRule(rule);
        return worksheet;
    }

    private static void AssertValidation(
        DataValidationType type,
        DataValidationOperator @operator,
        string formula1,
        string? formula2,
        CellValue candidate,
        bool expected)
    {
        var worksheet = CreateWorksheetWithRule(new DataValidationRule(
            Guid.NewGuid(),
            [new CellRange(default, default)],
            type,
            @operator,
            formula1,
            formula2));
        Assert.AreEqual(
            expected,
            DataValidationEvaluator.Evaluate(
                WorksheetSnapshot.Capture(worksheet),
                default,
                candidate).IsValid);
    }
}
