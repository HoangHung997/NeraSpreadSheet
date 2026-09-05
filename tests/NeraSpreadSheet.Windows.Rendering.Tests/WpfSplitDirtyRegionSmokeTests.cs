using System.Reflection;
using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Layout;
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
public sealed class WpfSplitDirtyRegionSmokeTests
{
    private static readonly TimeSpan StaThreadTimeout = TimeSpan.FromSeconds(75d);
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(15d);

    [TestMethod]
    [Timeout(105_000)]
    public void D3DImageUsesMultipleDirtyRectanglesAndDrawingContextFallsBackToFull()
    {
        RunInSta(() =>
        {
            var workbook = new Workbook();
            var worksheet = workbook.Worksheets[0];
            worksheet.SetValue(new CellAddress(200, 120), "extent");
            var session = new SpreadsheetSession(workbook);
            using var control = new NeraSpreadsheetControl
            {
                Width = 960d,
                Height = 700d,
                Session = session,
                RenderingBackend = WpfRenderingBackend.Direct2DD3DImage,
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
                controller.ScrollPaneTo(SpreadsheetPaneId.TopRight, 160d, 0d);
                controller.ScrollPaneTo(SpreadsheetPaneId.BottomLeft, 0d, 100d);
                controller.ScrollPaneTo(SpreadsheetPaneId.BottomRight, 160d, 100d);
                controller.RenderNow();
                PumpUntil(
                    () => controller.GpuDiagnostics is
                    {
                        TextureWidth: > 0,
                        TextureHeight: > 0,
                    } &&
                    GetPresentedDirtyRectangles(GetAdorner(controller)).Count > 0,
                    "The initial WPF split D3DImage frame was not presented.");

                var adorner = GetAdorner(controller);
                var diagnostics = controller.GpuDiagnostics ??
                    throw new AssertFailedException(
                        "WPF GPU diagnostics were unavailable after initial render.");
                var partialBefore = GetLong(adorner, "PartialDirtyRenderCount");
                var fullBefore = GetLong(adorner, "FullDirtyRenderCount");
                var target = new CellAddress(6, 3);

                worksheet.SetValue(target, "partial-dirty");
                PumpUntil(
                    () => GetLong(adorner, "PartialDirtyRenderCount") == partialBefore + 1L &&
                          GetPresentedDirtyRectangles(adorner).Count == 4,
                    "The WPF D3DImage split surface did not present four dirty rectangles.");

                Assert.AreEqual(
                    fullBefore,
                    GetLong(adorner, "FullDirtyRenderCount"));
                Assert.AreEqual(4, GetInt(adorner, "LastDirtyRegionCount"));
                var dirtyBounds = GetDirtyBounds(adorner);
                Assert.AreEqual(4, dirtyBounds.Count);
                var presented = GetPresentedDirtyRectangles(adorner);
                Assert.AreEqual(4, presented.Count);
                Assert.IsTrue(presented.All(rectangle =>
                    rectangle.Width > 0 &&
                    rectangle.Height > 0 &&
                    rectangle.Width < diagnostics.TextureWidth &&
                    rectangle.Height < diagnostics.TextureHeight));
                Assert.IsFalse(presented.Any(rectangle =>
                    rectangle.X == 0 &&
                    rectangle.Y == 0 &&
                    rectangle.Width == diagnostics.TextureWidth &&
                    rectangle.Height == diagnostics.TextureHeight));

                controller.RenderingBackend = WpfRenderingBackend.DrawingContext;
                controller.RenderNow();
                fullBefore = GetLong(adorner, "FullDirtyRenderCount");
                worksheet.SetValue(target, "drawing-context-full");
                PumpDispatcherOnce();

                Assert.AreEqual(
                    fullBefore + 1L,
                    GetLong(adorner, "FullDirtyRenderCount"));
                Assert.AreEqual(0, GetInt(adorner, "LastDirtyRegionCount"));
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
        return field.GetValue(controller) ??
            throw new AssertFailedException("The WPF split adorner is unavailable.");
    }

    private static long GetLong(object instance, string propertyName) =>
        (long)(GetProperty(instance, propertyName).GetValue(instance) ?? 0L);

    private static int GetInt(object instance, string propertyName) =>
        (int)(GetProperty(instance, propertyName).GetValue(instance) ?? 0);

    private static IReadOnlyList<RectD> GetDirtyBounds(object adorner) =>
        (IReadOnlyList<RectD>)(
            GetProperty(adorner, "LastDirtyBounds").GetValue(adorner) ??
            Array.Empty<RectD>());

    private static IReadOnlyList<Int32Rect> GetPresentedDirtyRectangles(
        object adorner) =>
        (IReadOnlyList<Int32Rect>)(
            GetProperty(adorner, "LastPresentedDirtyRectangles").GetValue(adorner) ??
            Array.Empty<Int32Rect>());

    private static PropertyInfo GetProperty(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(property);
        return property;
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
            "The WPF dirty-region STA thread timed out.");
        if (failure is not null)
        {
            throw new AssertFailedException(
                $"WPF split dirty-region smoke failed.{Environment.NewLine}{failure}",
                failure);
        }
    }
}
