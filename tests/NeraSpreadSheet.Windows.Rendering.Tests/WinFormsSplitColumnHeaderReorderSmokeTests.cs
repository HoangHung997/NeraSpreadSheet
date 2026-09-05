using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Viewport;
using NeraSpreadSheet.WinForms;
using WinFormsApplication = System.Windows.Forms.Application;
using WinFormsControl = System.Windows.Forms.Control;
using WinFormsForm = System.Windows.Forms.Form;
using WinFormsFormStartPosition = System.Windows.Forms.FormStartPosition;
using WinFormsMessage = System.Windows.Forms.Message;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WinFormsSplitColumnHeaderReorderSmokeTests
{
    private const int WindowMessageMouseMove = 0x0200;
    private const int WindowMessageLeftButtonDown = 0x0201;
    private const int WindowMessageLeftButtonUp = 0x0202;
    private const int MouseKeyLeftButton = 0x0001;

    [TestMethod]
    [Timeout(90_000)]
    public void SplitColumnHeaderDragReordersColumnAndPreservesFormulaIdentity()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                WinFormsApplication.SetHighDpiMode(
                    System.Windows.Forms.HighDpiMode.SystemAware);
                WinFormsApplication.EnableVisualStyles();

                var workbook = new Workbook();
                var worksheet = workbook.Worksheets[0];
                for (var column = 0; column <= 8; column++)
                {
                    worksheet.SetValue(
                        new CellAddress(0, column),
                        $"column-{column}");
                }
                worksheet.SetFormula(new CellAddress(1, 0), "=C1");
                worksheet.SetValue(new CellAddress(80, 20), "extent");
                var session = new SpreadsheetSession(workbook);
                using var form = new WinFormsForm
                {
                    Width = 1100,
                    Height = 760,
                    ShowInTaskbar = false,
                    StartPosition = WinFormsFormStartPosition.Manual,
                    Location = new System.Drawing.Point(-30_000, -30_000),
                };
                using var control = new NeraSpreadsheetControl
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Session = session,
                    RenderingBackend = WinFormsRenderingBackend.GdiPlus,
                };
                form.Controls.Add(control);
                form.Show();
                WinFormsApplication.DoEvents();

                using var split = control.EnableSplitPanes(
                    SpreadsheetSplitPaneMode.Both);
                split.SetSplit(600d, 230d);
                split.RenderNow();
                var frame = split.LastFrame ??
                    throw new AssertFailedException(
                        "The WinForms split frame is unavailable.");
                Assert.IsTrue(frame.TryGetPane(
                    SpreadsheetPaneId.TopLeft,
                    out var topLeft));
                var sourceSlot = FindColumn(topLeft, 2);
                var targetSlot = FindColumn(topLeft, 5);
                var sourceX = (int)Math.Round(
                    control.RenderTheme.RowHeaderWidth +
                    topLeft.Pane.Bounds.Left +
                    sourceSlot.Start +
                    (sourceSlot.Size / 2d));
                var targetX = (int)Math.Round(
                    control.RenderTheme.RowHeaderWidth +
                    topLeft.Pane.Bounds.Left +
                    targetSlot.Start +
                    (targetSlot.Size / 4d));
                var headerY = (int)Math.Round(
                    control.RenderTheme.ColumnHeaderHeight / 2d);
                var surface = control.Controls
                    .Cast<WinFormsControl>()
                    .Single(candidate =>
                        candidate.GetType().Name ==
                        "NeraSpreadsheetSplitSurface");

                Dispatch(
                    surface,
                    WindowMessageLeftButtonDown,
                    MouseKeyLeftButton,
                    sourceX,
                    headerY);
                Dispatch(
                    surface,
                    WindowMessageMouseMove,
                    MouseKeyLeftButton,
                    targetX,
                    headerY);
                Dispatch(
                    surface,
                    WindowMessageLeftButtonUp,
                    0,
                    targetX,
                    headerY);
                WinFormsApplication.DoEvents();

                Assert.AreEqual(
                    "column-3",
                    worksheet.GetValue(new CellAddress(0, 2)));
                Assert.AreEqual(
                    "column-4",
                    worksheet.GetValue(new CellAddress(0, 3)));
                Assert.AreEqual(
                    "column-2",
                    worksheet.GetValue(new CellAddress(0, 4)));
                Assert.AreEqual(
                    "=E1",
                    worksheet.GetFormula(new CellAddress(1, 0)));
                Assert.AreEqual(
                    new CellRange(
                        new CellAddress(0, 4),
                        new CellAddress(SpreadsheetLimits.MaxRows - 1, 4)),
                    session.Selection.Ranges.Single());
                Assert.IsTrue(session.Undo());
                Assert.AreEqual(
                    "column-2",
                    worksheet.GetValue(new CellAddress(0, 2)));
                Assert.AreEqual(
                    "=C1",
                    worksheet.GetFormula(new CellAddress(1, 0)));

                form.Close();
                WinFormsApplication.DoEvents();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        })
        {
            IsBackground = true,
            Name = "Nera WinForms split column reorder smoke",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(
            thread.Join(TimeSpan.FromSeconds(60d)),
            "The WinForms column reorder STA smoke timed out.");
        if (failure is not null)
        {
            throw new AssertFailedException(
                "WinForms split column header reorder smoke failed.",
                failure);
        }
    }

    private static AxisSlot FindColumn(
        SpreadsheetSplitPaneFrame pane,
        int columnIndex) =>
        pane.ViewportFrame.Layout.Columns.Single(
            slot => slot.Index == columnIndex);

    private static void Dispatch(
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
}
