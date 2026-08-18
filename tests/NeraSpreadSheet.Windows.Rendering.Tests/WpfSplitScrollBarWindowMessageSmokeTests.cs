using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
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
using WpfResizeMode = System.Windows.ResizeMode;
using WpfWindow = System.Windows.Window;
using WpfWindowStartupLocation = System.Windows.WindowStartupLocation;
using WpfWindowStyle = System.Windows.WindowStyle;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WpfSplitScrollBarWindowMessageSmokeTests
{
    private const uint WindowMessageMouseMove = 0x0200;
    private const uint WindowMessageLeftButtonDown = 0x0201;
    private const uint WindowMessageLeftButtonUp = 0x0202;
    private const uint MouseKeyLeftButton = 0x0001;
    private static readonly TimeSpan StaThreadTimeout = TimeSpan.FromSeconds(90d);
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(15d);

    [TestMethod]
    [Timeout(120_000)]
    public void PublicWpfScrollBarDragMovesOnlyBottomRightPaneAndPersistsState()
    {
        RunInSta(() =>
        {
            var workbook = new Workbook();
            var worksheet = workbook.Worksheets[0];
            worksheet.SetValue(default, "WPF split scrollbar smoke");
            worksheet.SetValue(new CellAddress(260, 100), "extent");
            var session = new SpreadsheetSession(workbook);
            using var control = new NeraSpreadsheetControl
            {
                Background = Brushes.White,
                Session = session,
                RenderingBackend = WpfRenderingBackend.DrawingContext,
            };
            var decorator = new WpfAdornerDecorator { Child = control };
            var window = CreateOffscreenWindow(decorator);

            try
            {
                window.Show();
                window.Activate();
                window.UpdateLayout();

                using var split = control.EnableSplitPanes(
                    SpreadsheetSplitPaneMode.Both);
                split.SetSplit(340d, 230d);
                split.RenderNow();
                using var scrollBars = control.EnableSplitPaneScrollBars();
                scrollBars.Refresh();
                PumpUntil(
                    () => split.LastFrame is { Panes.Count: 4 } &&
                          scrollBars.Layout is { Count: >= 8 },
                    "The WPF split scrollbar overlay did not compose eight pane-local bars.");

                var layout = scrollBars.Layout ??
                    throw new AssertFailedException(
                        "The WPF split scrollbar layout is unavailable.");
                Assert.IsTrue(layout.TryGet(
                    SpreadsheetPaneId.BottomRight,
                    SpreadsheetScrollBarAxis.Horizontal,
                    out var horizontal));
                Assert.IsTrue(horizontal.IsScrollable);
                var trackTravel = horizontal.TrackLength - horizontal.ThumbLength;
                Assert.IsTrue(trackTravel > 8d);

                var beforeTopLeft = split.GetPaneScroll(SpreadsheetPaneId.TopLeft);
                var beforeTopRight = split.GetPaneScroll(SpreadsheetPaneId.TopRight);
                var beforeBottomLeft = split.GetPaneScroll(SpreadsheetPaneId.BottomLeft);
                var beforeBottomRight = split.GetPaneScroll(SpreadsheetPaneId.BottomRight);
                var startBody = Center(horizontal.ThumbBounds);
                var targetThumbStart = horizontal.TrackStart + (trackTravel * 0.68d);
                var targetBody = new PointD(
                    targetThumbStart + (horizontal.ThumbLength / 2d),
                    startBody.Y);
                var start = ToWindowClientPixels(control, window, startBody);
                var target = ToWindowClientPixels(control, window, targetBody);
                var handle = new WindowInteropHelper(window).Handle;
                Assert.AreNotEqual(IntPtr.Zero, handle);

                SendMouseMessage(
                    handle,
                    WindowMessageLeftButtonDown,
                    MouseKeyLeftButton,
                    start.X,
                    start.Y);
                PumpDispatcherOnce();
                SendMouseMessage(
                    handle,
                    WindowMessageMouseMove,
                    MouseKeyLeftButton,
                    target.X,
                    target.Y);
                PumpDispatcherOnce();
                SendMouseMessage(
                    handle,
                    WindowMessageLeftButtonUp,
                    0U,
                    target.X,
                    target.Y);

                PumpUntil(
                    () => split.GetPaneScroll(SpreadsheetPaneId.BottomRight).X >
                          beforeBottomRight.X + 1d,
                    "Dragging the WPF scrollbar thumb did not move the BottomRight pane.");

                var afterBottomRight = split.GetPaneScroll(
                    SpreadsheetPaneId.BottomRight);
                Assert.AreEqual(beforeBottomRight.Y, afterBottomRight.Y, 0.01d);
                Assert.AreEqual(
                    beforeTopLeft,
                    split.GetPaneScroll(SpreadsheetPaneId.TopLeft));
                Assert.AreEqual(
                    beforeTopRight,
                    split.GetPaneScroll(SpreadsheetPaneId.TopRight));
                Assert.AreEqual(
                    beforeBottomLeft,
                    split.GetPaneScroll(SpreadsheetPaneId.BottomLeft));
                Assert.AreEqual(
                    afterBottomRight.X,
                    session.View.SplitState.BottomRightScroll.OffsetX,
                    0.01d);
                Assert.AreEqual(
                    afterBottomRight.Y,
                    session.View.SplitState.BottomRightScroll.OffsetY,
                    0.01d);

                scrollBars.Refresh();
                var updatedLayout = scrollBars.Layout ??
                    throw new AssertFailedException(
                        "The WPF split scrollbar layout disappeared after dragging.");
                Assert.IsTrue(scrollBars.ScrollBarCount >= 8);
                Assert.IsTrue(updatedLayout.TryGet(
                    SpreadsheetPaneId.BottomRight,
                    SpreadsheetScrollBarAxis.Horizontal,
                    out var updatedHorizontal));
                Assert.AreEqual(
                    afterBottomRight.X,
                    updatedHorizontal.Offset,
                    0.01d);

                split.RenderingBackend = WpfRenderingBackend.Direct2DD3DImage;
                split.RenderNow();
                scrollBars.Refresh();
                PumpUntil(
                    () => split.GpuDiagnostics is
                    {
                        TextureWidth: > 0,
                        TextureHeight: > 0,
                    } &&
                    scrollBars.Layout is { Count: >= 8 },
                    "The WPF scrollbar overlay did not survive D3DImage rendering.");

                Assert.IsTrue(control.DisableSplitPaneScrollBars());
                Assert.IsFalse(control.TryGetSplitPaneScrollBarController(out _));
                Assert.IsTrue(control.DisableSplitPanes());
                Assert.IsFalse(control.TryGetSplitPaneController(out _));
            }
            finally
            {
                window.Close();
                PumpDispatcherOnce();
            }
        });
    }

    private static WpfWindow CreateOffscreenWindow(object content) => new()
    {
        Background = Brushes.White,
        Content = content,
        Height = 760d,
        Left = -30_000d,
        ResizeMode = WpfResizeMode.NoResize,
        ShowInTaskbar = false,
        Title = "Nera WPF split scrollbar smoke host",
        Top = -30_000d,
        Width = 1000d,
        WindowStartupLocation = WpfWindowStartupLocation.Manual,
        WindowStyle = WpfWindowStyle.None,
    };

    private static PointD Center(RectD bounds) => new(
        bounds.Left + (bounds.Width / 2d),
        bounds.Top + (bounds.Height / 2d));

    private static DevicePoint ToWindowClientPixels(
        NeraSpreadsheetControl control,
        WpfWindow window,
        PointD bodyPoint)
    {
        var controlPoint = new Point(
            control.RenderTheme.RowHeaderWidth + bodyPoint.X,
            control.RenderTheme.ColumnHeaderHeight + bodyPoint.Y);
        var windowPoint = control.TranslatePoint(controlPoint, window);
        var source = PresentationSource.FromVisual(window) ??
            throw new AssertFailedException(
                "The WPF presentation source is unavailable.");
        var pixels = source.CompositionTarget.TransformToDevice.Transform(windowPoint);
        return new DevicePoint(
            (int)Math.Round(pixels.X),
            (int)Math.Round(pixels.Y));
    }

    private static void SendMouseMessage(
        IntPtr windowHandle,
        uint message,
        uint keyState,
        int x,
        int y)
    {
        Assert.IsTrue(x >= short.MinValue && x <= short.MaxValue);
        Assert.IsTrue(y >= short.MinValue && y <= short.MaxValue);
        var packed = unchecked(
            ((y & 0xFFFF) << 16) |
            (x & 0xFFFF));
        _ = SendMessage(
            windowHandle,
            message,
            new UIntPtr(keyState),
            new IntPtr(packed));
    }

    private static void PumpUntil(Func<bool> condition, string timeoutMessage)
    {
        ArgumentNullException.ThrowIfNull(condition);
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
            TimeSpan.FromMilliseconds(2d),
            WpfDispatcherPriority.Background,
            (_, _) => frame.Continue = false,
            WpfDispatcher.CurrentDispatcher);
        timer.Start();
        WpfDispatcher.PushFrame(frame);
        timer.Stop();
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
            Name = "Nera WPF split scrollbar window-message smoke",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(StaThreadTimeout))
        {
            Assert.Fail("The WPF split scrollbar window-message smoke timed out.");
        }
        failure?.Throw();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(
        IntPtr windowHandle,
        uint message,
        UIntPtr keyState,
        IntPtr packedCoordinates);

    private readonly record struct DevicePoint(int X, int Y);
}
