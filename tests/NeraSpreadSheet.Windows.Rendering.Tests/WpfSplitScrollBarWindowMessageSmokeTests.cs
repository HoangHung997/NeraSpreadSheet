using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Windows;
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
using WpfPoint = System.Windows.Point;
using WpfResizeMode = System.Windows.ResizeMode;
using WpfWindow = System.Windows.Window;
using WpfWindowStartupLocation = System.Windows.WindowStartupLocation;
using WpfWindowStyle = System.Windows.WindowStyle;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WpfSplitScrollBarWindowMessageSmokeTests
{
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private static readonly TimeSpan StaThreadTimeout = TimeSpan.FromSeconds(90d);
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(15d);

    [TestMethod]
    [Timeout(120_000)]
    public void PublicWpfScrollBarDragMovesOnlyBottomRightPaneAndPersistsState()
    {
        RunInSta(() =>
        {
            Assert.IsTrue(
                GetCursorPos(out var originalCursor),
                "The native mouse cursor position could not be captured.");
            var workbook = new Workbook();
            var worksheet = workbook.Worksheets[0];
            worksheet.SetValue(default, "WPF split scrollbar smoke");
            worksheet.SetValue(new CellAddress(260, 100), "extent");
            var session = new SpreadsheetSession(workbook);
            using var control = new NeraSpreadsheetControl
            {
                Background = WpfBrushes.White,
                Session = session,
                RenderingBackend = WpfRenderingBackend.DrawingContext,
            };
            var decorator = new WpfAdornerDecorator { Child = control };
            var window = CreateInputHostWindow(decorator);

            try
            {
                window.Show();
                Assert.IsTrue(window.Activate());
                window.UpdateLayout();
                Assert.IsTrue(control.Focus());

                using var split = control.EnableSplitPanes(
                    SpreadsheetSplitPaneMode.Both);
                split.SetSplit(300d, 190d);
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
                var start = ToScreenPixels(control, startBody);
                var target = ToScreenPixels(control, targetBody);

                Assert.IsTrue(SetCursorPos(start.X, start.Y));
                PumpDispatcherOnce();
                MouseEvent(MouseEventLeftDown, 0U, 0U, 0U, UIntPtr.Zero);
                PumpDispatcherOnce();
                Assert.IsTrue(SetCursorPos(target.X, target.Y));
                PumpDispatcherOnce();
                MouseEvent(MouseEventLeftUp, 0U, 0U, 0U, UIntPtr.Zero);

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
                MouseEvent(MouseEventLeftUp, 0U, 0U, 0U, UIntPtr.Zero);
                _ = SetCursorPos(originalCursor.X, originalCursor.Y);
                window.Close();
                PumpDispatcherOnce();
            }
        });
    }

    private static WpfWindow CreateInputHostWindow(object content) => new()
    {
        Background = WpfBrushes.White,
        Content = content,
        Height = 650d,
        Left = 0d,
        ResizeMode = WpfResizeMode.NoResize,
        ShowActivated = true,
        ShowInTaskbar = false,
        Title = "Nera WPF split scrollbar input smoke host",
        Top = 0d,
        Topmost = true,
        Width = 900d,
        WindowStartupLocation = WpfWindowStartupLocation.Manual,
        WindowStyle = WpfWindowStyle.None,
    };

    private static PointD Center(RectD bounds) => new(
        bounds.Left + (bounds.Width / 2d),
        bounds.Top + (bounds.Height / 2d));

    private static DevicePoint ToScreenPixels(
        NeraSpreadsheetControl control,
        PointD bodyPoint)
    {
        var point = control.PointToScreen(new WpfPoint(
            control.RenderTheme.RowHeaderWidth + bodyPoint.X,
            control.RenderTheme.ColumnHeaderHeight + bodyPoint.Y));
        return new DevicePoint(
            (int)Math.Round(point.X),
            (int)Math.Round(point.Y));
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
            Name = "Nera WPF split scrollbar native-input smoke",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(StaThreadTimeout))
        {
            Assert.Fail("The WPF split scrollbar native-input smoke timed out.");
        }
        failure?.Throw();
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", EntryPoint = "mouse_event")]
    private static extern void MouseEvent(
        uint flags,
        uint deltaX,
        uint deltaY,
        uint data,
        UIntPtr extraInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    private readonly record struct DevicePoint(int X, int Y);
}
