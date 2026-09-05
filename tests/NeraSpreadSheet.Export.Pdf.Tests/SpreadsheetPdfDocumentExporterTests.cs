using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Rendering.Skia;

namespace NeraSpreadSheet.Export.Pdf.Tests;

[TestClass]
public sealed class SpreadsheetPdfDocumentExporterTests
{
    private static readonly string[] ExpectedDefaultWorksheetNames =
        ["One", "Two"];

    private static readonly string[] ExpectedExplicitWorksheetNames =
        ["Two", "One"];

    [TestMethod]
    public async Task DefaultExportIncludesEveryNonEmptyWorksheet()
    {
        var workbook = new Workbook();
        workbook.RenameWorksheet(workbook.Worksheets[0], "One");
        workbook.Worksheets[0].SetValue(new CellAddress(0, 0), "First");
        var second = workbook.AddWorksheet("Two");
        second.SetValue(new CellAddress(0, 0), "Second");
        workbook.AddWorksheet("Blank");
        await using var destination = new MemoryStream();

        var result = await SpreadsheetPdfDocumentExporter.SaveAsync(
            workbook,
            destination);

        Assert.AreEqual(2, result.Sections.Count);
        CollectionAssert.AreEqual(
            ExpectedDefaultWorksheetNames,
            result.Sections
                .Select(static section => section.WorksheetName)
                .ToArray());
        Assert.AreEqual(
            result.Sections.Sum(static section => section.PageCount),
            result.TotalPageCount);
        Assert.AreEqual(1, result.Sections[0].FirstPageNumber);
        Assert.AreEqual(
            result.Sections[0].PageCount + 1,
            result.Sections[1].FirstPageNumber);
        AssertPdf(destination);
    }

    [TestMethod]
    public async Task ExplicitSelectionsControlOrderAndSettings()
    {
        var workbook = new Workbook();
        workbook.RenameWorksheet(workbook.Worksheets[0], "One");
        workbook.Worksheets[0].SetValue(new CellAddress(0, 0), "First");
        var second = workbook.AddWorksheet("Two");
        second.SetValue(new CellAddress(0, 0), "Second");
        var selections = new[]
        {
            new SpreadsheetPdfWorksheetSelection(
                1,
                new SpreadsheetPdfExportOptions
                {
                    PageSetup = new SpreadsheetPageSetup
                    {
                        Orientation = SpreadsheetPageOrientation.Landscape,
                    },
                }),
            new SpreadsheetPdfWorksheetSelection(0),
        };
        await using var destination = new MemoryStream();

        var result = await SpreadsheetPdfDocumentExporter.SaveAsync(
            workbook,
            destination,
            new SpreadsheetPdfDocumentExportOptions
            {
                Worksheets = selections,
            });

        CollectionAssert.AreEqual(
            ExpectedExplicitWorksheetNames,
            result.Sections
                .Select(static section => section.WorksheetName)
                .ToArray());
        Assert.AreEqual(
            SpreadsheetPageOrientation.Landscape,
            result.Sections[0].PageLayout.Setup.Orientation);
    }

    [TestMethod]
    public async Task AllBlankSelectionFailurePreservesDestination()
    {
        var workbook = new Workbook();
        var sentinel = Encoding.UTF8.GetBytes("existing");
        await using var destination = new MemoryStream();
        await destination.WriteAsync(sentinel);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await SpreadsheetPdfDocumentExporter.SaveAsync(
                workbook,
                destination));

        CollectionAssert.AreEqual(sentinel, destination.ToArray());
    }

    [TestMethod]
    public async Task DuplicateAndOutOfRangeSelectionsAreRejected()
    {
        var workbook = new Workbook();
        workbook.Worksheets[0].SetValue(new CellAddress(0, 0), "Value");
        await using var destination = new MemoryStream();
        var duplicates = new[]
        {
            new SpreadsheetPdfWorksheetSelection(0),
            new SpreadsheetPdfWorksheetSelection(0),
        };

        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await SpreadsheetPdfDocumentExporter.SaveAsync(
                workbook,
                destination,
                new SpreadsheetPdfDocumentExportOptions
                {
                    Worksheets = duplicates,
                }));
        var outOfRange = new[]
        {
            new SpreadsheetPdfWorksheetSelection(3),
        };
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(async () =>
            await SpreadsheetPdfDocumentExporter.SaveAsync(
                workbook,
                destination,
                new SpreadsheetPdfDocumentExportOptions
                {
                    Worksheets = outOfRange,
                }));
    }

    [TestMethod]
    public async Task GlobalPageLimitFailurePreservesDestination()
    {
        var workbook = new Workbook();
        workbook.Worksheets[0].SetValue(new CellAddress(0, 0), "First");
        workbook.AddWorksheet("Second")
            .SetValue(new CellAddress(0, 0), "Second");
        var sentinel = Encoding.UTF8.GetBytes("existing");
        await using var destination = new MemoryStream();
        await destination.WriteAsync(sentinel);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
            await SpreadsheetPdfDocumentExporter.SaveAsync(
                workbook,
                destination,
                new SpreadsheetPdfDocumentExportOptions
                {
                    PdfOptions = new SkiaPdfExportOptions
                    {
                        MaximumPages = 1,
                    },
                }));

        CollectionAssert.AreEqual(sentinel, destination.ToArray());
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
