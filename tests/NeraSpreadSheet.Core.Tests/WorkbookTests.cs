using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class WorkbookTests
{
    [TestMethod]
    public void Constructor_Should_CreateDefaultWorksheet_When_DefaultOptionIsUsed()
    {
        var workbook = new Workbook();

        Assert.AreEqual(1, workbook.Worksheets.Count);
        Assert.AreEqual("Sheet1", workbook.Worksheets[0].Name);
    }

    [TestMethod]
    public void AddWorksheet_Should_RejectDuplicateName_When_CasingDiffers()
    {
        var workbook = new Workbook();

        Assert.ThrowsExactly<InvalidOperationException>(() => workbook.AddWorksheet("sheet1"));
    }

    [TestMethod]
    public void Worksheet_Should_RemainSparse_When_EmptyCellIsCleared()
    {
        var worksheet = new Workbook().Worksheets[0];
        var address = CellAddress.ParseA1("D51");

        worksheet.SetValue(address, 42d);
        worksheet.Clear(address);

        Assert.AreEqual(0, worksheet.UsedCellCount);
        Assert.IsTrue(worksheet.GetCell(address).IsEmpty);
    }
}
