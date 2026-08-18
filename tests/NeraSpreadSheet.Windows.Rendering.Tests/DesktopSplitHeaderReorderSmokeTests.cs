using System.Reflection;
using System.Runtime.ExceptionServices;
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
using WpfResizeMode = System.Windows.ResizeMode;
using WpfWindow = System.Windows.Window;
using WpfWindowStartupLocation = System.Windows.WindowStartupLocation;
using WpfWindowStyle = System.Windows.WindowStyle;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class DesktopSplitHeaderReorderSmokeTests
{
    private const uint WindowMessageMouseMove = 0x0200;
    private const uint WindowMessageLeftButtonDown = 0x0201;
    private const uint WindowMessageLeftButtonUp = 0x0202;
    private const uint MouseKeyLeftButton = 0x0001;
    private static readonly TimeSpan StaThreadTimeout = TimeSpan.FromSeconds(90d);
    private static readonly TimeSpan WpfRenderTimeout = TimeSpan.FromSeconds(15d);

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
                throw new AssertFailedException(
                    "The WinForms split frame is unavailable.");
            Assert.IsTrue(frame.TryGetPane(
                SpreadsheetPaneId.TopLeft,
                out var topLeft));
            var sourceSlot = FindRow(topLeft, 2);
            var targetSlot = FindRow(topLeft, 5);
            var sourceX = (int)Math.Round(
                control.RenderTheme.RowHeaderWidth / 2d);
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
                    candidate.GetType().Name ==
                    "NeraSpreadsheetSplitSurface");

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
                0U,
                sourceX,
                targetY);
            WinFormsApplication.DoEvents();

            AssertRowMoveCommitted(session);
            Assert.IsNull(GetPrivateField(
                surface,
                "_headerReorderDropTarget"));
            Assert.AreEqual(
                "Reorder rows",
                session.History.NextUndoDescription);
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
    public void WpfSplitHeaderDragStateMachineCommitsAndRendersAfterReorder()
    {
        RunInSta(() =>
        {
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
                window.UpdateLayout();
                using var split = control.EnableSplitPanes(
                    NeraSpreadSheet.Wpf.SpreadsheetSplitPaneMode.Both);
                split.SetSplit(340d, 230d);
                split.RenderNow();
                PumpUntil(
                    () => split.LastFrame is { Panes.Count: 4 },
                    "The WPF split frame was not composed.");
                var frame = split.LastFrame ??
                    throw new AssertFailedException(
                        "The WPF split frame is unavailable.");
                Assert.IsTrue(frame.TryGetPane(
                    SpreadsheetPaneId.TopLeft,
                    out var topLeft));
                var sourceSlot = FindRow(topLeft, 2);
                var targetSlot = FindRow(topLeft, 5);
                var sourceX = control.RenderTheme.RowHeaderWidth / 2d;
                var sourceY =
                    control.RenderTheme.ColumnHeaderHeight +
                    topLeft.Pane.Bounds.Top +
                    sourceSlot.Start +
                    (sourceSlot.Size / 2d);
                var targetY =
                    control.RenderTheme.ColumnHeaderHeight +
                    topLeft.Pane.Bounds.Top +
                    targetSlot.Start +
                    (targetSlot.Size / 4d);
                var adorner = GetSplitAdorner(split);

                Assert.IsTrue((bool)(InvokePrivate(
                    adorner,
                    "TryBeginHeaderReorderCandidate",
                    sourceX,
                    sourceY) ?? false));
                Assert.IsTrue((bool)(InvokePrivate(
                    adorner,
                    "UpdateHeaderReorder",
                    sourceX,
                    sourceY + 8d,
                    true) ?? false));
                Assert.IsNotNull(GetPrivateField(
                    adorner,
                    "_headerReorderDropTarget"));
                Assert.IsNotNull(GetPrivateField(
                    adorner,
                    "_headerReorderPreviewVisual"));
                Assert.IsTrue((bool)(InvokePrivate(
                    adorner,
                    "UpdateHeaderReorder",
                    sourceX,
                    targetY,
                    true) ?? false));
                Assert.IsTrue((bool)(InvokePrivate(
                    adorner,
                    "CompleteHeaderReorder",
                    sourceX,
                    targetY) ?? false));

                AssertRowMoveCommitted(session);
                Assert.AreEqual(
                    "Reorder rows",
                    session.History.NextUndoDescription);
                Assert.IsNull(GetPrivateField(
                    adorner,
                    "_headerReorderDropTarget"));
                Assert.IsNull(GetPrivateField(
                    adorner,
                    "_headerReorderPreviewVisual"));

                split.RenderingBackend =
                    WpfRenderingBackend.Direct2DD3DImage;
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
        Assert.AreEqual(
            "row-3",
            worksheet.GetValue(new CellAddress(2, 0)));
        Assert.AreEqual(
            "row-4",
            worksheet.GetValue(new CellAddress(3, 0)));
        Assert.AreEqual(
            "row-2",
            worksheet.GetValue(new CellAddress(4, 0)));
        Assert.AreEqual(
            "=A5",
            worksheet.GetFormula(new CellAddress(0, 1)));
        Assert.AreEqual(
            new CellRange(
                new CellAddress(4, 0),
                new CellAddress(
                    4,
                    SpreadsheetLimits.MaxColumns - 1)),
            session.Selection.Ranges.Single());
    }

    private static AxisSlot FindRow(
        SpreadsheetSplitPaneFrame pane,
        int rowIndex) =>
        pane.ViewportFrame.Layout.Rows.Single(
            slot => slot.Index == rowIndex);

    private static object GetSplitAdorner(
        NeraSpreadSheet.Wpf.NeraSpreadsheetSplitController controller)
    {
        var field = controller.GetType().GetField(
            "_adorner",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        return field.GetValue(controller) ??
            throw new AssertFailedException(
                "The WPF split adorner is unavailable.");
    }

    private static object? GetPrivateField(
        object target,
        string fieldName)
    {
        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        return field.GetValue(target);
    }

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

    private static void DispatchMouseMessage(
        WinFormsControl surface,
        uint messageId,
        uint keyState,
        int x,
        int y)
    {
        var method = surface.GetType().GetMethod(
            "WndProc",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        object?[] arguments =
        [
            WinFormsMessage.Create(
                surface.Handle,
                checked((int)messageId),
                new IntPtr(checked((int)keyState)),
                new IntPtr(PackCoordinates(x, y))),
        ];
        method.Invoke(surface, arguments);
    }

    private static int PackCoordinates(int x, int y) =>
        unchecked(((y & 0xFFFF) << 16) | (x & 0xFFFF));

    private static WpfWindow CreateWpfInputHost(object content) => new()
    {
        Background = WpfBrushes.White,
        Content = content,
        Height = 650d,
        Left = -30_000d,
        ResizeMode = WpfResizeMode.NoResize,
        ShowActivated = false,
        ShowInTaskbar = false,
        Title = "Nera WPF split header reorder smoke host",
        Top = -30_000d,
        Width = 900d,
        WindowStartupLocation = WpfWindowStartupLocation.Manual,
        WindowStyle = WpfWindowStyle.ToolWindow,
    };

    private static void PumpUntil(
        Func<bool> condition,
        string timeoutMessage)
    {
        ArgumentNullException.ThrowIfNull(condition);
        var deadline = DateTime.UtcNow + WpfRenderTimeout;
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
}
