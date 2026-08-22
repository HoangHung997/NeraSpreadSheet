using NeraSpreadSheet.Core;
using NeraSpreadSheet.Rendering.Skia;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Export.Pdf;

public sealed record SpreadsheetPdfPrintJobOptions
{
    public SpreadsheetPdfExportOptions WorksheetOptions { get; init; } =
        new();

    public SpreadsheetPrintTicket Ticket { get; init; } = new();
}

public sealed record SpreadsheetPdfPrintJobResult(
    SpreadsheetPageLayoutPlan PageLayout,
    IReadOnlyList<SpreadsheetPrintPageInvocation> Invocations,
    long? OutputLength)
{
    public int PageCount => Invocations.Count;
}

/// <summary>
/// Exports one worksheet to a staged PDF while honoring the shared print-page
/// selection, parity, reverse-order, copy and collation contract.
/// </summary>
public static class SpreadsheetPdfPrintJobExporter
{
    public static async Task<SpreadsheetPdfPrintJobResult> SaveAsync(
        Worksheet worksheet,
        Stream destination,
        SpreadsheetPdfPrintJobOptions? options = null,
        CellStyleCatalog? styles = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentNullException.ThrowIfNull(destination);
        options ??= new SpreadsheetPdfPrintJobOptions();
        ArgumentNullException.ThrowIfNull(options.WorksheetOptions);
        ArgumentNullException.ThrowIfNull(options.Ticket);
        cancellationToken.ThrowIfCancellationRequested();

        var plan = SpreadsheetPdfExporter.CreatePlan(
            worksheet,
            options.WorksheetOptions);
        var invocations = SpreadsheetPrintTicketPlanner.CreateSequence(
            plan.PageLayout,
            options.Ticket);
        if (invocations.Count == 0)
        {
            throw new InvalidOperationException(
                "The PDF print ticket does not select any page.");
        }

        IEnumerable<SkiaPdfPage> ComposePages()
        {
            foreach (var invocation in invocations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var composed = SpreadsheetPrintDisplayListComposer.Compose(
                    plan.Worksheet,
                    plan.PageLayout,
                    invocation.PageIndex,
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
        return new SpreadsheetPdfPrintJobResult(
            plan.PageLayout,
            invocations,
            destination.CanSeek ? destination.Length : null);
    }
}
