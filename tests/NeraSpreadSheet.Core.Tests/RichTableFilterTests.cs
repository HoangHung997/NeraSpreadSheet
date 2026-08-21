using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class RichTableFilterTests
{
    [TestMethod]
    public void TextPredicatesAreOrdinalIgnoreCase()
    {
        Assert.IsTrue(Matches(
            "Quarterly Report",
            TableFilterComparisonOperator.BeginsWith,
            "quarter"));
        Assert.IsTrue(Matches(
            "Quarterly Report",
            TableFilterComparisonOperator.EndsWith,
            "REPORT"));
        Assert.IsTrue(Matches(
            "Quarterly Report",
            TableFilterComparisonOperator.Contains,
            "terly"));
        Assert.IsTrue(Matches(
            "Quarterly Report",
            TableFilterComparisonOperator.DoesNotContain,
            "draft"));
        Assert.IsFalse(Matches(
            "Quarterly Report",
            TableFilterComparisonOperator.DoesNotContain,
            "REPORT"));
    }

    [TestMethod]
    public void BlankPredicatesDoNotCoerceBlankToZero()
    {
        Assert.IsTrue(new TableFilterColumn(
            Guid.NewGuid(),
            firstCondition: new TableFilterCondition(
                TableFilterComparisonOperator.IsBlank,
                CellValue.Blank)).Matches(CellValue.Blank));
        Assert.IsFalse(new TableFilterColumn(
            Guid.NewGuid(),
            firstCondition: new TableFilterCondition(
                TableFilterComparisonOperator.GreaterThan,
                CellValue.FromNumber(-1d))).Matches(CellValue.Blank));
        Assert.IsTrue(new TableFilterColumn(
            Guid.NewGuid(),
            firstCondition: new TableFilterCondition(
                TableFilterComparisonOperator.IsNotBlank,
                CellValue.Blank)).Matches(CellValue.FromText(string.Empty)));
    }

    [TestMethod]
    public void RelativeDatePeriodsUseExplicitReferenceDate()
    {
        var reference = new DateTime(2026, 8, 21);
        Assert.IsTrue(MatchesDate(
            new DateTime(2026, 8, 17),
            TableFilterComparisonOperator.ThisWeek,
            reference));
        Assert.IsFalse(MatchesDate(
            new DateTime(2026, 8, 16),
            TableFilterComparisonOperator.ThisWeek,
            reference));
        Assert.IsTrue(MatchesDate(
            new DateTime(2026, 7, 31),
            TableFilterComparisonOperator.LastMonth,
            reference));
        Assert.IsTrue(MatchesDate(
            new DateTime(2027, 1, 1),
            TableFilterComparisonOperator.NextYear,
            reference));
    }

    [TestMethod]
    public void OnBeforeAndAfterDateIgnoreTimeOfDay()
    {
        var reference = new DateTime(2026, 8, 21);
        Assert.IsTrue(MatchesDate(
            new DateTime(2026, 8, 21, 23, 59, 59),
            TableFilterComparisonOperator.OnDate,
            reference));
        Assert.IsTrue(MatchesDate(
            new DateTime(2026, 8, 20, 23, 59, 59),
            TableFilterComparisonOperator.BeforeDate,
            reference));
        Assert.IsTrue(MatchesDate(
            new DateTime(2026, 8, 22),
            TableFilterComparisonOperator.AfterDate,
            reference));
    }

    private static bool Matches(
        string candidate,
        TableFilterComparisonOperator comparisonOperator,
        string requested) =>
        new TableFilterColumn(
            Guid.NewGuid(),
            firstCondition: new TableFilterCondition(
                comparisonOperator,
                CellValue.FromText(requested)))
        .Matches(CellValue.FromText(candidate));

    private static bool MatchesDate(
        DateTime candidate,
        TableFilterComparisonOperator comparisonOperator,
        DateTime reference) =>
        new TableFilterColumn(
            Guid.NewGuid(),
            firstCondition: new TableFilterCondition(
                comparisonOperator,
                CellValue.FromDateTime(reference)))
        .Matches(CellValue.FromDateTime(candidate));
}
