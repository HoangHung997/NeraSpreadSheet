using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.WinForms;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WinFormsSplitHeaderResizeSmokeTests
{
    private const int WindowMessageMouseMove = 0x0200;
    private const int WindowMessageLeftButtonDown = 0x0201;
    private const int WindowMessageLeftButtonUp = 0x0202;

    [TestMethod]
    public void PublicSplitSurfaceResizesRowsAndColumnsThroughMouseMessages()
    {
        RunSta(() =>
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();

            var workbook = new Workbook();
            var worksheet = workbook.Worksheets[0];
            worksheet.SetValue(new CellAddress(160, 60), "extent");
            var session = new SpreadsheetSession(workbook);
            using var form = new Form
            {
                Width = 1000,
                Height = 760,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Location = new System.Drawing.Point(-30000, -30000),
            };
            using var control = new NeraSpreadsheetControl
            {
                Dock = DockStyle.Fill,
                Session = session,
                RenderingBackend = WinFormsRenderingBackend.GdiPlus,
            };
            form.Controls.Add(control);
            form.Show();
            Application.DoEvents();

            using var controller = control.EnableSplitPanes(
                SpreadsheetSplitPaneMode.Both);
            controller.SetSplit(340d, 230d);
            controller.RenderNow();
            var surface = control.Controls
                .Cast<Control>()
                .Single(candidate =>
                    candidate.GetType().Name == "NeraSpreadsheetSplitSurface");
            Assert.IsTrue(surface.IsHandleCreated);

            var frame = controller.LastFrame;
            Assert.IsNotNull(frame);
            Assert.IsTrue(frame.TryGetPane(
                SpreadsheetPaneId.BottomLeft,
                out var bottomLeft));
            var row = bottomLeft.ViewportFrame.Layout.Rows[0];
            var originalRowHeight = worksheet.Dimensions.GetRowHeight(row.Index);
            var rowX = (int)Math.Round(control.RenderTheme.RowHeaderWidth / 2d);
            var rowEdgeY = (int)Math.Round(
                control.RenderTheme.ColumnHeaderHeight +
                bottomLeft.Pane.Bounds.Top +
                row.End);

            DispatchMouseMessage(
                surface,
                WindowMessageLeftButtonDown,
                rowX,
                rowEdgeY);
            DispatchMouseMessage(
                surface,
                WindowMessageMouseMove,
                rowX,
                rowEdgeY + 13);
            DispatchMouseMessage(
                surface,
                WindowMessageLeftButtonUp,
                rowX,
                rowEdgeY + 13);

            Assert.AreEqual(
                originalRowHeight + 13d,
                worksheet.Dimensions.GetRowHeight(row.Index),
                0.01d);

            controller.RenderNow();
            frame = controller.LastFrame;
            Assert.IsNotNull(frame);
            Assert.IsTrue(frame.TryGetPane(
                SpreadsheetPaneId.TopRight,
                out var topRight));
            var column = topRight.ViewportFrame.Layout.Columns[0];
            var originalColumnWidth = worksheet.Dimensions.GetColumnWidth(column.Index);
            var columnEdgeX = (int)Math.Round(
                control.RenderTheme.RowHeaderWidth +
                topRight.Pane.Bounds.Left +
                column.End);
            var columnY = (int)Math.Round(
                control.RenderTheme.ColumnHeaderHeight / 2d);

            DispatchMouseMessage(
                surface,
                WindowMessageLeftButtonDown,
                columnEdgeX,
                columnY);
            DispatchMouseMessage(
                surface,
                WindowMessageMouseMove,
                columnEdgeX + 17,
                columnY);
            DispatchMouseMessage(
                surface,
                WindowMessageLeftButtonUp,
                columnEdgeX + 17,
                columnY);

            Assert.AreEqual(
                originalColumnWidth + 17d,
                worksheet.Dimensions.GetColumnWidth(column.Index),
                0.01d);
            controller.RenderNow();
            form.Close();
            Application.DoEvents();
        });
    }

    private static void DispatchMouseMessage(
        Control surface,
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
            Message.Create(
                surface.Handle,
                messageId,
                IntPtr.Zero,
                new IntPtr(packed)),
        ];
        method.Invoke(surface, arguments);
    }

    private static void RunSta(Action action)
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
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(45)), "STA resize smoke timed out.");
        if (failure is not null)
        {
            throw new AssertFailedException(
                "WinForms split header resize smoke failed.",
                failure);
        }
    }
}
