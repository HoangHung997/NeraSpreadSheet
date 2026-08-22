using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetTableFilterPagedSessionTests
{
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

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await session.GetPageAsync(
                null,
                0,
                10,
                cancellation.Token));
    }

    [TestMethod]
    public async Task DisposedSessionRejectsRefreshAndPages()
    {
        var fixture = CreateFixture();
        var session = new SpreadsheetTableFilterPagedSession(
            fixture.Session,
            fixture.TableId,
            fixture.ColumnId);
        await session.RefreshAsync();
        session.Dispose();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () =>
            await session.RefreshAsync());
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () =>
            await session.GetPageAsync(null, 0, 10));
    }

    private static readonly string[] ExpectedSearchValues =
        ["Open", "Pending"];

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
