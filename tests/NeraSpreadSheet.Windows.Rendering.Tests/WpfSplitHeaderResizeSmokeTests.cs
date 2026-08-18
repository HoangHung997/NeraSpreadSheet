using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Wpf;
using WpfAdornerDecorator = System.Windows.Documents.AdornerDecorator;
using WpfDispatcher = System.Windows.Threading.Dispatcher;
using WpfDispatcherFrame = System.Windows.Threading.DispatcherFrame;
using WpfDispatcherPriority = System.Windows.Threading.DispatcherPriority;
using WpfDispatcherTimer = System.Windows.Threading.DispatcherTimer;
using WpfWindow = System.Windows.Window;
using WpfWindowStartupLocation = System.Windows.WindowStartupLocation;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WpfSplitHeaderResizeSmokeTests
{
    private static readonly TimeSpan StaThreadTimeout = TimeSpan.FromSeconds(60d);
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(12d);

    [TestMethod]
    [Timeout(90_000)]
    public void PublicSplitAdornerResizesRowsAndColumnsAndRendersBothBackends()
    {
        RunInSta(() =>
        {
            var workbook = new Workbook();
            var worksheet = workbook.Worksheets[0];
            worksheet.SetValue(new CellAddress(160, 60), "extent");
            var session = new SpreadsheetSession(workbook);
            using var control = new NeraSpreadsheetControl
            {
                Width = 960d,
                Height = 700d,
                Session = session,
                RenderingBackend = WpfRenderingBackend.DrawingContext,
            };
            var decorator = new WpfAdornerDecorator { Child = control };
            var window = new WpfWindow
            {
                Width = 1000d,
                Height = 760d,
                ShowInTaskbar = false,
                WindowStartupLocation = WpfWindowStartupLocation.Manual,
                Left = -30000d,
                Top = -30000d,
                Content = decorator,
            };

            try
            {
                window.Show();
                window.UpdateLayout();
                using var controller = control.EnableSplitPanes(
                    SpreadsheetSplitPaneMode.Both);
                controller.SetSplit(340d, 230d);
                controller.RenderNow();
                PumpUntil(
                    () => controller.LastFrame is { Panes.Count: 4 },
                    "The WPF resize smoke did not compose four split panes.");

                var frame = controller.LastFrame;
                Assert.IsNotNull(frame);
                Assert.IsTrue(frame.TryGetPane(
                    SpreadsheetPaneId.BottomLeft,
                    out var bottomLeft));
                var adorner = GetAdorner(controller);
                var row = bottomLeft.ViewportFrame.Layout.Rows[0];
                var originalRowHeight = worksheet.Dimensions.GetRowHeight(row.Index);
                var rowX = control.RenderTheme.RowHeaderWidth / 2d;
                var rowEdgeY =
                    control.RenderTheme.ColumnHeaderHeight +
                    bottomLeft.Pane.Bounds.Top +
                    row.End;

                Assert.IsTrue(TryResizeHeader(
                    adorner,
                    rowX,
                    rowEdgeY,
                    rowX,
                    rowEdgeY + 13d));
                Assert.AreEqual(
                    originalRowHeight + 13d,
                    worksheet.Dimensions.GetRowHeight(row.Index),
                    0.01d);

                controller.RenderNow();
                frame = controller.LastFrame;
                Assert.IsNotNull(frame);
                Assert.IsTrue(frame.TryGetPane(
                    SpreadsheetPaneId.TopRight,
                    out var topRight));
                var column = topRight.ViewportFrame.Layout.Columns[0];
                var originalColumnWidth = worksheet.Dimensions.GetColumnWidth(column.Index);
                var columnEdgeX =
                    control.RenderTheme.RowHeaderWidth +
                    topRight.Pane.Bounds.Left +
                    column.End;
                var columnY = control.RenderTheme.ColumnHeaderHeight / 2d;

                Assert.IsTrue(TryResizeHeader(
                    adorner,
                    columnEdgeX,
                    columnY,
                    columnEdgeX + 17d,
                    columnY));
                Assert.AreEqual(
                    originalColumnWidth + 17d,
                    worksheet.Dimensions.GetColumnWidth(column.Index),
                    0.01d);

                controller.RenderingBackend = WpfRenderingBackend.Direct2DD3DImage;
                controller.RenderNow();
                PumpUntil(
                    () => controller.GpuDiagnostics is
                    {
                        TextureWidth: > 0,
                        TextureHeight: > 0,
                    },
                    "The WPF split resize smoke did not render its D3DImage surface.");
                var diagnostics = controller.GpuDiagnostics ??
                    throw new AssertFailedException(
                        "WPF GPU diagnostics were unavailable after rendering.");
                Assert.IsTrue(diagnostics.TextureWidth > 0);
                Assert.IsTrue(diagnostics.TextureHeight > 0);
            }
            finally
            {
                window.Close();
                PumpDispatcherOnce();
            }
        });
    }

    private static object GetAdorner(NeraSpreadsheetSplitController controller)
    {
        var field = typeof(NeraSpreadsheetSplitController).GetField(
            "_adorner",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        var adorner = field.GetValue(controller);
        Assert.IsNotNull(adorner);
        return adorner;
    }

    private static bool TryResizeHeader(
        object adorner,
        double hitX,
        double hitY,
        double targetX,
        double targetY)
    {
        var type = adorner.GetType();
        var tryHit = type.GetMethod(
            "TryGetHeaderResizeHandle",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var apply = type.GetMethod(
            "ApplyHeaderResize",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(tryHit);
        Assert.IsNotNull(apply);
        object?[] hitArguments = [hitX, hitY, null];
        var hit = (bool)(tryHit.Invoke(adorner, hitArguments) ?? false);
        if (!hit ||
            hitArguments[2] is not SpreadsheetSplitHeaderResizeHandle handle)
        {
            return false;
        }

        apply.Invoke(adorner, [handle, targetX, targetY]);
        return true;
    }

    private static void PumpUntil(Func<bool> condition, string timeoutMessage)
    {
        var deadline = DateTime.UtcNow + RenderTimeout;
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                Assert.Fail(timeoutMessage);
            }
            PumpDispatcherOnce();
            Thread.Sleep(10);
        }
    }

    private static void PumpDispatcherOnce()
    {
        var frame = new WpfDispatcherFrame();
        var timer = new WpfDispatcherTimer(
            TimeSpan.FromMilliseconds(1d),
            WpfDispatcherPriority.Background,
            (_, _) => frame.Continue = false,
            WpfDispatcher.CurrentDispatcher);
        timer.Start();
        WpfDispatcher.PushFrame(frame);
        timer.Stop();
    }

    private static void RunInSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(
            thread.Join(StaThreadTimeout),
            "The WPF split resize STA thread timed out.");
        if (failure is not null)
        {
            throw new AssertFailedException(
                "WPF split header resize smoke failed.",
                failure);
        }
    }
}
