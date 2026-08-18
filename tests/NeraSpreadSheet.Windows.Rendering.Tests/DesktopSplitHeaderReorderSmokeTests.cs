using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.WinForms;
using NeraSpreadSheet.Wpf;
using WinFormsApplication = System.Windows.Forms.Application;
using WinFormsControl = System.Windows.Forms.Control;
using WinFormsForm = System.Windows.Forms.Form;
using WinFormsFormStartPosition = System.Windows.Forms.FormStartPosition;
using WinFormsMessage = System.Windows.Forms.Message;
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
public sealed class DesktopSplitHeaderReorderSmokeTests
{
    private const int WindowMessageMouseMove = 0x0200;
    private const int WindowMessageLeftButtonDown = 0x0201;
    private const int WindowMessageLeftButtonUp = 0x0202;
    private const int MouseKeyLeftButton = 0x0001;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private static readonly TimeSpan StaThreadTimeout = TimeSpan.FromSeconds(90d);
    private static readonly TimeSpan WpfInputTimeout = TimeSpan.FromSeconds(15d);

    [TestMethod]
    [Timeout(120_000)]
    public void WinFormsSplitHeaderDragReordersRowAndSupportsUndo()
    {
        RunInSta(() =>
        {
            WinFormsApplication.SetHighDpiMode(
                System.Windows.Forms.HighDpiMode.SystemAware);
            WinFormsApplication.EnableVisualStyles();

            var session = CreateRowReorderSession();
            using var form = new WinFormsForm
            {
                Width = 1000,
                Height = 760,
                ShowInTaskbar = false,
                StartPosition = WinFormsFormStartPosition.Manual,
                Location = new System.Drawing.Point(-30_000, -30_000),
            };
            using var control = new NeraSpreadSheet.WinForms.NeraSpreadsheetControl
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                Session = session,
                RenderingBackend = WinFormsRenderingBackend.GdiPlus,
            };
            form.Controls.Add(control);
            form.Show();
            WinFormsApplication.DoEvents();

            using var split = control.EnableSplitPanes(
                NeraSpreadSheet.WinForms.SpreadsheetSplitPaneMode.Both);
            split.SetSplit(340d, 230d);
            split.RenderNow();
            var frame = split.LastFrame ??
                throw new AssertFailedException("The WinForms split frame is unavailable.");
            Assert.IsTrue(frame.TryGetPane(
                SpreadsheetPaneId.TopLeft,
                out var topLeft));
            var sourceSlot = FindRow(topLeft, 2);
            var targetSlot = FindRow(topLeft, 5);
            var sourceX = (int)Math.Round(control.RenderTheme.RowHeaderWidth / 2d);
            var sourceY = (int)Math.Round(
                control.RenderTheme.ColumnHeaderHeight +
                topLeft.Pane.Bounds.Top +
                sourceSlot.Start +
                (sourceSlot.Size / 2d));
            var targetY = (int)Math.Round(
                control.RenderTheme.ColumnHeaderHeight +
                topLeft.Pane.Bounds.Top +
                targetSlot.Start +
                (targetSlot.Size / 4d));
            var surface = control.Controls
                .Cast<WinFormsControl>()
                .Single(candidate =>
                    candidate.GetType().Name == "NeraSpreadsheetSplitSurface");

            DispatchMouseMessage(
                surface,
                WindowMessageLeftButtonDown,
                MouseKeyLeftButton,
                sourceX,
                sourceY);
            DispatchMouseMessage(
                surface,
                WindowMessageMouseMove,
                MouseKeyLeftButton,
                sourceX,
                targetY);
            Assert.IsNotNull(GetPrivateField(
                surface,
                "_headerReorderDropTarget"));
            DispatchMouseMessage(
                surface,
                WindowMessageLeftButtonUp,
                0,
                sourceX,
                targetY);
            WinFormsApplication.DoEvents();

            AssertRowMoveCommitted(session);
            Assert.IsNull(GetPrivateField(surface, "_headerReorderDropTarget"));
            Assert.AreEqual("Reorder rows", session.History.NextUndoDescription);
            Assert.IsTrue(session.Undo());
            Assert.AreEqual(
                "row-2",
                session.ActiveWorksheet.GetValue(new CellAddress(2, 0)));
            split.RenderNow();

            form.Close();
            WinFormsApplication.DoEvents();
        });
    }

    [TestMethod]
    [Timeout(120_000)]
    public void WpfSplitHeaderDragUsesNativePointerAndRendersAfterReorder()
    {
        RunInSta(() =>
        {
            Assert.IsTrue(
                GetCursorPos(out var originalCursor),
                "The native cursor position could not be captured.");
            var session = CreateRowReorderSession();
            using var control = new NeraSpreadSheet.Wpf.NeraSpreadsheetControl
            {
                Background = WpfBrushes.White,
                Session = session,
                RenderingBackend = WpfRenderingBackend.DrawingContext,
            };
            var decorator = new WpfAdornerDecorator { Child = control };
            var window = CreateWpfInputHost(decorator);

            try
            {
                window.Show();
                Assert.IsTrue(window.Activate());
                window.UpdateLayout();
                Assert.IsTrue(control.Focus());

                using var split = control.EnableSplitPanes(
                    NeraSpreadSheet.Wpf.SpreadsheetSplitPaneMode.Both);
                split.SetSplit(340d, 230d);
                split.RenderNow();
                PumpUntil(
                    () => split.LastFrame is { Panes.Count: 4 },
                    "The WPF split frame was not composed.");
                var frame = split.LastFrame ??
                    throw new AssertFailedException("The WPF split frame is unavailable.");
                Assert.IsTrue(frame.TryGetPane(
                    SpreadsheetPaneId.TopLeft,
                    out var topLeft));
                var sourceSlot = FindRow(topLeft, 2);
                var targetSlot = FindRow(topLeft, 5);
                var sourceBody = new NeraSpreadSheet.Foundation.PointD(
                    -control.RenderTheme.RowHeaderWidth / 2d,
                    topLeft.Pane.Bounds.Top +
                    sourceSlot.Start +
                    (sourceSlot.Size / 2d));
                var targetBody = new NeraSpreadSheet.Foundation.PointD(
                    sourceBody.X,
                    topLeft.Pane.Bounds.Top +
                    targetSlot.Start +
                    (targetSlot.Size / 4d));
                var source = ToScreenPixels(control, sourceBody);
                var target = ToScreenPixels(control, targetBody);

                Assert.IsTrue(SetCursorPos(source.X, source.Y));
                PumpDispatcherOnce();
                MouseEvent(MouseEventLeftDown, 0U, 0U, 0U, UIntPtr.Zero);
                PumpDispatcherOnce();
                Assert.IsTrue(SetCursorPos(target.X, target.Y));
                PumpDispatcherOnce();
                MouseEvent(MouseEventLeftUp, 0U, 0U, 0U, UIntPtr.Zero);

                PumpUntil(
                    () => Equals(
                        "row-2",
                        session.ActiveWorksheet.GetValue(new CellAddress(4, 0))),
                    "The native WPF header drag did not commit the row reorder.");
                AssertRowMoveCommitted(session);
                Assert.AreEqual(
                    "Reorder rows",
                    session.History.NextUndoDescription);

                split.RenderingBackend = WpfRenderingBackend.Direct2DD3DImage;
                split.RenderNow();
                PumpUntil(
                    () => split.GpuDiagnostics is
                    {
                        TextureWidth: > 0,
                        TextureHeight: > 0,
                    },
                    "The WPF D3DImage path did not render after header reordering.");
                Assert.IsTrue(session.Undo());
                Assert.AreEqual(
                    "row-2",
                    session.ActiveWorksheet.GetValue(new CellAddress(2, 0)));
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

    private static SpreadsheetSession CreateRowReorderSession()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        for (var row = 0; row <= 12; row++)
        {
            worksheet.SetValue(
                new CellAddress(row, 0),
                $"row-{row}");
        }
        worksheet.SetValue(new CellAddress(120, 30), "extent");
        worksheet.SetFormula(new CellAddress(0, 1), "=A3");
        return new SpreadsheetSession(workbook);
    }

    private static void AssertRowMoveCommitted(SpreadsheetSession session)
    {
        var worksheet = session.ActiveWorksheet;
        Assert.AreEqual("row-3", worksheet.GetValue(new CellAddress(2, 0)));
        Assert.AreEqual("row-4", worksheet.GetValue(new CellAddress(3, 0)));
        Assert.AreEqual("row-2", worksheet.GetValue(new CellAddress(4, 0)));
        Assert.AreEqual("=A5", worksheet.GetFormula(new CellAddress(0, 1)));
        Assert.AreEqual(
            new CellRange(
                new CellAddress(4, 0),
                new CellAddress(4, SpreadsheetLimits.MaxColumns - 1)),
            session.Selection.Ranges.Single());
    }

    private static AxisSlot FindRow(
        SpreadsheetSplitPaneFrame pane,
        int rowIndex) =>
        pane.ViewportFrame.Layout.Rows.Single(slot => slot.Index == rowIndex);

    private static object? GetPrivateField(object target, string fieldName)
    {
        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        return field.GetValue(target);
    }

    private static void DispatchMouseMessage(
        WinFormsControl surface,
        int messageId,
        int keyState,
        int x,
        int y)
    {
        var method = surface.GetType().GetMethod(
            "WndProc",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        var packed = unchecked((y << 16) | (x & 0xFFFF));
        object?[] arguments =
        [
            WinFormsMessage.Create(
                surface.Handle,
                messageId,
                new IntPtr(keyState),
                new IntPtr(packed)),
        ];
        method.Invoke(surface, arguments);
    }

    private static WpfWindow CreateWpfInputHost(object content) => new()
    {
        Background = WpfBrushes.White,
        Content = content,
        Height = 650d,
        Left = 0d,
        ResizeMode = WpfResizeMode.NoResize,
        ShowActivated = true,
        ShowInTaskbar = false,
        Title = "Nera WPF split header reorder input smoke host",
        Top = 0d,
        Topmost = true,
        Width = 900d,
        WindowStartupLocation = WpfWindowStartupLocation.Manual,
        WindowStyle = WpfWindowStyle.None,
    };

    private static DevicePoint ToScreenPixels(
        NeraSpreadSheet.Wpf.NeraSpreadsheetControl control,
        NeraSpreadSheet.Foundation.PointD bodyPoint)
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
        var deadline = DateTime.UtcNow + WpfInputTimeout;
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
            Name = "Nera split header reorder smoke",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(StaThreadTimeout))
        {
            Assert.Fail("The split header reorder STA smoke timed out.");
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
