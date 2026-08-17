using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Wpf;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfDispatcher = System.Windows.Threading.Dispatcher;
using WpfDispatcherFrame = System.Windows.Threading.DispatcherFrame;
using WpfDispatcherPriority = System.Windows.Threading.DispatcherPriority;
using WpfDispatcherTimer = System.Windows.Threading.DispatcherTimer;
using WpfGrid = System.Windows.Controls.Grid;
using WpfResizeMode = System.Windows.ResizeMode;
using WpfWindow = System.Windows.Window;
using WpfWindowStartupLocation = System.Windows.WindowStartupLocation;
using WpfWindowStyle = System.Windows.WindowStyle;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WpfSharedTextureSmokeTests
{
    private static readonly TimeSpan StaThreadTimeout = TimeSpan.FromSeconds(45d);
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(12d);

    [TestMethod]
    [Timeout(60_000)]
    public void SharedTextureControlLoadsRendersReusesLayoutsAndResizes()
    {
        RunInSta(() =>
        {
            var workbook = new Workbook();
            var sheet = workbook.Worksheets[0];
            sheet.SetValue(default, "Nera WPF shared-texture runtime smoke");
            sheet.SetValue(new CellAddress(1, 1), 42d);
            var session = new SpreadsheetSession(workbook);
            using var control = CreateGpuControl(session);
            var window = CreateOffscreenWindow(control, 360d, 240d);

            try
            {
                window.Show();
                window.UpdateLayout();
                WaitForRenderedTexture(control);

                var initial = control.GpuDiagnostics;
                Assert.IsTrue(initial.HasValue);
                var initialDiagnostics = initial.GetValueOrDefault();
                Assert.IsTrue(initialDiagnostics.TextureWidth > 0);
                Assert.IsTrue(initialDiagnostics.TextureHeight > 0);

                control.InvalidateVisual();
                PumpUntil(
                    () => control.GpuDiagnostics is { TextLayoutCacheHits: > 0 },
                    "The WPF shared-texture surface did not reuse its DirectWrite text layout.");

                window.Width = 440d;
                window.Height = 300d;
                window.UpdateLayout();
                control.InvalidateVisual();
                PumpUntil(
                    () => control.GpuDiagnostics is { } diagnostics &&
                        diagnostics.TextureWidth > initialDiagnostics.TextureWidth &&
                        diagnostics.TextureHeight > initialDiagnostics.TextureHeight,
                    "The WPF shared-texture surface did not resize its native texture.");
            }
            finally
            {
                window.Close();
                PumpFor(TimeSpan.FromMilliseconds(40d));
            }
        });
    }

    [TestMethod]
    [Timeout(60_000)]
    public void SharedTextureControlRecreatesSurfaceAcrossRepeatedUnloadAndReload()
    {
        RunInSta(() =>
        {
            var workbook = new Workbook();
            workbook.Worksheets[0].SetValue(default, "Nera WPF visual-tree reload smoke");
            var session = new SpreadsheetSession(workbook);
            using var control = CreateGpuControl(session);
            var host = new WpfGrid();
            host.Children.Add(control);
            var window = CreateOffscreenWindow(host, 380d, 250d);

            try
            {
                window.Show();
                window.UpdateLayout();
                WaitForRenderedTexture(control);
                var previousHits = control.GpuDiagnostics?.TextLayoutCacheHits ?? 0L;

                for (var cycle = 0; cycle < 3; cycle++)
                {
                    Assert.IsTrue(host.Children.Remove(control));
                    window.UpdateLayout();
                    PumpUntil(
                        () => control.GpuDiagnostics is { TextureWidth: 0, TextureHeight: 0 },
                        $"The WPF GPU surface did not release its texture during unload cycle {cycle + 1}.");

                    host.Children.Add(control);
                    window.UpdateLayout();
                    control.InvalidateVisual();
                    PumpUntil(
                        () => control.GpuDiagnostics is
                        {
                            TextureWidth: > 0,
                            TextureHeight: > 0,
                            CachedTextLayouts: > 0,
                        },
                        $"The WPF GPU surface did not recreate and render during reload cycle {cycle + 1}.");
                    PumpUntil(
                        () => control.GpuDiagnostics is { } diagnostics &&
                            diagnostics.TextLayoutCacheHits > previousHits,
                        $"The WPF GPU surface did not reuse text layouts during reload cycle {cycle + 1}.");
                    previousHits = control.GpuDiagnostics?.TextLayoutCacheHits ?? previousHits;
                }
            }
            finally
            {
                window.Close();
                PumpFor(TimeSpan.FromMilliseconds(40d));
            }
        });
    }

    private static NeraSpreadsheetControl CreateGpuControl(SpreadsheetSession session) => new()
    {
        Background = WpfBrushes.White,
        RenderingBackend = WpfRenderingBackend.Direct2DD3DImage,
        Session = session,
    };

    private static WpfWindow CreateOffscreenWindow(object content, double width, double height) => new()
    {
        Background = WpfBrushes.White,
        Content = content,
        Height = height,
        Left = -32_000d,
        ResizeMode = WpfResizeMode.NoResize,
        ShowActivated = false,
        ShowInTaskbar = false,
        Title = "NeraSpreadSheet WPF GPU smoke host",
        Top = -32_000d,
        Width = width,
        WindowStartupLocation = WpfWindowStartupLocation.Manual,
        WindowStyle = WpfWindowStyle.ToolWindow,
    };

    private static void WaitForRenderedTexture(NeraSpreadsheetControl control) =>
        PumpUntil(
            () => control.GpuDiagnostics is
            {
                TextureWidth: > 0,
                TextureHeight: > 0,
                CachedTextLayouts: > 0,
            },
            "The WPF shared-texture surface did not load and render a text layout.");

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
            Name = "NeraSpreadSheet WPF shared-texture smoke",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        if (!thread.Join(StaThreadTimeout))
        {
            Assert.Fail("The WPF STA renderer smoke thread did not complete within the timeout.");
        }

        failure?.Throw();
    }
}
