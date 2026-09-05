using NeraSpreadSheet.Core;
using NeraSpreadSheet.Rendering.Skia;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Export.Pdf;

public sealed record SpreadsheetPdfWorksheetSelection(
    int WorksheetIndex,
    SpreadsheetPdfExportOptions? Options = null);

public sealed record SpreadsheetPdfDocumentExportOptions
{
    public IReadOnlyList<SpreadsheetPdfWorksheetSelection>? Worksheets
    {
        get;
        init;
    }

    public bool SkipEmptyWorksheets { get; init; } = true;

    public SkiaPdfExportOptions PdfOptions { get; init; } = new();
}

public sealed record SpreadsheetPdfDocumentSectionResult(
    int WorksheetIndex,
    string WorksheetName,
    int FirstPageNumber,
    SpreadsheetPageLayoutPlan PageLayout)
{
    public int PageCount => PageLayout.Pages.Count;
}

public sealed record SpreadsheetPdfDocumentExportResult(
    IReadOnlyList<SpreadsheetPdfDocumentSectionResult> Sections,
    int TotalPageCount,
    long? OutputLength);

/// <summary>
/// Exports selected workbook worksheets to one staged PDF document. Page
/// numbering inside worksheet header/footer templates currently resets per
/// worksheet because each section retains its own page-layout plan.
/// </summary>
public static class SpreadsheetPdfDocumentExporter
{
    public static async Task<SpreadsheetPdfDocumentExportResult> SaveAsync(
        Workbook workbook,
        Stream destination,
        SpreadsheetPdfDocumentExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(destination);
        options ??= new SpreadsheetPdfDocumentExportOptions();
        ArgumentNullException.ThrowIfNull(options.PdfOptions);
        cancellationToken.ThrowIfCancellationRequested();

        var selections = ResolveSelections(workbook, options);
        var prepared = new List<PreparedSection>(selections.Length);
        foreach (var selection in selections)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var worksheet = workbook.Worksheets[selection.WorksheetIndex];
            try
            {
                prepared.Add(new PreparedSection(
                    selection.WorksheetIndex,
                    worksheet.Name,
                    SpreadsheetPdfExporter.CreatePlan(
                        worksheet,
                        selection.Options)));
            }
            catch (InvalidOperationException) when (
                options.SkipEmptyWorksheets &&
                selection.Options?.PrintArea is null &&
                worksheet.GetPrintSettings().PrintArea is null &&
                worksheet.UsedCellCount == 0)
            {
                // Blank worksheets without an explicit print area do not
                // contribute pages to the multi-sheet document.
            }
        }

        if (prepared.Count == 0)
        {
            throw new InvalidOperationException(
                "No selected worksheet produced a printable PDF section.");
        }

        var totalPageCount = prepared.Sum(static section =>
            section.Plan.PageLayout.Pages.Count);
        var firstPageNumber = 1;
        var sectionResults = new List<SpreadsheetPdfDocumentSectionResult>(
            prepared.Count);
        foreach (var section in prepared)
        {
            sectionResults.Add(new SpreadsheetPdfDocumentSectionResult(
                section.WorksheetIndex,
                section.WorksheetName,
                firstPageNumber,
                section.Plan.PageLayout));
            firstPageNumber = checked(
                firstPageNumber + section.Plan.PageLayout.Pages.Count);
        }

        IEnumerable<SkiaPdfPage> ComposePages()
        {
            foreach (var section in prepared)
            {
                for (var pageIndex = 0;
                     pageIndex < section.Plan.PageLayout.Pages.Count;
                     pageIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var composed = SpreadsheetPrintDisplayListComposer.Compose(
                        section.Plan.Worksheet,
                        section.Plan.PageLayout,
                        pageIndex,
                        workbook.Styles,
                        section.Plan.DisplayListOptions);
                    yield return new SkiaPdfPage(
                        composed.Page.PaperSizeDips,
                        composed.DisplayList);
                }
            }
        }

        await SkiaDisplayListPdfExporter.SaveAsync(
            ComposePages(),
            destination,
            options.PdfOptions,
            cancellationToken).ConfigureAwait(false);
        return new SpreadsheetPdfDocumentExportResult(
            sectionResults,
            totalPageCount,
            destination.CanSeek ? destination.Length : null);
    }

    private static SpreadsheetPdfWorksheetSelection[] ResolveSelections(
        Workbook workbook,
        SpreadsheetPdfDocumentExportOptions options)
    {
        var selections = options.Worksheets?.ToArray() ??
            Enumerable.Range(0, workbook.Worksheets.Count)
                .Select(static index =>
                    new SpreadsheetPdfWorksheetSelection(index))
                .ToArray();
        var seen = new HashSet<int>();
        foreach (var selection in selections)
        {
            ArgumentNullException.ThrowIfNull(selection);
            if (selection.WorksheetIndex < 0 ||
                selection.WorksheetIndex >= workbook.Worksheets.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    selection.WorksheetIndex,
                    "A PDF worksheet selection is outside the workbook.");
            }
            if (!seen.Add(selection.WorksheetIndex))
            {
                throw new ArgumentException(
                    "A worksheet may be selected only once for one PDF document.",
                    nameof(options));
            }
        }
        return selections;
    }

    private sealed record PreparedSection(
        int WorksheetIndex,
        string WorksheetName,
        SpreadsheetPdfExportPlan Plan);
}
