using System.Threading;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Rendering.Spreadsheet;
using WinFormsPrintDocument = NeraSpreadSheet.WinForms.NeraWinFormsPrintDocument;
using WpfPrintDocument = NeraSpreadSheet.Wpf.NeraWpfPrintDocument;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
public sealed class NativePrintDocumentAdaptersTests
{
    private static readonly int[] ExpectedSelectedPages =
        [2, 3, 2, 3];

    [TestMethod]
    [Timeout(60_000)]
    public async Task WpfPaginatorComposesSelectedCopiesFromSharedDisplayLists()
    {
        await RunOnWpfDispatcherAsync(() =>
        {
            var fixture = CreateFixture();
            var document = new WpfPrintDocument(
                fixture.Snapshot,
                fixture.Plan,
                new SpreadsheetPrintTicket
                {
                    Selection = SpreadsheetPrintPageSelection.Parse(
                        "2-3",
                        fixture.Plan.Pages.Count),
                    Copies = 2,
                    Collate = true,
                },
                fixture.Workbook.Styles);

            Assert.AreEqual(4, document.Paginator.PageCount);
            CollectionAssert.AreEqual(
                ExpectedSelectedPages,
                document.Paginator.Invocations
                    .Select(static item => item.PageNumber)
                    .ToArray());
            var page = document.Paginator.GetPage(0);
            Assert.IsNotNull(page.Visual);
            Assert.IsTrue(page.Size.Width > 0d);
            Assert.IsTrue(page.Size.Height > 0d);
            Assert.AreSame(
                document,
                ((DocumentPaginator)document.Paginator).Source);
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    [Timeout(60_000)]
    public void WinFormsDocumentComposesWithoutRequiringAPhysicalPrinter()
    {
        var fixture = CreateFixture();
        using var document = new WinFormsPrintDocument(
            fixture.Snapshot,
            fixture.Plan,
            new SpreadsheetPrintTicket
            {
                Selection = SpreadsheetPrintPageSelection.Parse(
                    "2-3",
                    fixture.Plan.Pages.Count),
                Copies = 2,
                Collate = true,
            },
            fixture.Workbook.Styles);

        Assert.AreEqual(4, document.Invocations.Count);
        CollectionAssert.AreEqual(
            ExpectedSelectedPages,
            document.Invocations
                .Select(static item => item.PageNumber)
                .ToArray());
        var page = document.ComposePage(0);
        Assert.AreEqual(2, page.Invocation.PageNumber);
        Assert.IsTrue(page.DisplayList.Commands.Count > 0);
    }

    [TestMethod]
    [Timeout(60_000)]
    public async Task NativeDocumentsRejectOutOfRangePageAccess()
    {
        await RunOnWpfDispatcherAsync(() =>
        {
            var fixture = CreateFixture();
            var document = new WpfPrintDocument(
                fixture.Snapshot,
                fixture.Plan,
                styles: fixture.Workbook.Styles);
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                document.Paginator.GetPage(document.Paginator.PageCount));
            return Task.CompletedTask;
        });

        var winFormsFixture = CreateFixture();
        using var winForms = new WinFormsPrintDocument(
            winFormsFixture.Snapshot,
            winFormsFixture.Plan,
            styles: winFormsFixture.Workbook.Styles);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            winForms.ComposePage(winForms.Invocations.Count));
    }

    private static Fixture CreateFixture()
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
        return new Fixture(workbook, snapshot, plan);
    }

    private static Task RunOnWpfDispatcherAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            dispatcher.BeginInvoke(
                DispatcherPriority.Send,
                new Action(async () =>
                {
                    try
                    {
                        await action();
                        completion.TrySetResult();
                    }
                    catch (Exception exception)
                    {
                        completion.TrySetException(exception);
                    }
                    finally
                    {
                        dispatcher.BeginInvokeShutdown(
                            DispatcherPriority.Send);
                    }
                }));
            Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "Nera native print document test dispatcher",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(45d));
    }

    private sealed record Fixture(
        Workbook Workbook,
        WorksheetSnapshot Snapshot,
        SpreadsheetPageLayoutPlan Plan);
}
