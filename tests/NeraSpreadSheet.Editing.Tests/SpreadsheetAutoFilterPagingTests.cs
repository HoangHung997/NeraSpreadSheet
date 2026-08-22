using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetAutoFilterPagingTests
{
    [TestMethod]
    public void UnifiedResolverIdentifiesTableAndWorksheetOwners()
    {
        var fixture = CreateFixture();

        Assert.IsTrue(fixture.Session.TryResolveAutoFilterTarget(
            new CellAddress(2, 0),
            out var tableTarget));
        Assert.AreEqual(
            SpreadsheetAutoFilterOwnerKind.Table,
            tableTarget.OwnerKind);
        Assert.AreEqual(fixture.TableId, tableTarget.TableId);
        Assert.AreEqual(fixture.TableStatusColumnId, tableTarget.TableColumnId);
        Assert.AreEqual("Sales", tableTarget.OwnerName);
        Assert.AreEqual("Status", tableTarget.ColumnName);

        Assert.IsTrue(fixture.Session.TryResolveAutoFilterTarget(
            new CellAddress(2, 3),
            out var worksheetTarget));
        Assert.AreEqual(
            SpreadsheetAutoFilterOwnerKind.Worksheet,
            worksheetTarget.OwnerKind);
        Assert.IsNull(worksheetTarget.TableId);
        Assert.AreEqual(new CellRange(
            new CellAddress(0, 3),
            new CellAddress(4, 4)),
            worksheetTarget.FilterRange);
        Assert.AreEqual("Region", worksheetTarget.ColumnName);

        Assert.IsFalse(fixture.Session.TryResolveAutoFilterTarget(
            new CellAddress(20, 20),
            out _));
    }

    [TestMethod]
    public async Task PagedViewLoadsSearchesAndCachesTableValues()
    {
        var fixture = CreateFixture();
        Assert.IsTrue(fixture.Session.TryResolveAutoFilterTarget(
            new CellAddress(1, 0),
            out var target));
        await using var view = new SpreadsheetAutoFilterPagedView(
            SpreadsheetAutoFilterPagedSessionFactory.Create(
                fixture.Session,
                target),
            pageSize: 2);

        await view.InitializeAsync();
        var initial = view.Capture();
        Assert.IsTrue(initial.IsInitialized);
        Assert.AreEqual(3, initial.TotalItemCount);
        Assert.AreEqual(2, initial.LoadedItemCount);
        Assert.IsTrue(initial.HasMoreItems);

        var next = await view.LoadNextPageAsync();
        Assert.IsNotNull(next);
        var loaded = view.Capture();
        Assert.AreEqual(3, loaded.LoadedItemCount);
        Assert.IsFalse(loaded.HasMoreItems);
        Assert.AreEqual("Pending", (await view.GetItemAsync(2)).DisplayText);

        await view.SetSearchTextAsync("pen");
        var searched = view.Capture();
        Assert.AreEqual("pen", searched.SearchText);
        Assert.AreEqual(2, searched.TotalItemCount);
        CollectionAssert.AreEqual(
            new[] { "Open", "Pending" },
            searched.LoadedItems
                .Select(static item => item.DisplayText)
                .ToArray());
    }

    [TestMethod]
    public async Task WorksheetViewAppliesSelectionThroughProductionHistory()
    {
        var fixture = CreateFixture();
        Assert.IsTrue(fixture.Session.TryResolveAutoFilterTarget(
            new CellAddress(1, 3),
            out var target));
        await using var view = new SpreadsheetAutoFilterPagedView(
            SpreadsheetAutoFilterPagedSessionFactory.Create(
                fixture.Session,
                target),
            pageSize: 10);

        await view.InitializeAsync();
        var snapshot = view.Capture();
        var southIndex = snapshot.LoadedItems
            .Select((item, index) => (item, index))
            .Single(pair => pair.item.DisplayText == "South")
            .index;
        var westIndex = snapshot.LoadedItems
            .Select((item, index) => (item, index))
            .Single(pair => pair.item.DisplayText == "West")
            .index;
        await view.SetSelectedAsync(southIndex, selected: false);
        await view.SetSelectedAsync(westIndex, selected: false);
        await view.ApplyValueSelectionAsync();

        var filtered = WorksheetSnapshot.Capture(fixture.Worksheet);
        Assert.IsTrue(filtered.IsRowVisible(1));
        Assert.IsFalse(filtered.IsRowVisible(2));
        Assert.IsTrue(filtered.IsRowVisible(3));
        Assert.IsFalse(filtered.IsRowVisible(4));
        Assert.IsFalse(view.Capture().IsInitialized);
        Assert.IsTrue(fixture.Session.Undo());

        var restored = WorksheetSnapshot.Capture(fixture.Worksheet);
        Assert.IsTrue(restored.IsRowVisible(1));
        Assert.IsTrue(restored.IsRowVisible(2));
        Assert.IsTrue(restored.IsRowVisible(3));
        Assert.IsTrue(restored.IsRowVisible(4));
    }

    [TestMethod]
    public async Task RandomAccessLoadsOnlyTheRequestedPage()
    {
        var fixture = CreateLargeFixture();
        Assert.IsTrue(fixture.Session.TryResolveAutoFilterTarget(
            new CellAddress(1, 0),
            out var target));
        await using var view = new SpreadsheetAutoFilterPagedView(
            SpreadsheetAutoFilterPagedSessionFactory.Create(
                fixture.Session,
                target),
            pageSize: 25);

        await view.InitializeAsync();
        var item = await view.GetItemAsync(73);
        Assert.AreEqual("Value073", item.DisplayText);
        Assert.IsTrue(view.TryGetLoadedItem(73, out var cached));
        Assert.AreEqual(item, cached);
        Assert.IsFalse(view.TryGetLoadedItem(40, out _));
    }

    [TestMethod]
    public async Task ViewRejectsUsageAfterDisposal()
    {
        var fixture = CreateFixture();
        Assert.IsTrue(fixture.Session.TryResolveAutoFilterTarget(
            new CellAddress(1, 0),
            out var target));
        var view = new SpreadsheetAutoFilterPagedView(
            SpreadsheetAutoFilterPagedSessionFactory.Create(
                fixture.Session,
                target));
        await view.InitializeAsync();
        view.Dispose();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () =>
            await view.RefreshAsync());
    }

    private static Fixture CreateFixture()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var tableId = Guid.NewGuid();
        var statusColumnId = Guid.NewGuid();
        var amountColumnId = Guid.NewGuid();

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
        worksheet.AddTable(new SpreadsheetTable(
            tableId,
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(4, 1)),
            [
                new SpreadsheetTableColumn(statusColumnId, "Status"),
                new SpreadsheetTableColumn(amountColumnId, "Amount"),
            ]));

        worksheet.SetValue(new CellAddress(0, 3), "Region");
        worksheet.SetValue(new CellAddress(0, 4), "Owner");
        worksheet.SetValue(new CellAddress(1, 3), "North");
        worksheet.SetValue(new CellAddress(1, 4), "A");
        worksheet.SetValue(new CellAddress(2, 3), "South");
        worksheet.SetValue(new CellAddress(2, 4), "B");
        worksheet.SetValue(new CellAddress(3, 3), "North");
        worksheet.SetValue(new CellAddress(3, 4), "C");
        worksheet.SetValue(new CellAddress(4, 3), "West");
        worksheet.SetValue(new CellAddress(4, 4), "D");
        worksheet.SetAutoFilter(new WorksheetAutoFilter(
            new CellRange(
                new CellAddress(0, 3),
                new CellAddress(4, 4))));

        return new Fixture(
            worksheet,
            new SpreadsheetSession(workbook),
            tableId,
            statusColumnId);
    }

    private static Fixture CreateLargeFixture()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var tableId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        worksheet.SetValue(new CellAddress(0, 0), "Value");
        for (var index = 0; index < 100; index++)
        {
            worksheet.SetValue(
                new CellAddress(index + 1, 0),
                $"Value{index:000}");
        }
        worksheet.AddTable(new SpreadsheetTable(
            tableId,
            "Values",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(100, 0)),
            [new SpreadsheetTableColumn(columnId, "Value")]));
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
        Guid TableStatusColumnId);
}
