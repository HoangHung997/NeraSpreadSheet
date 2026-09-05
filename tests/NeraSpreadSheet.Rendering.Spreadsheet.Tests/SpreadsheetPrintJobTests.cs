using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class SpreadsheetPrintJobTests
{
    private static readonly int[] ExpectedReverseEvenIndexes =
        [5, 3, 1];

    private static readonly int[] ExpectedCollatedPages =
        [1, 2, 1, 2];

    private static readonly int[] ExpectedUncollatedPages =
        [1, 1, 2, 2];

    private static readonly int[] ExpectedCollatedCopies =
        [1, 1, 2, 2];

    private static readonly int[] ExpectedUncollatedCopies =
        [1, 2, 1, 2];

    private static readonly int[] ExpectedSelectedPages =
        [2, 3, 2, 3];

    [TestMethod]
    public void PageSelectionParsesRangesParityAndReverseOrder()
    {
        var selection = SpreadsheetPrintPageSelection.Parse(
            "1-4,6",
            totalPages: 6,
            SpreadsheetPrintPageParity.Even,
            reverseOrder: true);

        CollectionAssert.AreEqual(
            ExpectedReverseEvenIndexes,
            selection.ResolvePageIndexes(totalPages: 6).ToArray());
    }

    [TestMethod]
    public void TicketPlannerProducesCollatedAndUncollatedSequences()
    {
        var plan = CreatePlan(pageCount: 2);
        var collated = SpreadsheetPrintTicketPlanner.CreateSequence(
            plan,
            new SpreadsheetPrintTicket
            {
                Copies = 2,
                Collate = true,
            });
        var uncollated = SpreadsheetPrintTicketPlanner.CreateSequence(
            plan,
            new SpreadsheetPrintTicket
            {
                Copies = 2,
                Collate = false,
            });

        CollectionAssert.AreEqual(
            ExpectedCollatedPages,
            collated.Select(static item => item.PageNumber).ToArray());
        CollectionAssert.AreEqual(
            ExpectedUncollatedPages,
            uncollated.Select(static item => item.PageNumber).ToArray());
        CollectionAssert.AreEqual(
            ExpectedCollatedCopies,
            collated.Select(static item => item.CopyNumber).ToArray());
        CollectionAssert.AreEqual(
            ExpectedUncollatedCopies,
            uncollated.Select(static item => item.CopyNumber).ToArray());
    }

    [TestMethod]
    public async Task RunnerWritesSelectedPagesAndCompletesOnce()
    {
        var fixture = CreatePrintableFixture();
        var sink = new RecordingSink();
        var ticket = new SpreadsheetPrintTicket
        {
            Selection = SpreadsheetPrintPageSelection.Parse(
                "2-3",
                fixture.Plan.Pages.Count),
            Copies = 2,
            Collate = true,
        };

        var result = await SpreadsheetPrintJobRunner.RunAsync(
            fixture.Snapshot,
            fixture.Plan,
            sink,
            ticket,
            fixture.Workbook.Styles,
            jobName: "Estimate print");

        Assert.IsNotNull(sink.Context);
        Assert.AreEqual("Estimate print", sink.Context.JobName);
        Assert.AreEqual(fixture.Plan.Pages.Count, sink.Context.SourcePageCount);
        Assert.AreEqual(2, sink.Context.SelectedPageCount);
        Assert.AreEqual(4, result.PagesWritten);
        Assert.AreEqual(1, sink.BeginCount);
        Assert.AreEqual(1, sink.CompleteCount);
        Assert.AreEqual(0, sink.AbortCount);
        CollectionAssert.AreEqual(
            ExpectedSelectedPages,
            sink.Pages
                .Select(static page => page.Invocation.PageNumber)
                .ToArray());
        Assert.IsTrue(sink.Pages.All(static page =>
            page.DisplayList.Commands.Count > 0));
    }

    [TestMethod]
    public async Task SinkFailureAbortsAndPreservesOriginalException()
    {
        var fixture = CreatePrintableFixture();
        var expected = new IOException("printer disconnected");
        var sink = new RecordingSink
        {
            FailAtPageWrite = 2,
            WriteFailure = expected,
        };

        var actual = await Assert.ThrowsExactlyAsync<IOException>(async () =>
            await SpreadsheetPrintJobRunner.RunAsync(
                fixture.Snapshot,
                fixture.Plan,
                sink,
                styles: fixture.Workbook.Styles));

        Assert.AreSame(expected, actual);
        Assert.AreEqual(1, sink.BeginCount);
        Assert.AreEqual(0, sink.CompleteCount);
        Assert.AreEqual(1, sink.AbortCount);
        Assert.AreSame(expected, sink.AbortFailure);
    }

    [TestMethod]
    public async Task CancellationAfterFirstPageAbortsWithoutCompleting()
    {
        var fixture = CreatePrintableFixture();
        using var cancellation = new CancellationTokenSource();
        var sink = new RecordingSink
        {
            AfterPageWritten = pageNumber =>
            {
                if (pageNumber == 1)
                {
                    cancellation.Cancel();
                }
            },
        };

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await SpreadsheetPrintJobRunner.RunAsync(
                fixture.Snapshot,
                fixture.Plan,
                sink,
                styles: fixture.Workbook.Styles,
                cancellationToken: cancellation.Token));

        Assert.AreEqual(1, sink.Pages.Count);
        Assert.AreEqual(0, sink.CompleteCount);
        Assert.AreEqual(1, sink.AbortCount);
        Assert.IsInstanceOfType<OperationCanceledException>(sink.AbortFailure);
    }

    [TestMethod]
    public async Task PreCanceledJobDoesNotBeginTheSink()
    {
        var fixture = CreatePrintableFixture();
        var sink = new RecordingSink();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await SpreadsheetPrintJobRunner.RunAsync(
                fixture.Snapshot,
                fixture.Plan,
                sink,
                styles: fixture.Workbook.Styles,
                cancellationToken: cancellation.Token));

        Assert.AreEqual(0, sink.BeginCount);
        Assert.AreEqual(0, sink.AbortCount);
    }

    private static PrintableFixture CreatePrintableFixture()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        for (var column = 0; column < 6; column++)
        {
            worksheet.Dimensions.SetColumnWidth(column, 80d);
            worksheet.SetValue(new CellAddress(0, column), $"C{column}");
        }
        var snapshot = WorksheetSnapshot.Capture(worksheet);
        var plan = SpreadsheetPageLayoutPlanner.CreatePlan(
            snapshot,
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(0, 5)),
            new SpreadsheetPageSetup
            {
                PaperSize = new SpreadsheetPaperSize(1.25d, 2d),
                Margins = new SpreadsheetPageMargins(0d, 0d, 0d, 0d),
            });
        Assert.IsTrue(plan.Pages.Count >= 3);
        return new PrintableFixture(workbook, snapshot, plan);
    }

    private static SpreadsheetPageLayoutPlan CreatePlan(int pageCount)
    {
        var pages = Enumerable.Range(0, pageCount)
            .Select(index => new SpreadsheetPrintPage(
                index + 1,
                index,
                0,
                new CellRange(
                    new CellAddress(index, 0),
                    new CellAddress(index, 0)),
                null,
                null,
                1d,
                new NeraSpreadSheet.Foundation.SizeD(800d, 1000d),
                new NeraSpreadSheet.Foundation.RectD(50d, 50d, 700d, 900d),
                new NeraSpreadSheet.Foundation.SizeD(80d, 20d),
                new NeraSpreadSheet.Foundation.PointD(0d, 0d)))
            .ToArray();
        return new SpreadsheetPageLayoutPlan(
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(pageCount - 1, 0)),
            new SpreadsheetPageSetup(),
            1d,
            new NeraSpreadSheet.Foundation.SizeD(800d, 1000d),
            new NeraSpreadSheet.Foundation.RectD(50d, 50d, 700d, 900d),
            pages);
    }

    private sealed record PrintableFixture(
        Workbook Workbook,
        WorksheetSnapshot Snapshot,
        SpreadsheetPageLayoutPlan Plan);

    private sealed class RecordingSink : ISpreadsheetPrintSink
    {
        public int BeginCount { get; private set; }

        public int CompleteCount { get; private set; }

        public int AbortCount { get; private set; }

        public int? FailAtPageWrite { get; init; }

        public Exception? WriteFailure { get; init; }

        public Action<int>? AfterPageWritten { get; init; }

        public SpreadsheetPrintJobContext? Context { get; private set; }

        public Exception? AbortFailure { get; private set; }

        public List<SpreadsheetPrintJobPage> Pages { get; } = [];

        public Task BeginJobAsync(
            SpreadsheetPrintJobContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BeginCount++;
            Context = context;
            return Task.CompletedTask;
        }

        public Task WritePageAsync(
            SpreadsheetPrintJobPage page,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (FailAtPageWrite == Pages.Count + 1)
            {
                throw WriteFailure ?? new IOException("write failed");
            }
            Pages.Add(page);
            AfterPageWritten?.Invoke(Pages.Count);
            return Task.CompletedTask;
        }

        public Task CompleteJobAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CompleteCount++;
            return Task.CompletedTask;
        }

        public Task AbortJobAsync(
            Exception? failure,
            CancellationToken cancellationToken)
        {
            AbortCount++;
            AbortFailure = failure;
            return Task.CompletedTask;
        }
    }
}
