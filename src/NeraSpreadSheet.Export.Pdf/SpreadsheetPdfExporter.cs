using NeraSpreadSheet.Core;
using NeraSpreadSheet.Rendering.Skia;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Export.Pdf;

public sealed record SpreadsheetPdfExportOptions
{
    public bool UseWorksheetPrintSettings { get; init; } = true;

    public CellRange? PrintArea { get; init; }

    public SpreadsheetPageSetup? PageSetup { get; init; }

    public SpreadsheetPrintDisplayListOptions DisplayListOptions { get; init; } =
        new();

    public SkiaPdfExportOptions PdfOptions { get; init; } = new();
}

public sealed record SpreadsheetPdfExportPlan(
    WorksheetSnapshot Worksheet,
    SpreadsheetPageLayoutPlan PageLayout,
    SpreadsheetPrintDisplayListOptions DisplayListOptions,
    SkiaPdfExportOptions PdfOptions);

public sealed record SpreadsheetPdfExportResult(
    SpreadsheetPageLayoutPlan PageLayout,
    int PageCount,
    long? OutputLength);

/// <summary>
/// High-level worksheet-to-PDF orchestration. Pagination and spreadsheet
/// composition stay in Rendering.Spreadsheet, while final PDF serialization
/// stays in Rendering.Skia.
/// </summary>
public static class SpreadsheetPdfExporter
{
    public static SpreadsheetPdfExportPlan CreatePlan(
        Worksheet worksheet,
        SpreadsheetPdfExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        options ??= new SpreadsheetPdfExportOptions();
        ValidateOptions(options);

        var storedSettings = options.UseWorksheetPrintSettings
            ? worksheet.GetPrintSettings()
            : new WorksheetPrintSettings();
        var printArea = options.PrintArea ??
                        storedSettings.PrintArea ??
                        GetUsedRange(worksheet) ??
                        throw new InvalidOperationException(
                            "The worksheet is empty and has no explicit print area.");
        var pageSetup = options.PageSetup?.Copy() ??
                        storedSettings.PageSetup.Copy();
        var snapshot = WorksheetSnapshot.Capture(worksheet);
        var layout = SpreadsheetPageLayoutPlanner.CreatePlan(
            snapshot,
            printArea,
            pageSetup);
        return new SpreadsheetPdfExportPlan(
            snapshot,
            layout,
            options.DisplayListOptions,
            options.PdfOptions);
    }

    public static async Task<SpreadsheetPdfExportResult> SaveAsync(
        Worksheet worksheet,
        Stream destination,
        SpreadsheetPdfExportOptions? options = null,
        CellStyleCatalog? styles = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentNullException.ThrowIfNull(destination);
        cancellationToken.ThrowIfCancellationRequested();
        var plan = CreatePlan(worksheet, options);

        IEnumerable<SkiaPdfPage> ComposePages()
        {
            for (var index = 0;
                 index < plan.PageLayout.Pages.Count;
                 index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var composed = SpreadsheetPrintDisplayListComposer.Compose(
                    plan.Worksheet,
                    plan.PageLayout,
                    index,
                    styles,
                    plan.DisplayListOptions);
                yield return new SkiaPdfPage(
                    composed.Page.PaperSizeDips,
                    composed.DisplayList);
            }
        }

        await SkiaDisplayListPdfExporter.SaveAsync(
            ComposePages(),
            destination,
            plan.PdfOptions,
            cancellationToken).ConfigureAwait(false);
        return new SpreadsheetPdfExportResult(
            plan.PageLayout,
            plan.PageLayout.Pages.Count,
            destination.CanSeek ? destination.Length : null);
    }

    private static CellRange? GetUsedRange(Worksheet worksheet)
    {
        var cells = worksheet.EnumerateUsedCells().ToArray();
        if (cells.Length == 0)
        {
            return null;
        }

        return new CellRange(
            new CellAddress(
                cells.Min(static cell => cell.Key.RowIndex),
                cells.Min(static cell => cell.Key.ColumnIndex)),
            new CellAddress(
                cells.Max(static cell => cell.Key.RowIndex),
                cells.Max(static cell => cell.Key.ColumnIndex)));
    }

    private static void ValidateOptions(SpreadsheetPdfExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options.DisplayListOptions);
        ArgumentNullException.ThrowIfNull(options.PdfOptions);
    }
}
