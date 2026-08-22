using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class DelimitedTextStreamingBoundaryTests
{
    [TestMethod]
    public async Task EscapedQuotePairMayCrossParserBufferBoundary()
    {
        var expected = new string('a', 8190) + '"';
        var csv = '"' + new string('a', 8190) + "\"\"\"";
        await using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(csv));

        var workbook = await DelimitedTextWorkbookSerializer.LoadAsync(stream);

        Assert.AreEqual(
            expected,
            workbook.Worksheets[0].GetValue(new CellAddress(0, 0)));
    }

    [TestMethod]
    public async Task FinalCarriageReturnDoesNotCreateAnExtraRow()
    {
        await using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes("a\rb\r"));

        var workbook = await DelimitedTextWorkbookSerializer.LoadAsync(stream);
        var worksheet = workbook.Worksheets[0];

        Assert.AreEqual("a", worksheet.GetValue(new CellAddress(0, 0)));
        Assert.AreEqual("b", worksheet.GetValue(new CellAddress(1, 0)));
        Assert.IsNull(worksheet.GetValue(new CellAddress(2, 0)));
        Assert.AreEqual(2, worksheet.UsedCellCount);
    }

    [TestMethod]
    public async Task WhitespaceAfterClosingQuoteIsAcceptedButTextIsRejected()
    {
        await using var accepted = new MemoryStream(
            Encoding.UTF8.GetBytes("\"value\"  ,next"));
        var workbook = await DelimitedTextWorkbookSerializer.LoadAsync(accepted);
        Assert.AreEqual(
            "value",
            workbook.Worksheets[0].GetValue(new CellAddress(0, 0)));

        await using var rejected = new MemoryStream(
            Encoding.UTF8.GetBytes("\"value\"oops,next"));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
            await DelimitedTextWorkbookSerializer.LoadAsync(rejected));
    }
}
