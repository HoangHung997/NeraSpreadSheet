using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Ribbon.Core;
using NeraSpreadSheet.Wpf;
using NeraSpreadSheet.Wpf.Sample;
using ScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using Orientation = System.Windows.Controls.Orientation;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class RibbonWorksheetNavigationSmokeTests
{
    [TestMethod]
    [Timeout(60_000)]
    public void NativeThumbInputShouldCoalesceFractionalOffsetsWithoutChangingWorkbookState() => RunSta(() =>
    {
        var session = CreateSession();
        using var window = new RibbonPreviewWindow(session) { ShowInTaskbar = false };
        try
        {
            window.Show();
            PumpUntil(() => Bars(window).All(bar => bar.Maximum > 0d), "The native worksheet bars never received their extents.");
            var grid = Descendants(window).OfType<NeraSpreadsheetControl>().Single();
            var horizontal = Bar(window, Orientation.Horizontal);
            var vertical = Bar(window, Orientation.Vertical);
            Assert.IsFalse(grid.TryGetSplitPaneController(out _), "Standalone bars must not implicitly enable split.");
            var worksheet = session.ActiveWorksheet;
            var selection = session.Selection.Capture();
            var version = worksheet.Version;
            var used = worksheet.UsedCellCount;
            var history = session.History.UndoCount;
            var viewVersion = session.View.Version;
            var formula = worksheet.GetCell(new CellAddress(1, 0));
            var changes = 0;
            grid.ScrollChanged += (_, _) => changes++;
            var track = horizontal.Template.FindName("PART_Track", horizontal) as Track;
            Assert.IsNotNull(track);
            Assert.IsNotNull(track.Thumb);
            var initial = grid.ScrollSnapshot;
            track.Thumb.RaiseEvent(new DragStartedEventArgs(0d, 0d) { RoutedEvent = Thumb.DragStartedEvent });
            for (var index = 0; index < 64; index++)
                track.Thumb.RaiseEvent(new DragDeltaEventArgs(0.375d, 0d) { RoutedEvent = Thumb.DragDeltaEvent });
            track.Thumb.RaiseEvent(new DragCompletedEventArgs(24d, 0d, false) { RoutedEvent = Thumb.DragCompletedEvent });
            var expected = horizontal.Value;
            Assert.IsTrue(expected > 0d, "The test must exercise the real WPF thumb/track value mapping.");
            Assert.IsTrue(Math.Abs(expected - Math.Round(expected)) > 1e-6, "Thumb mapping must retain a fractional offset.");
            Assert.AreEqual(initial, grid.ScrollSnapshot, "Raw thumb events must wait for the rendering frame.");
            PumpUntil(() => Math.Abs(grid.ScrollSnapshot.OffsetX - expected) < 1e-6, "The coalesced thumb position was not applied.");
            Assert.AreEqual(1, changes, "One burst must produce one host scroll update.");
            Assert.AreEqual(initial.OffsetY, grid.ScrollSnapshot.OffsetY, 1e-9);
            Assert.AreEqual(expected, horizontal.Value, 1e-9);

            grid.ScrollTo(75.25d, 120.5d);
            PumpUntil(() => horizontal.Value == 75.25d && vertical.Value == 120.5d, "Bars did not follow programmatic scrolling.");
            grid.QueuePrecisionScroll(0.125d, 0.375d);
            PumpUntil(() => horizontal.Value == 75.375d && vertical.Value == 120.875d, "Bars did not follow the existing precision scheduler.");
            grid.Focus();
            grid.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120)
            { RoutedEvent = Mouse.MouseWheelEvent, Source = grid });
            PumpUntil(() => vertical.Value > 120.875d, "Bars did not follow wheel input.");
            Assert.AreEqual(selection.ActiveCell, session.Selection.ActiveCell);
            Assert.AreEqual(selection.Version, session.Selection.Version);
            Assert.AreEqual(version, worksheet.Version);
            Assert.AreEqual(used, worksheet.UsedCellCount);
            Assert.AreEqual(history, session.History.UndoCount);
            Assert.AreEqual(viewVersion, session.View.Version);
            Assert.AreEqual(formula, worksheet.GetCell(new CellAddress(1, 0)));
        }
        finally { window.Close(); }
    });

    [TestMethod]
    [Timeout(60_000)]
    public void NavigationExtentShouldRefreshForContentSelectionZoomAndFrozenHiddenAxes() => RunSta(() =>
    {
        var session = CreateSession();
        using var window = new RibbonPreviewWindow(session) { ShowInTaskbar = false };
        try
        {
            window.Show();
            PumpUntil(() => Bars(window).All(bar => bar.Maximum > 0d), "Missing navigation extent.");
            var grid = Descendants(window).OfType<NeraSpreadsheetControl>().Single();
            var horizontal = Bar(window, Orientation.Horizontal);
            var vertical = Bar(window, Orientation.Vertical);
            var original = vertical.Maximum;
            var far = new CellAddress(500, 200);
            session.ActiveWorksheet.SetValue(far, 42d);
            PumpUntil(() => vertical.Maximum > original * 2d, "An extent-only cell change was not reflected.");
            session.ActiveWorksheet.Clear(far);
            PumpUntil(() => Math.Abs(vertical.Maximum - original) < 0.01d, "Clearing far content did not contract the extent.");
            var mergedRange = new CellRange(far, new CellAddress(502, 201));
            session.ActiveWorksheet.MergeCells(mergedRange);
            PumpUntil(() => vertical.Maximum > original * 2d, "A far merge must update the navigation extent without selection changes.");
            Assert.IsTrue(session.ActiveWorksheet.UnmergeCells(mergedRange));
            PumpUntil(() => Math.Abs(vertical.Maximum - original) < 0.01d, "Removing a merge must release its extent.");
            var table = new SpreadsheetTable(Guid.NewGuid(), "FarNavigation", new CellRange(far, new CellAddress(505, 200)),
                [new SpreadsheetTableColumn(Guid.NewGuid(), "Value")]);
            session.Tables.Add(table);
            PumpUntil(() => vertical.Maximum > original * 2d, "A table-only change must update the navigation extent.");
            Assert.IsTrue(session.Tables.Remove(table.Id));
            PumpUntil(() => Math.Abs(vertical.Maximum - original) < 0.01d, "Removing a metadata-only table must release its extent.");
            session.Selection.SetActiveCell(far);
            PumpUntil(() => vertical.Maximum > original * 2d, "An empty navigation cell must expand the extent.");
            grid.ScrollCellIntoView(far);
            PumpUntil(() => vertical.Value > original, "The active far cell did not become visible.");
            var farOffset = grid.ScrollSnapshot.OffsetY;
            session.Selection.SetActiveCell(default);
            PumpFor(TimeSpan.FromMilliseconds(80d));
            Assert.AreEqual(farOffset, grid.ScrollSnapshot.OffsetY, 1e-9, "Selection must not clamp away the inspected viewport.");
            grid.ScrollTo(0d, 0d);
            PumpUntil(() => Math.Abs(vertical.Maximum - original) < 0.01d, "Returning to the origin did not release the transient tail.");
            var maximum = vertical.Maximum;
            for (var index = 0; index < 3; index++)
            {
                vertical.Value = maximum - 0.5d;
                PumpUntil(() => grid.ScrollSnapshot.OffsetY == maximum - 0.5d, "The fractional end position was not applied.");
                vertical.Value = maximum;
                PumpUntil(() => grid.ScrollSnapshot.OffsetY == maximum, "The end thumb was not applied.");
                Assert.AreEqual(maximum, vertical.Maximum, 0.01d, "Thumb dragging must not recursively append a new tail.");
            }
            grid.ScrollTo(0d, 0d);
            session.View.SetFrozenPanes(1, 1);
            session.ActiveWorksheet.Dimensions.HideRows(1, 2);
            session.ActiveWorksheet.Dimensions.HideColumns(1, 2);
            session.Selection.SetActiveCell(default);
            grid.Focus();
            RaiseKey(grid, Key.Right);
            RaiseKey(grid, Key.Down);
            PumpFor(TimeSpan.FromMilliseconds(80d));
            Assert.AreEqual(new CellAddress(3, 3), session.Selection.ActiveCell);
            Assert.AreEqual(1, session.View.FrozenRows);
            Assert.AreEqual(1, session.View.FrozenColumns);
            var viewport = horizontal.ViewportSize;
            window.Width = 800d;
            grid.Zoom = 1.5d;
            PumpUntil(() => horizontal.ViewportSize < viewport - 100d, "Resize/zoom did not refresh logical viewport metrics.");
            var chrome = SpreadsheetChromeGeometry.Calculate(grid.ActualWidth, grid.ActualHeight, grid.RenderTheme);
            Assert.AreEqual(chrome.BodyWidth, horizontal.ViewportSize, 0.01d);
            Assert.AreEqual(chrome.BodyHeight, vertical.ViewportSize, 0.01d);
            Assert.AreEqual(Math.Max(0d, grid.ContentWidth - chrome.BodyWidth), horizontal.Maximum, 0.01d);
            Assert.AreEqual(Math.Max(0d, grid.ContentHeight - chrome.BodyHeight), vertical.Maximum, 0.01d);
            var beforeHeaderToggle = horizontal.ViewportSize;
            Assert.IsTrue(Field<RibbonRuntimeController>(window, "_runtime").TryActivateAsync("Sample.Headers").GetAwaiter().GetResult());
            PumpUntil(() => horizontal.ViewportSize > beforeHeaderToggle + 10d, "A shell header command must refresh body metrics without an SDK metrics event.");
        }
        finally { window.Close(); }
    });

    [TestMethod]
    [Timeout(60_000)]
    public void PendingThumbInputShouldBeDiscardedOnWorksheetSwitchAndDispose() => RunSta(() =>
    {
        var session = CreateSession();
        var second = session.Workbook.AddWorksheet("Second");
        using var window = new RibbonPreviewWindow(session) { ShowInTaskbar = false };
        try
        {
            window.Show();
            PumpUntil(() => Bars(window).All(bar => bar.Maximum > 0d), "Missing navigation extent.");
            var grid = Descendants(window).OfType<NeraSpreadsheetControl>().Single();
            var horizontal = Bar(window, Orientation.Horizontal);
            horizontal.Value = 123.25d;
            session.ActivateWorksheet(second);
            PumpFor(TimeSpan.FromMilliseconds(100d));
            Assert.AreEqual(0d, grid.ScrollSnapshot.OffsetX, 1e-9);
            Assert.AreEqual(0d, horizontal.Value, 1e-9);
            horizontal.Value = 123.25d;
            var far = new CellAddress(300, 200);
            session.Selection.SetActiveCell(far);
            Assert.IsTrue(grid.ScrollCellIntoView(far));
            var navigationOffset = grid.ScrollSnapshot.OffsetX;
            Assert.IsTrue(navigationOffset > 123.25d);
            PumpFor(TimeSpan.FromMilliseconds(80d));
            Assert.AreEqual(navigationOffset, grid.ScrollSnapshot.OffsetX, 1e-9,
                "A stale thumb frame must not override subsequent cell navigation.");
            horizontal.Value = 234.5d;
            window.Dispose();
            PumpFor(TimeSpan.FromMilliseconds(80d));
            Assert.IsFalse(Field<bool>(window, "_navigationFrameAttached"));
            Assert.AreEqual(0, second.UsedCellCount);
            Assert.AreEqual(0, session.History.UndoCount);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    [Timeout(60_000)]
    public void NavigationNamesShouldFollowHostLocalizationWithoutChangingAnotherWindow() => RunSta(() =>
    {
        using var englishWindow = new RibbonPreviewWindow(CreateSession()) { ShowInTaskbar = false };
        using var vietnameseWindow = new RibbonPreviewWindow(CreateSession()) { ShowInTaskbar = false };
        try
        {
            englishWindow.Show();
            vietnameseWindow.Show();
            PumpUntil(() => Bars(englishWindow).All(bar => bar.Maximum > 0d) && Bars(vietnameseWindow).All(bar => bar.Maximum > 0d),
                "The localized windows were not loaded.");
            var runtime = Field<RibbonRuntimeController>(englishWindow, "_runtime");
            runtime.SetLocalization(new PresentationLocalization(CultureInfo.GetCultureInfo("en-US")));
            PumpUntil(() => AutomationProperties.GetName(Bar(englishWindow, Orientation.Horizontal)) == "Scroll worksheet horizontally",
                "The horizontal automation name did not refresh after changing the host culture.");
            Assert.AreEqual("Scroll worksheet vertically", AutomationProperties.GetName(Bar(englishWindow, Orientation.Vertical)));
            Assert.AreEqual("Cuộn ngang trang tính", AutomationProperties.GetName(Bar(vietnameseWindow, Orientation.Horizontal)));
            Assert.AreEqual("Cuộn dọc trang tính", AutomationProperties.GetName(Bar(vietnameseWindow, Orientation.Vertical)));
            runtime.SetLocalization(PresentationLocalization.Default);
            PumpUntil(() => AutomationProperties.GetName(Bar(englishWindow, Orientation.Vertical)) == "Cuộn dọc trang tính",
                "The Vietnamese automation name was not restored.");
        }
        finally { englishWindow.Close(); vietnameseWindow.Close(); }
    });

    private static SpreadsheetSession CreateSession()
    {
        var workbook = new Workbook();
        workbook.Worksheets[0].SetValue(default, 7d);
        workbook.Worksheets[0].SetFormula(new CellAddress(1, 0), "=A1*2");
        var session = new SpreadsheetSession(workbook);
        session.Recalculate();
        return session;
    }

    private static void RaiseKey(NeraSpreadsheetControl grid, Key key) => grid.RaiseEvent(
        new System.Windows.Input.KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(grid), 0, key)
        { RoutedEvent = Keyboard.KeyDownEvent, Source = grid });

    private static ScrollBar[] Bars(DependencyObject window) => Descendants(window).OfType<ScrollBar>()
        .Where(bar => AutomationProperties.GetAutomationId(bar).StartsWith("preview-worksheet-scroll-", StringComparison.Ordinal)).ToArray();

    private static ScrollBar Bar(DependencyObject window, Orientation orientation) => Bars(window).Single(bar => bar.Orientation == orientation);

    private static T Field<T>(object target, string name) =>
        (T)(target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target)
            ?? throw new InvalidOperationException($"Missing native field {name}."));

    private static void RunSta(Action verify)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try { verify(); }
            catch (Exception exception) { failure = ExceptionDispatchInfo.Capture(exception); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(45d)), "Navigation smoke timed out.");
        failure?.Throw();
    }

    private static void PumpUntil(Func<bool> condition, string message)
    {
        var watch = Stopwatch.StartNew();
        do { PumpFor(TimeSpan.FromMilliseconds(30d)); }
        while (!condition() && watch.Elapsed < TimeSpan.FromSeconds(5d));
        Assert.IsTrue(condition(), message);
    }

    private static void PumpFor(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = duration };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            foreach (var child in Descendants(VisualTreeHelper.GetChild(root, index))) yield return child;
    }
}
