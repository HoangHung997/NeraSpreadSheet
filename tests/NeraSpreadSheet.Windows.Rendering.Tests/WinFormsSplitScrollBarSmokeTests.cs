using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.WinForms;
using DrawingPoint = System.Drawing.Point;
using DrawingSize = System.Drawing.Size;
using WinFormsApplication = System.Windows.Forms.Application;
using WinFormsControl = System.Windows.Forms.Control;
using WinFormsDockStyle = System.Windows.Forms.DockStyle;
using WinFormsForm = System.Windows.Forms.Form;
using WinFormsFormBorderStyle = System.Windows.Forms.FormBorderStyle;
using WinFormsFormStartPosition = System.Windows.Forms.FormStartPosition;
using WinFormsMessage = System.Windows.Forms.Message;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WinFormsSplitScrollBarSmokeTests
{
    private const int WindowMessageMouseMove = 0x0200;
    private const int WindowMessageLeftButtonDown = 0x0201;
    private const int WindowMessageLeftButtonUp = 0x0202;
    private static readonly TimeSpan StaThreadTimeout = TimeSpan.FromSeconds(60d);

    [TestMethod]
    [Timeout(90_000)]
    public void PublicSplitSurfaceScrollBarsMoveOnlyTheirTargetPane()
    {
        RunInSta(() =>
        {
            using var form = CreateOffscreenHost(760, 520);
            using var control = CreateSpreadsheetControl();
            form.Controls.Add(control);
            WinFormsApplication.DoEvents();
            using var split = control.EnableSplitPanes(SpreadsheetSplitPaneMode.Both);
            split.SetSplit(300d, 190d);
            split.RenderNow();

            var frame = split.LastFrame;
            Assert.IsNotNull(frame);
            Assert.AreEqual(8, frame.ScrollBars.Bars.Count);
            var surface = control.Controls
                .Cast<WinFormsControl>()
                .Single(candidate =>
                    candidate.GetType().Name == "NeraSpreadsheetSplitSurface");
            Assert.IsTrue(frame.ScrollBars.TryGetBar(
                SpreadsheetPaneId.TopRight,
                SpreadsheetScrollBarOrientation.Horizontal,
                out var topRightHorizontal));
            var beforeTopLeft = split.GetPaneScroll(SpreadsheetPaneId.TopLeft);
            var beforeTopRight = split.GetPaneScroll(SpreadsheetPaneId.TopRight);
            var beforeBottomRight = split.GetPaneScroll(SpreadsheetPaneId.BottomRight);
            var increaseClient = ToClientPoint(
                control,
                Center(topRightHorizontal.IncreaseButtonBounds));

            DispatchMouseMessage(
                surface,
                WindowMessageLeftButtonDown,
                increaseClient.X,
                increaseClient.Y);
            WinFormsApplication.DoEvents();

            var afterTopRight = split.GetPaneScroll(SpreadsheetPaneId.TopRight);
            Assert.AreEqual(
                beforeTopRight.X + control.RenderTheme.ScrollBarLineStep,
                afterTopRight.X,
                0.01d);
            Assert.AreEqual(beforeTopRight.Y, afterTopRight.Y, 0.01d);
            Assert.AreEqual(
                beforeTopLeft,
                split.GetPaneScroll(SpreadsheetPaneId.TopLeft));
            Assert.AreEqual(
                beforeBottomRight,
                split.GetPaneScroll(SpreadsheetPaneId.BottomRight));

            split.RenderNow();
            frame = split.LastFrame;
            Assert.IsNotNull(frame);
            Assert.IsTrue(frame.ScrollBars.TryGetBar(
                SpreadsheetPaneId.BottomRight,
                SpreadsheetScrollBarOrientation.Vertical,
                out var bottomRightVertical));
            var thumbCenter = Center(bottomRightVertical.ThumbBounds);
            var downClient = ToClientPoint(control, thumbCenter);
            var targetThumbStart = bottomRightVertical.TrackBounds.Top +
                (bottomRightVertical.TrackTravel * 0.72d);
            var targetClient = ToClientPoint(
                control,
                new NeraSpreadSheet.Foundation.PointD(
                    thumbCenter.X,
                    targetThumbStart +
                    (bottomRightVertical.ThumbBounds.Height / 2d)));
            var expectedY = bottomRightVertical.GetOffsetForThumbStart(
                targetThumbStart);
            var topRightBeforeDrag = split.GetPaneScroll(SpreadsheetPaneId.TopRight);

            DispatchMouseMessage(
                surface,
                WindowMessageLeftButtonDown,
                downClient.X,
                downClient.Y);
            DispatchMouseMessage(
                surface,
                WindowMessageMouseMove,
                targetClient.X,
                targetClient.Y);
            DispatchMouseMessage(
                surface,
                WindowMessageLeftButtonUp,
                targetClient.X,
                targetClient.Y);
            WinFormsApplication.DoEvents();

            var bottomRightAfterDrag = split.GetPaneScroll(
                SpreadsheetPaneId.BottomRight);
            Assert.AreEqual(expectedY, bottomRightAfterDrag.Y, 0.5d);
            Assert.AreEqual(beforeBottomRight.X, bottomRightAfterDrag.X, 0.01d);
            Assert.AreEqual(
                topRightBeforeDrag,
                split.GetPaneScroll(SpreadsheetPaneId.TopRight));
            Assert.AreEqual(
                new SpreadsheetPaneScrollOffset(
                    bottomRightAfterDrag.X,
                    bottomRightAfterDrag.Y),
                control.Session?.View.SplitState.BottomRightScroll);

            foreach (var backend in Enum.GetValues<WinFormsRenderingBackend>())
            {
                control.RenderingBackend = backend;
                control.SwapChainVSync = false;
                split.RenderNow();
                WinFormsApplication.DoEvents();
                Assert.IsNotNull(split.LastFrame);
                Assert.IsTrue(split.LastFrame.ScrollBars.Bars.Count >= 8);
            }

            form.Close();
            WinFormsApplication.DoEvents();
        });
    }

    private static NeraSpreadsheetControl CreateSpreadsheetControl()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(default, "Scroll-bar smoke");
        sheet.SetValue(new CellAddress(240, 80), "extent");
        return new NeraSpreadsheetControl
        {
            Dock = WinFormsDockStyle.Fill,
            Session = new SpreadsheetSession(workbook),
            RenderingBackend = WinFormsRenderingBackend.GdiPlus,
        };
    }

    private static DrawingPoint ToClientPoint(
        NeraSpreadsheetControl control,
        NeraSpreadSheet.Foundation.PointD bodyPoint) => new(
        (int)Math.Round(control.RenderTheme.RowHeaderWidth + bodyPoint.X),
        (int)Math.Round(control.RenderTheme.ColumnHeaderHeight + bodyPoint.Y));

    private static NeraSpreadSheet.Foundation.PointD Center(
        NeraSpreadSheet.Foundation.RectD bounds) => new(
        bounds.Left + (bounds.Width / 2d),
        bounds.Top + (bounds.Height / 2d));

    private static void DispatchMouseMessage(
        WinFormsControl surface,
        int messageId,
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
                IntPtr.Zero,
                new IntPtr(packed)),
        ];
        method.Invoke(surface, arguments);
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
            Text = "Nera split scroll-bar smoke host",
        };
        form.Show();
        WinFormsApplication.DoEvents();
        Assert.AreNotEqual(IntPtr.Zero, form.Handle);
        return form;
    }

    private static void RunInSta(Action action)
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
            Name = "Nera WinForms split scroll-bar smoke",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(StaThreadTimeout))
        {
            Assert.Fail("The WinForms split scroll-bar smoke timed out.");
        }
        failure?.Throw();
    }
}
