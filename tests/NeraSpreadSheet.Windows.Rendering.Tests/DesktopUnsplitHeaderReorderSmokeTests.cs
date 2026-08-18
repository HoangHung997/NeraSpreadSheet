using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Viewport;
using WinFormsApplication = System.Windows.Forms.Application;
using WinFormsControl = System.Windows.Forms.Control;
using WinFormsDockStyle = System.Windows.Forms.DockStyle;
using WinFormsForm = System.Windows.Forms.Form;
using WinFormsFormStartPosition = System.Windows.Forms.FormStartPosition;
using WinFormsMouseButtons = System.Windows.Forms.MouseButtons;
using WinFormsMouseEventArgs = System.Windows.Forms.MouseEventArgs;
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
public sealed class DesktopUnsplitHeaderReorderSmokeTests
{
    private static readonly TimeSpan StaThreadTimeout = TimeSpan.FromSeconds(90d);
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(15d);

    [TestMethod]
    [Timeout(120_000)]
    public void WinFormsUnsplitControllerCommitsRowMoveAndSupportsUndo()
    {
        RunInSta(() =>
        {
            WinFormsApplication.EnableVisualStyles();
            var session = CreateRowReorderSession();
            using var form = new WinFormsForm
            {
                Width = 900,
                Height = 650,
                ShowInTaskbar = false,
                StartPosition = WinFormsFormStartPosition.Manual,
                Location = new System.Drawing.Point(-30_000, -30_000),
            };
            using var control = new NeraSpreadSheet.WinForms.NeraSpreadsheetControl
            {
                Dock = WinFormsDockStyle.Fill,
                Session = session,
                RenderingBackend =
                    NeraSpreadSheet.WinForms.WinFormsRenderingBackend.GdiPlus,
            };
            form.Controls.Add(control);
            form.Show();
            WinFormsApplication.DoEvents();
            control.Refresh();

            using var reorder = control.EnableHeaderReordering();
            var (sourceX, sourceY, targetX, targetY) =
                GetWinFormsRowDragCoordinates(control, session, 2, 5);
            InvokePrivate(
                reorder,
                "OnOwnerMouseDown",
                null,
                new WinFormsMouseEventArgs(
                    WinFormsMouseButtons.Left,
                    1,
                    sourceX,
                    sourceY,
                    0));
            SetPrivateField(reorder, "_pointerX", (double)targetX);
            SetPrivateField(reorder, "_pointerY", (double)targetY);
            Assert.AreEqual(true, InvokePrivate(reorder, "UpdateDrag", true));
            Assert.IsTrue(reorder.IsDragging);
            Assert.IsNotNull(reorder.DropTarget);
            Assert.IsTrue(control.Controls
                .Cast<WinFormsControl>()
                .Any(candidate =>
                    candidate.Visible &&
                    candidate.GetType().Name ==
                    "HeaderReorderPreviewControl"));

            InvokePrivate(reorder, "Complete");
            AssertRowMoveCommitted(session);
            Assert.AreEqual("Reorder rows", session.History.NextUndoDescription);
            Assert.IsTrue(session.Undo());
            Assert.AreEqual(
                "row-2",
                session.ActiveWorksheet.GetValue(new CellAddress(2, 0)));
            Assert.AreEqual(
                "=A3",
                session.ActiveWorksheet.GetFormula(new CellAddress(0, 1)));

            form.Close();
            WinFormsApplication.DoEvents();
        });
    }

