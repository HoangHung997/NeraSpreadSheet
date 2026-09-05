using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Viewport;
using WinFormsApplication = System.Windows.Forms.Application;
using WinFormsButton = System.Windows.Forms.Button;
using WinFormsControl = NeraSpreadSheet.WinForms.NeraSpreadsheetControl;
using WinFormsForm = System.Windows.Forms.Form;
using WinFormsFormStartPosition = System.Windows.Forms.FormStartPosition;
using WinFormsPresenter = NeraSpreadSheet.WinForms.NeraTableFilterDropDownPresenter;
using WpfAdornerDecorator = System.Windows.Documents.AdornerDecorator;
using WpfControl = NeraSpreadSheet.Wpf.NeraSpreadsheetControl;
using WpfDispatcher = System.Windows.Threading.Dispatcher;
using WpfDispatcherFrame = System.Windows.Threading.DispatcherFrame;
using WpfDispatcherPriority = System.Windows.Threading.DispatcherPriority;
using WpfDispatcherTimer = System.Windows.Threading.DispatcherTimer;
using WpfPresenter = NeraSpreadSheet.Wpf.NeraTableFilterPopupPresenter;
using WpfWindow = System.Windows.Window;
using WpfWindowStartupLocation = System.Windows.WindowStartupLocation;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class DesktopTableFilterPresenterSmokeTests
{
    private static readonly TimeSpan StaTimeout = TimeSpan.FromSeconds(90d);

    [TestMethod]
    [Timeout(120_000)]
    public void WpfPresenterOpensNativePopupFromRenderedTableHeaderButton()
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
            var window = new WpfWindow
            {
                Content = new WpfAdornerDecorator
                {
                    Child = control,
                },
                Width = 540d,
                Height = 320d,
                Left = -32_000d,
                Top = -32_000d,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStartupLocation = WpfWindowStartupLocation.Manual,
            };
            using var presenter = new WpfPresenter(control);
            try
            {
                window.Show();
                window.UpdateLayout();
                PumpFor(TimeSpan.FromMilliseconds(100d));

                var session = control.Session ??
                    throw new AssertFailedException(
                        "The WPF spreadsheet session was not created.");
                var chrome = SpreadsheetChromeGeometry.Calculate(
                    control.ActualWidth,
                    control.ActualHeight,
                    control.RenderTheme);
                var viewport = new SpreadsheetViewportEngine(session);
                var scroll = control.ScrollSnapshot;
                var frame = viewport.Compose(
                    scroll.OffsetX,
                    scroll.OffsetY,
                    chrome.BodyWidth,
                    chrome.BodyHeight,
                    overscan: 0d,
                    control.RenderTheme);
                var buttons = SpreadsheetTableFilterButtonGeometry
                    .GetVisibleButtons(
                        WorksheetSnapshot.Capture(session.ActiveWorksheet),
                        frame.Layout,
                        control.RenderTheme);

                Assert.AreEqual(2, buttons.Count);
                var first = buttons[0];
                var x = chrome.RowHeaderWidth +
                        first.Bounds.X +
                        (first.Bounds.Width / 2d);
                var y = chrome.ColumnHeaderHeight +
                        first.Bounds.Y +
                        (first.Bounds.Height / 2d);

                Assert.IsTrue(presenter.TryOpenAt(x, y));
                PumpFor(TimeSpan.FromMilliseconds(60d));
                Assert.IsTrue(presenter.IsOpen);

                presenter.Close();
                PumpFor(TimeSpan.FromMilliseconds(40d));
                Assert.IsFalse(presenter.IsOpen);
            }
            finally
            {
                window.Close();
                PumpFor(TimeSpan.FromMilliseconds(40d));
            }
        });
    }

    [TestMethod]
    [Timeout(120_000)]
    public void WinFormsPresenterCreatesVisibleHeaderButtonsAndOpensDropDown()
    {
        RunInSta(() =>
        {
            var workbook = CreateWorkbook();
            using var form = new WinFormsForm
            {
                ShowInTaskbar = false,
                StartPosition = WinFormsFormStartPosition.Manual,
                Location = new System.Drawing.Point(-32_000, -32_000),
                ClientSize = new System.Drawing.Size(520, 280),
            };
            using var control = new WinFormsControl
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                Workbook = workbook,
            };
            form.Controls.Add(control);
            form.Show();
            WinFormsApplication.DoEvents();

            using var presenter = new WinFormsPresenter(control);
            presenter.Refresh();
            control.Refresh();
            WinFormsApplication.DoEvents();

            var buttons = control.Controls
                .OfType<WinFormsButton>()
                .Where(static button =>
                    button.Visible && button.Text == "▼")
                .ToArray();
            Assert.AreEqual(2, buttons.Length);

            buttons[0].PerformClick();
            WinFormsApplication.DoEvents();
            Assert.IsTrue(presenter.IsOpen);

            presenter.Close();
            WinFormsApplication.DoEvents();
            Assert.IsFalse(presenter.IsOpen);

            form.Close();
            WinFormsApplication.DoEvents();
        });
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
        worksheet.AddTable(new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(2, 1)),
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
            Name = "Nera desktop Table-filter presenter smoke",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(StaTimeout))
        {
            Assert.Fail("The desktop Table-filter presenter smoke timed out.");
        }
        failure?.Throw();
    }
}
