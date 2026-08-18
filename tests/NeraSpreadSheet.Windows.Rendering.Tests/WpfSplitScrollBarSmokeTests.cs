using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Wpf;
using WpfAdornerDecorator = System.Windows.Documents.AdornerDecorator;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfDispatcher = System.Windows.Threading.Dispatcher;
using WpfDispatcherFrame = System.Windows.Threading.DispatcherFrame;
using WpfDispatcherPriority = System.Windows.Threading.DispatcherPriority;
using WpfDispatcherTimer = System.Windows.Threading.DispatcherTimer;
using WpfWindow = System.Windows.Window;
using WpfWindowStartupLocation = System.Windows.WindowStartupLocation;
using WpfWindowStyle = System.Windows.WindowStyle;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WpfSplitScrollBarSmokeTests
{
    private static readonly TimeSpan StaThreadTimeout = TimeSpan.FromSeconds(60d);
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(12d);

    [TestMethod]
    [Timeout(90_000)]
    public void PublicSplitAdornerScrollBarsMoveOnlyTheirTargetPane()
    {
        RunInSta(() =>
        {
            var workbook = new Workbook();
            var sheet = workbook.Worksheets[0];
            sheet.SetValue(default, "WPF scroll-bar smoke");
            sheet.SetValue(new CellAddress(240, 80), "extent");
            var session = new SpreadsheetSession(workbook);
            using var control = new NeraSpreadsheetControl
            {
                Background = WpfBrushes.White,
                RenderingBackend = WpfRenderingBackend.DrawingContext,
                Session = session,
            };
            var host = new WpfAdornerDecorator { Child = control };
            var window = CreateOffscreenWindow(host, 760d, 520d);

            try
            {
                window.Show();
                window.UpdateLayout();
                using var split = control.EnableSplitPanes(
                    SpreadsheetSplitPaneMode.Both);
                split.SetSplit(300d, 190d);
                split.RenderNow();
                PumpUntil(
                    () => split.LastFrame is
                    {
                        Panes.Count: 4,
                        ScrollBars.Bars.Count: 8,
                    },
                    "The WPF split frame did not expose eight independent scroll bars.");
                var adorner = GetAdorner(split);
                var frame = split.LastFrame;
                Assert.IsNotNull(frame);
                Assert.IsTrue(frame.ScrollBars.TryGetBar(
                    SpreadsheetPaneId.TopRight,
                    SpreadsheetScrollBarOrientation.Horizontal,
                    out var topRightHorizontal));
                var beforeTopLeft = split.GetPaneScroll(SpreadsheetPaneId.TopLeft);
                var beforeTopRight = split.GetPaneScroll(SpreadsheetPaneId.TopRight);
                var button = ToControlPoint(
                    control,
                    Center(topRightHorizontal.IncreaseButtonBounds));

                Assert.IsTrue(InvokeBeginInteraction(adorner, button.X, button.Y));
                PumpFor(TimeSpan.FromMilliseconds(30d));

                var afterTopRight = split.GetPaneScroll(SpreadsheetPaneId.TopRight);
                Assert.AreEqual(
                    beforeTopRight.X + control.RenderTheme.ScrollBarLineStep,
                    afterTopRight.X,
                    0.01d);
                Assert.AreEqual(beforeTopRight.Y, afterTopRight.Y, 0.01d);
                Assert.AreEqual(
                    beforeTopLeft,
                    split.GetPaneScroll(SpreadsheetPaneId.TopLeft));

                split.RenderNow();
                frame = split.LastFrame;
                Assert.IsNotNull(frame);
                Assert.IsTrue(frame.ScrollBars.TryGetBar(
                    SpreadsheetPaneId.BottomRight,
                    SpreadsheetScrollBarOrientation.Vertical,
                    out var bottomRightVertical));
                var thumbCenter = Center(bottomRightVertical.ThumbBounds);
                var down = ToControlPoint(control, thumbCenter);
                var targetThumbStart = bottomRightVertical.TrackBounds.Top +
                    (bottomRightVertical.TrackTravel * 0.68d);
                var target = ToControlPoint(
                    control,
                    new NeraSpreadSheet.Foundation.PointD(
                        thumbCenter.X,
                        targetThumbStart +
                        (bottomRightVertical.ThumbBounds.Height / 2d)));
                var expectedY = bottomRightVertical.GetOffsetForThumbStart(
                    targetThumbStart);
                var topRightBeforeDrag = split.GetPaneScroll(
                    SpreadsheetPaneId.TopRight);

                Assert.IsTrue(InvokeBeginInteraction(adorner, down.X, down.Y));
                InvokePrivate(
                    adorner,
                    "UpdateScrollBarDrag",
                    target.X,
                    target.Y);
                InvokePrivate(adorner, "EndScrollBarDrag", true);
                PumpFor(TimeSpan.FromMilliseconds(30d));

                var bottomRightAfter = split.GetPaneScroll(
                    SpreadsheetPaneId.BottomRight);
                Assert.AreEqual(expectedY, bottomRightAfter.Y, 0.5d);
                Assert.AreEqual(0d, bottomRightAfter.X, 0.01d);
                Assert.AreEqual(
                    topRightBeforeDrag,
                    split.GetPaneScroll(SpreadsheetPaneId.TopRight));
                Assert.AreEqual(
                    new SpreadsheetPaneScrollOffset(
                        bottomRightAfter.X,
                        bottomRightAfter.Y),
                    session.View.SplitState.BottomRightScroll);

                split.RenderingBackend = WpfRenderingBackend.Direct2DD3DImage;
                split.RenderNow();
                PumpUntil(
                    () => split.GpuDiagnostics is
                    {
                        TextureWidth: > 0,
                        TextureHeight: > 0,
                    },
                    "The WPF GPU path did not render the scroll-bar display list.");
                Assert.IsTrue(split.LastFrame?.ScrollBars.Bars.Count >= 8);
            }
            finally
            {
                window.Close();
                PumpFor(TimeSpan.FromMilliseconds(40d));
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

    private static bool InvokeBeginInteraction(
        object adorner,
        double x,
        double y) =>
        (bool)(InvokePrivate(
            adorner,
            "TryBeginScrollBarInteraction",
            x,
            y) ?? false);

    private static object? InvokePrivate(
        object target,
        string methodName,
        params object?[] arguments)
    {
        var method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        return method.Invoke(target, arguments);
    }

    private static NeraSpreadSheet.Foundation.PointD ToControlPoint(
        NeraSpreadsheetControl control,
        NeraSpreadSheet.Foundation.PointD bodyPoint) => new(
        control.RenderTheme.RowHeaderWidth + bodyPoint.X,
        control.RenderTheme.ColumnHeaderHeight + bodyPoint.Y);

    private static NeraSpreadSheet.Foundation.PointD Center(
        NeraSpreadSheet.Foundation.RectD bounds) => new(
        bounds.Left + (bounds.Width / 2d),
        bounds.Top + (bounds.Height / 2d));

    private static WpfWindow CreateOffscreenWindow(
        object content,
        double width,
        double height) => new()
    {
        Background = WpfBrushes.White,
        Content = content,
        Height = height,
        Left = -32_000d,
        ShowActivated = false,
        ShowInTaskbar = false,
        Title = "Nera WPF split scroll-bar smoke host",
        Top = -32_000d,
        Width = width,
        WindowStartupLocation = WpfWindowStartupLocation.Manual,
        WindowStyle = WpfWindowStyle.ToolWindow,
    };

    private static void PumpUntil(Func<bool> condition, string failureMessage)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            if (stopwatch.Elapsed >= RenderTimeout)
            {
                Assert.Fail(failureMessage);
            }
            PumpFor(TimeSpan.FromMilliseconds(40d));
        }
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
            Name = "Nera WPF split scroll-bar smoke",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(StaThreadTimeout))
        {
            Assert.Fail("The WPF split scroll-bar smoke timed out.");
        }
        failure?.Throw();
    }
}
