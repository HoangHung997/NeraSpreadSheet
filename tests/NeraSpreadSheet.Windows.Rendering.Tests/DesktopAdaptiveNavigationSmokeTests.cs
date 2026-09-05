using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using WinFormsApplication = System.Windows.Forms.Application;
using WinFormsBaseControl = System.Windows.Forms.Control;
using WinFormsControl = NeraSpreadSheet.WinForms.NeraSpreadsheetControl;
using WinFormsDockStyle = System.Windows.Forms.DockStyle;
using WinFormsForm = System.Windows.Forms.Form;
using WinFormsFormStartPosition = System.Windows.Forms.FormStartPosition;
using WinFormsKeyEventArgs = System.Windows.Forms.KeyEventArgs;
using WinFormsKeys = System.Windows.Forms.Keys;
using WpfAdornerDecorator = System.Windows.Documents.AdornerDecorator;
using WpfControl = NeraSpreadSheet.Wpf.NeraSpreadsheetControl;
using WpfDispatcher = System.Windows.Threading.Dispatcher;
using WpfDispatcherFrame = System.Windows.Threading.DispatcherFrame;
using WpfDispatcherPriority = System.Windows.Threading.DispatcherPriority;
using WpfDispatcherTimer = System.Windows.Threading.DispatcherTimer;
using WpfKey = System.Windows.Input.Key;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfKeyboard = System.Windows.Input.Keyboard;
using WpfPresentationSource = System.Windows.PresentationSource;
using WpfWindow = System.Windows.Window;
using WpfWindowStartupLocation = System.Windows.WindowStartupLocation;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class DesktopAdaptiveNavigationSmokeTests
{
    private static readonly TimeSpan StaTimeout = TimeSpan.FromSeconds(75d);

    [TestMethod]
    [Timeout(105_000)]
    public void WpfArrowNavigationKeepsActiveCellVisibleAndRetainsScrollableTail()
    {
        RunInSta(() =>
        {
            var workbook = new Workbook();
            workbook.Worksheets[0].SetValue(default, "used");
            using var control = new WpfControl
            {
                Workbook = workbook,
                UseAdaptiveNavigationExtent = true,
            };
            var window = new WpfWindow
            {
                Content = new WpfAdornerDecorator { Child = control },
                Width = 280d,
                Height = 180d,
                Left = -32_000d,
                Top = -32_000d,
                ShowInTaskbar = false,
                WindowStartupLocation = WpfWindowStartupLocation.Manual,
            };
            try
            {
                window.Show();
                window.UpdateLayout();
                PumpFor(TimeSpan.FromMilliseconds(100d));
                Assert.IsTrue(control.Focus());
                Assert.IsGreaterThan(control.ActualWidth, control.ContentWidth);
                Assert.IsGreaterThan(control.ActualHeight, control.ContentHeight);

                control.ScrollTo(120d, 120d);
                Assert.AreEqual(new CellAddress(0, 0), control.Session!.Selection.ActiveCell);
                Assert.AreEqual(120d, control.ScrollSnapshot.OffsetX, 1e-9);
                Assert.AreEqual(120d, control.ScrollSnapshot.OffsetY, 1e-9);
                control.ScrollTo(0d, 0d);

                for (var index = 0; index < 5; index++)
                {
                    RaiseWpfKey(control, WpfKey.Right);
                }

                Assert.AreEqual(
                    new CellAddress(0, 5),
                    control.Session!.Selection.ActiveCell);
                Assert.IsGreaterThan(0d, control.ScrollSnapshot.OffsetX);

                for (var index = 0; index < 5; index++)
                {
                    RaiseWpfKey(control, WpfKey.Left);
                }

                Assert.AreEqual(
                    new CellAddress(0, 0),
                    control.Session.Selection.ActiveCell);
                Assert.AreEqual(0d, control.ScrollSnapshot.OffsetX, 1e-9);

                for (var index = 0; index < 10; index++)
                {
                    RaiseWpfKey(control, WpfKey.Down);
                }
                Assert.AreEqual(
                    new CellAddress(10, 0),
                    control.Session.Selection.ActiveCell);
                Assert.IsGreaterThan(0d, control.ScrollSnapshot.OffsetY);

                for (var index = 0; index < 10; index++)
                {
                    RaiseWpfKey(control, WpfKey.Up);
                }
                Assert.AreEqual(
                    new CellAddress(0, 0),
                    control.Session.Selection.ActiveCell);
                Assert.AreEqual(0d, control.ScrollSnapshot.OffsetY, 1e-9);
            }
            finally
            {
                window.Close();
                PumpFor(TimeSpan.FromMilliseconds(40d));
            }
        });
    }

    [TestMethod]
    [Timeout(105_000)]
    public void WinFormsArrowNavigationKeepsActiveCellVisibleAndRetainsScrollableTail()
    {
        RunInSta(() =>
        {
            var workbook = new Workbook();
            workbook.Worksheets[0].SetValue(default, "used");
            using var form = new WinFormsForm
            {
                ShowInTaskbar = false,
                StartPosition = WinFormsFormStartPosition.Manual,
                Location = new System.Drawing.Point(-32_000, -32_000),
                ClientSize = new System.Drawing.Size(280, 180),
            };
            using var control = new WinFormsControl
            {
                Dock = WinFormsDockStyle.Fill,
                Workbook = workbook,
                UseAdaptiveNavigationExtent = true,
            };
            form.Controls.Add(control);
            form.Show();
            WinFormsApplication.DoEvents();
            Assert.IsGreaterThan(control.ClientSize.Width, control.ContentWidth);
            Assert.IsGreaterThan(control.ClientSize.Height, control.ContentHeight);

            control.ScrollTo(120d, 120d);
            Assert.AreEqual(new CellAddress(0, 0), control.Session!.Selection.ActiveCell);
            Assert.AreEqual(120d, control.ScrollSnapshot.OffsetX, 1e-9);
            Assert.AreEqual(120d, control.ScrollSnapshot.OffsetY, 1e-9);
            control.ScrollTo(0d, 0d);

            for (var index = 0; index < 5; index++)
            {
                RaiseWinFormsKey(control, WinFormsKeys.Right);
            }

            Assert.AreEqual(
                new CellAddress(0, 5),
                control.Session!.Selection.ActiveCell);
            Assert.IsGreaterThan(0d, control.ScrollSnapshot.OffsetX);

            for (var index = 0; index < 5; index++)
            {
                RaiseWinFormsKey(control, WinFormsKeys.Left);
            }

            Assert.AreEqual(
                new CellAddress(0, 0),
                control.Session.Selection.ActiveCell);
            Assert.AreEqual(0d, control.ScrollSnapshot.OffsetX, 1e-9);

            for (var index = 0; index < 10; index++)
            {
                RaiseWinFormsKey(control, WinFormsKeys.Down);
            }
            Assert.AreEqual(
                new CellAddress(10, 0),
                control.Session.Selection.ActiveCell);
            Assert.IsGreaterThan(0d, control.ScrollSnapshot.OffsetY);

            for (var index = 0; index < 10; index++)
            {
                RaiseWinFormsKey(control, WinFormsKeys.Up);
            }
            Assert.AreEqual(
                new CellAddress(0, 0),
                control.Session.Selection.ActiveCell);
            Assert.AreEqual(0d, control.ScrollSnapshot.OffsetY, 1e-9);
        });
    }

    [TestMethod]
    [Timeout(105_000)]
    public void WpfArrowNavigationShouldSkipHiddenRowsAndColumns()
    {
        RunInSta(() =>
        {
            var workbook = new Workbook();
            workbook.Worksheets[0].Dimensions.HideRows(1, 3);
            workbook.Worksheets[0].Dimensions.HideColumns(1, 2);
            using var control = new WpfControl { Workbook = workbook };
            var window = new WpfWindow
            {
                Content = new WpfAdornerDecorator { Child = control },
                Width = 280d,
                Height = 180d,
                Left = -32_000d,
                Top = -32_000d,
                ShowInTaskbar = false,
                WindowStartupLocation = WpfWindowStartupLocation.Manual,
            };
            try
            {
                window.Show();
                window.UpdateLayout();
                PumpFor(TimeSpan.FromMilliseconds(100d));
                Assert.IsTrue(control.Focus());

                RaiseWpfKey(control, WpfKey.Right);
                Assert.AreEqual(
                    new CellAddress(0, 3),
                    control.Session!.Selection.ActiveCell);
                RaiseWpfKey(control, WpfKey.Down);
                Assert.AreEqual(
                    new CellAddress(4, 3),
                    control.Session.Selection.ActiveCell);
                RaiseWpfKey(control, WpfKey.Left);
                Assert.AreEqual(
                    new CellAddress(4, 0),
                    control.Session.Selection.ActiveCell);
                RaiseWpfKey(control, WpfKey.Up);
                Assert.AreEqual(
                    new CellAddress(0, 0),
                    control.Session.Selection.ActiveCell);
            }
            finally
            {
                window.Close();
                PumpFor(TimeSpan.FromMilliseconds(40d));
            }
        });
    }

    [TestMethod]
    [Timeout(105_000)]
    public void WinFormsArrowNavigationShouldSkipHiddenRowsAndColumns()
    {
        RunInSta(() =>
        {
            var workbook = new Workbook();
            workbook.Worksheets[0].Dimensions.HideRows(1, 3);
            workbook.Worksheets[0].Dimensions.HideColumns(1, 2);
            using var form = new WinFormsForm
            {
                ShowInTaskbar = false,
                StartPosition = WinFormsFormStartPosition.Manual,
                Location = new System.Drawing.Point(-32_000, -32_000),
                ClientSize = new System.Drawing.Size(280, 180),
            };
            using var control = new WinFormsControl
            {
                Dock = WinFormsDockStyle.Fill,
                Workbook = workbook,
            };
            form.Controls.Add(control);
            form.Show();
            WinFormsApplication.DoEvents();

            RaiseWinFormsKey(control, WinFormsKeys.Right);
            Assert.AreEqual(
                new CellAddress(0, 3),
                control.Session!.Selection.ActiveCell);
            RaiseWinFormsKey(control, WinFormsKeys.Down);
            Assert.AreEqual(
                new CellAddress(4, 3),
                control.Session.Selection.ActiveCell);
            RaiseWinFormsKey(control, WinFormsKeys.Left);
            Assert.AreEqual(
                new CellAddress(4, 0),
                control.Session.Selection.ActiveCell);
            RaiseWinFormsKey(control, WinFormsKeys.Up);
            Assert.AreEqual(
                new CellAddress(0, 0),
                control.Session.Selection.ActiveCell);
        });
    }

    private static void RaiseWpfKey(WpfControl target, WpfKey key)
    {
        var source = WpfPresentationSource.FromVisual(target) ??
            throw new AssertFailedException(
                "The WPF spreadsheet did not have a presentation source.");
        var args = new WpfKeyEventArgs(
            WpfKeyboard.PrimaryDevice,
            source,
            Environment.TickCount,
            key)
        {
            RoutedEvent = WpfKeyboard.KeyDownEvent,
        };
        target.RaiseEvent(args);
        Assert.IsTrue(args.Handled);
    }

    private static void RaiseWinFormsKey(
        WinFormsBaseControl target,
        WinFormsKeys key)
    {
        var method = target.GetType().GetMethod(
            "OnKeyDown",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new AssertFailedException(
                $"{target.GetType().FullName}.OnKeyDown was not found.");
        var args = new WinFormsKeyEventArgs(key);
        method.Invoke(target, [args]);
        Assert.IsTrue(args.Handled);
        Assert.IsTrue(args.SuppressKeyPress);
        WinFormsApplication.DoEvents();
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
            Name = "Nera desktop adaptive navigation smoke",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(StaTimeout))
        {
            Assert.Fail("The adaptive navigation smoke timed out.");
        }
        failure?.Throw();
    }
}
