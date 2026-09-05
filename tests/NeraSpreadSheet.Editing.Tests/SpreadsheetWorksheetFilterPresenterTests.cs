using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetWorksheetFilterPresenterTests
{
    private static readonly string[] ExpectedSearchValues =
        ["Open", "Pending"];

    [TestMethod]
    public void ActiveCellResolverReturnsRangeColumnAndFilterState()
    {
        var fixture = CreateFixture();
        fixture.Session.Selection.SetActiveCell(
            new CellAddress(2, 0));

        Assert.IsTrue(
            fixture.Session.TryResolveActiveWorksheetFilterTarget(
                out var target));
        Assert.AreEqual(FilterRange, target.FilterRange);
        Assert.AreEqual(0, target.ColumnOffset);
        Assert.AreEqual(0, target.WorksheetColumnIndex);
        Assert.AreEqual(new CellAddress(0, 0), target.HeaderCell);
        Assert.IsFalse(target.IsFiltered);

        fixture.Session.WorksheetFilter.ApplyValueFilter(
            0,
            [CellValue.FromText("Open")]);
        Assert.IsTrue(
            fixture.Session.TryResolveActiveWorksheetFilterTarget(
                out target));
        Assert.IsTrue(target.IsFiltered);
    }

    [TestMethod]
    public void MenuSearchSelectionAndApplyUseProductionHistory()
    {
        var fixture = CreateFixture();
        var menu =
            new SpreadsheetWorksheetFilterPresenterController(
                fixture.Session)
            .OpenFilterMenu(0);

        menu.SetSearchText("pen");
        var searchPage = menu.CapturePage(0, 10);
        CollectionAssert.AreEqual(
            ExpectedSearchValues,
            searchPage.Values
                .Select(static value => value.DisplayText)
                .ToArray());

        menu.SetSearchText(null);
        menu.ClearVisibleSelection();
        menu.SetSelected(
            CellValue.FromText("Open"),
            selected: true);
        menu.ApplyValueSelection();

        Assert.AreEqual(2, fixture.Session.History.UndoCount);
        Assert.IsFalse(WorksheetSnapshot.Capture(fixture.Worksheet)
            .IsRowVisible(2));
        Assert.IsTrue(fixture.Session.Undo());
        Assert.IsTrue(WorksheetSnapshot.Capture(fixture.Worksheet)
            .IsRowVisible(2));
    }

    [TestMethod]
    public async Task PagedSessionAppliesSelectionAndInvalidatesGeneration()
    {
        var fixture = CreateFixture();
        await using var paged =
            new SpreadsheetWorksheetFilterPagedSession(
                fixture.Session,
                worksheetColumnIndex: 0);
        var generation = await paged.RefreshAsync();
        var search = await paged.GetPageAsync(
            "pen",
            0,
            10);

        Assert.AreEqual(generation, search.Generation);
        CollectionAssert.AreEqual(
            ExpectedSearchValues,
            search.Page.Values
                .Select(static value => value.DisplayText)
                .ToArray());

        await paged.ClearVisibleSelectionAsync(
            generation,
            searchText: null);
        await paged.SetSelectedAsync(
            generation,
            CellValue.FromText("Pending"),
            selected: true);
        var invalidated =
            await paged.ApplyValueSelectionAsync(generation);

        Assert.AreEqual(generation + 1L, invalidated);
        Assert.IsFalse(paged.IsReady);
        var criterion = fixture.Worksheet.AutoFilter?
            .Columns.Single()
            ?? throw new AssertFailedException(
                "Worksheet filter criterion was not applied.");
        Assert.AreEqual(0, criterion.ColumnOffset);
        Assert.AreEqual(
            "Pending",
            criterion.Values.Single().RawValue);
        Assert.IsTrue(WorksheetSnapshot.Capture(fixture.Worksheet)
            .IsRowVisible(3));
        Assert.IsFalse(WorksheetSnapshot.Capture(fixture.Worksheet)
            .IsRowVisible(1));
    }

    [TestMethod]
    public async Task StaleGenerationCannotMutateDirectFilter()
    {
        var fixture = CreateFixture();
        await using var paged =
            new SpreadsheetWorksheetFilterPagedSession(
                fixture.Session,
                worksheetColumnIndex: 0);
        var first = await paged.RefreshAsync();
        var second = await paged.RefreshAsync();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await paged.ClearColumnFilterAsync(first));

        Assert.AreEqual(first + 1L, second);
        Assert.AreEqual(0, fixture.Worksheet.AutoFilter!.Columns.Count);
        Assert.AreEqual(1, fixture.Session.History.UndoCount);
    }

    [TestMethod]
    public void MenuRejectsColumnOutsideDirectFilterRange()
    {
        var fixture = CreateFixture();
        var presenter =
            new SpreadsheetWorksheetFilterPresenterController(
                fixture.Session);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            presenter.OpenFilterMenu(2));
    }

    [TestMethod]
    public void TruncatedWorksheetCatalogCannotSilentlyApplyRetainedValues()
    {
        var fixture = CreateFixture();
        var menu = new SpreadsheetWorksheetFilterPresenterController(
                fixture.Session)
            .OpenFilterMenu(
                0,
                maximumRows: 4,
                maximumDistinctValues: 2);
        var undoCount = fixture.Session.History.UndoCount;

        Assert.IsTrue(menu.IsDistinctValueTruncated);
        Assert.IsFalse(menu.CanApplyValueSelection);
        Assert.ThrowsExactly<InvalidOperationException>(
            menu.ApplyValueSelection);
        Assert.AreEqual(undoCount, fixture.Session.History.UndoCount);
        Assert.AreEqual(0, fixture.Worksheet.AutoFilter!.Columns.Count);
    }

    private static readonly CellRange FilterRange = new(
        new CellAddress(0, 0),
        new CellAddress(4, 1));

    private static Fixture CreateFixture()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), "Status");
        worksheet.SetValue(new CellAddress(0, 1), "Amount");
        worksheet.SetValue(new CellAddress(1, 0), "Open");
        worksheet.SetValue(new CellAddress(1, 1), 10d);
        worksheet.SetValue(new CellAddress(2, 0), "Closed");
        worksheet.SetValue(new CellAddress(2, 1), 20d);
        worksheet.SetValue(new CellAddress(3, 0), "Pending");
        worksheet.SetValue(new CellAddress(3, 1), 30d);
        worksheet.SetValue(new CellAddress(4, 0), "Open");
        worksheet.SetValue(new CellAddress(4, 1), 40d);
        var session = new SpreadsheetSession(workbook);
        session.WorksheetFilter.SetRange(FilterRange);
        return new Fixture(worksheet, session);
    }

    private sealed record Fixture(
        Worksheet Worksheet,
        SpreadsheetSession Session);
}
