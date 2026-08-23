using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class FormulaCriteriaTests
{
    [TestMethod]
    public void NumericAndDateComparisonsUseInvariantOperands()
    {
        var numeric = FormulaCriteria.Parse(
            CellValue.FromText(">=10.5"));
        Assert.AreEqual(
            FormulaCriteriaOperator.GreaterThanOrEqual,
            numeric.Operator);
        Assert.IsTrue(numeric.Matches(CellValue.FromNumber(10.5d)));
        Assert.IsTrue(numeric.Matches(CellValue.FromNumber(12d)));
        Assert.IsFalse(numeric.Matches(CellValue.FromNumber(10d)));
        Assert.IsFalse(numeric.Matches(CellValue.FromText("12")));

        var date = FormulaCriteria.Parse(
            CellValue.FromText("<2026-02-01"));
        Assert.IsTrue(date.Matches(CellValue.FromDateTime(
            new DateTime(2026, 1, 31))));
        Assert.IsFalse(date.Matches(CellValue.FromDateTime(
            new DateTime(2026, 2, 1))));
    }

    [TestMethod]
    public void TextAndBooleanCriteriaAreCaseInsensitiveAndTyped()
    {
        var text = FormulaCriteria.Parse(CellValue.FromText("open"));
        Assert.IsTrue(text.Matches(CellValue.FromText("OPEN")));
        Assert.IsFalse(text.Matches(CellValue.FromText("closed")));
        Assert.IsFalse(text.Matches(CellValue.FromBoolean(true)));

        var logical = FormulaCriteria.Parse(CellValue.FromText("TRUE"));
        Assert.IsTrue(logical.Matches(CellValue.FromBoolean(true)));
        Assert.IsFalse(logical.Matches(CellValue.FromBoolean(false)));
        Assert.IsFalse(logical.Matches(CellValue.FromText("TRUE")));
    }

    [TestMethod]
    public void WildcardsAndTildeEscapesFollowCriteriaRules()
    {
        var wildcard = FormulaCriteria.Parse(
            CellValue.FromText("inv-??-*"));
        Assert.IsTrue(wildcard.UsesWildcards);
        Assert.IsTrue(wildcard.Matches(
            CellValue.FromText("INV-01-GOLD")));
        Assert.IsFalse(wildcard.Matches(
            CellValue.FromText("INV-1-GOLD")));

        var escapedStar = FormulaCriteria.Parse(
            CellValue.FromText("item~*01"));
        Assert.IsFalse(escapedStar.UsesWildcards);
        Assert.IsTrue(escapedStar.Matches(
            CellValue.FromText("ITEM*01")));
        Assert.IsFalse(escapedStar.Matches(
            CellValue.FromText("item-X01")));

        var notWildcard = FormulaCriteria.Parse(
            CellValue.FromText("<>draft*"));
        Assert.IsFalse(notWildcard.Matches(
            CellValue.FromText("Draft 1")));
        Assert.IsTrue(notWildcard.Matches(
            CellValue.FromText("Approved")));
    }

    [TestMethod]
    public void BlankNonBlankAndErrorCriteriaAreExplicit()
    {
        var blank = FormulaCriteria.Parse(CellValue.FromText("="));
        Assert.IsTrue(blank.Matches(CellValue.Blank));
        Assert.IsFalse(blank.Matches(CellValue.FromNumber(0d)));

        var nonBlank = FormulaCriteria.Parse(CellValue.FromText("<>"));
        Assert.IsFalse(nonBlank.Matches(CellValue.Blank));
        Assert.IsTrue(nonBlank.Matches(CellValue.FromText("value")));
        Assert.IsTrue(nonBlank.Matches(CellValue.FromNumber(0d)));

        var error = FormulaCriteria.Parse(CellValue.FromText("#N/A"));
        Assert.IsTrue(error.Matches(CellValue.FromError("#N/A")));
        Assert.IsFalse(error.Matches(CellValue.FromError("#VALUE!")));
        var notError = FormulaCriteria.Parse(CellValue.FromText("<>#N/A"));
        Assert.IsFalse(notError.Matches(CellValue.FromError("#N/A")));
        Assert.IsTrue(notError.Matches(CellValue.FromError("#VALUE!")));
    }

    [TestMethod]
    public void DirectTypedCriteriaDoNotPassThroughTextParsing()
    {
        var number = FormulaCriteria.Parse(CellValue.FromNumber(5d));
        Assert.IsTrue(number.Matches(CellValue.FromNumber(5d)));
        Assert.IsFalse(number.Matches(CellValue.FromText("5")));

        var dateValue = new DateTime(2026, 8, 23);
        var date = FormulaCriteria.Parse(
            CellValue.FromDateTime(dateValue));
        Assert.IsTrue(date.Matches(
            CellValue.FromDateTime(dateValue)));
        Assert.IsFalse(date.Matches(CellValue.FromNumber(5d)));
    }

    [TestMethod]
    public void ExcessiveCriteriaLengthIsRejected()
    {
        var text = new string('x',
            FormulaCriteria.MaximumCriteriaLength + 1);

        Assert.ThrowsExactly<FormatException>(() =>
            FormulaCriteria.Parse(CellValue.FromText(text)));
    }
}