    [TestMethod]
    [Timeout(120_000)]
    public void WinFormsUnsplitControllerAutoScrollsAtViewportEdge()
    {
        RunInSta(() =>
        {
            var session = CreateRowReorderSession();
            using var form = new WinFormsForm
            {
                Width = 900,
                Height = 650,
                ShowInTaskbar = false,
                StartPosition = WinFormsFormStartPosition.Manual,
                Location = new System.Drawing.Point(-30_000, -30_000),
            };
            using var control = new NeraSpreadSheet.WinForms.NeraSpreadsheetControl
            {
                Dock = WinFormsDockStyle.Fill,
                Session = session,
            };
            form.Controls.Add(control);
            form.Show();
            WinFormsApplication.DoEvents();
            control.Refresh();

            using var reorder = control.EnableHeaderReordering();
            var (sourceX, sourceY, _, _) =
                GetWinFormsRowDragCoordinates(control, session, 2, 5);
            InvokePrivate(
                reorder,
                "OnOwnerMouseDown",
                null,
                new WinFormsMouseEventArgs(
                    WinFormsMouseButtons.Left,
                    1,
                    sourceX,
                    sourceY,
                    0));
            SetPrivateField(reorder, "_pointerX", (double)sourceX);
            SetPrivateField(
                reorder,
                "_pointerY",
                Math.Max(0d, control.ClientSize.Height - 1d));
            Assert.AreEqual(true, InvokePrivate(reorder, "UpdateDrag", true));
            Assert.IsTrue(reorder.AutoScrollVelocity.Y > 0d);
            SetPrivateField(
                reorder,
                "_lastAutoScrollUtc",
                DateTime.UtcNow - TimeSpan.FromMilliseconds(50d));
            InvokePrivate(reorder, "OnAutoScrollTick", null, EventArgs.Empty);

            Assert.IsTrue(control.ScrollSnapshot.OffsetY > 0d);
            Assert.IsTrue(reorder.IsDragging);
            form.Close();
            WinFormsApplication.DoEvents();
        });
    }

