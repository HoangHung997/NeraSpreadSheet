using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Rendering.Skia.Tests;

[TestClass]
public sealed class SkiaDisplayListPdfExporterTests
{
    [TestMethod]
    public async Task SpreadsheetPrintDisplayListsExportAsPdf()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), "Nera PDF");
        worksheet.SetValue(new CellAddress(1, 0), 42d);
        worksheet.Dimensions.SetColumnWidth(0, 120d);
        worksheet.Dimensions.SetRowHeight(0, 24d);
        worksheet.Dimensions.SetRowHeight(1, 20d);
        var snapshot = WorksheetSnapshot.Capture(worksheet);
        var plan = SpreadsheetPageLayoutPlanner.CreatePlan(
            snapshot,
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(1, 0)),
            new SpreadsheetPageSetup
            {
                PaperSize = SpreadsheetPaperSize.A4,
                Margins = SpreadsheetPageMargins.Narrow,
                PrintGridlines = true,
                OddHeader = "&A — &P/&N",
            });
        var pages = plan.Pages.Select((page, index) =>
        {
            var composed = SpreadsheetPrintDisplayListComposer.Compose(
                snapshot,
                plan,
                index,
                workbook.Styles,
                new SpreadsheetPrintDisplayListOptions
                {
                    WorkbookName = "Nera.xlsx",
                    Timestamp = new DateTime(2026, 8, 22, 12, 0, 0),
                });
            return new SkiaPdfPage(
                page.PaperSizeDips,
                composed.DisplayList);
        });
        await using var destination = new MemoryStream();

        await SkiaDisplayListPdfExporter.SaveAsync(
            pages,
            destination);

        var bytes = destination.ToArray();
        Assert.IsTrue(bytes.Length > 500);
        Assert.AreEqual(
            "%PDF-",
            Encoding.ASCII.GetString(bytes, 0, 5));
        Assert.IsTrue(
            Encoding.ASCII.GetString(bytes)
                .TrimEnd('\0', '\r', '\n', ' ')
                .EndsWith("%%EOF", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task MultiplePagesAreEnumeratedAndWritten()
    {
        var enumerated = 0;
        IEnumerable<SkiaPdfPage> CreatePages()
        {
            for (var index = 0; index < 3; index++)
            {
                enumerated++;
                yield return CreatePage(
                    96d,
                    192d,
                    $"Page {index + 1}");
            }
        }
        await using var destination = new MemoryStream();

        await SkiaDisplayListPdfExporter.SaveAsync(
            CreatePages(),
            destination);

        Assert.AreEqual(3, enumerated);
        Assert.IsTrue(destination.Length > 500L);
    }

    [TestMethod]
    public async Task PageLimitFailureLeavesSeekableDestinationUnchanged()
    {
        var sentinel = Encoding.UTF8.GetBytes("existing-pdf");
        await using var destination = new MemoryStream();
        await destination.WriteAsync(sentinel);
        var pages = new[]
        {
            CreatePage(96d, 96d, "One"),
            CreatePage(96d, 96d, "Two"),
        };

        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
            await SkiaDisplayListPdfExporter.SaveAsync(
                pages,
                destination,
                new SkiaPdfExportOptions
                {
                    MaximumPages = 1,
                }));

        CollectionAssert.AreEqual(sentinel, destination.ToArray());
    }

    [TestMethod]
    public async Task OutputLimitFailureLeavesDestinationUnchanged()
    {
        var sentinel = Encoding.UTF8.GetBytes("existing-pdf");
        await using var destination = new MemoryStream();
        await destination.WriteAsync(sentinel);
        var pages = new[]
        {
            CreatePage(800d, 1000d, new string('X', 5000)),
        };

        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
            await SkiaDisplayListPdfExporter.SaveAsync(
                pages,
                destination,
                new SkiaPdfExportOptions
                {
                    MaximumOutputBytes = 128L,
                }));

        CollectionAssert.AreEqual(sentinel, destination.ToArray());
    }

    [TestMethod]
    public async Task PreCanceledExportLeavesDestinationUnchanged()
    {
        var sentinel = Encoding.UTF8.GetBytes("existing-pdf");
        await using var destination = new MemoryStream();
        await destination.WriteAsync(sentinel);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var pages = new[]
        {
            CreatePage(96d, 96d, "Canceled"),
        };

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await SkiaDisplayListPdfExporter.SaveAsync(
                pages,
                destination,
                cancellationToken: cancellation.Token));

        CollectionAssert.AreEqual(sentinel, destination.ToArray());
    }

    [TestMethod]
    public async Task EmptyAndInvalidPagesAreRejectedBeforeDestinationCommit()
    {
        await using var destination = new MemoryStream();
        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
            await SkiaDisplayListPdfExporter.SaveAsync(
                Array.Empty<SkiaPdfPage>(),
                destination));
        var invalidPages = new[]
        {
            CreatePage(0d, 96d, "Invalid"),
        };
        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
            await SkiaDisplayListPdfExporter.SaveAsync(
                invalidPages,
                destination));
    }

    private static SkiaPdfPage CreatePage(
        double widthDips,
        double heightDips,
        string text)
    {
        var builder = new DisplayListBuilder();
        builder.FillRectangle(
            new RectD(0d, 0d, Math.Max(widthDips, 1d), Math.Max(heightDips, 1d)),
            ColorRgba.White);
        builder.DrawText(
            text,
            new RectD(8d, 8d, Math.Max(widthDips - 16d, 1d), 24d),
            new TextStyle(
                "Arial",
                12d,
                400,
                ColorRgba.Black,
                Wrap: false));
        return new SkiaPdfPage(
            new SizeD(widthDips, heightDips),
            builder.Build());
    }
}
