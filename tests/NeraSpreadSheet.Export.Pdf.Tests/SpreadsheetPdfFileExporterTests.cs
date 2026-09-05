using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Rendering.Skia;

namespace NeraSpreadSheet.Export.Pdf.Tests;

[TestClass]
public sealed class SpreadsheetPdfFileExporterTests
{
    [TestMethod]
    public async Task SuccessfulExportAtomicallyReplacesExistingFile()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "estimate.pdf");
            await File.WriteAllTextAsync(path, "existing");
            var workbook = CreateWorkbook();

            var result = await SpreadsheetPdfFileExporter.SaveAsync(
                workbook.Worksheets[0],
                path,
                styles: workbook.Styles);

            var bytes = await File.ReadAllBytesAsync(path);
            Assert.AreEqual(
                "%PDF-",
                Encoding.ASCII.GetString(bytes, 0, 5));
            Assert.AreEqual(bytes.LongLength, result.OutputLength);
            Assert.AreEqual(0, Directory.GetFiles(directory, "*.tmp").Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task GenerationFailurePreservesExistingFile()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "estimate.pdf");
            const string sentinel = "existing";
            await File.WriteAllTextAsync(path, sentinel);
            var worksheet = new Workbook().Worksheets[0];

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
                await SpreadsheetPdfFileExporter.SaveAsync(
                    worksheet,
                    path));

            Assert.AreEqual(sentinel, await File.ReadAllTextAsync(path));
            Assert.AreEqual(0, Directory.GetFiles(directory, "*.tmp").Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task PageLimitFailurePreservesExistingFile()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "estimate.pdf");
            const string sentinel = "existing";
            await File.WriteAllTextAsync(path, sentinel);
            var workbook = new Workbook();
            var worksheet = workbook.Worksheets[0];
            for (var column = 0; column < 8; column++)
            {
                worksheet.Dimensions.SetColumnWidth(column, 100d);
                worksheet.SetValue(
                    new CellAddress(0, column),
                    $"C{column}");
            }

            await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
                await SpreadsheetPdfFileExporter.SaveAsync(
                    worksheet,
                    path,
                    new SpreadsheetPdfExportOptions
                    {
                        PageSetup = new SpreadsheetPageSetup
                        {
                            PaperSize = new SpreadsheetPaperSize(2d, 2d),
                            Margins = new SpreadsheetPageMargins(
                                0d,
                                0d,
                                0d,
                                0d),
                        },
                        PdfOptions = new SkiaPdfExportOptions
                        {
                            MaximumPages = 1,
                        },
                    },
                    workbook.Styles));

            Assert.AreEqual(sentinel, await File.ReadAllTextAsync(path));
            Assert.AreEqual(0, Directory.GetFiles(directory, "*.tmp").Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task PreCanceledExportDoesNotCreateDestination()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "estimate.pdf");
            var workbook = CreateWorkbook();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
                await SpreadsheetPdfFileExporter.SaveAsync(
                    workbook.Worksheets[0],
                    path,
                    styles: workbook.Styles,
                    cancellationToken: cancellation.Token));

            Assert.IsFalse(File.Exists(path));
            Assert.AreEqual(0, Directory.GetFiles(directory, "*.tmp").Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static Workbook CreateWorkbook()
    {
        var workbook = new Workbook();
        workbook.Worksheets[0].SetValue(
            new CellAddress(0, 0),
            "PDF");
        return workbook;
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"NeraSpreadSheet.PdfTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
