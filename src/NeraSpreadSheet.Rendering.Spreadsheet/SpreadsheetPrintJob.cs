using NeraSpreadSheet.Core;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Rendering.Spreadsheet;

public sealed record SpreadsheetPrintJobContext(
    Guid JobId,
    string JobName,
    int SourcePageCount,
    int SelectedPageCount,
    int Copies,
    bool Collated,
    DateTime StartedUtc);

public sealed record SpreadsheetPrintJobPage(
    SpreadsheetPrintPageInvocation Invocation,
    SpreadsheetPrintPage Page,
    DisplayList DisplayList);

public sealed record SpreadsheetPrintJobResult(
    Guid JobId,
    int PagesWritten,
    DateTime StartedUtc,
    DateTime CompletedUtc)
{
    public TimeSpan Duration => CompletedUtc - StartedUtc;
}

public interface ISpreadsheetPrintSink
{
    Task BeginJobAsync(
        SpreadsheetPrintJobContext context,
        CancellationToken cancellationToken);

    Task WritePageAsync(
        SpreadsheetPrintJobPage page,
        CancellationToken cancellationToken);

    Task CompleteJobAsync(CancellationToken cancellationToken);

    Task AbortJobAsync(
        Exception? failure,
        CancellationToken cancellationToken);
}

public static class SpreadsheetPrintJobRunner
{
    public static async Task<SpreadsheetPrintJobResult> RunAsync(
        WorksheetSnapshot worksheet,
        SpreadsheetPageLayoutPlan plan,
        ISpreadsheetPrintSink sink,
        SpreadsheetPrintTicket? ticket = null,
        CellStyleCatalog? styles = null,
        SpreadsheetPrintDisplayListOptions? displayListOptions = null,
        string? jobName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(sink);
        cancellationToken.ThrowIfCancellationRequested();
        ticket ??= new SpreadsheetPrintTicket();
        displayListOptions ??= new SpreadsheetPrintDisplayListOptions();
        var sequence = SpreadsheetPrintTicketPlanner.CreateSequence(
            plan,
            ticket);
        if (sequence.Count == 0)
        {
            throw new InvalidOperationException(
                "The print ticket does not select any page.");
        }

        var startedUtc = DateTime.UtcNow;
        var jobId = Guid.NewGuid();
        var context = new SpreadsheetPrintJobContext(
            jobId,
            string.IsNullOrWhiteSpace(jobName)
                ? worksheet.Name
                : jobName.Trim(),
            plan.Pages.Count,
            sequence.Select(static item => item.PageIndex)
                .Distinct()
                .Count(),
            ticket.Copies,
            ticket.Collate,
            startedUtc);
        var began = false;
        var completed = false;
        var pagesWritten = 0;
        Exception? failure = null;
        try
        {
            await sink.BeginJobAsync(
                context,
                cancellationToken).ConfigureAwait(false);
            began = true;
            foreach (var invocation in sequence)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var composed = SpreadsheetPrintDisplayListComposer.Compose(
                    worksheet,
                    plan,
                    invocation.PageIndex,
                    styles,
                    displayListOptions);
                await sink.WritePageAsync(
                    new SpreadsheetPrintJobPage(
                        invocation,
                        composed.Page,
                        composed.DisplayList),
                    cancellationToken).ConfigureAwait(false);
                pagesWritten++;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await sink.CompleteJobAsync(cancellationToken)
                .ConfigureAwait(false);
            completed = true;
            return new SpreadsheetPrintJobResult(
                jobId,
                pagesWritten,
                startedUtc,
                DateTime.UtcNow);
        }
        catch (Exception exception)
        {
            failure = exception;
            throw;
        }
        finally
        {
            if (began && !completed)
            {
                try
                {
                    await sink.AbortJobAsync(
                        failure,
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch when (failure is not null)
                {
                    // Preserve the original print or cancellation failure.
                }
            }
        }
    }
}
