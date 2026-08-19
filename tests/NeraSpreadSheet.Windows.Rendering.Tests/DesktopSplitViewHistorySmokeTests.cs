using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Layout;
using WinFormsApplication = System.Windows.Forms.Application;
using WinFormsDockStyle = System.Windows.Forms.DockStyle;
using WinFormsForm = System.Windows.Forms.Form;
using WinFormsFormStartPosition = System.Windows.Forms.FormStartPosition;
using WpfAdornerDecorator = System.Windows.Documents.AdornerDecorator;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfDispatcher = System.Windows.Threading.Dispatcher;
using WpfDispatcherPriority = System.Windows.Threading.DispatcherPriority;
using WpfWindow = System.Windows.Window;
using WpfWindowStartupLocation = System.Windows.WindowStartupLocation;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class DesktopSplitViewHistorySmokeTests
{
    [TestMethod]
    [Timeout(120_000)]
    public void WinFormsPublicSplitControllerUndoRedoRestoresExactViewState()
    {
        RunSta(() =>
        {
            var session = CreateSession();
            using var form = new WinFormsForm
            {
                Width = 1000,
                Height = 760,
                ShowInTaskbar = false,
                StartPosition = WinFormsFormStartPosition.Manual,
                Location = new System.Drawing.Point(-30_000, -30_000),
            };
            using var control = new NeraSpreadSheet.WinForms.NeraSpreadsheetControl
            {
                Dock = WinFormsDockStyle.Fill,
                Session = session,
                RenderingBackend = NeraSpreadSheet.WinForms.WinFormsRenderingBackend.GdiPlus,
            };
            form.Controls.Add(control);
            form.Show();
            WinFormsApplication.DoEvents();

            using var split = control.EnableSplitPanes(
                NeraSpreadSheet.WinForms.SpreadsheetSplitPaneMode.Both);
            split.RenderNow();
            session.View.ClearSplitViewHistory();
            var before = session.View.SplitState;

            split.SetSplit(340.5d, 230.25d);
            split.RenderNow();
            var afterTopology = session.View.SplitState;
            split.ScrollPaneTo(
                SpreadsheetPaneId.BottomRight,
                321.75d,
                187.5d,
                animated: false);
            split.RenderNow();
            var afterScroll = session.View.SplitState;

            Assert.AreEqual(2, session.View.SplitViewUndoCount);
            Assert.AreEqual(0, session.History.UndoCount);
            Assert.IsTrue(split.CanUndoViewChange);
            Assert.AreEqual("Scroll split pane", split.NextViewUndoDescription);

            Assert.IsTrue(split.UndoViewChange());
            split.RenderNow();
            Assert.AreEqual(afterTopology, session.View.SplitState);
            Assert.IsTrue(split.UndoViewChange());
            split.RenderNow();
            Assert.AreEqual(before, session.View.SplitState);
            Assert.IsFalse(split.CanUndoViewChange);
            Assert.IsTrue(split.CanRedoViewChange);

            Assert.IsTrue(split.RedoViewChange());
            Assert.IsTrue(split.RedoViewChange());
            split.RenderNow();
            Assert.AreEqual(afterScroll, session.View.SplitState);
            Assert.AreEqual(0, session.History.UndoCount);

            form.Close();
            WinFormsApplication.DoEvents();
        });
    }

    [TestMethod]
    [Timeout(120_000)]
    public void WpfPublicSplitControllerUndoRedoRestoresExactViewStateAndGpuRender()
    {
        RunSta(() =>
        {
            var session = CreateSession();
            using var control = new NeraSpreadSheet.Wpf.NeraSpreadsheetControl
            {
                Background = WpfBrushes.White,
                Session = session,
                RenderingBackend = NeraSpreadSheet.Wpf.WpfRenderingBackend.DrawingContext,
            };
            var decorator = new WpfAdornerDecorator { Child = control };
            var window = new WpfWindow
            {
                Width = 1000d,
                Height = 760d,
                ShowInTaskbar = false,
                WindowStartupLocation = WpfWindowStartupLocation.Manual,
                Left = -30_000d,
                Top = -30_000d,
                Content = decorator,
            };

            try
            {
                window.Show();
                PumpDispatcher();
                using var split = control.EnableSplitPanes(
                    NeraSpreadSheet.Wpf.SpreadsheetSplitPaneMode.Both);
                split.RenderNow();
                session.View.ClearSplitViewHistory();
                var before = session.View.SplitState;

                split.SetSplit(335.5d, 225.25d);
                split.RenderNow();
                var afterTopology = session.View.SplitState;
                split.ScrollPaneTo(
                    SpreadsheetPaneId.BottomRight,
                    288.75d,
                    166.5d,
                    animated: false);
                split.RenderNow();
                var afterScroll = session.View.SplitState;

                Assert.AreEqual(2, session.View.SplitViewUndoCount);
                Assert.AreEqual(0, session.History.UndoCount);
                Assert.AreEqual("Scroll split pane", split.NextViewUndoDescription);
                Assert.IsTrue(split.UndoViewChange());
                split.RenderNow();
                Assert.AreEqual(afterTopology, session.View.SplitState);
                Assert.IsTrue(split.UndoViewChange());
                split.RenderNow();
                Assert.AreEqual(before, session.View.SplitState);

                Assert.IsTrue(split.RedoViewChange());
                Assert.IsTrue(split.RedoViewChange());
                split.RenderingBackend =
                    NeraSpreadSheet.Wpf.WpfRenderingBackend.Direct2DD3DImage;
                split.RenderNow();
                PumpDispatcher();

                Assert.AreEqual(afterScroll, session.View.SplitState);
                Assert.IsNotNull(split.GpuDiagnostics);
                Assert.IsTrue(split.GpuDiagnostics.TextureWidth > 0);
                Assert.IsTrue(split.GpuDiagnostics.TextureHeight > 0);
                Assert.AreEqual(0, session.History.UndoCount);
            }
            finally
            {
                window.Close();
                PumpDispatcher();
            }
        });
    }

    private static SpreadsheetSession CreateSession()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(180, 45), "extent");
        return new SpreadsheetSession(workbook);
    }

    private static void PumpDispatcher() =>
        WpfDispatcher.CurrentDispatcher.Invoke(
            WpfDispatcherPriority.ApplicationIdle,
            static () => { });

    private static void RunSta(Action action)
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
            Name = "Nera split-view history smoke",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(90d)))
        {
            Assert.Fail("The split-view history STA smoke timed out.");
        }
        failure?.Throw();
    }
}
