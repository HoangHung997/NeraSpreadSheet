using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class FormulaCriterionTests
{
    [TestMethod]
    public void NumericComparisonOperatorsUseRangeNumericValues()
    {
        Assert.IsTrue(FormulaCriterion.Parse(
            CellValue.FromText(">=10")).Matches(
                CellValue.FromNumber(10d)));
        Assert.IsTrue(FormulaCriterion.Parse(
            CellValue.FromText("<10")).Matches(
                CellValue.FromNumber(9d)));
        Assert.IsFalse(FormulaCriterion.Parse(
            CellValue.FromText("<>10")).Matches(
                CellValue.FromNumber(10d)));
        Assert.IsFalse(FormulaCriterion.Parse(
            CellValue.FromText(">10")).Matches(
                CellValue.FromText("11")));
    }

    [TestMethod]
    public void TextEqualityIsCaseInsensitiveAndSupportsWildcards()
    {
        Assert.IsTrue(FormulaCriterion.Parse(
            CellValue.FromText("north")).Matches(
                CellValue.FromText("NORTH")));
        Assert.IsTrue(FormulaCriterion.Parse(
            CellValue.FromText("N*th")).Matches(
                CellValue.FromText("North")));
        Assert.IsTrue(FormulaCriterion.Parse(
            CellValue.FromText("?outh")).Matches(
                CellValue.FromText("South")));
        Assert.IsFalse(FormulaCriterion.Parse(
            CellValue.FromText("N*th")).Matches(
                CellValue.FromText("South")));
    }

    [TestMethod]
    public void TildeEscapesWildcardCharacters()
    {
        Assert.IsTrue(FormulaCriterion.Parse(
            CellValue.FromText("file~*1")).Matches(
                CellValue.FromText("file*1")));
        Assert.IsTrue(FormulaCriterion.Parse(
            CellValue.FromText("code~?x")).Matches(
                CellValue.FromText("code?x")));
        Assert.IsFalse(FormulaCriterion.Parse(
            CellValue.FromText("file~*1")).Matches(
                CellValue.FromText("fileABC1")));
    }

    [TestMethod]
    public void EmptyAndNonEmptyCriteriaDistinguishBlankValues()
    {
        Assert.IsTrue(FormulaCriterion.Parse(
            CellValue.FromText("=")).Matches(CellValue.Blank));
        Assert.IsFalse(FormulaCriterion.Parse(
            CellValue.FromText("=")).Matches(
                CellValue.FromText("value")));
        Assert.IsFalse(FormulaCriterion.Parse(
            CellValue.FromText("<>")).Matches(CellValue.Blank));
        Assert.IsTrue(FormulaCriterion.Parse(
            CellValue.FromText("<>")).Matches(
                CellValue.FromText("value")));
    }

    [TestMethod]
    public void BooleanDateAndErrorCriteriaRemainTyped()
    {
        Assert.IsTrue(FormulaCriterion.Parse(
            CellValue.FromBoolean(true)).Matches(
                CellValue.FromBoolean(true)));
        Assert.IsFalse(FormulaCriterion.Parse(
            CellValue.FromBoolean(true)).Matches(
                CellValue.FromNumber(1d)));

        var date = new DateTime(2026, 8, 23);
        Assert.IsTrue(FormulaCriterion.Parse(
            CellValue.FromDateTime(date)).Matches(
                CellValue.FromDateTime(date)));

        Assert.IsTrue(FormulaCriterion.Parse(
            CellValue.FromError("#N/A")).Matches(
                CellValue.FromError("#N/A")));
        Assert.IsFalse(FormulaCriterion.Parse(
            CellValue.FromError("#N/A")).Matches(
                CellValue.FromError("#VALUE!")));
    }
}
