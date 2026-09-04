using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Foundation;
using System.Globalization;

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
    public void TextPredicateDoesNotChangeWithCurrentLocale()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.IsTrue(Matches(
                "FILE",
                TableFilterComparisonOperator.Contains,
                "file"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
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
        Assert.IsFalse(new TableFilterColumn(
            Guid.NewGuid(),
            firstCondition: new TableFilterCondition(
                TableFilterComparisonOperator.IsNotBlank,
                CellValue.Blank)).Matches(CellValue.FromText(string.Empty)));
        Assert.IsTrue(new TableFilterColumn(
            Guid.NewGuid(),
            firstCondition: new TableFilterCondition(
                TableFilterComparisonOperator.IsNotBlank,
                CellValue.Blank)).Matches(CellValue.FromText(" ")));
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

    [TestMethod]
    public void DateGroupsHonorWorkbookDateSystemAndRejectMixedNonDates()
    {
        var workbook = new Workbook { DateSystem = ExcelDateSystem.Date1904 };
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), "Date");
        worksheet.SetValue(new CellAddress(1, 0), 0d);
        worksheet.SetValue(new CellAddress(2, 0), new DateTime(1904, 1, 2));
        worksheet.SetValue(new CellAddress(3, 0), "1904-01-01");
        worksheet.SetValue(new CellAddress(4, 0), CellValue.FromError("#VALUE!"));
        worksheet.SetAutoFilter(new WorksheetAutoFilter(
            new CellRange(new CellAddress(0, 0), new CellAddress(4, 0)),
            [new WorksheetAutoFilterColumn(0, dateGroups: [new SpreadsheetFilterDateGroup(
                1904, SpreadsheetFilterDateGrouping.Day, month: 1, day: 1)])]));

        var snapshot = WorksheetSnapshot.Capture(worksheet);

        Assert.IsTrue(snapshot.IsRowVisible(1));
        Assert.IsFalse(snapshot.IsRowVisible(2));
        Assert.IsFalse(snapshot.IsRowVisible(3));
        Assert.IsFalse(snapshot.IsRowVisible(4));
    }

    [TestMethod]
    public void TopBottomAndDynamicAverageUseOneBoundedColumnScan()
    {
        var worksheet = CreateNumberWorksheet();
        worksheet.SetAutoFilter(new WorksheetAutoFilter(
            new CellRange(new CellAddress(0, 0), new CellAddress(6, 0)),
            [new WorksheetAutoFilterColumn(0, topBottom: new SpreadsheetTopBottomFilter(top: true, percent: true, value: 40d))]));
        var top = WorksheetSnapshot.Capture(worksheet);
        Assert.IsFalse(top.IsRowVisible(1));
        Assert.IsTrue(top.IsRowVisible(4));
        Assert.IsTrue(top.IsRowVisible(5));
        Assert.IsFalse(top.IsRowVisible(6));

        worksheet.SetAutoFilter(new WorksheetAutoFilter(
            new CellRange(new CellAddress(0, 0), new CellAddress(6, 0)),
            [new WorksheetAutoFilterColumn(0, dynamicFilter: new SpreadsheetDynamicFilter(SpreadsheetDynamicFilterType.AboveAverage))]));
        var average = WorksheetSnapshot.Capture(worksheet);
        Assert.IsFalse(average.IsRowVisible(2));
        Assert.IsTrue(average.IsRowVisible(4));
        Assert.IsFalse(average.IsRowVisible(6));
    }

    [TestMethod]
    public void DynamicDateUsesExplicitReferenceAndLocaleIndependentValues()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), "Date");
        worksheet.SetValue(new CellAddress(1, 0), new DateTime(2026, 9, 4));
        worksheet.SetValue(new CellAddress(2, 0), new DateTime(2026, 9, 3));
        worksheet.SetAutoFilter(new WorksheetAutoFilter(
            new CellRange(new CellAddress(0, 0), new CellAddress(2, 0)),
            [new WorksheetAutoFilterColumn(0, dynamicFilter: new SpreadsheetDynamicFilter(
                SpreadsheetDynamicFilterType.Today, ReferenceDate: new DateTime(2026, 9, 4)))]));

        var snapshot = WorksheetSnapshot.Capture(worksheet);
        Assert.IsTrue(snapshot.IsRowVisible(1));
        Assert.IsFalse(snapshot.IsRowVisible(2));
    }

    [TestMethod]
    public void FillAndFontColorFiltersUseCapturedEffectiveStyles()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var blue = new ColorRgba(20, 80, 210);
        var fillStyle = workbook.Styles.Intern(CellStyle.Default with
        {
            Fill = new CellFillStyle { IsVisible = true, Pattern = CellFillPattern.Solid, Color = blue },
        });
        worksheet.SetValue(new CellAddress(0, 0), "Value");
        worksheet.SetValue(new CellAddress(1, 0), 1d);
        worksheet.SetStyle(new CellAddress(1, 0), fillStyle);
        worksheet.SetValue(new CellAddress(2, 0), 2d);
        worksheet.SetAutoFilter(new WorksheetAutoFilter(
            new CellRange(new CellAddress(0, 0), new CellAddress(2, 0)),
            [new WorksheetAutoFilterColumn(0, colorFilter: new SpreadsheetColorFilter(SpreadsheetFilterColorKind.Fill, blue))]));

        var snapshot = WorksheetSnapshot.Capture(worksheet);
        Assert.IsTrue(snapshot.IsRowVisible(1));
        Assert.IsFalse(snapshot.IsRowVisible(2));
    }

    [TestMethod]
    public void SortStateCopiesAndParticipatesInWorksheetEquality()
    {
        var state = new SpreadsheetFilterSortState([
            new SpreadsheetFilterSortCondition(1, descending: true),
        ], caseSensitive: true);
        var filter = new WorksheetAutoFilter(
            new CellRange(new CellAddress(0, 0), new CellAddress(3, 1)),
            columns: null,
            sortState: state);

        Assert.AreEqual(filter, filter.Copy());
        Assert.AreEqual(1, filter.SortState!.Conditions[0].ColumnOffset);
        Assert.IsTrue(filter.SortState.CaseSensitive);
    }

    [TestMethod]
    public void IconFilterUsesDeterministicDefaultIconSetBuckets()
    {
        var worksheet = CreateNumberWorksheet();
        worksheet.SetAutoFilter(new WorksheetAutoFilter(
            new CellRange(new CellAddress(0, 0), new CellAddress(6, 0)),
            [new WorksheetAutoFilterColumn(0, iconFilter: new SpreadsheetIconFilter("3TrafficLights1", 2))]));

        var snapshot = WorksheetSnapshot.Capture(worksheet);
        Assert.IsFalse(snapshot.IsRowVisible(1));
        Assert.IsTrue(snapshot.IsRowVisible(4));
        Assert.IsTrue(snapshot.IsRowVisible(5));
        Assert.IsFalse(snapshot.IsRowVisible(6));
    }

    private static Worksheet CreateNumberWorksheet()
    {
        var worksheet = new Workbook().Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), "Amount");
        worksheet.SetValue(new CellAddress(1, 0), 10d);
        worksheet.SetValue(new CellAddress(2, 0), 20d);
        worksheet.SetValue(new CellAddress(3, 0), 30d);
        worksheet.SetValue(new CellAddress(4, 0), 40d);
        worksheet.SetValue(new CellAddress(5, 0), 40d);
        worksheet.SetValue(new CellAddress(6, 0), CellValue.FromError("#N/A"));
        return worksheet;
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
