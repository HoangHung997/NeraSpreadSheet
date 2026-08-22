using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Rendering.Spreadsheet;
using WinFormsPreview = NeraSpreadSheet.WinForms.NeraPrintPreviewControl;
using WpfPreview = NeraSpreadSheet.Wpf.NeraPrintPreviewControl;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
public sealed class NativePrintPreviewControlsTests
{
    [TestMethod]
    public async Task WpfPreviewHostsSharedSessionAndPreservesFractionalViewport()
    {
        await RunOnWpfDispatcherAsync(() =>
        {
            var session = CreatePreviewSession();
            var control = new WpfPreview
            {
                Width = 480d,
                Height = 320d,
                Session = session,
            };
            control.Measure(new System.Windows.Size(480d, 320d));
            control.Arrange(new Rect(0d, 0d, 480d, 320d));

            control.SetZoom(0.5d, 125.25d, 84.75d);
            control.ScrollTo(17.25d, 31.75d);

            Assert.AreSame(session, control.Session);
            Assert.AreEqual(0.5d, control.Zoom, 0.000001d);
            Assert.AreEqual(17.25d, control.OffsetX, 0.000001d);
            Assert.AreEqual(31.75d, control.OffsetY, 0.000001d);
            Assert.IsTrue(control.Focusable);
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public void WinFormsPreviewHostsSharedSessionAndDisposesRenderer()
    {
        var session = CreatePreviewSession();
        using var control = new WinFormsPreview
        {
            Size = new System.Drawing.Size(480, 320),
            Session = session,
        };

        control.SetZoom(0.5d, 125.25d, 84.75d);
        control.ScrollTo(17.25d, 31.75d);

        Assert.AreSame(session, control.Session);
        Assert.AreEqual(0.5d, control.Zoom, 0.000001d);
        Assert.AreEqual(17.25d, control.OffsetX, 0.000001d);
        Assert.AreEqual(31.75d, control.OffsetY, 0.000001d);
        Assert.IsTrue(control.TabStop);
    }

    [TestMethod]
    public async Task NativeControlsRejectViewportChangesWithoutASession()
    {
        await RunOnWpfDispatcherAsync(() =>
        {
            var control = new WpfPreview();
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                control.SetZoom(1d));
            return Task.CompletedTask;
        });

        using var winForms = new WinFormsPreview();
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            winForms.ScrollBy(1d, 1d));
    }

    private static SpreadsheetPrintPreviewSession CreatePreviewSession()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        for (var row = 0; row < 20; row++)
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
        var snapshot = WorksheetSnapshot.Capture(worksheet);
        var plan = SpreadsheetPageLayoutPlanner.CreatePlan(
            snapshot,
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(19, 3)),
            new SpreadsheetPageSetup
            {
                PaperSize = new SpreadsheetPaperSize(4d, 5d),
                Margins = SpreadsheetPageMargins.Narrow,
            });
        return new SpreadsheetPrintPreviewSession(
            snapshot,
            plan,
            workbook.Styles,
            previewOptions: new SpreadsheetPrintPreviewOptions
            {
                Zoom = 0.35d,
                Columns = 1,
            });
    }

    private static Task<object?> RunOnWpfDispatcherAsync(
        Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var completion = new TaskCompletionSource<object?>(
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
                        completion.TrySetResult(null);
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
            Name = "Nera native print preview test dispatcher",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
