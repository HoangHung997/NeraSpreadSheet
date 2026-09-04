using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetAutoFilterPagedPresenterTests
{
    [TestMethod]
    public async Task PresenterNavigatesSearchesAndUpdatesSelection()
    {
        var fixture = CreateTableFixture(7);
        Assert.IsTrue(fixture.Session.TryResolveActiveAutoFilterTarget(
            out var target));
        await using var presenter = new SpreadsheetAutoFilterPagedPresenter(
            fixture.Session,
            target,
            pageSize: 3);

        await presenter.InitializeAsync();
        var first = presenter.Capture();
        Assert.AreEqual(0, first.PageOffset);
        Assert.AreEqual(3, first.Values.Count);
        Assert.IsTrue(first.HasNextPage);

        Assert.IsTrue(await presenter.MoveNextPageAsync());
        var second = presenter.Capture();
        Assert.AreEqual(3, second.PageOffset);
        Assert.IsTrue(second.HasPreviousPage);
        Assert.AreEqual("Value3", second.Values[0].DisplayText);

        await presenter.SetSelectedAsync(0, selected: false);
        Assert.IsFalse(presenter.Capture().Values[0].IsSelected);
        await presenter.SetSearchTextAsync("Value6");
        var searched = presenter.Capture();
        Assert.AreEqual(1, searched.TotalItemCount);
        Assert.AreEqual("Value6", searched.Values.Single().DisplayText);
    }

    [TestMethod]
    public async Task PresenterAppliesDirectWorksheetFilterAndInvalidatesPage()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), "Region");
        worksheet.SetValue(new CellAddress(1, 0), "North");
        worksheet.SetValue(new CellAddress(2, 0), "South");
        worksheet.SetValue(new CellAddress(3, 0), "North");
        worksheet.SetAutoFilter(new WorksheetAutoFilter(
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(3, 0))));
        var session = new SpreadsheetSession(workbook);
        session.Selection.SetActiveCell(new CellAddress(1, 0));
        Assert.IsTrue(session.TryResolveActiveAutoFilterTarget(
            out var target));
        await using var presenter = new SpreadsheetAutoFilterPagedPresenter(
            session,
            target,
            pageSize: 10);

        await presenter.InitializeAsync();
        var page = presenter.Capture();
        Assert.AreEqual(3, page.ResultRowCount);
        Assert.Contains("3 kết quả", page.AccessibilityAnnouncement);
        var southIndex = page.Values
            .Select((item, index) => (item, index))
            .Single(pair => pair.item.DisplayText == "South")
            .index;
        await presenter.SetSelectedAsync(southIndex, selected: false);
        await presenter.ApplyValueSelectionAsync();

        Assert.IsFalse(presenter.Capture().IsInitialized);
        var filtered = WorksheetSnapshot.Capture(worksheet);
        Assert.IsTrue(filtered.IsRowVisible(1));
        Assert.IsFalse(filtered.IsRowVisible(2));
        Assert.IsTrue(filtered.IsRowVisible(3));
        Assert.IsTrue(session.Undo());
        Assert.IsTrue(WorksheetSnapshot.Capture(worksheet).IsRowVisible(2));
    }

    [TestMethod]
    public async Task PresenterRejectsNavigationBeforeInitialization()
    {
        var fixture = CreateTableFixture(3);
        Assert.IsTrue(fixture.Session.TryResolveActiveAutoFilterTarget(
            out var target));
        await using var presenter = new SpreadsheetAutoFilterPagedPresenter(
            fixture.Session,
            target,
            pageSize: 2);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await presenter.MoveNextPageAsync());
    }

    [TestMethod]
    public async Task RapidSelectionChangesCompleteBeforeQueuedApply()
    {
        var fixture = CreateTableFixture(5);
        Assert.IsTrue(fixture.Session.TryResolveActiveAutoFilterTarget(
            out var target));
        await using var presenter = new SpreadsheetAutoFilterPagedPresenter(
            fixture.Session,
            target,
            pageSize: 5);
        await presenter.InitializeAsync();

        var firstToggle = presenter.SetSelectedAsync(0, selected: false);
        var secondToggle = presenter.SetSelectedAsync(1, selected: false);
        var apply = presenter.ApplyValueSelectionAsync();
        await Task.WhenAll(firstToggle, secondToggle, apply);

        var values = fixture.Session.ActiveWorksheet.Tables.Single()
            .AutoFilter!.Columns.Single().Values;
        CollectionAssert.DoesNotContain(
            values.ToArray(),
            CellValue.FromText("Value0"));
        CollectionAssert.DoesNotContain(
            values.ToArray(),
            CellValue.FromText("Value1"));
        Assert.AreEqual(3, values.Count);
        Assert.AreEqual(1, fixture.Session.History.UndoCount);
    }

    [TestMethod]
    public async Task PresenterSortsReappliesAndAnnouncesHeaderStateAndResultCount()
    {
        var fixture = CreateTableFixture(5);
        fixture.Session.WorksheetFilter.Clear();
        Assert.IsTrue(fixture.Session.TryResolveActiveAutoFilterTarget(out var target));
        fixture.Session.Structure.InsertColumns(0);
        await using var presenter = new SpreadsheetAutoFilterPagedPresenter(
            fixture.Session,
            target,
            pageSize: 5);

        await presenter.InitializeAsync();
        Assert.AreEqual(5, presenter.Capture().ResultRowCount);
        Assert.IsTrue(await presenter.ApplyColumnSortAsync(descending: true));
        Assert.AreEqual("Value4", fixture.Session.ActiveWorksheet
            .GetCell(new CellAddress(1, 1)).Value.RawValue);
        Assert.IsTrue(await presenter.ClearSortAsync());
        Assert.IsFalse(await presenter.ClearSortAsync());

        Assert.IsTrue(fixture.Session.TryResolveActiveAutoFilterTarget(out var sortedTarget));
        Assert.AreEqual(SpreadsheetFilterHeaderState.None, sortedTarget.HeaderState);
        Assert.Contains("5 kết quả", presenter.Capture().AccessibilityAnnouncement);
    }

    [TestMethod]
    public async Task ConcurrentSortAndClearAreSerializedWithoutPartialMetadata()
    {
        var fixture = CreateTableFixture(250);
        Assert.IsTrue(fixture.Session.TryResolveActiveAutoFilterTarget(out var target));
        await using var presenter = new SpreadsheetAutoFilterPagedPresenter(
            fixture.Session,
            target,
            pageSize: 25);
        await presenter.InitializeAsync();

        var sort = presenter.ApplyColumnSortAsync(descending: true);
        var clear = presenter.ClearSortAsync();
        await Task.WhenAll(sort, clear);

        Assert.IsTrue(sort.Result);
        Assert.IsTrue(clear.Result);
        Assert.AreEqual(2, fixture.Session.History.UndoCount);
        Assert.IsNull(fixture.Session.ActiveWorksheet.Tables.Single()
            .AutoFilter!.SortState);
        Assert.AreEqual("Value99", fixture.Session.ActiveWorksheet
            .GetCell(new CellAddress(1, 0)).Value.RawValue);
    }

    private static Fixture CreateTableFixture(int valueCount)
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var tableId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        worksheet.SetValue(new CellAddress(0, 0), "Value");
        for (var index = 0; index < valueCount; index++)
        {
            worksheet.SetValue(
                new CellAddress(index + 1, 0),
                $"Value{index}");
        }
        worksheet.AddTable(new SpreadsheetTable(
            tableId,
            "Values",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(valueCount, 0)),
            [new SpreadsheetTableColumn(columnId, "Value")]));
        var session = new SpreadsheetSession(workbook);
        session.Selection.SetActiveCell(new CellAddress(1, 0));
        return new Fixture(session);
    }

    private sealed record Fixture(SpreadsheetSession Session);
}
