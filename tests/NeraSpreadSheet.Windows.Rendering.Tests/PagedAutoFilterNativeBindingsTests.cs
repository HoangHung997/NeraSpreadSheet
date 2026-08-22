using System.Threading;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.WinForms;
using NeraSpreadSheet.Wpf;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
public sealed class PagedAutoFilterNativeBindingsTests
{
    [TestMethod]
    [Timeout(60_000)]
    public async Task WpfBindingPublishesOnlyTheCurrentPage()
    {
        await RunOnWpfDispatcherAsync(async () =>
        {
            var fixture = CreateFixture();
            var presenter = new SpreadsheetAutoFilterPagedPresenter(
                fixture.Session,
                fixture.Target,
                pageSize: 2);
            await using var binding = new NeraWpfAutoFilterPagedBinding(
                presenter,
                Dispatcher.CurrentDispatcher);

            await binding.InitializeAsync();

            Assert.AreEqual(2, binding.Items.Count);
            Assert.AreEqual(5, binding.TotalItemCount);
            Assert.IsTrue(binding.HasNextPage);
            Assert.IsFalse(binding.HasPreviousPage);
            Assert.IsTrue(await binding.MoveNextPageAsync());
            Assert.AreEqual(2, binding.Items.Count);
            Assert.AreEqual(2, binding.PageOffset);
        });
    }

    [TestMethod]
    [Timeout(60_000)]
    public async Task WinFormsBindingPublishesOnlyTheCurrentPage()
    {
        await RunOnWinFormsThreadAsync(async dispatcher =>
        {
            var fixture = CreateFixture();
            var presenter = new SpreadsheetAutoFilterPagedPresenter(
                fixture.Session,
                fixture.Target,
                pageSize: 2);
            await using var binding = new NeraWinFormsAutoFilterPagedBinding(
                presenter,
                dispatcher);

            await binding.InitializeAsync();

            Assert.AreEqual(2, binding.Items.Count);
            Assert.AreEqual(5, binding.TotalItemCount);
            Assert.IsTrue(binding.HasNextPage);
            Assert.IsTrue(await binding.MoveNextPageAsync());
            Assert.AreEqual(2, binding.Items.Count);
            Assert.AreEqual(2, binding.PageOffset);
        });
    }

    [TestMethod]
    [Timeout(60_000)]
    public async Task NativePagedPresentersConstructAndDisposeWithoutAWorkbook()
    {
        await RunOnWpfDispatcherAsync(() =>
        {
            using NeraSpreadSheet.Wpf.NeraSpreadsheetControl wpfControl = new();
            using var wpfPresenter =
                new NeraAutoFilterPagedPopupPresenter(wpfControl);
            Assert.IsFalse(wpfPresenter.IsOpen);
            return Task.CompletedTask;
        });

        await RunOnWinFormsThreadAsync(_ =>
        {
            using NeraSpreadSheet.WinForms.NeraSpreadsheetControl
                winFormsControl = new();
            using var winFormsPresenter =
                new NeraAutoFilterPagedDropDownPresenter(winFormsControl);
            Assert.IsFalse(winFormsPresenter.IsOpen);
            return Task.CompletedTask;
        });
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
            Name = "Nera WPF paged AutoFilter test dispatcher",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(45d));
    }

    private static Task RunOnWinFormsThreadAsync(
        Func<System.Windows.Forms.Control, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var form = new System.Windows.Forms.Form
            {
                ShowInTaskbar = false,
                StartPosition = System.Windows.Forms.FormStartPosition.Manual,
                Location = new System.Drawing.Point(-32000, -32000),
                Size = new System.Drawing.Size(1, 1),
                FormBorderStyle =
                    System.Windows.Forms.FormBorderStyle.FixedToolWindow,
            };
            form.Shown += async (_, _) =>
            {
                try
                {
                    await action(form);
                    completion.TrySetResult();
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
                finally
                {
                    form.Close();
                }
            };
            System.Windows.Forms.Application.Run(form);
        })
        {
            IsBackground = true,
            Name = "Nera WinForms paged AutoFilter test dispatcher",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(45d));
    }

    private static Fixture CreateFixture()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var tableId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        worksheet.SetValue(new CellAddress(0, 0), "Value");
        for (var index = 0; index < 5; index++)
        {
            worksheet.SetValue(
                new CellAddress(index + 1, 0),
                $"Value{index}");
        }
        worksheet.AddTable(new SpreadsheetTable(
            tableId,
            "Values",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(5, 0)),
            [new SpreadsheetTableColumn(columnId, "Value")]));
        var session = new SpreadsheetSession(workbook);
        session.Selection.SetActiveCell(new CellAddress(1, 0));
        Assert.IsTrue(session.TryResolveActiveAutoFilterTarget(
            out var target));
        return new Fixture(session, target);
    }

    private sealed record Fixture(
        SpreadsheetSession Session,
        SpreadsheetAutoFilterTarget Target);
}