    [TestMethod]
    [Timeout(120_000)]
    public void WpfUnsplitControllerCommitsAndRendersThroughD3DImage()
    {
        RunInSta(() =>
        {
            var session = CreateRowReorderSession();
            using var control = new NeraSpreadSheet.Wpf.NeraSpreadsheetControl
            {
                Background = WpfBrushes.White,
                Session = session,
                RenderingBackend =
                    NeraSpreadSheet.Wpf.WpfRenderingBackend.DrawingContext,
            };
            var decorator = new WpfAdornerDecorator { Child = control };
            var window = CreateWpfWindow(decorator);

            try
            {
                window.Show();
                window.UpdateLayout();
                using var reorder = control.EnableHeaderReordering();
                Assert.IsTrue(reorder.IsAttached);
                var (sourceX, sourceY, targetX, targetY) =
                    GetWpfRowDragCoordinates(control, session, 2, 5);
                SetControllerState(
                    reorder,
                    WorksheetAxis.Row,
                    2,
                    1,
                    new PointD(sourceX, sourceY),
                    isActive: false);
                SetPrivateField(reorder, "_pointerX", targetX);
                SetPrivateField(reorder, "_pointerY", targetY);
                Assert.AreEqual(true, InvokePrivate(reorder, "UpdateDrag", true));
                Assert.IsTrue(reorder.IsDragging);
                Assert.IsNotNull(reorder.DropTarget);

                InvokePrivate(reorder, "Complete");
                AssertRowMoveCommitted(session);
                Assert.AreEqual(
                    "Reorder rows",
                    session.History.NextUndoDescription);

                control.RenderingBackend =
                    NeraSpreadSheet.Wpf.WpfRenderingBackend.Direct2DD3DImage;
                control.InvalidateVisual();
                PumpUntil(
                    () => control.GpuDiagnostics is
                    {
                        TextureWidth: > 0,
                        TextureHeight: > 0,
                    },
                    "The unsplit WPF control did not render through D3DImage after reordering.");
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

    [TestMethod]
    [Timeout(120_000)]
    public void WpfUnsplitControllerAutoScrollsAtViewportEdge()
    {
        RunInSta(() =>
        {
            var session = CreateRowReorderSession();
            using var control = new NeraSpreadSheet.Wpf.NeraSpreadsheetControl
            {
                Background = WpfBrushes.White,
                Session = session,
            };
            var decorator = new WpfAdornerDecorator { Child = control };
            var window = CreateWpfWindow(decorator);

            try
            {
                window.Show();
                window.UpdateLayout();
                using var reorder = control.EnableHeaderReordering();
                var (sourceX, sourceY, _, _) =
                    GetWpfRowDragCoordinates(control, session, 2, 5);
                SetControllerState(
                    reorder,
                    WorksheetAxis.Row,
                    2,
                    1,
                    new PointD(sourceX, sourceY),
                    isActive: false);
                SetPrivateField(reorder, "_pointerX", sourceX);
                SetPrivateField(
                    reorder,
                    "_pointerY",
                    Math.Max(0d, control.ActualHeight - 1d));
                Assert.AreEqual(true, InvokePrivate(reorder, "UpdateDrag", true));
                Assert.IsTrue(reorder.AutoScrollVelocity.Y > 0d);

                PumpUntil(
                    () => control.ScrollSnapshot.OffsetY > 0d,
                    "The unsplit WPF header drag did not auto-scroll at the viewport edge.");
                Assert.IsTrue(reorder.IsDragging);
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
            worksheet.SetValue(new CellAddress(row, 0), $"row-{row}");
        }
        worksheet.SetValue(new CellAddress(500, 30), "extent");
        worksheet.SetFormula(new CellAddress(0, 1), "=A3");
        var session = new SpreadsheetSession(workbook);
        session.Selection.SelectRow(2);
        return session;
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

    private static (int SourceX, int SourceY, int TargetX, int TargetY)
        GetWinFormsRowDragCoordinates(
            NeraSpreadSheet.WinForms.NeraSpreadsheetControl control,
            SpreadsheetSession session,
            int sourceRow,
            int targetRow)
    {
        var chrome = SpreadsheetChromeGeometry.Calculate(
            control.ClientSize.Width,
            control.ClientSize.Height,
            control.RenderTheme);
        var frame = new SpreadsheetViewportEngine(session).Compose(
            control.ScrollSnapshot.OffsetX,
            control.ScrollSnapshot.OffsetY,
            chrome.BodyWidth,
            chrome.BodyHeight,
            control.OverscanPixels,
            control.RenderTheme);
        var source = FindRow(frame.Layout, sourceRow);
        var target = FindRow(frame.Layout, targetRow);
        var x = (int)Math.Round(control.RenderTheme.RowHeaderWidth / 2d);
        return (
            x,
            (int)Math.Round(
                control.RenderTheme.ColumnHeaderHeight +
                source.Start +
                (source.Size / 2d)),
            x,
            (int)Math.Round(
                control.RenderTheme.ColumnHeaderHeight +
                target.Start +
                (target.Size / 4d)));
    }

    private static (double SourceX, double SourceY, double TargetX, double TargetY)
        GetWpfRowDragCoordinates(
            NeraSpreadSheet.Wpf.NeraSpreadsheetControl control,
            SpreadsheetSession session,
            int sourceRow,
            int targetRow)
    {
        var chrome = SpreadsheetChromeGeometry.Calculate(
            control.ActualWidth,
            control.ActualHeight,
            control.RenderTheme);
        var frame = new SpreadsheetViewportEngine(session).Compose(
            control.ScrollSnapshot.OffsetX,
            control.ScrollSnapshot.OffsetY,
            chrome.BodyWidth,
            chrome.BodyHeight,
            control.OverscanPixels,
            control.RenderTheme);
        var source = FindRow(frame.Layout, sourceRow);
        var target = FindRow(frame.Layout, targetRow);
        var x = control.RenderTheme.RowHeaderWidth / 2d;
        return (
            x,
            control.RenderTheme.ColumnHeaderHeight +
            source.Start +
            (source.Size / 2d),
            x,
            control.RenderTheme.ColumnHeaderHeight +
            target.Start +
            (target.Size / 4d));
    }

    private static AxisSlot FindRow(ViewportLayout layout, int rowIndex) =>
        layout.Rows.Single(slot => slot.Index == rowIndex);

    private static void SetControllerState(
        object controller,
        WorksheetAxis axis,
        int sourceIndex,
        int count,
        PointD startPoint,
        bool isActive)
    {
        var stateType = controller.GetType().GetNestedType(
            "HeaderReorderState",
            BindingFlags.NonPublic);
        Assert.IsNotNull(stateType);
        var state = Activator.CreateInstance(
            stateType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [axis, sourceIndex, count, startPoint, isActive],
            culture: null);
        Assert.IsNotNull(state);
        SetPrivateField(controller, "_state", state);
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

    private static void SetPrivateField(
        object target,
        string fieldName,
        object? value)
    {
        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        field.SetValue(target, value);
    }

    private static WpfWindow CreateWpfWindow(object content) => new()
    {
        Background = WpfBrushes.White,
        Content = content,
        Height = 650d,
        Left = 0d,
        ResizeMode = WpfResizeMode.NoResize,
        ShowActivated = true,
        ShowInTaskbar = false,
        Title = "Nera unsplit header reorder smoke host",
        Top = 0d,
        Topmost = true,
        Width = 900d,
        WindowStartupLocation = WpfWindowStartupLocation.Manual,
        WindowStyle = WpfWindowStyle.None,
    };

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
            Name = "Nera unsplit header reorder smoke",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(StaThreadTimeout))
        {
            Assert.Fail("The unsplit header reorder STA smoke timed out.");
        }
        failure?.Throw();
    }
}
