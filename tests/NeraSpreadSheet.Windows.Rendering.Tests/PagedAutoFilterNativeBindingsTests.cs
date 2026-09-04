using System.Threading;
using System.Reflection;
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
            CollectionAssert.Contains(
                binding.MenuKinds.ToArray(),
                SpreadsheetAutoFilterMenuKind.Text);
            Assert.IsTrue(await binding.MoveNextPageAsync());
            Assert.AreEqual(2, binding.Items.Count);
            Assert.AreEqual(2, binding.PageOffset);
            await binding.ApplyRichFilterAsync(
                new SpreadsheetAutoFilterRichCriterion(
                    topBottom: new SpreadsheetTopBottomFilter(true, false, 2)));
            Assert.AreEqual(1, fixture.Session.History.UndoCount);
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
            CollectionAssert.Contains(
                binding.MenuKinds.ToArray(),
                SpreadsheetAutoFilterMenuKind.Text);
            Assert.IsTrue(await binding.MoveNextPageAsync());
            Assert.AreEqual(2, binding.Items.Count);
            Assert.AreEqual(2, binding.PageOffset);
            await binding.ApplyRichFilterAsync(
                new SpreadsheetAutoFilterRichCriterion(
                    topBottom: new SpreadsheetTopBottomFilter(true, false, 2)));
            Assert.AreEqual(1, fixture.Session.History.UndoCount);
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

    [TestMethod]
    [Timeout(60_000)]
    public async Task LoadedDesktopPresentersExposeBoundedRichMenuSurface()
    {
        await RunOnWpfDispatcherAsync(async () =>
        {
            var fixture = CreateFixture();
            using var control = new NeraSpreadSheet.Wpf.NeraSpreadsheetControl
            {
                Workbook = fixture.Session.Workbook,
            };
            var window = new System.Windows.Window
            {
                Width = 520d,
                Height = 320d,
                Left = -32_000d,
                Top = -32_000d,
                ShowInTaskbar = false,
                Content = new System.Windows.Documents.AdornerDecorator
                {
                    Child = control,
                },
            };
            using var presenter = new NeraAutoFilterPagedPopupPresenter(control);
            try
            {
                window.Show();
                window.UpdateLayout();
                control.Session!.Selection.SetActiveCell(new CellAddress(1, 0));
                Assert.IsTrue(presenter.TryOpenForActiveCell());
                await Task.Delay(250);
                var kinds = GetPrivateField<System.Windows.Controls.ComboBox>(
                    presenter,
                    "_menuKindBox");
                var values = GetPrivateField<List<System.Windows.Controls.CheckBox>>(
                    presenter,
                    "_valueCheckBoxes");
                var criterion = GetPrivateField<System.Windows.Controls.TextBox>(
                    presenter,
                    "_criterionInput");
                Assert.IsTrue(kinds.Items.Count >= 2);
                Assert.AreEqual(
                    "NeraAutoFilterPagedCriterion",
                    System.Windows.Automation.AutomationProperties.GetAutomationId(criterion));
                Assert.IsTrue(values.Count <= 100);
            }
            finally
            {
                presenter.Close();
                window.Close();
            }
        });

        await RunOnWinFormsThreadAsync(async formControl =>
        {
            var fixture = CreateFixture();
            formControl.Size = new System.Drawing.Size(520, 320);
            using var control = new NeraSpreadSheet.WinForms.NeraSpreadsheetControl
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                Workbook = fixture.Session.Workbook,
            };
            formControl.Controls.Add(control);
            control.CreateControl();
            using var presenter = new NeraAutoFilterPagedDropDownPresenter(control);
            control.Session!.Selection.SetActiveCell(new CellAddress(1, 0));
            presenter.Refresh();
            System.Windows.Forms.Application.DoEvents();
            Assert.IsTrue(presenter.TryOpenForActiveCell());
            await Task.Delay(250);
            var kinds = GetPrivateField<System.Windows.Forms.ComboBox>(
                presenter,
                "_menuKindBox");
            var values = GetPrivateField<System.Windows.Forms.CheckedListBox>(
                presenter,
                "_valuesList");
            var criterion = GetPrivateField<System.Windows.Forms.TextBox>(
                presenter,
                "_criterionInput");
            Assert.AreEqual(
                System.Windows.Forms.ComboBoxStyle.DropDownList,
                kinds.DropDownStyle);
            Assert.IsFalse(string.IsNullOrWhiteSpace(kinds.AccessibleName));
            Assert.IsFalse(string.IsNullOrWhiteSpace(criterion.AccessibleName));
            Assert.IsTrue(values.Items.Count <= 100);
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

    private static T GetPrivateField<T>(object instance, string fieldName)
        where T : class =>
        (T)(instance.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance)
            ?? throw new AssertFailedException(
                $"Field '{fieldName}' was not initialized."));

    private sealed record Fixture(
        SpreadsheetSession Session,
        SpreadsheetAutoFilterTarget Target);
}
