using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Rendering.Spreadsheet;
using WpfAutomationControlType = System.Windows.Automation.Peers.AutomationControlType;
using WpfAutomationPeer = System.Windows.Automation.Peers.AutomationPeer;
using WpfPatternInterface = System.Windows.Automation.Peers.PatternInterface;
using WpfUIElementAutomationPeer = System.Windows.Automation.Peers.UIElementAutomationPeer;
using WpfInvokeProvider = System.Windows.Automation.Provider.IInvokeProvider;
using WpfTransformProvider = System.Windows.Automation.Provider.ITransformProvider;
using WpfDispatcherPriority = System.Windows.Threading.DispatcherPriority;
using WpfControl = NeraSpreadSheet.Wpf.NeraSpreadsheetControl;
using WpfWindow = System.Windows.Window;
using WpfWindowStartupLocation = System.Windows.WindowStartupLocation;
using WinFormsApplication = System.Windows.Forms.Application;
using WinFormsControl = NeraSpreadSheet.WinForms.NeraSpreadsheetControl;
using WinFormsDockStyle = System.Windows.Forms.DockStyle;
using WinFormsForm = System.Windows.Forms.Form;
using WinFormsFormStartPosition = System.Windows.Forms.FormStartPosition;
using WinFormsPaintEventArgs = System.Windows.Forms.PaintEventArgs;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class DesktopAnalyticsAccessibilitySmokeTests
{
    private static readonly TimeSpan StaTimeout = TimeSpan.FromSeconds(90d);

    [TestMethod]
    [Timeout(120_000)]
    public void WinFormsAccessibleObjectExposesChartChildAndSelectionAction()
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
            var chart = session.Analytics.InsertChart(
                new CellRange(
                    new CellAddress(0, 0),
                    new CellAddress(3, 1)),
                SpreadsheetChartType.Column,
                title: "Accessible chart",
                requestedName: "AccessibleChart");
            var item = SpreadsheetAnalyticsItemKey.ForChart(chart.Id);

            ForceWinFormsPaint(control);

            var root = control.AccessibilityObject;
            Assert.AreEqual(System.Windows.Forms.AccessibleRole.Table, root.Role);
            Assert.AreEqual("Spreadsheet", root.Name);
            Assert.AreEqual(1, root.GetChildCount());

            var child = root.GetChild(0) ??
                throw new AssertFailedException(
                    "The WinForms accessibility root did not expose its chart child.");
            Assert.AreEqual("AccessibleChart", child.Name);
            Assert.AreEqual(System.Windows.Forms.AccessibleRole.Chart, child.Role);
            Assert.AreEqual("Select", child.DefaultAction);
            Assert.IsFalse(child.Bounds.IsEmpty);
            StringAssert.Contains(child.Description ?? string.Empty, "Move");
            StringAssert.Contains(child.Description ?? string.Empty, "Resize");
            StringAssert.Contains(child.Description ?? string.Empty, "Delete");

            child.DoDefaultAction();
            Assert.AreEqual(item, session.AnalyticsInteraction.SelectedItem);
            Assert.IsTrue(
                (child.State & System.Windows.Forms.AccessibleStates.Selected) != 0,
                "The WinForms accessibility child did not reflect selection state.");
            Assert.AreSame(child, root.GetSelected());
            Assert.AreSame(child, root.HitTest(
                child.Bounds.Left + (child.Bounds.Width / 2),
                child.Bounds.Top + (child.Bounds.Height / 2)));

            form.Close();
            WinFormsApplication.DoEvents();
        });
    }

    [TestMethod]
    [Timeout(120_000)]
    public void WpfAutomationPeerExposesChartInvokeMoveAndResizePatterns()
    {
        RunInSta(() =>
        {
            var workbook = CreateWorkbook();
            var control = new WpfControl
            {
                Workbook = workbook,
            };
            var window = new WpfWindow
            {
                ShowInTaskbar = false,
                WindowStartupLocation = WpfWindowStartupLocation.Manual,
                Left = -32_000d,
                Top = -32_000d,
                Width = 720d,
                Height = 480d,
                Content = control,
            };

            try
            {
                window.Show();
                FlushWpf(window, control);

                var session = control.Session ??
                    throw new AssertFailedException(
                        "The WPF spreadsheet session was not created.");
                var chart = session.Analytics.InsertChart(
                    new CellRange(
                        new CellAddress(0, 0),
                        new CellAddress(3, 1)),
                    SpreadsheetChartType.Column,
                    title: "Accessible chart",
                    requestedName: "AccessibleChart");
                var item = SpreadsheetAnalyticsItemKey.ForChart(chart.Id);
                FlushWpf(window, control);

                var rootPeer = WpfUIElementAutomationPeer.CreatePeerForElement(control) ??
                    throw new AssertFailedException(
                        "The WPF spreadsheet did not create an AutomationPeer.");
                Assert.AreEqual(
                    WpfAutomationControlType.DataGrid,
                    rootPeer.GetAutomationControlType());
                Assert.AreEqual("Spreadsheet", rootPeer.GetName());

                var automationId = $"analytics-chart-{item.Id:N}";
                var child = (rootPeer.GetChildren() ?? [])
                    .SingleOrDefault(peer => peer.GetAutomationId() == automationId) ??
                    throw new AssertFailedException(
                        "The WPF AutomationPeer did not expose its chart child.");
                Assert.AreEqual("AccessibleChart", child.GetName());
                Assert.AreEqual(WpfAutomationControlType.Group, child.GetAutomationControlType());
                Assert.IsFalse(child.GetBoundingRectangle().IsEmpty);

                var invoke = child.GetPattern(WpfPatternInterface.Invoke) as WpfInvokeProvider ??
                    throw new AssertFailedException(
                        "The WPF analytics child did not expose InvokePattern.");
                invoke.Invoke();
                Assert.AreEqual(item, session.AnalyticsInteraction.SelectedItem);

                var transform = child.GetPattern(WpfPatternInterface.Transform) as WpfTransformProvider ??
                    throw new AssertFailedException(
                        "The WPF analytics child did not expose TransformPattern.");
                Assert.IsTrue(transform.CanMove);
                Assert.IsTrue(transform.CanResize);
                Assert.IsFalse(transform.CanRotate);

                var before = session.AnalyticsPlacements.GetPlacement(item).DocumentBounds;
                var beforeScreen = child.GetBoundingRectangle();
                var scaleX = beforeScreen.Width / before.Width;
                var scaleY = beforeScreen.Height / before.Height;
                Assert.IsTrue(scaleX > 0d && scaleY > 0d);

                transform.Move(
                    beforeScreen.Left + (10d * scaleX),
                    beforeScreen.Top + (5d * scaleY));
                var moved = session.AnalyticsPlacements.GetPlacement(item).DocumentBounds;
                Assert.AreEqual(before.X + 10d, moved.X, 1e-6);
                Assert.AreEqual(before.Y + 5d, moved.Y, 1e-6);
                Assert.AreEqual(before.Width, moved.Width, 1e-6);
                Assert.AreEqual(before.Height, moved.Height, 1e-6);

                FlushWpf(window, control);
                var movedScreen = child.GetBoundingRectangle();
                transform.Resize(
                    movedScreen.Width + (20d * scaleX),
                    movedScreen.Height + (10d * scaleY));
                var resized = session.AnalyticsPlacements.GetPlacement(item).DocumentBounds;
                Assert.AreEqual(moved.X, resized.X, 1e-6);
                Assert.AreEqual(moved.Y, resized.Y, 1e-6);
                Assert.AreEqual(moved.Width + 20d, resized.Width, 1e-6);
                Assert.AreEqual(moved.Height + 10d, resized.Height, 1e-6);
            }
            finally
            {
                window.Close();
                control.Dispose();
                window.Dispatcher.Invoke(
                    WpfDispatcherPriority.Background,
                    new Action(static () => { }));
            }
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

    private static void ForceWinFormsPaint(WinFormsControl control)
    {
        using var bitmap = new System.Drawing.Bitmap(
            Math.Max(1, control.ClientSize.Width),
            Math.Max(1, control.ClientSize.Height));
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        var method = control.GetType().GetMethod(
            "OnPaint",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new AssertFailedException(
                $"{control.GetType().FullName}.OnPaint was not found.");
        method.Invoke(
            control,
            [new WinFormsPaintEventArgs(graphics, control.ClientRectangle)]);
        WinFormsApplication.DoEvents();
    }

    private static void FlushWpf(WpfWindow window, WpfControl control)
    {
        window.UpdateLayout();
        control.InvalidateVisual();
        window.Dispatcher.Invoke(
            WpfDispatcherPriority.Render,
            new Action(static () => { }));
        window.UpdateLayout();
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
            Name = "Nera desktop analytics accessibility smoke",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(StaTimeout))
        {
            Assert.Fail("The desktop analytics accessibility smoke timed out.");
        }
        failure?.Throw();
    }
}
