using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetTableFilterPagingTests
{
    [TestMethod]
    public void CapturePageReturnsStableOrderedWindow()
    {
        var context = CreateContext(25);
        var page = context.Menu.CapturePage(
            offset: 10,
            pageSize: 5);

        Assert.AreEqual(25, page.TotalVisibleValueCount);
        Assert.AreEqual(10, page.Offset);
        Assert.AreEqual(5, page.Values.Count);
        Assert.IsTrue(page.HasPreviousPage);
        Assert.IsTrue(page.HasNextPage);
        CollectionAssert.AreEqual(
            ["V11", "V12", "V13", "V14", "V15"],
            page.Values.Select(static item =>
                item.DisplayText).ToArray());
    }

    [TestMethod]
    public void SearchPagingPreservesSelectionsOutsideCurrentPage()
    {
        var context = CreateContext(30);
        var original = context.Menu.CapturePage(0, 5);
        context.Menu.SetSelected(
            original.Values[0].Value,
            selected: false);
        context.Menu.SetSearchText("2");

        var searched = context.Menu.CapturePage(0, 4);
        context.Menu.SelectAllVisible();
        context.Menu.SetSearchText(string.Empty);
        var restored = context.Menu.CapturePage(0, 5);

        Assert.IsFalse(restored.Values[0].IsSelected);
        Assert.IsTrue(searched.Values.Count > 0);
    }

    [TestMethod]
    public void InvalidPageAndCancellationAreRejected()
    {
        var context = CreateContext(5);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            context.Menu.CapturePage(
                0,
                SpreadsheetTableFilterMenu.MaximumPageSize + 1));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() =>
            context.Menu.CapturePage(
                0,
                5,
                cancellation.Token));
    }

    private static TestContext CreateContext(int valueCount)
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var columnId = Guid.NewGuid();
        var table = new SpreadsheetTable(
            Guid.NewGuid(),
            "Items",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(valueCount, 0)),
            [new SpreadsheetTableColumn(columnId, "Code")]);
        worksheet.AddTable(table);
        for (var row = 1; row <= valueCount; row++)
        {
            worksheet.SetValue(
                new CellAddress(row, 0),
                $"V{row:00}");
        }
        var session = new SpreadsheetSession(workbook);
        var menu = new SpreadsheetTablePresenterController(session)
            .OpenFilterMenu(table.Id, columnId);
        return new TestContext(menu);
    }

    private sealed record TestContext(
        SpreadsheetTableFilterMenu Menu);
}
