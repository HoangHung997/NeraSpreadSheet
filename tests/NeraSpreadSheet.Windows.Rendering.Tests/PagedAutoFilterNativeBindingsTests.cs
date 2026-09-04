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
            Assert.Contains("5 kết quả", binding.AccessibilityAnnouncement);
            Assert.IsTrue(await binding.MoveNextPageAsync());
            Assert.AreEqual(2, binding.Items.Count);
            Assert.AreEqual(2, binding.PageOffset);
            Assert.IsTrue(await binding.ApplyColumnSortAsync(descending: true));
            await binding.ApplyRichFilterAsync(
                new SpreadsheetAutoFilterRichCriterion(
                    topBottom: new SpreadsheetTopBottomFilter(true, false, 2)));
            Assert.AreEqual(2, fixture.Session.History.UndoCount);
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
            Assert.Contains("5 kết quả", binding.AccessibilityAnnouncement);
            Assert.IsTrue(await binding.MoveNextPageAsync());
            Assert.AreEqual(2, binding.Items.Count);
            Assert.AreEqual(2, binding.PageOffset);
            Assert.IsTrue(await binding.ApplyColumnSortAsync(descending: true));
            await binding.ApplyRichFilterAsync(
                new SpreadsheetAutoFilterRichCriterion(
                    topBottom: new SpreadsheetTopBottomFilter(true, false, 2)));
            Assert.AreEqual(2, fixture.Session.History.UndoCount);
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
                var search = GetPrivateField<System.Windows.Controls.TextBox>(
                    presenter,
                    "_searchBox");
                Assert.IsTrue(kinds.Items.Count >= 2);
                Assert.AreEqual(
                    "NeraAutoFilterPagedCriterion",
                    System.Windows.Automation.AutomationProperties.GetAutomationId(criterion));
                var popup = GetPrivateField<System.Windows.Controls.Primitives.Popup>(
                    presenter,
                    "_popup");
                var automationIds = EnumerateWpfElements(popup.Child)
                    .Select(System.Windows.Automation.AutomationProperties.GetAutomationId)
                    .ToArray();
                CollectionAssert.Contains(automationIds, "NeraAutoFilterSortAscending");
                CollectionAssert.Contains(automationIds, "NeraAutoFilterSortDescending");
                CollectionAssert.Contains(automationIds, "NeraAutoFilterReapply");
                CollectionAssert.Contains(automationIds, "NeraAutoFilterClearSort");
                Assert.IsTrue(values.Count <= 100);

                Assert.IsTrue(search.Focus());
                Assert.IsFalse(RaiseWpfPreviewKey(popup.Child, System.Windows.Input.Key.Home));
                Assert.IsTrue(RaiseWpfPreviewKey(popup.Child, System.Windows.Input.Key.Down));
                Assert.IsTrue(values[0].IsKeyboardFocusWithin);
                Assert.IsTrue(kinds.Focus());
                Assert.IsFalse(RaiseWpfPreviewKey(popup.Child, System.Windows.Input.Key.Enter));
                Assert.IsFalse(RaiseWpfPreviewKey(popup.Child, System.Windows.Input.Key.PageDown));
            }
            finally
            {
                presenter.Close();
                window.Close();
            }
        });

        await RunOnWinFormsThreadAsync(async formControl =>
        {
            var fixture = CreateFixture(101);
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
            control.Focus();
            System.Windows.Forms.Application.DoEvents();
            Assert.IsTrue(presenter.TryOpenForActiveCell());
            var binding = GetPrivateField<NeraWinFormsAutoFilterPagedBinding>(
                presenter,
                "_binding");
            await binding.InitializeAsync();
            InvokePrivate(presenter, "RebuildPage");
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
            var dropDown = GetPrivateField<System.Windows.Forms.ToolStripDropDown>(
                presenter,
                "_dropDown");
            var panel = ((System.Windows.Forms.ToolStripControlHost)dropDown.Items[0]).Control;
            var buttonLabels = EnumerateWinFormsControls(panel)
                .OfType<System.Windows.Forms.Button>()
                .Select(button => button.Text)
                .ToArray();
            CollectionAssert.Contains(buttonLabels, "Sắp xếp ↑");
            CollectionAssert.Contains(buttonLabels, "Sắp xếp ↓");
            CollectionAssert.Contains(buttonLabels, "Áp dụng lại");
            CollectionAssert.Contains(buttonLabels, "Xóa SX");
            Assert.IsTrue(values.Items.Count <= 100);

            var search = GetPrivateField<System.Windows.Forms.TextBox>(
                presenter,
                "_searchBox");
            var next = GetPrivateField<System.Windows.Forms.Button>(
                presenter,
                "_nextButton");
            Assert.IsTrue(next.Enabled);

            foreach (var nativeControl in new System.Windows.Forms.Control[]
                     {
                         search,
                         kinds,
                         next,
                     })
            {
                Assert.IsTrue(nativeControl.Focus());
                System.Windows.Forms.Application.DoEvents();
                var nativePageDown = new System.Windows.Forms.KeyEventArgs(
                    System.Windows.Forms.Keys.PageDown);
                InvokePrivate(
                    presenter,
                    "OnDropDownKeyDown",
                    nativePageDown);
                Assert.IsFalse(nativePageDown.Handled);
                Assert.IsFalse(nativePageDown.SuppressKeyPress);
                Assert.AreEqual(0, binding.PageOffset);
            }

            Assert.IsTrue(values.Focus());
            System.Windows.Forms.Application.DoEvents();
            var navigationPageDown = new System.Windows.Forms.KeyEventArgs(
                System.Windows.Forms.Keys.PageDown);
            InvokePrivate(
                presenter,
                "OnDropDownKeyDown",
                navigationPageDown);
            Assert.IsTrue(navigationPageDown.Handled);
            Assert.IsTrue(navigationPageDown.SuppressKeyPress);
            await GetPrivateField<Task>(presenter, "_operationTail");
            Assert.AreEqual(100, binding.PageOffset);
        });
    }

    [TestMethod]
    public void WinFormsFilterHeaderGlyphsDistinguishEveryStateWithoutColor()
    {
        var glyphType = typeof(NeraAutoFilterPagedDropDownPresenter).Assembly.GetType(
            "NeraSpreadSheet.WinForms.NeraWinFormsFilterHeaderGlyphs",
            throwOnError: true) ?? throw new AssertFailedException(
            "The WinForms filter-header glyph provider was not found.");
        var getGlyph = glyphType.GetMethod(
            "Get",
            BindingFlags.Static | BindingFlags.NonPublic) ??
            throw new AssertFailedException(
                "The WinForms filter-header glyph method was not found.");

        string Resolve(SpreadsheetFilterHeaderState state, bool? descending) =>
            (string)(getGlyph.Invoke(null, new object?[] { state, descending }) ??
                throw new AssertFailedException(
                    $"No glyph was returned for {state}."));

        var glyphs = new[]
        {
            Resolve(SpreadsheetFilterHeaderState.None, null),
            Resolve(SpreadsheetFilterHeaderState.Filtered, null),
            Resolve(SpreadsheetFilterHeaderState.Sorted, false),
            Resolve(SpreadsheetFilterHeaderState.Sorted, true),
            Resolve(SpreadsheetFilterHeaderState.FilteredAndSorted, false),
            Resolve(SpreadsheetFilterHeaderState.FilteredAndSorted, true),
        };

        Assert.AreEqual(glyphs.Length, glyphs.Distinct().Count());
        Assert.AreEqual("↑", glyphs[2]);
        Assert.AreEqual("↓", glyphs[3]);
        Assert.AreEqual("⇈", glyphs[4]);
        Assert.AreEqual("⇊", glyphs[5]);
    }

    [TestMethod]
    [Timeout(60_000)]
    public async Task LoadedDesktopPresentersRenderLazyDateTreeAndTwoConditionEditor()
    {
        await RunOnWpfDispatcherAsync(async () =>
        {
            var fixture = CreateDateFixture();
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
                kinds.SelectedIndex = FindMenuIndex(
                    kinds,
                    SpreadsheetAutoFilterMenuKind.Date);
                await GetPrivateField<Task>(presenter, "_operationTail");
                var datePage = GetPrivateField<SpreadsheetAutoFilterDatePage>(
                    presenter,
                    "_datePage");
                var items = GetPrivateField<System.Windows.Controls.StackPanel>(
                    presenter,
                    "_itemsPanel");
                Assert.AreEqual(2, datePage.TotalNodeCount);
                Assert.AreEqual(2, items.Children.Count);
                Assert.IsTrue(items.Children.Cast<System.Windows.Controls.Grid>()
                    .All(row => row.Children.OfType<System.Windows.Controls.CheckBox>().Any()));

                kinds.SelectedIndex = FindMenuIndex(
                    kinds,
                    SpreadsheetAutoFilterMenuKind.Custom);
                await GetPrivateField<Task>(presenter, "_operationTail");
                Assert.AreEqual(
                    System.Windows.Visibility.Visible,
                    GetPrivateField<System.Windows.UIElement>(presenter, "_customConditionPanel")
                        .Visibility);
                Assert.IsNotNull(GetPrivateField<System.Windows.Controls.TextBox>(
                    presenter,
                    "_secondCriterionInput"));
            }
            finally
            {
                presenter.Close();
                window.Close();
            }
        });

        await RunOnWinFormsThreadAsync(async formControl =>
        {
            var fixture = CreateDateFixture();
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
            control.Focus();
            System.Windows.Forms.Application.DoEvents();
            Assert.IsTrue(presenter.TryOpenForActiveCell());
            var binding = GetPrivateField<NeraWinFormsAutoFilterPagedBinding>(
                presenter,
                "_binding");
            await binding.InitializeAsync();
            InvokePrivate(presenter, "RebuildPage");
            var kinds = GetPrivateField<System.Windows.Forms.ComboBox>(
                presenter,
                "_menuKindBox");
            kinds.SelectedIndex = FindMenuIndex(
                kinds,
                SpreadsheetAutoFilterMenuKind.Date);
            await GetPrivateField<Task>(presenter, "_operationTail");
            var datePage = GetPrivateField<SpreadsheetAutoFilterDatePage>(
                presenter,
                "_datePage");
            var values = GetPrivateField<System.Windows.Forms.CheckedListBox>(
                presenter,
                "_valuesList");
            Assert.AreEqual(2, datePage.TotalNodeCount);
            Assert.AreEqual(2, values.Items.Count);

            kinds.SelectedIndex = FindMenuIndex(
                kinds,
                SpreadsheetAutoFilterMenuKind.Custom);
            await GetPrivateField<Task>(presenter, "_operationTail");
            Assert.IsTrue(GetPrivateField<System.Windows.Forms.TextBox>(
                presenter,
                "_secondCriterionInput").Visible);
            Assert.IsTrue(GetPrivateField<System.Windows.Forms.ComboBox>(
                presenter,
                "_conditionJoinBox").Visible);
        });
    }

    [TestMethod]
    [Timeout(60_000)]
    public async Task WinFormsHeaderButtonPoolRemainsBoundedWhileScrolling()
    {
        await RunOnWinFormsThreadAsync(formControl =>
        {
            var workbook = new Workbook();
            var worksheet = workbook.Worksheets[0];
            const int lastColumn = 249;
            worksheet.SetValue(new CellAddress(0, 0), "First");
            worksheet.SetValue(new CellAddress(0, lastColumn), "Last");
            worksheet.SetValue(new CellAddress(1, lastColumn), "Value");
            worksheet.SetAutoFilter(new WorksheetAutoFilter(
                new CellRange(
                    new CellAddress(0, 0),
                    new CellAddress(1, lastColumn))));
            formControl.Size = new System.Drawing.Size(520, 320);
            using var control = new NeraSpreadSheet.WinForms.NeraSpreadsheetControl
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                Workbook = workbook,
            };
            formControl.Controls.Add(control);
            control.CreateControl();
            using var presenter = new NeraAutoFilterPagedDropDownPresenter(control);

            presenter.Refresh();
            System.Windows.Forms.Application.DoEvents();
            var buttons = GetPrivateField<System.Collections.IDictionary>(
                presenter,
                "_buttons");
            Assert.IsGreaterThan(0, buttons.Count);
            Assert.IsLessThanOrEqualTo(10, buttons.Count);

            control.ScrollTo(12_000d, 0d);
            presenter.Refresh();
            System.Windows.Forms.Application.DoEvents();
            Assert.IsGreaterThan(0, buttons.Count);
            Assert.IsLessThanOrEqualTo(10, buttons.Count);
            Assert.AreEqual(
                buttons.Count,
                control.Controls.OfType<System.Windows.Forms.Button>()
                    .Count(static button => button.Text == "▼"));
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

    private static Fixture CreateFixture(int valueCount = 5)
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var tableId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        worksheet.SetValue(new CellAddress(0, 0), "Value");
        for (var index = 0; index < valueCount; index++)
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
                new CellAddress(valueCount, 0)),
            [new SpreadsheetTableColumn(columnId, "Value")]));
        var session = new SpreadsheetSession(workbook);
        session.Selection.SetActiveCell(new CellAddress(1, 0));
        Assert.IsTrue(session.TryResolveActiveAutoFilterTarget(
            out var target));
        return new Fixture(session, target);
    }

    private static Fixture CreateDateFixture()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var columnId = Guid.NewGuid();
        worksheet.SetValue(new CellAddress(0, 0), "Date");
        worksheet.SetValue(new CellAddress(1, 0), new DateTime(2025, 1, 2));
        worksheet.SetValue(new CellAddress(2, 0), new DateTime(2026, 3, 4));
        worksheet.AddTable(new SpreadsheetTable(
            Guid.NewGuid(),
            "Dates",
            new CellRange(new CellAddress(0, 0), new CellAddress(2, 0)),
            [new SpreadsheetTableColumn(columnId, "Date")]));
        var session = new SpreadsheetSession(workbook);
        session.Selection.SetActiveCell(new CellAddress(1, 0));
        Assert.IsTrue(session.TryResolveActiveAutoFilterTarget(out var target));
        return new Fixture(session, target);
    }

    private static int FindMenuIndex(
        System.Windows.Controls.ComboBox comboBox,
        SpreadsheetAutoFilterMenuKind kind)
    {
        var expected = kind.GetDefaultDisplayName();
        for (var index = 0; index < comboBox.Items.Count; index++)
        {
            if (string.Equals(comboBox.Items[index]?.ToString(), expected, StringComparison.Ordinal))
            {
                return index;
            }
        }
        throw new AssertFailedException(
            $"Menu '{expected}' was not found. Actual: {string.Join(", ", comboBox.Items.Cast<object>())}.");
    }

    private static int FindMenuIndex(
        System.Windows.Forms.ComboBox comboBox,
        SpreadsheetAutoFilterMenuKind kind)
    {
        var expected = kind.GetDefaultDisplayName();
        for (var index = 0; index < comboBox.Items.Count; index++)
        {
            if (string.Equals(comboBox.Items[index]?.ToString(), expected, StringComparison.Ordinal))
            {
                return index;
            }
        }
        throw new AssertFailedException(
            $"Menu '{expected}' was not found. Actual: {string.Join(", ", comboBox.Items.Cast<object>())}.");
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
        where T : class =>
        (T)(instance.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance)
            ?? throw new AssertFailedException(
                $"Field '{fieldName}' was not initialized."));

    private static IEnumerable<System.Windows.DependencyObject> EnumerateWpfElements(
        System.Windows.DependencyObject root)
    {
        yield return root;
        foreach (var child in System.Windows.LogicalTreeHelper.GetChildren(root)
                     .OfType<System.Windows.DependencyObject>())
        {
            foreach (var descendant in EnumerateWpfElements(child))
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<System.Windows.Forms.Control> EnumerateWinFormsControls(
        System.Windows.Forms.Control root)
    {
        yield return root;
        foreach (System.Windows.Forms.Control child in root.Controls)
        {
            foreach (var descendant in EnumerateWinFormsControls(child))
            {
                yield return descendant;
            }
        }
    }

    private static void InvokePrivate(
        object instance,
        string methodName,
        params object?[] arguments) =>
        (instance.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertFailedException(
                $"Method '{methodName}' was not found."))
        .Invoke(instance, arguments);

    private static bool RaiseWpfPreviewKey(
        System.Windows.UIElement target,
        System.Windows.Input.Key key)
    {
        var source = System.Windows.PresentationSource.FromVisual(target) ??
            throw new AssertFailedException(
                "The WPF AutoFilter popup did not have a presentation source.");
        var args = new System.Windows.Input.KeyEventArgs(
            System.Windows.Input.Keyboard.PrimaryDevice,
            source,
            Environment.TickCount,
            key)
        {
            RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent,
        };
        target.RaiseEvent(args);
        return args.Handled;
    }

    private sealed record Fixture(
        SpreadsheetSession Session,
        SpreadsheetAutoFilterTarget Target);
}
