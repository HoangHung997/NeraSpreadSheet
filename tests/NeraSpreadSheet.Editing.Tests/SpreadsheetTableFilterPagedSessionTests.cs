using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetTableFilterPagedSessionTests
{
    private static readonly string[] ExpectedSearchValues =
        ["Open", "Pending"];

    [TestMethod]
    public async Task RefreshPublishesGenerationAndSearchablePages()
    {
        var fixture = CreateFixture();
        await using var session = new SpreadsheetTableFilterPagedSession(
            fixture.Session,
            fixture.TableId,
            fixture.ColumnId);

        var generation = await session.RefreshAsync();
        var result = await session.GetPageAsync(
            "pen",
            offset: 0,
            pageSize: 10);

        Assert.AreEqual(1L, generation);
        Assert.AreEqual(generation, result.Generation);
        Assert.IsTrue(session.IsReady);
        Assert.AreEqual(2, result.Page.TotalVisibleValueCount);
        CollectionAssert.AreEqual(
            ExpectedSearchValues,
            result.Page.Values
                .Select(static value => value.DisplayText)
                .ToArray());
    }

    [TestMethod]
    public async Task RefreshReplacesSnapshotAndIncrementsGeneration()
    {
        var fixture = CreateFixture();
        await using var session = new SpreadsheetTableFilterPagedSession(
            fixture.Session,
            fixture.TableId,
            fixture.ColumnId);

        var firstGeneration = await session.RefreshAsync();
        var first = await session.GetPageAsync(
            null,
            offset: 0,
            pageSize: 10);
        fixture.Worksheet.SetValue(
            new CellAddress(1, 0),
            "Reopened");
        var unchanged = await session.GetPageAsync(
            null,
            offset: 0,
            pageSize: 10);
        var secondGeneration = await session.RefreshAsync();
        var second = await session.GetPageAsync(
            null,
            offset: 0,
            pageSize: 10);

        Assert.AreEqual(firstGeneration, first.Generation);
        Assert.AreEqual(firstGeneration, unchanged.Generation);
        Assert.IsTrue(unchanged.Page.Values.Any(value =>
            value.DisplayText == "Open"));
        Assert.IsFalse(unchanged.Page.Values.Any(value =>
            value.DisplayText == "Reopened"));
        Assert.AreEqual(firstGeneration + 1L, secondGeneration);
        Assert.AreEqual(secondGeneration, second.Generation);
        Assert.IsTrue(second.Page.Values.Any(value =>
            value.DisplayText == "Reopened"));
    }

    [TestMethod]
    public async Task SelectionAndApplyUseGenerationAndProductionHistory()
    {
        var fixture = CreateFixture();
        await using var paged = new SpreadsheetTableFilterPagedSession(
            fixture.Session,
            fixture.TableId,
            fixture.ColumnId);
        var generation = await paged.RefreshAsync();

        await paged.ClearVisibleSelectionAsync(
            generation,
            searchText: null);
        await paged.SetSelectedAsync(
            generation,
            CellValue.FromText("Open"),
            selected: true);
        var invalidatedGeneration =
            await paged.ApplyValueSelectionAsync(generation);

        Assert.AreEqual(generation + 1L, invalidatedGeneration);
        Assert.IsFalse(paged.IsReady);
        Assert.AreEqual(1, fixture.Session.History.UndoCount);
        var filter = fixture.Worksheet.Tables.Single().AutoFilter
            ?? throw new AssertFailedException(
                "Table filter was not applied.");
        Assert.AreEqual(
            "Open",
            filter.Columns.Single().Values.Single().RawValue);
        Assert.IsFalse(WorksheetSnapshot.Capture(fixture.Worksheet)
            .IsRowVisible(2));
        Assert.IsTrue(fixture.Session.Undo());
        Assert.IsTrue(WorksheetSnapshot.Capture(fixture.Worksheet)
            .IsRowVisible(2));
    }

    [TestMethod]
    public async Task StaleGenerationCannotChangeSelectionOrWorkbook()
    {
        var fixture = CreateFixture();
        await using var paged = new SpreadsheetTableFilterPagedSession(
            fixture.Session,
            fixture.TableId,
            fixture.ColumnId);
        var firstGeneration = await paged.RefreshAsync();
        var secondGeneration = await paged.RefreshAsync();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await paged.SetSelectedAsync(
                firstGeneration,
                CellValue.FromText("Open"),
                selected: false));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await paged.ApplyValueSelectionAsync(
                firstGeneration));

        Assert.AreEqual(firstGeneration + 1L, secondGeneration);
        Assert.IsNull(fixture.Worksheet.Tables.Single().AutoFilter);
        Assert.AreEqual(0, fixture.Session.History.UndoCount);
    }

    [TestMethod]
    public async Task PageRequestHonorsCancellation()
    {
        var fixture = CreateFixture();
        await using var session = new SpreadsheetTableFilterPagedSession(
            fixture.Session,
            fixture.TableId,
            fixture.ColumnId);
        await session.RefreshAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () =>
            await session.GetPageAsync(
                null,
                0,
                10,
                cancellation.Token));
    }

    [TestMethod]
    public async Task DisposedSessionRejectsRefreshPagesAndMutations()
    {
        var fixture = CreateFixture();
        var session = new SpreadsheetTableFilterPagedSession(
            fixture.Session,
            fixture.TableId,
            fixture.ColumnId);
        var generation = await session.RefreshAsync();
        session.Dispose();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () =>
            await session.RefreshAsync());
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () =>
            await session.GetPageAsync(null, 0, 10));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () =>
            await session.SetSelectedAsync(
                generation,
                CellValue.FromText("Open"),
                selected: false));
    }

    private static Fixture CreateFixture()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var tableId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        worksheet.SetValue(new CellAddress(0, 0), "Status");
        worksheet.SetValue(new CellAddress(1, 0), "Open");
        worksheet.SetValue(new CellAddress(2, 0), "Closed");
        worksheet.SetValue(new CellAddress(3, 0), "Pending");
        worksheet.SetValue(new CellAddress(4, 0), "Open");
        worksheet.AddTable(new SpreadsheetTable(
            tableId,
            "Items",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(4, 0)),
            [new SpreadsheetTableColumn(columnId, "Status")]));
        return new Fixture(
            worksheet,
            new SpreadsheetSession(workbook),
            tableId,
            columnId);
    }

    private sealed record Fixture(
        Worksheet Worksheet,
        SpreadsheetSession Session,
        Guid TableId,
        Guid ColumnId);
}
