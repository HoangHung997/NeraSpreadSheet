using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetTableFilterNavigationTests
{
    [TestMethod]
    public void NavigatorMovesClampsAndPagesWithoutWrapping()
    {
        using var navigator = new SpreadsheetTableFilterNavigator(
            CreateMenu());

        AssertNavigation(navigator, 0, "Closed");
        Assert.IsFalse(navigator.Handle(
            SpreadsheetTableFilterNavigationCommand.MovePrevious));
        AssertNavigation(navigator, 0, "Closed");

        Assert.IsTrue(navigator.Handle(
            SpreadsheetTableFilterNavigationCommand.MoveNext));
        AssertNavigation(navigator, 1, "Open");

        Assert.IsTrue(navigator.Handle(
            SpreadsheetTableFilterNavigationCommand.MoveLast));
        AssertNavigation(navigator, 2, "Pending");
        Assert.IsFalse(navigator.Handle(
            SpreadsheetTableFilterNavigationCommand.MoveNext));

        Assert.IsTrue(navigator.Handle(
            SpreadsheetTableFilterNavigationCommand.PagePrevious,
            pageSize: 2));
        AssertNavigation(navigator, 0, "Closed");
        Assert.IsTrue(navigator.Handle(
            SpreadsheetTableFilterNavigationCommand.PageNext,
            pageSize: 2));
        AssertNavigation(navigator, 2, "Pending");
        Assert.IsTrue(navigator.Handle(
            SpreadsheetTableFilterNavigationCommand.MoveFirst));
        AssertNavigation(navigator, 0, "Closed");
    }

    [TestMethod]
    public void ToggleAndBulkSelectionMutateAuthoritativeMenuState()
    {
        var menu = CreateMenu();
        using var navigator = new SpreadsheetTableFilterNavigator(menu);

        Assert.IsTrue(navigator.Handle(
            SpreadsheetTableFilterNavigationCommand.ToggleCurrent));
        Assert.IsFalse(menu.Capture().Values[0].IsSelected);
        Assert.IsTrue(menu.Capture().CanApplyValueSelection);

        Assert.IsTrue(navigator.Handle(
            SpreadsheetTableFilterNavigationCommand.ClearVisibleSelection));
        Assert.IsTrue(menu.Capture().AreNoVisibleValuesSelected);
        Assert.IsFalse(menu.Capture().CanApplyValueSelection);
        Assert.IsFalse(navigator.Handle(
            SpreadsheetTableFilterNavigationCommand.ClearVisibleSelection));

        Assert.IsTrue(navigator.Handle(
            SpreadsheetTableFilterNavigationCommand.SelectAllVisible));
        Assert.IsTrue(menu.Capture().AreAllVisibleValuesSelected);
        Assert.IsTrue(menu.Capture().CanApplyValueSelection);
        Assert.IsFalse(navigator.Handle(
            SpreadsheetTableFilterNavigationCommand.SelectAllVisible));
    }

    [TestMethod]
    public void SearchPreservesActiveIdentityAndHandlesEmptyResults()
    {
        var menu = CreateMenu();
        using var navigator = new SpreadsheetTableFilterNavigator(menu);
        navigator.Handle(SpreadsheetTableFilterNavigationCommand.MoveNext);
        AssertNavigation(navigator, 1, "Open");

        menu.SetSearchText("Open");
        AssertNavigation(navigator, 0, "Open");

        menu.SetSearchText("not-present");
        var empty = navigator.Capture();
        Assert.AreEqual(-1, empty.ActiveIndex);
        Assert.AreEqual(0, empty.ItemCount);
        Assert.IsFalse(empty.HasActiveItem);

        menu.SetSearchText(string.Empty);
        AssertNavigation(navigator, 0, "Closed");
    }

    [TestMethod]
    public void SetActiveValueRejectsValuesOutsideVisibleMenu()
    {
        using var navigator = new SpreadsheetTableFilterNavigator(
            CreateMenu());

        Assert.IsFalse(navigator.SetActiveValue(
            CellValue.FromText("Missing")));
        AssertNavigation(navigator, 0, "Closed");
        Assert.IsTrue(navigator.SetActiveValue(
            CellValue.FromText("Pending")));
        AssertNavigation(navigator, 2, "Pending");
    }

    [TestMethod]
    public void PageCommandsRejectNonPositivePageSize()
    {
        using var navigator = new SpreadsheetTableFilterNavigator(
            CreateMenu());

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            navigator.Handle(
                SpreadsheetTableFilterNavigationCommand.PageNext,
                pageSize: 0));
    }

    private static SpreadsheetTableFilterMenu CreateMenu()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var columnId = Guid.NewGuid();
        var table = new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(4, 0)),
            [new SpreadsheetTableColumn(columnId, "Status")]);
        worksheet.AddTable(table);
        worksheet.SetValue(new CellAddress(1, 0), "Open");
        worksheet.SetValue(new CellAddress(2, 0), "Closed");
        worksheet.SetValue(new CellAddress(3, 0), "Pending");
        worksheet.SetValue(new CellAddress(4, 0), "Open");
        return new SpreadsheetTablePresenterController(
                new SpreadsheetSession(workbook))
            .OpenFilterMenu(table.Id, columnId);
    }

    private static void AssertNavigation(
        SpreadsheetTableFilterNavigator navigator,
        int expectedIndex,
        string expectedText)
    {
        var snapshot = navigator.Capture();
        Assert.AreEqual(expectedIndex, snapshot.ActiveIndex);
        Assert.AreEqual(expectedText, snapshot.ActiveItem?.DisplayText);
        Assert.IsTrue(snapshot.HasActiveItem);
    }
}
