using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class MergedCellRoundTripTests
{
    [TestMethod]
    public async Task SaveAndLoadRoundTripsMergedRanges()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(default, "Merged");
        var mergedRange = new CellRange(default, new CellAddress(2, 3));
        sheet.MergeCells(mergedRange);
        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var stream = new MemoryStream();

        await serializer.SaveAsync(workbook, stream, new OpenXmlExportOptions());
        stream.Position = 0;
        var loaded = await serializer.LoadAsync(stream, new OpenXmlImportOptions());

        Assert.AreEqual(1, loaded.Worksheets[0].MergedCells.Count);
        Assert.IsTrue(loaded.Worksheets[0].MergedCells.TryGetContaining(new CellAddress(2, 3), out var loadedRange));
        Assert.AreEqual(mergedRange, loadedRange);
        Assert.AreEqual("Merged", loaded.Worksheets[0].GetCell(default).Value.RawValue);
    }
}
