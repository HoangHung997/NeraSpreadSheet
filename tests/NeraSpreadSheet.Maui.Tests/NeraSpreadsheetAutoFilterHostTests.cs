using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Maui;

namespace NeraSpreadSheet.Maui.Tests;

[TestClass]
public sealed class NeraSpreadsheetAutoFilterHostTests
{
    [TestMethod]
    public void HostOwnsOneSpreadsheetAndStartsWithTheSheetClosed()
    {
        using var host = new NeraSpreadsheetAutoFilterHost();

        Assert.IsNotNull(host.Spreadsheet);
        Assert.IsFalse(host.IsFilterSheetOpen);
        Assert.AreEqual(
            "NeraAutoFilterSpreadsheet",
            host.Spreadsheet.AutomationId);
    }

    [TestMethod]
    public void WorkbookBindingFlowsToTheSharedSpreadsheetView()
    {
        var workbook = new Workbook();
        using var host = new NeraSpreadsheetAutoFilterHost
        {
            Workbook = workbook,
        };

        Assert.AreSame(workbook, host.Workbook);
        Assert.AreSame(workbook, host.Spreadsheet.Workbook);
        Assert.IsNotNull(host.Spreadsheet.Session);
    }
}
