using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class DelimitedTextAtomicExporterTests
{
    [TestMethod]
    public async Task OutputLimitFailureLeavesSeekableDestinationUnchanged()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(
            new CellAddress(0, 0),
            new string('x', 100));
        var sentinel = Encoding.UTF8.GetBytes("sentinel");
        await using var destination = new MemoryStream();
        await destination.WriteAsync(sentinel);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
            await DelimitedTextAtomicExporter.SaveAsync(
                worksheet,
                destination,
                maximumOutputBytes: 16));

        CollectionAssert.AreEqual(sentinel, destination.ToArray());
    }

    [TestMethod]
    public async Task PreCanceledExportLeavesDestinationUnchanged()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), "new content");
        var sentinel = Encoding.UTF8.GetBytes("old content");
        await using var destination = new MemoryStream();
        await destination.WriteAsync(sentinel);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await DelimitedTextAtomicExporter.SaveAsync(
                worksheet,
                destination,
                cancellationToken: cancellation.Token));

        CollectionAssert.AreEqual(sentinel, destination.ToArray());
    }

    [TestMethod]
    public async Task SuccessfulExportReplacesDestinationAfterStaging()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), "new content");
        await using var destination = new MemoryStream(
            Encoding.UTF8.GetBytes("old content"),
            writable: true);

        await DelimitedTextAtomicExporter.SaveAsync(
            worksheet,
            destination);

        Assert.AreEqual(
            "new content",
            Encoding.UTF8.GetString(destination.ToArray()));
    }
}
