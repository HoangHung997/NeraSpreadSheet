using Microsoft.Maui.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Maui.Tests;

[TestClass]
public sealed class NeraSpreadsheetAutoFilterHostTests
{
    [TestMethod]
    public void HostTypeExposesTheSharedSpreadsheetContract()
    {
        var type = typeof(NeraSpreadsheetAutoFilterHost);

        Assert.IsTrue(typeof(Grid).IsAssignableFrom(type));
        Assert.IsNotNull(type.GetProperty(nameof(NeraSpreadsheetAutoFilterHost.Workbook)));
        Assert.IsNotNull(type.GetProperty(nameof(NeraSpreadsheetAutoFilterHost.Spreadsheet)));
        Assert.IsNotNull(type.GetProperty(nameof(NeraSpreadsheetAutoFilterHost.IsFilterSheetOpen)));
        Assert.IsNotNull(type.GetMethod(nameof(NeraSpreadsheetAutoFilterHost.TryOpenForActiveCell)));
        Assert.IsNotNull(type.GetMethod(nameof(NeraSpreadsheetAutoFilterHost.TryOpenFilter)));
        Assert.IsNotNull(type.GetMethod(nameof(NeraSpreadsheetAutoFilterHost.CloseFilterSheet)));
    }

    [TestMethod]
    public void FilterTargetsResolveWithoutConstructingANativeVisualTree()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var tableId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        worksheet.SetValue(new CellAddress(0, 0), "Status");
        worksheet.SetValue(new CellAddress(1, 0), "Open");
        worksheet.AddTable(new SpreadsheetTable(
            tableId,
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(1, 0)),
            [new SpreadsheetTableColumn(columnId, "Status")]));
        worksheet.SetValue(new CellAddress(0, 2), "Region");
        worksheet.SetValue(new CellAddress(1, 2), "North");
        worksheet.SetAutoFilter(new WorksheetAutoFilter(
            new CellRange(
                new CellAddress(0, 2),
                new CellAddress(1, 2))));
        var session = new SpreadsheetSession(workbook);

        Assert.IsTrue(session.TryResolveAutoFilterTarget(
            new CellAddress(1, 0),
            out var tableTarget));
        Assert.AreEqual(
            SpreadsheetAutoFilterOwnerKind.Table,
            tableTarget.OwnerKind);
        Assert.IsTrue(session.TryResolveAutoFilterTarget(
            new CellAddress(1, 2),
            out var worksheetTarget));
        Assert.AreEqual(
            SpreadsheetAutoFilterOwnerKind.Worksheet,
            worksheetTarget.OwnerKind);
    }
}
