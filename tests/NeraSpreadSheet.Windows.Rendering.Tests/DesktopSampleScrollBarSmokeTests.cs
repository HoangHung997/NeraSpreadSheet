using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.WinForms;
using NeraSpreadSheet.Wpf;
using SampleWinFormsMainForm = NeraSpreadSheet.WinForms.Sample.MainForm;
using SampleWpfMainWindow = NeraSpreadSheet.Wpf.Sample.MainWindow;
using WinFormsApplication = System.Windows.Forms.Application;
using WinFormsToolStrip = System.Windows.Forms.ToolStrip;
using WinFormsToolStripButton = System.Windows.Forms.ToolStripButton;
using WpfButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using WpfDispatcher = System.Windows.Threading.Dispatcher;
using WpfDispatcherFrame = System.Windows.Threading.DispatcherFrame;
using WpfDispatcherPriority = System.Windows.Threading.DispatcherPriority;
using WpfDispatcherTimer = System.Windows.Threading.DispatcherTimer;
using WpfRoutedEventArgs = System.Windows.RoutedEventArgs;
using WpfToggleButton = System.Windows.Controls.Primitives.ToggleButton;
using WpfWindowStartupLocation = System.Windows.WindowStartupLocation;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class DesktopSampleScrollBarSmokeTests
{
    private static readonly TimeSpan StaTimeout = TimeSpan.FromSeconds(75d);

    [TestMethod]
    [Timeout(105_000)]
    public void WinFormsSampleToggleControlsPaneScrollBarsAndCreatesSplitFour()
    {
        RunInSta(() =>
        {
            using var form = new SampleWinFormsMainForm
            {
                ShowInTaskbar = false,
                StartPosition = System.Windows.Forms.FormStartPosition.Manual,
                Location = new System.Drawing.Point(-32_000, -32_000),
            };
            form.Show();
            WinFormsApplication.DoEvents();

            var spreadsheet = form.Controls
                .OfType<NeraSpreadSheet.WinForms.NeraSpreadsheetControl>()
                .Single();
            var toolbar = form.Controls.OfType<WinFormsToolStrip>().Single();
            var toggle = toolbar.Items
                .OfType<WinFormsToolStripButton>()
                .Single(item => item.Text.StartsWith(
                    "Pane Scrollbars",
                    StringComparison.Ordinal));

            Assert.IsTrue(toggle.Checked);
            Assert.IsTrue(spreadsheet.RenderTheme.ShowSplitPaneScrollBars);

            toggle.PerformClick();
            WinFormsApplication.DoEvents();
            Assert.IsFalse(toggle.Checked);
            Assert.IsFalse(spreadsheet.RenderTheme.ShowSplitPaneScrollBars);

            toggle.PerformClick();
            WinFormsApplication.DoEvents();
            Assert.IsTrue(toggle.Checked);
            Assert.IsTrue(spreadsheet.RenderTheme.ShowSplitPaneScrollBars);
            Assert.IsTrue(
                NeraSpreadSheet.WinForms.NeraSpreadsheetSplitExtensions
                    .TryGetSplitPaneController(spreadsheet, out var split));
            Assert.AreEqual(
                NeraSpreadSheet.WinForms.SpreadsheetSplitPaneMode.Both,
                split.Mode);
            split.RenderNow();
            Assert.IsNotNull(split.LastFrame);
            Assert.AreEqual(4, split.LastFrame.Panes.Count);
            Assert.IsTrue(split.LastFrame.ScrollBars.Bars.Count >= 8);

            form.Close();
            WinFormsApplication.DoEvents();
        });
    }

    [TestMethod]
    [Timeout(105_000)]
    public void WpfSampleToggleControlsPaneScrollBarsAndCreatesSplitFour()
    {
        RunInSta(() =>
        {
            var window = new SampleWpfMainWindow
            {
                Left = -32_000d,
                Top = -32_000d,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStartupLocation = WpfWindowStartupLocation.Manual,
            };
            try
            {
                window.Show();
                window.UpdateLayout();
                PumpFor(TimeSpan.FromMilliseconds(80d));

                var spreadsheet = (NeraSpreadSheet.Wpf.NeraSpreadsheetControl?)
                    window.FindName("Spreadsheet");
                var toggle = (WpfToggleButton?)
                    window.FindName("ScrollBarsToggle");
                Assert.IsNotNull(spreadsheet);
                Assert.IsNotNull(toggle);
                Assert.AreEqual(true, toggle.IsChecked);
                Assert.IsTrue(
                    spreadsheet.RenderTheme.ShowSplitPaneScrollBars);

                toggle.IsChecked = false;
                toggle.RaiseEvent(new WpfRoutedEventArgs(
                    WpfButtonBase.ClickEvent,
                    toggle));
                PumpFor(TimeSpan.FromMilliseconds(40d));
                Assert.IsFalse(
                    spreadsheet.RenderTheme.ShowSplitPaneScrollBars);

                toggle.IsChecked = true;
                toggle.RaiseEvent(new WpfRoutedEventArgs(
                    WpfButtonBase.ClickEvent,
                    toggle));
                PumpFor(TimeSpan.FromMilliseconds(80d));
                Assert.IsTrue(
                    spreadsheet.RenderTheme.ShowSplitPaneScrollBars);
                Assert.IsTrue(
                    NeraSpreadSheet.Wpf.NeraSpreadsheetSplitExtensions
                        .TryGetSplitPaneController(spreadsheet, out var split));
                Assert.AreEqual(
                    NeraSpreadSheet.Wpf.SpreadsheetSplitPaneMode.Both,
                    split.Mode);
                split.RenderNow();
                Assert.IsNotNull(split.LastFrame);
                Assert.AreEqual(4, split.LastFrame.Panes.Count);
                Assert.IsTrue(split.LastFrame.ScrollBars.Bars.Count >= 8);
            }
            finally
            {
                window.Close();
                PumpFor(TimeSpan.FromMilliseconds(40d));
            }
        });
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
            Name = "Nera desktop sample scrollbar smoke",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(StaTimeout))
        {
            Assert.Fail("The desktop sample scrollbar smoke timed out.");
        }
        failure?.Throw();
    }
}
