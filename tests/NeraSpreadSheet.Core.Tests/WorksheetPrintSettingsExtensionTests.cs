using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class WorksheetPrintSettingsExtensionTests
{
    [TestMethod]
    public void WorksheetStoresDetachedPrintSettings()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var breaks = new List<int> { 10 };
        worksheet.SetPrintSettings(new WorksheetPrintSettings
        {
            PrintArea = new CellRange(
                new CellAddress(0, 0),
                new CellAddress(100, 5)),
            PageSetup = new SpreadsheetPageSetup
            {
                ManualRowBreaks = breaks,
                FitToPagesWide = 1,
            },
        });
        breaks[0] = 20;

        var first = worksheet.GetPrintSettings();
        Assert.AreEqual(10, first.PageSetup.ManualRowBreaks.Single());
        first.PageSetup.ManualRowBreaks
            .ToList()[0] = 30;
        var second = worksheet.GetPrintSettings();
        Assert.AreEqual(10, second.PageSetup.ManualRowBreaks.Single());
    }

    [TestMethod]
    public void ResetReturnsDefaultSettings()
    {
        var worksheet = new Workbook().Worksheets[0];
        worksheet.SetPrintSettings(new WorksheetPrintSettings
        {
            PageSetup = new SpreadsheetPageSetup
            {
                Orientation = SpreadsheetPageOrientation.Landscape,
            },
        });

        worksheet.ResetPrintSettings();
        var settings = worksheet.GetPrintSettings();

        Assert.AreEqual(
            SpreadsheetPageOrientation.Portrait,
            settings.PageSetup.Orientation);
        Assert.IsNull(settings.PrintArea);
    }
}
