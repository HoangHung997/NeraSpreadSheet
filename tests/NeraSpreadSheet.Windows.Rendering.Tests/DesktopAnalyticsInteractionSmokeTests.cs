using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Rendering.Spreadsheet;
using WinFormsApplication = System.Windows.Forms.Application;
using WinFormsBaseControl = System.Windows.Forms.Control;
using WinFormsControl = NeraSpreadSheet.WinForms.NeraSpreadsheetControl;
using WinFormsDockStyle = System.Windows.Forms.DockStyle;
using WinFormsForm = System.Windows.Forms.Form;
using WinFormsFormStartPosition = System.Windows.Forms.FormStartPosition;
using WinFormsKeyEventArgs = System.Windows.Forms.KeyEventArgs;
using WinFormsKeys = System.Windows.Forms.Keys;
using WinFormsMouseButtons = System.Windows.Forms.MouseButtons;
using WinFormsMouseEventArgs = System.Windows.Forms.MouseEventArgs;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class DesktopAnalyticsInteractionSmokeTests
{
    private static readonly TimeSpan StaTimeout = TimeSpan.FromSeconds(90d);

    [TestMethod]
    [Timeout(120_000)]
    public void WinFormsPointerKeyboardAndDeleteUseSharedAnalyticsContract()
    {
        RunInSta(() =>
        {
            var workbook = CreateWorkbook();
            using var form = new WinFormsForm
            {
                ShowInTaskbar = false,
                StartPosition = WinFormsFormStartPosition.Manual,
                Location = new System.Drawing.Point(-32_000, -32_000),
                ClientSize = new System.Drawing.Size(720, 480),
            };
            using var control = new WinFormsControl
            {
                Dock = WinFormsDockStyle.Fill,
                Workbook = workbook,
            };
            form.Controls.Add(control);
            form.Show();
            WinFormsApplication.DoEvents();

            var session = control.Session ??
                throw new AssertFailedException(
                    "The WinForms spreadsheet session was not created.");
            session.Selection.Select(new CellRange(
                new CellAddress(0, 0),
                new CellAddress(3, 1)));
            var chart = session.Analytics.InsertChartFromSelection(
                SpreadsheetChartType.Column,
                "Host analytics");
            var item = SpreadsheetAnalyticsItemKey.ForChart(chart.Id);

            control.Refresh();
            WinFormsApplication.DoEvents();
            var before = session.AnalyticsPlacements.GetPlacement(item);
            var chrome = SpreadsheetChromeGeometry.Calculate(
                control.ClientSize.Width,
                control.ClientSize.Height,
                control.RenderTheme);
            var startX = checked((int)Math.Round(
                chrome.RowHeaderWidth + before.DocumentBounds.Left + 40d));
            var startY = checked((int)Math.Round(
                chrome.ColumnHeaderHeight + before.DocumentBounds.Top + 40d));
            var undoBeforeDrag = session.History.UndoCount;

            RaiseMouse(
                control,
                "OnMouseDown",
                new WinFormsMouseEventArgs(
                    WinFormsMouseButtons.Left,
                    1,
                    startX,
                    startY,
                    0));
            Assert.AreEqual(item, session.AnalyticsInteraction.SelectedItem);
            Assert.IsTrue(session.AnalyticsInteraction.IsTransforming);

            RaiseMouse(
                control,
                "OnMouseMove",
                new WinFormsMouseEventArgs(
                    WinFormsMouseButtons.Left,
                    0,
                    startX + 25,
                    startY + 15,
                    0));
            RaiseMouse(
                control,
                "OnMouseUp",
                new WinFormsMouseEventArgs(
                    WinFormsMouseButtons.Left,
                    1,
                    startX + 25,
                    startY + 15,
                    0));

            var dragged = session.AnalyticsPlacements.GetPlacement(item);
            Assert.AreEqual(
                before.DocumentBounds.Translate(25d, 15d),
                dragged.DocumentBounds);
            Assert.AreEqual(undoBeforeDrag + 1, session.History.UndoCount);
            Assert.IsFalse(session.AnalyticsInteraction.IsTransforming);

            RaiseKey(control, WinFormsKeys.Control | WinFormsKeys.Right);
            var nudged = session.AnalyticsPlacements.GetPlacement(item);
            Assert.AreEqual(dragged.DocumentBounds.X + 10d, nudged.DocumentBounds.X);
            Assert.AreEqual(dragged.DocumentBounds.Y, nudged.DocumentBounds.Y);

            RaiseKey(control, WinFormsKeys.Shift | WinFormsKeys.Right);
            var resized = session.AnalyticsPlacements.GetPlacement(item);
            Assert.AreEqual(nudged.DocumentBounds.X, resized.DocumentBounds.X);
            Assert.AreEqual(nudged.DocumentBounds.Width + 1d, resized.DocumentBounds.Width);

            RaiseKey(control, WinFormsKeys.Delete);
            Assert.IsNull(session.AnalyticsInteraction.SelectedItem);
            Assert.AreEqual(0, session.Analytics.Charts.Count);
            Assert.AreEqual(0, session.AnalyticsPlacements.Placements.Count);

            Assert.IsTrue(session.Undo());
            Assert.AreEqual(1, session.Analytics.Charts.Count);
            Assert.AreEqual(1, session.AnalyticsPlacements.Placements.Count);

            form.Close();
            WinFormsApplication.DoEvents();
        });
    }

    private static Workbook CreateWorkbook()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), "Category");
        worksheet.SetValue(new CellAddress(0, 1), "Value");
        worksheet.SetValue(new CellAddress(1, 0), "A");
        worksheet.SetValue(new CellAddress(1, 1), 10d);
        worksheet.SetValue(new CellAddress(2, 0), "B");
        worksheet.SetValue(new CellAddress(2, 1), 20d);
        worksheet.SetValue(new CellAddress(3, 0), "C");
        worksheet.SetValue(new CellAddress(3, 1), 30d);
        return workbook;
    }

    private static void RaiseKey(
        WinFormsBaseControl target,
        WinFormsKeys keyData)
    {
        var method = target.GetType().GetMethod(
            "OnKeyDown",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new AssertFailedException(
                $"{target.GetType().FullName}.OnKeyDown was not found.");
        var args = new WinFormsKeyEventArgs(keyData);
        method.Invoke(target, [args]);
        Assert.IsTrue(args.Handled);
        Assert.IsTrue(args.SuppressKeyPress);
        WinFormsApplication.DoEvents();
    }

    private static void RaiseMouse(
        WinFormsBaseControl target,
        string methodName,
        WinFormsMouseEventArgs args)
    {
        var method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new AssertFailedException(
                $"{target.GetType().FullName}.{methodName} was not found.");
        method.Invoke(target, [args]);
        WinFormsApplication.DoEvents();
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
            Name = "Nera desktop analytics interaction smoke",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(StaTimeout))
        {
            Assert.Fail("The desktop analytics interaction smoke timed out.");
        }
        failure?.Throw();
    }
}
