using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Rendering.Skia;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Export.Pdf.Tests;

[TestClass]
public sealed class SpreadsheetPdfExporterTests
{
    [TestMethod]
    public async Task StoredWorksheetSettingsDrivePdfLayout()
    {
        var workbook = CreateWorkbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetPrintSettings(new WorksheetPrintSettings
        {
            PrintArea = new CellRange(
                new CellAddress(0, 0),
                new CellAddress(9, 3)),
            PageSetup = new SpreadsheetPageSetup
            {
                PaperSize = SpreadsheetPaperSize.A4,
                Orientation = SpreadsheetPageOrientation.Landscape,
                Margins = SpreadsheetPageMargins.Narrow,
                FitToPagesWide = 1,
                OddHeader = "&A &P/&N",
            },
        });
        await using var destination = new MemoryStream();

        var result = await SpreadsheetPdfExporter.SaveAsync(
            worksheet,
            destination,
            new SpreadsheetPdfExportOptions
            {
                DisplayListOptions = new SpreadsheetPrintDisplayListOptions
                {
                    WorkbookName = "Estimate.xlsx",
                    Timestamp = new DateTime(2026, 8, 22, 12, 0, 0),
                },
            },
            workbook.Styles);

        Assert.AreEqual(
            SpreadsheetPageOrientation.Landscape,
            result.PageLayout.Setup.Orientation);
        Assert.AreEqual(1, result.PageLayout.HorizontalPageCount);
        Assert.AreEqual(result.PageCount, result.PageLayout.Pages.Count);
        Assert.AreEqual(destination.Length, result.OutputLength);
        AssertPdf(destination);
    }

    [TestMethod]
    public async Task ExplicitOptionsOverrideWithoutMutatingWorksheetSettings()
    {
        var workbook = CreateWorkbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetPrintSettings(new WorksheetPrintSettings
        {
            PrintArea = new CellRange(
                new CellAddress(0, 0),
                new CellAddress(2, 1)),
            PageSetup = new SpreadsheetPageSetup
            {
                Orientation = SpreadsheetPageOrientation.Landscape,
            },
        });
        await using var destination = new MemoryStream();

        var result = await SpreadsheetPdfExporter.SaveAsync(
            worksheet,
            destination,
            new SpreadsheetPdfExportOptions
            {
                PrintArea = new CellRange(
                    new CellAddress(0, 0),
                    new CellAddress(1, 0)),
                PageSetup = new SpreadsheetPageSetup
                {
                    Orientation = SpreadsheetPageOrientation.Portrait,
                    PaperSize = SpreadsheetPaperSize.Letter,
                    Margins = SpreadsheetPageMargins.Narrow,
                },
            },
            workbook.Styles);

        Assert.AreEqual(
            SpreadsheetPageOrientation.Portrait,
            result.PageLayout.Setup.Orientation);
        Assert.AreEqual(
            SpreadsheetPaperSize.Letter,
            result.PageLayout.Setup.PaperSize);
        Assert.AreEqual(
            SpreadsheetPageOrientation.Landscape,
            worksheet.GetPrintSettings().PageSetup.Orientation);
    }

    [TestMethod]
    public async Task UsedRangeIsFallbackWhenPrintAreaIsAbsent()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(3, 2), "Only used cell");
        await using var destination = new MemoryStream();

        var result = await SpreadsheetPdfExporter.SaveAsync(
            worksheet,
            destination,
            styles: workbook.Styles);

        Assert.AreEqual(
            new CellRange(
                new CellAddress(3, 2),
                new CellAddress(3, 2)),
            result.PageLayout.PrintArea);
        AssertPdf(destination);
    }

    [TestMethod]
    public async Task EmptyWorksheetFailureLeavesDestinationUnchanged()
    {
        var worksheet = new Workbook().Worksheets[0];
        var sentinel = Encoding.UTF8.GetBytes("existing");
        await using var destination = new MemoryStream();
        await destination.WriteAsync(sentinel);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await SpreadsheetPdfExporter.SaveAsync(
                worksheet,
                destination));

        CollectionAssert.AreEqual(sentinel, destination.ToArray());
    }

    [TestMethod]
    public async Task PdfPageLimitFailureLeavesDestinationUnchanged()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        for (var column = 0; column < 8; column++)
        {
            worksheet.Dimensions.SetColumnWidth(column, 100d);
            worksheet.SetValue(
                new CellAddress(0, column),
                $"C{column}");
        }
        var sentinel = Encoding.UTF8.GetBytes("existing");
        await using var destination = new MemoryStream();
        await destination.WriteAsync(sentinel);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
            await SpreadsheetPdfExporter.SaveAsync(
                worksheet,
                destination,
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

        CollectionAssert.AreEqual(sentinel, destination.ToArray());
    }

    [TestMethod]
    public async Task PreCanceledExportLeavesDestinationUnchanged()
    {
        var workbook = CreateWorkbook();
        var sentinel = Encoding.UTF8.GetBytes("existing");
        await using var destination = new MemoryStream();
        await destination.WriteAsync(sentinel);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await SpreadsheetPdfExporter.SaveAsync(
                workbook.Worksheets[0],
                destination,
                styles: workbook.Styles,
                cancellationToken: cancellation.Token));

        CollectionAssert.AreEqual(sentinel, destination.ToArray());
    }

    private static Workbook CreateWorkbook()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        for (var row = 0; row < 10; row++)
        {
            worksheet.Dimensions.SetRowHeight(row, 20d);
            for (var column = 0; column < 4; column++)
            {
                if (row == 0)
                {
                    worksheet.Dimensions.SetColumnWidth(column, 80d);
                }
                worksheet.SetValue(
                    new CellAddress(row, column),
                    $"R{row}C{column}");
            }
        }
        return workbook;
    }

    private static void AssertPdf(MemoryStream stream)
    {
        var bytes = stream.ToArray();
        Assert.IsTrue(bytes.Length > 500);
        Assert.AreEqual(
            "%PDF-",
            Encoding.ASCII.GetString(bytes, 0, 5));
    }
}
