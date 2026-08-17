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
    private static readonly TimeSpan StaThreadTimeout = TimeSpan.FromSeconds(45d);

    [TestMethod]
    [Timeout(60_000)]
    public void HwndRendererRendersNestedDisplayListAndReusesTextLayout()
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

            form.ClientSize = new DrawingSize(360, 200);
            WinFormsApplication.DoEvents();
            renderer.Resize(360, 200);
            renderer.Render(displayList);

            diagnostics = renderer.Diagnostics;
            Assert.AreEqual(360, diagnostics.PixelWidth);
            Assert.AreEqual(200, diagnostics.PixelHeight);
        });
    }

    [TestMethod]
    [Timeout(60_000)]
    public void SwapChainRendererPresentsNestedDisplayListAndReportsAdapter()
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

            form.ClientSize = new DrawingSize(360, 200);
            WinFormsApplication.DoEvents();
            renderer.Resize(360, 200);
            renderer.Render(displayList);

            diagnostics = renderer.Diagnostics;
            Assert.AreEqual(360, diagnostics.PixelWidth);
            Assert.AreEqual(200, diagnostics.PixelHeight);
        });
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
