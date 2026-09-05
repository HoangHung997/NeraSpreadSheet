using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering;
using NeraSpreadSheet.Rendering.Direct2D;
using DrawingPoint = System.Drawing.Point;
using DrawingSize = System.Drawing.Size;
using WinFormsApplication = System.Windows.Forms.Application;
using WinFormsForm = System.Windows.Forms.Form;
using WinFormsFormBorderStyle = System.Windows.Forms.FormBorderStyle;
using WinFormsFormStartPosition = System.Windows.Forms.FormStartPosition;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class Direct2DRendererSmokeTests
{
    private const int ResizeStressCycles = 20;
    private static readonly TimeSpan StaThreadTimeout = TimeSpan.FromSeconds(45d);

    [TestMethod]
    [Timeout(60_000)]
    public void HwndRendererRendersNestedDisplayListReusesLayoutsAndSurvivesResizeStress()
    {
        RunInSta(() =>
        {
            using var form = CreateOffscreenHost(320, 180);
            using var renderer = new Direct2DHwndDisplayListRenderer(
                form.Handle,
                320,
                180,
                textLayoutCacheCapacity: 16);
            var displayList = CreateNestedDisplayList(320d, 180d);

            renderer.Render(displayList);
            renderer.Render(displayList);

            var diagnostics = renderer.Diagnostics;
            Assert.AreEqual(320, diagnostics.PixelWidth);
            Assert.AreEqual(180, diagnostics.PixelHeight);
            Assert.IsTrue(diagnostics.CachedTextLayouts >= 1);
            Assert.IsTrue(
                diagnostics.TextLayoutCacheHits >= 1,
                $"Expected a reused DirectWrite layout, but hits={diagnostics.TextLayoutCacheHits}.");

            var finalSize = ExerciseResizeAndRenderCycles(
                form,
                renderer.Resize,
                renderer.Render);

            diagnostics = renderer.Diagnostics;
            Assert.AreEqual(finalSize.Width, diagnostics.PixelWidth);
            Assert.AreEqual(finalSize.Height, diagnostics.PixelHeight);
            Assert.AreEqual(0L, diagnostics.DeviceRecoveryCount);
            Assert.IsTrue(
                diagnostics.TextLayoutCacheHits >= ResizeStressCycles,
                $"Expected layout reuse throughout resize stress, but hits={diagnostics.TextLayoutCacheHits}.");
        });
    }

    [TestMethod]
    [Timeout(60_000)]
    public void SwapChainRendererPresentsReportsAdapterAndSurvivesResizeStress()
    {
        RunInSta(() =>
        {
            using var form = CreateOffscreenHost(320, 180);
            using var renderer = new Direct2DSwapChainDisplayListRenderer(
                form.Handle,
                320,
                180,
                textLayoutCacheCapacity: 16)
            {
                VSync = false,
            };
            var displayList = CreateNestedDisplayList(320d, 180d);

            renderer.Render(displayList);
            renderer.Render(displayList);

            var diagnostics = renderer.Diagnostics;
            Assert.AreEqual(320, diagnostics.PixelWidth);
            Assert.AreEqual(180, diagnostics.PixelHeight);
            Assert.IsFalse(diagnostics.VSync);
            Assert.IsFalse(string.IsNullOrWhiteSpace(diagnostics.AdapterName));
            Assert.IsFalse(string.IsNullOrWhiteSpace(diagnostics.DeviceFeatureLevel));
            Assert.IsTrue(diagnostics.CachedTextLayouts >= 1);
            Assert.IsTrue(
                diagnostics.TextLayoutCacheHits >= 1,
                $"Expected a reused DirectWrite layout, but hits={diagnostics.TextLayoutCacheHits}.");

            var finalSize = ExerciseResizeAndRenderCycles(
                form,
                renderer.Resize,
                renderer.Render);

            diagnostics = renderer.Diagnostics;
            Assert.AreEqual(finalSize.Width, diagnostics.PixelWidth);
            Assert.AreEqual(finalSize.Height, diagnostics.PixelHeight);
            Assert.AreEqual(0L, diagnostics.DeviceRecoveryCount);
            Assert.IsFalse(string.IsNullOrWhiteSpace(diagnostics.AdapterName));
            Assert.IsTrue(
                diagnostics.TextLayoutCacheHits >= ResizeStressCycles,
                $"Expected layout reuse throughout resize stress, but hits={diagnostics.TextLayoutCacheHits}.");
        });
    }

    private static DrawingSize ExerciseResizeAndRenderCycles(
        WinFormsForm form,
        Action<int, int> resize,
        Action<DisplayList> render)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(resize);
        ArgumentNullException.ThrowIfNull(render);
        var finalSize = DrawingSize.Empty;
        for (var cycle = 0; cycle < ResizeStressCycles; cycle++)
        {
            finalSize = new DrawingSize(
                300 + ((cycle * 37) % 181),
                170 + ((cycle * 29) % 121));
            form.ClientSize = finalSize;
            WinFormsApplication.DoEvents();
            resize(finalSize.Width, finalSize.Height);
            render(CreateNestedDisplayList(finalSize.Width, finalSize.Height));
        }
        return finalSize;
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
            Text = "NeraSpreadSheet renderer smoke host",
        };
        form.Show();
        WinFormsApplication.DoEvents();
        Assert.AreNotEqual(IntPtr.Zero, form.Handle);
        return form;
    }

    private static DisplayList CreateNestedDisplayList(double width, double height)
    {
        var childBuilder = new DisplayListBuilder();
        childBuilder.FillRectangle(
            new RectD(8d, 8d, 220d, 42d),
            new ColorRgba(225, 236, 250));
        childBuilder.DrawText(
            "Nera GPU runtime smoke",
            new RectD(12d, 12d, 210d, 30d),
            new TextStyle("Segoe UI", 14d, 600, new ColorRgba(25, 70, 130)));
        var child = childBuilder.Build();

        var rootBuilder = new DisplayListBuilder();
        rootBuilder.FillRectangle(
            new RectD(0d, 0d, width, height),
            ColorRgba.White);
        rootBuilder.PushClip(new RectD(0d, 0d, width, height));
        rootBuilder.DrawLine(
            new PointD(4d, 4d),
            new PointD(width - 4d, height - 4d),
            1d,
            new ColorRgba(128, 128, 128));
        rootBuilder.FillPolygon(
            [
                new PointD(36d, 78d),
                new PointD(112d, 64d),
                new PointD(146d, 118d),
                new PointD(72d, 132d),
            ],
            new ColorRgba(68, 114, 196));
        rootBuilder.PushTranslation(4.25d, 2.5d);
        rootBuilder.DrawDisplayList(child);
        rootBuilder.PopTranslation();
        rootBuilder.PopClip();
        return rootBuilder.Build();
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
            Name = "NeraSpreadSheet Windows renderer smoke",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        if (!thread.Join(StaThreadTimeout))
        {
            Assert.Fail("The STA renderer smoke thread did not complete within the timeout.");
        }

        failure?.Throw();
    }
}
