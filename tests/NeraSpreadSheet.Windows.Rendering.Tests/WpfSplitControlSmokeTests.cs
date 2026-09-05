using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Wpf;
using WpfAdornerDecorator = System.Windows.Documents.AdornerDecorator;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfDispatcher = System.Windows.Threading.Dispatcher;
using WpfDispatcherFrame = System.Windows.Threading.DispatcherFrame;
using WpfDispatcherPriority = System.Windows.Threading.DispatcherPriority;
using WpfDispatcherTimer = System.Windows.Threading.DispatcherTimer;
using WpfResizeMode = System.Windows.ResizeMode;
using WpfWindow = System.Windows.Window;
using WpfWindowStartupLocation = System.Windows.WindowStartupLocation;
using WpfWindowStyle = System.Windows.WindowStyle;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WpfSplitControlSmokeTests
{
    private static readonly TimeSpan StaThreadTimeout = TimeSpan.FromSeconds(60d);
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(12d);

    [TestMethod]
    [Timeout(90_000)]
    public void PublicControlRendersSplitPanesAcrossBothWpfBackends()
    {
        RunInSta(() =>
        {
            var workbook = new Workbook();
            var sheet = workbook.Worksheets[0];
            sheet.SetValue(default, "Nera WPF split runtime smoke");
            sheet.SetValue(new CellAddress(40, 12), 42d);
            sheet.SetFormula(new CellAddress(41, 12), "=M41*2");
            var second = workbook.AddWorksheet("Second");
            second.SetValue(default, "Second WPF worksheet split state");
            second.SetValue(new CellAddress(50, 10), 84d);
            var session = new SpreadsheetSession(workbook);
            session.Recalculate();
            using var control = new NeraSpreadsheetControl
            {
                Background = WpfBrushes.White,
                RenderingBackend = WpfRenderingBackend.DrawingContext,
                Session = session,
            };
            var host = new WpfAdornerDecorator { Child = control };
            var window = CreateOffscreenWindow(host, 720d, 480d);

            try
            {
                window.Show();
                window.UpdateLayout();
                using var split = control.EnableSplitPanes(SpreadsheetSplitPaneMode.Both);
                split.SetSplit(280.5d, 170.25d);
                split.ScrollPaneTo(
                    SpreadsheetPaneId.BottomRight,
                    43.75d,
                    61.5d);
                split.RenderNow();
                PumpUntil(
                    () => split.LastFrame is { Panes.Count: 4 },
                    "The WPF split adorner did not compose its four-pane frame.");
                AssertSplitFrame(split);
                AssertPerWorksheetStateRestoration(session, split, sheet, second);

                split.RenderingBackend = WpfRenderingBackend.Direct2DD3DImage;
                split.RenderNow();
                PumpUntil(
                    () => split.GpuDiagnostics is
                    {
                        TextureWidth: > 0,
                        TextureHeight: > 0,
                        CachedTextLayouts: > 0,
                    },
                    "The WPF split D3DImage surface did not render its display list.");
                var initialHits = split.GpuDiagnostics?.TextLayoutCacheHits ?? 0L;
                split.RenderNow();
                PumpUntil(
                    () => split.GpuDiagnostics is { } diagnostics &&
                        diagnostics.TextLayoutCacheHits > initialHits,
                    "The WPF split D3DImage surface did not reuse DirectWrite text layouts.");

                window.Width = 810d;
                window.Height = 540d;
                window.UpdateLayout();
                split.RenderNow();
                PumpUntil(
                    () => split.LastFrame is { } frame &&
                        frame.Layout.ViewportSize.Width > 700d &&
                        frame.Layout.ViewportSize.Height > 450d,
                    "The WPF split adorner did not recompute its resized viewport.");

                var resized = split.LastFrame;
                Assert.IsNotNull(resized);
                var bottomRight = resized.Panes.Single(
                    pane => pane.Pane.PaneId == SpreadsheetPaneId.BottomRight);
                var theme = control.RenderTheme;
                var controlX = theme.RowHeaderWidth + bottomRight.Pane.Bounds.Left + 24d;
                var controlY = theme.ColumnHeaderHeight + bottomRight.Pane.Bounds.Top + 24d;
                Assert.IsTrue(split.TryHitTest(controlX, controlY, out var paneId, out _));
                Assert.AreEqual(SpreadsheetPaneId.BottomRight, paneId);

                Assert.IsTrue(control.DisableSplitPanes());
                Assert.IsFalse(control.TryGetSplitPaneController(out _));
            }
            finally
            {
                window.Close();
                PumpFor(TimeSpan.FromMilliseconds(40d));
            }
        });
    }

    private static void AssertPerWorksheetStateRestoration(
        SpreadsheetSession session,
        NeraSpreadsheetSplitController split,
        Worksheet first,
        Worksheet second)
    {
        split.SetActivePane(SpreadsheetPaneId.BottomRight);
        split.RenderNow();
        Assert.AreEqual(SpreadsheetSplitViewMode.Both, session.View.SplitState.Mode);
        Assert.AreEqual(SpreadsheetSplitViewPane.BottomRight, session.View.SplitState.ActivePane);
        Assert.AreEqual(280.5d, session.View.SplitState.SplitX);
        Assert.AreEqual(170.25d, session.View.SplitState.SplitY);
        Assert.AreEqual(
            new SpreadsheetPaneScrollOffset(43.75d, 61.5d),
            session.View.SplitState.BottomRightScroll);

        session.ActivateWorksheet(second);
        split.RenderNow();
        PumpUntil(
            () => split.Mode == SpreadsheetSplitPaneMode.None &&
                split.LastFrame is { Panes.Count: 1 },
            "The WPF split host did not apply the second worksheet's default view state.");
        Assert.AreEqual(default, session.View.SplitState);

        split.SetSplit(null, 190.75d);
        split.SetActivePane(SpreadsheetPaneId.BottomLeft);
        split.ScrollPaneTo(SpreadsheetPaneId.BottomLeft, 18.5d, 92.25d);
        split.RenderNow();
        Assert.AreEqual(SpreadsheetSplitViewMode.Horizontal, session.View.SplitState.Mode);
        Assert.AreEqual(SpreadsheetSplitViewPane.BottomLeft, session.View.SplitState.ActivePane);
        Assert.AreEqual(
            new SpreadsheetPaneScrollOffset(18.5d, 92.25d),
            session.View.SplitState.BottomLeftScroll);

        session.ActivateWorksheet(first);
        split.RenderNow();
        PumpUntil(
            () => split.Mode == SpreadsheetSplitPaneMode.Both &&
                split.ActivePane == SpreadsheetPaneId.BottomRight &&
                split.LastFrame is { Panes.Count: 4 },
            "The WPF split host did not restore the first worksheet's split state.");
        Assert.AreEqual(280.5d, split.SplitX);
        Assert.AreEqual(170.25d, split.SplitY);
        var restored = split.GetPaneScroll(SpreadsheetPaneId.BottomRight);
        Assert.AreEqual(43.75d, restored.X, 0.001d);
        Assert.AreEqual(61.5d, restored.Y, 0.001d);

        split.SetActivePane(SpreadsheetPaneId.TopLeft);
    }

    private static void AssertSplitFrame(NeraSpreadsheetSplitController split)
    {
        var frame = split.LastFrame;
        Assert.IsNotNull(frame);
        Assert.IsTrue(frame.Layout.HasVerticalSplit);
        Assert.IsTrue(frame.Layout.HasHorizontalSplit);
        Assert.AreEqual(4, frame.Panes.Count);
        Assert.AreEqual(SpreadsheetPaneId.TopLeft, frame.ActivePane);

        var bottomRight = split.GetPaneScroll(SpreadsheetPaneId.BottomRight);
        Assert.AreEqual(43.75d, bottomRight.X, 0.001d);
        Assert.AreEqual(61.5d, bottomRight.Y, 0.001d);
        var topLeft = split.GetPaneScroll(SpreadsheetPaneId.TopLeft);
        Assert.AreEqual(0d, topLeft.X, 0.001d);
        Assert.AreEqual(0d, topLeft.Y, 0.001d);
    }

    private static WpfWindow CreateOffscreenWindow(object content, double width, double height) => new()
    {
        Background = WpfBrushes.White,
        Content = content,
        Height = height,
        Left = -32_000d,
        ResizeMode = WpfResizeMode.CanResize,
        ShowActivated = false,
        ShowInTaskbar = false,
        Title = "NeraSpreadSheet WPF split smoke host",
        Top = -32_000d,
        Width = width,
        WindowStartupLocation = WpfWindowStartupLocation.Manual,
        WindowStyle = WpfWindowStyle.ToolWindow,
    };

    private static void PumpUntil(Func<bool> condition, string failureMessage)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureMessage);
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
        var timer = new WpfDispatcherTimer(WpfDispatcherPriority.Background, dispatcher)
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
            Name = "NeraSpreadSheet WPF split smoke",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        if (!thread.Join(StaThreadTimeout))
        {
            Assert.Fail("The WPF split smoke thread did not complete within the timeout.");
        }

        failure?.Throw();
    }
}
