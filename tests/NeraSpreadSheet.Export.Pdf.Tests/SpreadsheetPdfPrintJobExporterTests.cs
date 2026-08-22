using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Rendering.Skia;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Export.Pdf.Tests;

[TestClass]
public sealed class SpreadsheetPdfPrintJobExporterTests
{
    [TestMethod]
    public async Task PageSelectionCopiesAndCollationDrivePdfSequence()
    {
        var workbook = CreateWorkbook();
        var worksheet = workbook.Worksheets[0];
        await using var destination = new MemoryStream();
        var options = new SpreadsheetPdfPrintJobOptions
        {
            WorksheetOptions = CreateExportOptions(),
            Ticket = new SpreadsheetPrintTicket
            {
                Selection = SpreadsheetPrintPageSelection.Parse(
                    "2-3",
                    totalPages: 6),
                Copies = 2,
                Collate = true,
            },
        };

        var result = await SpreadsheetPdfPrintJobExporter.SaveAsync(
            worksheet,
            destination,
            options,
            workbook.Styles);

        Assert.AreEqual(4, result.PageCount);
        CollectionAssert.AreEqual(
            new[] { 2, 3, 2, 3 },
            result.Invocations
                .Select(static invocation => invocation.PageNumber)
                .ToArray());
        CollectionAssert.AreEqual(
            new[] { 1, 1, 2, 2 },
            result.Invocations
                .Select(static invocation => invocation.CopyNumber)
                .ToArray());
        Assert.AreEqual(destination.Length, result.OutputLength);
        AssertPdf(destination);
    }

    [TestMethod]
    public async Task ReverseOddSelectionProducesExpectedInvocationOrder()
    {
        var workbook = CreateWorkbook();
        await using var destination = new MemoryStream();
        var options = new SpreadsheetPdfPrintJobOptions
        {
            WorksheetOptions = CreateExportOptions(),
            Ticket = new SpreadsheetPrintTicket
            {
                Selection = new SpreadsheetPrintPageSelection
                {
                    Parity = SpreadsheetPrintPageParity.Odd,
                    ReverseOrder = true,
                },
            },
        };

        var result = await SpreadsheetPdfPrintJobExporter.SaveAsync(
            workbook.Worksheets[0],
            destination,
            options,
            workbook.Styles);

        CollectionAssert.AreEqual(
            new[] { 5, 3, 1 },
            result.Invocations
                .Select(static invocation => invocation.PageNumber)
                .ToArray());
        AssertPdf(destination);
    }

    [TestMethod]
    public async Task PdfInvocationLimitFailureLeavesDestinationUnchanged()
    {
        var workbook = CreateWorkbook();
        var sentinel = Encoding.UTF8.GetBytes("existing");
        await using var destination = new MemoryStream();
        await destination.WriteAsync(sentinel);
        var options = new SpreadsheetPdfPrintJobOptions
        {
            WorksheetOptions = CreateExportOptions() with
            {
                PdfOptions = new SkiaPdfExportOptions
                {
                    MaximumPages = 1,
                },
            },
            Ticket = new SpreadsheetPrintTicket
            {
                Copies = 2,
            },
        };

        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
            await SpreadsheetPdfPrintJobExporter.SaveAsync(
                workbook.Worksheets[0],
                destination,
                options,
                workbook.Styles));

        CollectionAssert.AreEqual(sentinel, destination.ToArray());
    }

    [TestMethod]
    public async Task PreCanceledTicketExportLeavesDestinationUnchanged()
    {
        var workbook = CreateWorkbook();
        var sentinel = Encoding.UTF8.GetBytes("existing");
        await using var destination = new MemoryStream();
        await destination.WriteAsync(sentinel);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await SpreadsheetPdfPrintJobExporter.SaveAsync(
                workbook.Worksheets[0],
                destination,
                new SpreadsheetPdfPrintJobOptions
                {
                    WorksheetOptions = CreateExportOptions(),
                },
                workbook.Styles,
                cancellation.Token));

        CollectionAssert.AreEqual(sentinel, destination.ToArray());
    }

    private static Workbook CreateWorkbook()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        for (var column = 0; column < 6; column++)
        {
            worksheet.Dimensions.SetColumnWidth(column, 80d);
            worksheet.SetValue(new CellAddress(0, column), $"C{column}");
        }
        return workbook;
    }

    private static SpreadsheetPdfExportOptions CreateExportOptions() =>
        new()
        {
            PrintArea = new CellRange(
                new CellAddress(0, 0),
                new CellAddress(0, 5)),
            PageSetup = new SpreadsheetPageSetup
            {
                PaperSize = new SpreadsheetPaperSize(1.25d, 2d),
                Margins = new SpreadsheetPageMargins(0d, 0d, 0d, 0d),
            },
            DisplayListOptions = new SpreadsheetPrintDisplayListOptions
            {
                Timestamp = new DateTime(2026, 8, 22, 12, 0, 0),
            },
        };

    private static void AssertPdf(MemoryStream stream)
    {
        var bytes = stream.ToArray();
        Assert.IsTrue(bytes.Length > 500);
        Assert.AreEqual(
            "%PDF-",
            Encoding.ASCII.GetString(bytes, 0, 5));
    }
}
