using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering;
using NeraSpreadSheet.Rendering.Direct2D;
using NeraSpreadSheet.Wpf;
using DrawingPoint = System.Drawing.Point;
using DrawingSize = System.Drawing.Size;
using WinFormsApplication = System.Windows.Forms.Application;
using WinFormsForm = System.Windows.Forms.Form;
using WinFormsFormBorderStyle = System.Windows.Forms.FormBorderStyle;
using WinFormsFormStartPosition = System.Windows.Forms.FormStartPosition;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfDispatcher = System.Windows.Threading.Dispatcher;
using WpfDispatcherFrame = System.Windows.Threading.DispatcherFrame;
using WpfDispatcherPriority = System.Windows.Threading.DispatcherPriority;
using WpfDispatcherTimer = System.Windows.Threading.DispatcherTimer;
using WpfWindow = System.Windows.Window;
using WpfWindowStartupLocation = System.Windows.WindowStartupLocation;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class RendererRecoveryStressTests
{
    private const int HwndRecoveryCycles = 32;
    private const int SwapChainRecoveryCycles = 16;
    private const int WpfDeviceRestartCycles = 8;
    private static readonly TimeSpan StaThreadTimeout = TimeSpan.FromSeconds(120d);
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(12d);

    [TestMethod]
    [Timeout(150_000)]
    public void HwndDirect2DRepeatedResourceRecreationRetainsRenderingAndLayoutCache()
    {
        RunSta(() =>
        {
            using var form = CreateOffscreenHost(360, 220);
            using var renderer = new Direct2DHwndDisplayListRenderer(
                form.Handle,
                360,
                220,
                textLayoutCacheCapacity: 32);
            var displayList = CreateDisplayList(360d, 220d);
            renderer.Render(displayList);
            renderer.Render(displayList);
            var initialHits = renderer.TextLayoutCacheHits;

            for (var cycle = 0; cycle < HwndRecoveryCycles; cycle++)
            {
                renderer.RecreateDeviceResources();
                var width = 320 + ((cycle * 31) % 161);
                var height = 180 + ((cycle * 23) % 101);
                renderer.Resize(width, height);
                renderer.Render(CreateDisplayList(width, height));
                renderer.Render(CreateDisplayList(width, height));
            }

            var diagnostics = renderer.Diagnostics;
            Assert.IsTrue(diagnostics.CachedTextLayouts > 0);
            Assert.IsTrue(diagnostics.TextLayoutCacheHits > initialHits);
            Assert.AreEqual(0L, diagnostics.DeviceRecoveryCount);
        });
    }

    [TestMethod]
    [Timeout(150_000)]
    public void DxgiSwapChainRepeatedDeviceStackRecreationContinuesPresenting()
    {
        RunSta(() =>
        {
            using var form = CreateOffscreenHost(360, 220);
            using var renderer = new Direct2DSwapChainDisplayListRenderer(
                form.Handle,
                360,
                220,
                textLayoutCacheCapacity: 32)
            {
                VSync = false,
            };
            renderer.Render(CreateDisplayList(360d, 220d));
            renderer.Render(CreateDisplayList(360d, 220d));
            var initialHits = renderer.TextLayoutCacheHits;

            for (var cycle = 0; cycle < SwapChainRecoveryCycles; cycle++)
            {
                renderer.RecreateDeviceResources();
                var width = 330 + ((cycle * 29) % 141);
                var height = 190 + ((cycle * 19) % 91);
                renderer.Resize(width, height);
                renderer.Render(CreateDisplayList(width, height));
                renderer.Render(CreateDisplayList(width, height));
                Assert.IsFalse(string.IsNullOrWhiteSpace(renderer.AdapterName));
            }

            var diagnostics = renderer.Diagnostics;
            Assert.IsTrue(diagnostics.CachedTextLayouts > 0);
            Assert.IsTrue(diagnostics.TextLayoutCacheHits > initialHits);
            Assert.AreEqual(0L, diagnostics.DeviceRecoveryCount);
            Assert.IsFalse(string.IsNullOrWhiteSpace(diagnostics.DeviceFeatureLevel));
        });
    }

    [TestMethod]
    [Timeout(150_000)]
    public void WpfSharedTextureForcedDeviceRestartCyclesRecreateAndRender()
    {
        RunSta(() =>
        {
            var workbook = new Workbook();
            workbook.Worksheets[0].SetValue(default, "Nera forced WPF device restart stress");
            workbook.Worksheets[0].SetValue(new CellAddress(2, 2), 123.5d);
            var session = new SpreadsheetSession(workbook);
            using var control = new NeraSpreadsheetControl
            {
                Background = WpfBrushes.White,
                RenderingBackend = WpfRenderingBackend.Direct2DD3DImage,
                Session = session,
            };
            var window = new WpfWindow
            {
                Background = WpfBrushes.White,
                Content = control,
                Width = 420d,
                Height = 280d,
                Left = -32_000d,
                Top = -32_000d,
                ShowInTaskbar = false,
                WindowStartupLocation = WpfWindowStartupLocation.Manual,
            };

            try
            {
                window.Show();
                window.UpdateLayout();
                PumpUntil(
                    () => control.GpuDiagnostics is
                    {
                        TextureWidth: > 0,
                        TextureHeight: > 0,
                        CachedTextLayouts: > 0,
                    },
                    "The WPF GPU surface did not render before recovery stress.");

                var gpuSurface = GetGpuSurface(control);
                var baseType = gpuSurface.GetType().BaseType
                    ?? throw new AssertFailedException("The WPF GPU surface base type is unavailable.");
                var endD3D = baseType.GetMethod(
                    "EndD3D",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new AssertFailedException("EndD3D lifecycle method was not found.");
                var startD3D = baseType.GetMethod(
                    "StartD3D",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new AssertFailedException("StartD3D lifecycle method was not found.");

                for (var cycle = 0; cycle < WpfDeviceRestartCycles; cycle++)
                {
                    endD3D.Invoke(gpuSurface, null);
                    Assert.AreEqual(0, control.GpuDiagnostics?.TextureWidth ?? 0);
                    Assert.AreEqual(0, control.GpuDiagnostics?.TextureHeight ?? 0);

                    startD3D.Invoke(gpuSurface, null);
                    control.InvalidateVisual();
                    PumpUntil(
                        () => control.GpuDiagnostics is
                        {
                            TextureWidth: > 0,
                            TextureHeight: > 0,
                            CachedTextLayouts: > 0,
                        },
                        $"The WPF GPU surface did not recover during forced restart cycle {cycle + 1}.");

                    var hitsBefore = control.GpuDiagnostics?.TextLayoutCacheHits ?? 0L;
                    control.InvalidateVisual();
                    PumpUntil(
                        () => (control.GpuDiagnostics?.TextLayoutCacheHits ?? 0L) > hitsBefore,
                        $"The WPF GPU renderer did not reuse text layout after restart cycle {cycle + 1}.");
                }
            }
            finally
            {
                window.Close();
                PumpFor(TimeSpan.FromMilliseconds(40d));
            }
        });
    }

    private static object GetGpuSurface(NeraSpreadsheetControl control)
    {
        var field = typeof(NeraSpreadsheetControl).GetField(
            "_gpuSurface",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        var surface = field.GetValue(control);
        Assert.IsNotNull(surface);
        return surface;
    }

    private static WinFormsForm CreateOffscreenHost(int width, int height)
    {
        var form = new WinFormsForm
        {
            ClientSize = new DrawingSize(width, height),
            FormBorderStyle = WinFormsFormBorderStyle.FixedToolWindow,
            Location = new DrawingPoint(-32_000, -32_000),
            ShowInTaskbar = false,
            StartPosition = WinFormsFormStartPosition.Manual,
        };
        form.Show();
        WinFormsApplication.DoEvents();
        return form;
    }

    private static DisplayList CreateDisplayList(double width, double height)
    {
        var child = new DisplayListBuilder();
        child.DrawText(
            "Nera recovery stress",
            new RectD(12d, 12d, Math.Max(40d, width - 24d), 32d),
            new TextStyle("Segoe UI", 14d, 600, new ColorRgba(25, 70, 130)));
        var childList = child.Build();

        var root = new DisplayListBuilder();
        root.FillRectangle(new RectD(0d, 0d, width, height), ColorRgba.White);
        root.PushClip(new RectD(0d, 0d, width, height));
        root.DrawDisplayList(childList);
        root.PopClip();
        return root.Build();
    }

    private static void PumpUntil(Func<bool> condition, string failureMessage)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            if (stopwatch.Elapsed >= RenderTimeout)
            {
                Assert.Fail(failureMessage);
            }
            PumpFor(TimeSpan.FromMilliseconds(30d));
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

    private static void RunSta(Action action)
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
            Name = "Nera renderer recovery stress",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(StaThreadTimeout))
        {
            Assert.Fail("Renderer recovery stress exceeded the STA timeout.");
        }
        failure?.Throw();
    }
}
