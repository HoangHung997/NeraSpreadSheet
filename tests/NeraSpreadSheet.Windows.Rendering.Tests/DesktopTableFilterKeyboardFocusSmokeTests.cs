using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using WinFormsApplication = System.Windows.Forms.Application;
using WinFormsBaseControl = System.Windows.Forms.Control;
using WinFormsButton = System.Windows.Forms.Button;
using WinFormsCheckedListBox = System.Windows.Forms.CheckedListBox;
using WinFormsControl = NeraSpreadSheet.WinForms.NeraSpreadsheetControl;
using WinFormsDockStyle = System.Windows.Forms.DockStyle;
using WinFormsForm = System.Windows.Forms.Form;
using WinFormsFormStartPosition = System.Windows.Forms.FormStartPosition;
using WinFormsKeyEventArgs = System.Windows.Forms.KeyEventArgs;
using WinFormsKeys = System.Windows.Forms.Keys;
using WinFormsPanel = System.Windows.Forms.Panel;
using WinFormsPresenter = NeraSpreadSheet.WinForms.NeraTableFilterDropDownPresenter;
using WinFormsTextBox = System.Windows.Forms.TextBox;
using WpfAdornerDecorator = System.Windows.Documents.AdornerDecorator;
using WpfAutomationProperties = System.Windows.Automation.AutomationProperties;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfControl = NeraSpreadSheet.Wpf.NeraSpreadsheetControl;
using WpfDispatcher = System.Windows.Threading.Dispatcher;
using WpfDispatcherFrame = System.Windows.Threading.DispatcherFrame;
using WpfDispatcherPriority = System.Windows.Threading.DispatcherPriority;
using WpfDispatcherTimer = System.Windows.Threading.DispatcherTimer;
using WpfDockPanel = System.Windows.Controls.DockPanel;
using WpfKey = System.Windows.Input.Key;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfKeyboard = System.Windows.Input.Keyboard;
using WpfPresentationSource = System.Windows.PresentationSource;
using WpfPresenter = NeraSpreadSheet.Wpf.NeraTableFilterPopupPresenter;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfUIElement = System.Windows.UIElement;
using WpfWindow = System.Windows.Window;
using WpfWindowStartupLocation = System.Windows.WindowStartupLocation;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class DesktopTableFilterKeyboardFocusSmokeTests
{
    private static readonly TimeSpan StaTimeout = TimeSpan.FromSeconds(90d);

    [TestMethod]
    [Timeout(120_000)]
    public void WpfPresenterNavigatesTogglesClosesAndRestoresFocus()
    {
        RunInSta(() =>
        {
            var workbook = CreateWorkbook();
            var control = new WpfControl
            {
                Workbook = workbook,
                Width = 520d,
                Height = 280d,
            };
            var focusTarget = new WpfButton
            {
                Content = "Focus return target",
                Height = 28d,
            };
            WpfDockPanel.SetDock(focusTarget, System.Windows.Controls.Dock.Top);
            var root = new WpfDockPanel();
            root.Children.Add(focusTarget);
            root.Children.Add(new WpfAdornerDecorator
            {
                Child = control,
            });
            var window = new WpfWindow
            {
                Content = root,
                Width = 540d,
                Height = 340d,
                Left = -32_000d,
                Top = -32_000d,
                ShowInTaskbar = false,
                WindowStartupLocation = WpfWindowStartupLocation.Manual,
            };
            using var presenter = new WpfPresenter(control);
            try
            {
                window.Show();
                window.Activate();
                window.UpdateLayout();
                Assert.IsTrue(focusTarget.Focus());
                PumpFor(TimeSpan.FromMilliseconds(100d));

                var session = control.Session ??
                    throw new AssertFailedException(
                        "The WPF spreadsheet session was not created.");
                session.Selection.SetActiveCell(new CellAddress(1, 0));

                Assert.IsTrue(presenter.TryOpenForActiveCell());
                PumpFor(TimeSpan.FromMilliseconds(120d));
                Assert.IsTrue(presenter.IsOpen);

                var search = GetPrivateField<WpfTextBox>(
                    presenter,
                    "_searchBox");
                Assert.IsTrue(search.IsKeyboardFocusWithin);
                Assert.AreEqual(
                    "NeraTableFilterSearch",
                    WpfAutomationProperties.GetAutomationId(search));
                Assert.IsFalse(string.IsNullOrWhiteSpace(
                    WpfAutomationProperties.GetName(search)));

                RaiseWpfPreviewKey(search, WpfKey.Down);
                PumpFor(TimeSpan.FromMilliseconds(40d));
                var values = GetPrivateField<List<WpfCheckBox>>(
                    presenter,
                    "_valueCheckBoxes");
                Assert.IsTrue(values.Count >= 2);
                Assert.IsTrue(values[0].IsKeyboardFocusWithin);
                var selectedBefore = values[0].IsChecked;

                RaiseWpfPreviewKey(values[0], WpfKey.Space);
                PumpFor(TimeSpan.FromMilliseconds(80d));
                values = GetPrivateField<List<WpfCheckBox>>(
                    presenter,
                    "_valueCheckBoxes");
                Assert.AreNotEqual(
                    selectedBefore,
                    values[0].IsChecked);

                RaiseWpfPreviewKey(values[0], WpfKey.Escape);
                PumpFor(TimeSpan.FromMilliseconds(100d));
                Assert.IsFalse(presenter.IsOpen);
                Assert.IsTrue(focusTarget.IsKeyboardFocused);

                Assert.IsTrue(presenter.TryOpenForActiveCell());
                PumpFor(TimeSpan.FromMilliseconds(100d));
                var popup = GetPrivateField<System.Windows.Controls.Primitives.Popup>(
                    presenter,
                    "_popup");
                var commandButtons = EnumerateWpfElements(popup.Child)
                    .OfType<WpfButton>()
                    .ToArray();
                var automationIds = commandButtons
                    .Select(WpfAutomationProperties.GetAutomationId)
                    .ToArray();
                CollectionAssert.Contains(automationIds, "NeraTableFilterSortAscending");
                CollectionAssert.Contains(automationIds, "NeraTableFilterSortDescending");
                CollectionAssert.Contains(automationIds, "NeraTableFilterReapply");
                CollectionAssert.Contains(automationIds, "NeraTableFilterClearSort");
                var descending = commandButtons
                    .Single(button => WpfAutomationProperties.GetAutomationId(button) ==
                        "NeraTableFilterSortDescending");
                descending.RaiseEvent(new System.Windows.RoutedEventArgs(
                    WpfButton.ClickEvent));
                PumpFor(TimeSpan.FromMilliseconds(80d));
                Assert.IsFalse(presenter.IsOpen);
                Assert.AreEqual(
                    "Pending",
                    workbook.Worksheets[0].GetValue(new CellAddress(1, 0)));
            }
            finally
            {
                presenter.Close();
                window.Close();
                PumpFor(TimeSpan.FromMilliseconds(40d));
            }
        });
    }

    [TestMethod]
    [Timeout(120_000)]
    public void WinFormsPresenterNavigatesTogglesClosesAndRestoresButtonFocus()
    {
        RunInSta(() =>
        {
            var workbook = CreateWorkbook();
            using var form = new WinFormsForm
            {
                ShowInTaskbar = false,
                StartPosition = WinFormsFormStartPosition.Manual,
                Location = new System.Drawing.Point(-32_000, -32_000),
                ClientSize = new System.Drawing.Size(520, 320),
            };
            using var control = new WinFormsControl
            {
                Dock = WinFormsDockStyle.Fill,
                Workbook = workbook,
            };
            var root = new WinFormsPanel
            {
                Dock = WinFormsDockStyle.Fill,
            };
            root.Controls.Add(control);
            form.Controls.Add(root);
            form.Show();
            WinFormsApplication.DoEvents();

            using var presenter = new WinFormsPresenter(control);
            presenter.Refresh();
            control.Refresh();
            WinFormsApplication.DoEvents();
            var session = control.Session ??
                throw new AssertFailedException(
                    "The WinForms spreadsheet session was not created.");
            session.Selection.SetActiveCell(new CellAddress(1, 0));

            Assert.IsTrue(presenter.TryOpenForActiveCell());
            WinFormsApplication.DoEvents();
            Assert.IsTrue(presenter.IsOpen);

            var search = GetPrivateField<WinFormsTextBox>(
                presenter,
                "_searchBox");
            var values = GetPrivateField<WinFormsCheckedListBox>(
                presenter,
                "_valuesList");
            Assert.IsTrue(search.Focused);
            Assert.IsFalse(string.IsNullOrWhiteSpace(
                search.AccessibleName));

            RaiseWinFormsKey(search, WinFormsKeys.Down);
            WinFormsApplication.DoEvents();
            Assert.IsTrue(values.Focused);
            Assert.IsTrue(values.Items.Count >= 2);
            var selectedBefore = values.GetItemChecked(0);

            RaiseWinFormsKey(values, WinFormsKeys.Space);
            WinFormsApplication.DoEvents();
            Assert.AreNotEqual(
                selectedBefore,
                values.GetItemChecked(0));

            RaiseWinFormsKey(values, WinFormsKeys.Escape);
            WinFormsApplication.DoEvents();
            Assert.IsFalse(presenter.IsOpen);
            var filterButtons = control.Controls
                .OfType<WinFormsButton>()
                .Where(static button =>
                    button.Visible && button.Text == "▼")
                .ToArray();
            Assert.AreEqual(2, filterButtons.Length);
            Assert.IsTrue(filterButtons.Any(static button => button.Focused));
            Assert.IsTrue(filterButtons.All(static button =>
                !string.IsNullOrWhiteSpace(button.AccessibleName)));

            Assert.IsTrue(presenter.TryOpenForActiveCell());
            WinFormsApplication.DoEvents();
            var dropDown = GetPrivateField<System.Windows.Forms.ToolStripDropDown>(
                presenter,
                "_dropDown");
            var panel = ((System.Windows.Forms.ToolStripControlHost)dropDown.Items[0]).Control;
            var commandButtons = EnumerateWinFormsControls(panel)
                .OfType<WinFormsButton>()
                .ToArray();
            var accessibleNames = commandButtons
                .Select(static button => button.AccessibleName)
                .ToArray();
            CollectionAssert.Contains(accessibleNames, "Sắp xếp tăng dần");
            CollectionAssert.Contains(accessibleNames, "Sắp xếp giảm dần");
            CollectionAssert.Contains(accessibleNames, "Áp dụng lại");
            CollectionAssert.Contains(accessibleNames, "Xóa SX");
            var descending = commandButtons
                .Single(static button => button.AccessibleName == "Sắp xếp giảm dần");
            descending.PerformClick();
            WinFormsApplication.DoEvents();
            Assert.IsFalse(presenter.IsOpen);
            Assert.AreEqual(
                "Pending",
                workbook.Worksheets[0].GetValue(new CellAddress(1, 0)));

            form.Close();
            WinFormsApplication.DoEvents();
        });
    }

    private static T GetPrivateField<T>(object target, string fieldName)
        where T : class
    {
        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new AssertFailedException(
                $"Private field '{fieldName}' was not found.");
        return field.GetValue(target) as T ??
               throw new AssertFailedException(
                   $"Private field '{fieldName}' did not contain {typeof(T).Name}.");
    }

    private static void RaiseWpfPreviewKey(
        WpfUIElement target,
        WpfKey key)
    {
        var source = WpfPresentationSource.FromVisual(target) ??
            throw new AssertFailedException(
                "The WPF filter presenter did not have a presentation source.");
        var args = new WpfKeyEventArgs(
            WpfKeyboard.PrimaryDevice,
            source,
            Environment.TickCount,
            key)
        {
            RoutedEvent = WpfKeyboard.PreviewKeyDownEvent,
        };
        target.RaiseEvent(args);
        Assert.IsTrue(args.Handled);
    }

    private static void RaiseWinFormsKey(
        WinFormsBaseControl target,
        WinFormsKeys key)
    {
        var method = typeof(WinFormsBaseControl).GetMethod(
            "OnKeyDown",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new AssertFailedException(
                "System.Windows.Forms.Control.OnKeyDown was not found.");
        var args = new WinFormsKeyEventArgs(key);
        method.Invoke(target, [args]);
        Assert.IsTrue(args.Handled);
        Assert.IsTrue(args.SuppressKeyPress);
    }

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

    private static IEnumerable<WinFormsBaseControl> EnumerateWinFormsControls(
        WinFormsBaseControl root)
    {
        yield return root;
        foreach (WinFormsBaseControl child in root.Controls)
        {
            foreach (var descendant in EnumerateWinFormsControls(child))
            {
                yield return descendant;
            }
        }
    }

    private static Workbook CreateWorkbook()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var statusColumnId = Guid.NewGuid();
        var amountColumnId = Guid.NewGuid();
        worksheet.SetValue(new CellAddress(0, 0), "Status");
        worksheet.SetValue(new CellAddress(0, 1), "Amount");
        worksheet.SetValue(new CellAddress(1, 0), "Open");
        worksheet.SetValue(new CellAddress(1, 1), 10d);
        worksheet.SetValue(new CellAddress(2, 0), "Closed");
        worksheet.SetValue(new CellAddress(2, 1), 20d);
        worksheet.SetValue(new CellAddress(3, 0), "Pending");
        worksheet.SetValue(new CellAddress(3, 1), 30d);
        worksheet.AddTable(new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(3, 1)),
            [
                new SpreadsheetTableColumn(statusColumnId, "Status"),
                new SpreadsheetTableColumn(amountColumnId, "Amount"),
            ]));
        return workbook;
    }

    private static void PumpFor(TimeSpan duration)
    {
        var dispatcher = WpfDispatcher.CurrentDispatcher;
        var frame = new WpfDispatcherFrame();
        var timer = new WpfDispatcherTimer(
            WpfDispatcherPriority.Background,
            dispatcher)
        {
            Interval = duration,
        };
        EventHandler? handler = null;
        handler = (_, _) =>
        {
            timer.Stop();
            timer.Tick -= handler;
            frame.Continue = false;
        };
        timer.Tick += handler;
        timer.Start();
        WpfDispatcher.PushFrame(frame);
    }

    private static void RunInSta(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        })
        {
            IsBackground = true,
            Name = "Nera desktop Table-filter keyboard focus smoke",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(StaTimeout))
        {
            Assert.Fail("The desktop Table-filter keyboard smoke timed out.");
        }
        failure?.Throw();
    }
}
