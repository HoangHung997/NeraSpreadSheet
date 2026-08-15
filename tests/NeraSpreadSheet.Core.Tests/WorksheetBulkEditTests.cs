using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class WorksheetBulkEditTests
{
    [TestMethod]
    public void SetCellsRaisesSingleBoundingRangeNotification()
    {
        var sheet = new Workbook().Worksheets[0];
        CellsChangedEventArgs? change = null;
        var eventCount = 0;
        sheet.CellsChanged += (_, args) => { eventCount++; change = args; };

        sheet.SetCells([
            new KeyValuePair<CellAddress, CellData>(new CellAddress(1, 2), new CellData(CellValue.FromText("A"))),
            new KeyValuePair<CellAddress, CellData>(new CellAddress(4, 6), new CellData(CellValue.FromText("B"))),
        ]);

        Assert.AreEqual(1, eventCount);
        Assert.IsNotNull(change);
        Assert.AreEqual(new CellRange(new CellAddress(1, 2), new CellAddress(4, 6)), change.Range);
        Assert.AreEqual(1L, sheet.Version);
    }
}
