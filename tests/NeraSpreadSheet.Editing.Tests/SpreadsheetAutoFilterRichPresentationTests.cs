using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetAutoFilterRichPresentationTests
{
    private static readonly int[] ExpectedMonths = [1, 2];

    [TestMethod]
    public async Task PresenterProjectsKindsAndLazyPagedDateTree()
    {
        var fixture = CreateMixedTableFixture();
        await using var presenter = new SpreadsheetAutoFilterPagedPresenter(
            fixture.Session,
            fixture.Target,
            pageSize: 2);

        await presenter.InitializeAsync();
        var snapshot = presenter.Capture();
        CollectionAssert.Contains(
            snapshot.MenuKinds.ToArray(),
            SpreadsheetAutoFilterMenuKind.Date);

        var years = await presenter.GetDatePageAsync(
            new SpreadsheetAutoFilterDateParent(null, null),
            offset: 0,
            pageSize: 1);
        Assert.AreEqual(2, years.TotalNodeCount);
        Assert.AreEqual(2025, years.Nodes.Single().Year);
        Assert.IsTrue(years.HasNextPage);

        var months = await presenter.GetDatePageAsync(
            new SpreadsheetAutoFilterDateParent(2026, null),
            offset: 0,
            pageSize: 10);
        CollectionAssert.AreEqual(
            ExpectedMonths,
            months.Nodes.Select(static node => node.Month!.Value).ToArray());
        Assert.IsTrue(months.Nodes.All(static node => node.HasChildren));
    }

    [TestMethod]
    public async Task RichApplyUsesOneHistoryEntryAndRejectsStaleGeneration()
    {
        var fixture = CreateMixedTableFixture();
        await using var paged = new SpreadsheetTableFilterPagedSession(
            fixture.Session,
            fixture.TableId,
            fixture.ColumnId);
        var generation = await paged.RefreshAsync();
        var criterion = new SpreadsheetAutoFilterRichCriterion(
            dateGroups:
            [
                new SpreadsheetFilterDateGroup(
                    2026,
                    SpreadsheetFilterDateGrouping.Month,
                    month: 1),
            ]);

        var invalidated = await paged.ApplyRichFilterAsync(
            generation,
            criterion);

        Assert.AreEqual(generation + 1, invalidated);
        Assert.AreEqual(1, fixture.Session.History.UndoCount);
        var filter = fixture.Session.ActiveWorksheet.Tables.Single()
            .AutoFilter!.Columns.Single();
        Assert.AreEqual(1, filter.DateGroups.Count);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await paged.ApplyRichFilterAsync(generation, criterion));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await paged.GetDatePageAsync(
                generation,
                new SpreadsheetAutoFilterDateParent(null, null),
                0,
                10));
        Assert.AreEqual(1, fixture.Session.History.UndoCount);
        Assert.IsTrue(fixture.Session.Undo());
        Assert.IsNull(fixture.Session.ActiveWorksheet.Tables.Single().AutoFilter);
        Assert.IsTrue(fixture.Session.Redo());
    }

    [TestMethod]
    public void SharedNativeEditorParsesEveryRichMenuFamily()
    {
        Assert.AreEqual(
            TableFilterComparisonOperator.Contains,
            SpreadsheetAutoFilterCriterionParser.Parse(
                SpreadsheetAutoFilterMenuKind.Text,
                "North").CustomCondition!.Operator);
        Assert.AreEqual(
            42d,
            SpreadsheetAutoFilterCriterionParser.Parse(
                SpreadsheetAutoFilterMenuKind.Number,
                "42").CustomCondition!.Value.RawValue);
        Assert.AreEqual(
            SpreadsheetFilterDateGrouping.Day,
            SpreadsheetAutoFilterCriterionParser.Parse(
                SpreadsheetAutoFilterMenuKind.Date,
                "2026-09-04").RichCriterion!.DateGroups.Single().Grouping);
        var groupedDates = SpreadsheetAutoFilterCriterionParser.Parse(
            SpreadsheetAutoFilterMenuKind.Date,
            "Year:2025;Month:2026-02;Day:2026-03-04;" +
            "Hour:2026-03-04 05;Minute:2026-03-04 05:06;" +
            "Second:2026-03-04 05:06:07").RichCriterion!.DateGroups;
        CollectionAssert.AreEqual(
            Enum.GetValues<SpreadsheetFilterDateGrouping>(),
            groupedDates.Select(static group => group.Grouping).ToArray());
        Assert.AreEqual(
            SpreadsheetFilterColorKind.Fill,
            SpreadsheetAutoFilterCriterionParser.Parse(
                SpreadsheetAutoFilterMenuKind.FillColor,
                "#336699").RichCriterion!.ColorFilter!.Kind);
        Assert.AreEqual(
            SpreadsheetFilterColorKind.Font,
            SpreadsheetAutoFilterCriterionParser.Parse(
                SpreadsheetAutoFilterMenuKind.FontColor,
                "#FF336699").RichCriterion!.ColorFilter!.Kind);
        Assert.AreEqual(
            2u,
            SpreadsheetAutoFilterCriterionParser.Parse(
                SpreadsheetAutoFilterMenuKind.Icon,
                "3TrafficLights1:2").RichCriterion!.IconFilter!.IconId);
        Assert.AreEqual(
            TableFilterComparisonOperator.GreaterThan,
            SpreadsheetAutoFilterCriterionParser.Parse(
                SpreadsheetAutoFilterMenuKind.Custom,
                "GreaterThan:10").CustomCondition!.Operator);
        var combined = SpreadsheetAutoFilterCriterionParser.Parse(
            SpreadsheetAutoFilterMenuKind.Custom,
            "GreaterThan:10 AND LessThanOrEqual:20");
        Assert.AreEqual(
            TableFilterComparisonOperator.GreaterThan,
            combined.CustomCondition!.Operator);
        Assert.AreEqual(
            TableFilterComparisonOperator.LessThanOrEqual,
            combined.SecondCustomCondition!.Operator);
        Assert.IsTrue(combined.CombineWithAnd);
        var alternative = SpreadsheetAutoFilterCriterionParser.Parse(
            SpreadsheetAutoFilterMenuKind.Custom,
            "Contains:North OR Contains:South");
        Assert.IsFalse(alternative.CombineWithAnd);
        Assert.AreEqual(
            TableFilterComparisonOperator.IsBlank,
            SpreadsheetAutoFilterCriterionParser.ParseCustomCondition(
                "IsBlank").Operator);
        Assert.IsTrue(
            SpreadsheetAutoFilterCriterionParser.Parse(
                SpreadsheetAutoFilterMenuKind.TopBottom,
                "Top10%").RichCriterion!.TopBottom!.Percent);
        Assert.AreEqual(
            SpreadsheetDynamicFilterType.Today,
            SpreadsheetAutoFilterCriterionParser.Parse(
                SpreadsheetAutoFilterMenuKind.Dynamic,
                "Today").RichCriterion!.DynamicFilter!.Type);
        Assert.AreEqual(
            SpreadsheetDynamicFilterType.AboveAverage,
            SpreadsheetAutoFilterCriterionParser.Parse(
                SpreadsheetAutoFilterMenuKind.Dynamic,
                "AboveAverage").RichCriterion!.DynamicFilter!.Type);
    }

    [TestMethod]
    public async Task WorksheetRichApplyUsesTheSamePresenterAndOneHistoryEntry()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), "Amount");
        worksheet.SetValue(new CellAddress(1, 0), 10d);
        worksheet.SetValue(new CellAddress(2, 0), 20d);
        worksheet.SetValue(new CellAddress(3, 0), 30d);
        worksheet.SetAutoFilter(new WorksheetAutoFilter(
            new CellRange(new CellAddress(0, 0), new CellAddress(3, 0))));
        var session = new SpreadsheetSession(workbook);
        session.Selection.SetActiveCell(new CellAddress(1, 0));
        Assert.IsTrue(session.TryResolveActiveAutoFilterTarget(out var target));
        await using var presenter = new SpreadsheetAutoFilterPagedPresenter(session, target);
        await presenter.InitializeAsync();

        await presenter.ApplyRichFilterAsync(new SpreadsheetAutoFilterRichCriterion(
            topBottom: new SpreadsheetTopBottomFilter(true, false, 1)));

        Assert.AreEqual(1, session.History.UndoCount);
        Assert.IsNotNull(worksheet.AutoFilter!.Columns.Single().TopBottom);
        Assert.IsFalse(WorksheetSnapshot.Capture(worksheet).IsRowVisible(1));
        Assert.IsTrue(WorksheetSnapshot.Capture(worksheet).IsRowVisible(3));
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(0, worksheet.AutoFilter!.Columns.Count);
        Assert.IsTrue(session.Redo());
    }

    [TestMethod]
    [Timeout(60_000)]
    public async Task LargeSourceRetainsTenThousandValuesAndOneNativePageProjection()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var tableId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        worksheet.SetValue(new CellAddress(0, 0), "Value");
        for (var row = 1; row <= 100_000; row++)
        {
            worksheet.SetValue(
                new CellAddress(row, 0),
                $"Value{(row - 1) % 10_000:00000}");
        }
        worksheet.AddTable(new SpreadsheetTable(
            tableId,
            "LargeValues",
            new CellRange(new CellAddress(0, 0), new CellAddress(100_000, 0)),
            [new SpreadsheetTableColumn(columnId, "Value")]));
        var session = new SpreadsheetSession(workbook);
        session.Selection.SetActiveCell(new CellAddress(1, 0));
        Assert.IsTrue(session.TryResolveActiveAutoFilterTarget(out var target));
        await using var presenter = new SpreadsheetAutoFilterPagedPresenter(
            session,
            target,
            pageSize: 100);

        await presenter.InitializeAsync();
        var snapshot = presenter.Capture();
        Assert.AreEqual(10_000, snapshot.TotalItemCount);
        Assert.AreEqual(100, snapshot.Values.Count);
        Assert.IsFalse(snapshot.IsSourceTruncated);
    }

    [TestMethod]
    [Timeout(60_000)]
    public async Task MoreThanTenThousandDistinctValuesAreTruncatedAndCannotApply()
    {
        const int distinctValueCount = 10_001;
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var tableId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        worksheet.SetValue(new CellAddress(0, 0), "Value");
        for (var row = 1; row <= distinctValueCount; row++)
        {
            worksheet.SetValue(new CellAddress(row, 0), $"Value{row:00000}");
        }
        worksheet.AddTable(new SpreadsheetTable(
            tableId,
            "DistinctValues",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(distinctValueCount, 0)),
            [new SpreadsheetTableColumn(columnId, "Value")]));
        var session = new SpreadsheetSession(workbook);
        session.Selection.SetActiveCell(new CellAddress(1, 0));
        Assert.IsTrue(session.TryResolveActiveAutoFilterTarget(out var target));
        await using var presenter = new SpreadsheetAutoFilterPagedPresenter(
            session,
            target,
            pageSize: 100);

        await presenter.InitializeAsync();

        Assert.AreEqual(10_000, presenter.Capture().TotalItemCount);
        Assert.IsTrue(presenter.Capture().IsSourceTruncated);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await presenter.ApplyValueSelectionAsync());
        Assert.AreEqual(0, session.History.UndoCount);
        Assert.IsNull(worksheet.Tables.Single().AutoFilter);
    }

    [TestMethod]
    public async Task DateTreeUsesEffectiveNumberFormatAndWorkbookDateSystem()
    {
        await AssertDateSerialProjectionAsync(ExcelDateSystem.Date1900);
        await AssertDateSerialProjectionAsync(ExcelDateSystem.Date1904);
    }

    [TestMethod]
    public async Task DateTreeRejectsMonthWithoutYearParent()
    {
        var fixture = CreateMixedTableFixture();
        await using var presenter = new SpreadsheetAutoFilterPagedPresenter(
            fixture.Session,
            fixture.Target);
        await presenter.InitializeAsync();

        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await presenter.GetDatePageAsync(
                new SpreadsheetAutoFilterDateParent(null, 1),
                0,
                10));
    }

    private static async Task AssertDateSerialProjectionAsync(
        ExcelDateSystem dateSystem)
    {
        var workbook = new Workbook { DateSystem = dateSystem };
        var worksheet = workbook.Worksheets[0];
        var tableId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        var expectedDate = new DateTime(2026, 9, 4);
        var serial = dateSystem == ExcelDateSystem.Date1904
            ? (expectedDate - new DateTime(1904, 1, 1)).TotalDays
            : expectedDate.ToOADate();
        worksheet.SetValue(new CellAddress(0, 0), "Date");
        worksheet.SetValue(new CellAddress(1, 0), serial);
        worksheet.SetValue(new CellAddress(2, 0), serial + 1d);
        worksheet.AddTable(new SpreadsheetTable(
            tableId,
            "SerialDates",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(2, 0)),
            [new SpreadsheetTableColumn(columnId, "Date")]));
        var session = new SpreadsheetSession(workbook);
        session.Selection.SelectColumn(0);
        session.Styles.SetNumberFormat("yyyy-mm-dd");
        session.Selection.SetActiveCell(new CellAddress(1, 0));
        Assert.IsTrue(session.TryResolveActiveAutoFilterTarget(out var target));
        await using var presenter = new SpreadsheetAutoFilterPagedPresenter(
            session,
            target);

        await presenter.InitializeAsync();
        var snapshot = presenter.Capture();
        CollectionAssert.Contains(
            snapshot.MenuKinds.ToArray(),
            SpreadsheetAutoFilterMenuKind.Date);
        CollectionAssert.Contains(
            snapshot.MenuKinds.ToArray(),
            SpreadsheetAutoFilterMenuKind.Dynamic);
        var years = await presenter.GetDatePageAsync(
            new SpreadsheetAutoFilterDateParent(null, null),
            0,
            10);

        Assert.AreEqual(1, years.TotalNodeCount);
        Assert.AreEqual(expectedDate.Year, years.Nodes.Single().Year);
        Assert.AreEqual(2, years.Nodes.Single().Count);
    }

    private static Fixture CreateMixedTableFixture()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var tableId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        worksheet.SetValue(new CellAddress(0, 0), "Ngày");
        worksheet.SetValue(new CellAddress(1, 0), new DateTime(2025, 12, 31));
        worksheet.SetValue(new CellAddress(2, 0), new DateTime(2026, 1, 5));
        worksheet.SetValue(new CellAddress(3, 0), new DateTime(2026, 2, 8));
        worksheet.AddTable(new SpreadsheetTable(
            tableId,
            "Dates",
            new CellRange(new CellAddress(0, 0), new CellAddress(3, 0)),
            [new SpreadsheetTableColumn(columnId, "Ngày")]));
        var session = new SpreadsheetSession(workbook);
        session.Selection.SetActiveCell(new CellAddress(1, 0));
        Assert.IsTrue(session.TryResolveActiveAutoFilterTarget(out var target));
        return new Fixture(session, target, tableId, columnId);
    }

    private sealed record Fixture(
        SpreadsheetSession Session,
        SpreadsheetAutoFilterTarget Target,
        Guid TableId,
        Guid ColumnId);
}
