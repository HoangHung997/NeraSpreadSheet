using System.Runtime.ExceptionServices;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.OpenXml;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Ribbon.Core;
using NeraSpreadSheet.Wpf;
using NeraSpreadSheet.Wpf.Sample;
using ListBox = System.Windows.Controls.ListBox;
using ListBoxItem = System.Windows.Controls.ListBoxItem;
using TextBox = System.Windows.Controls.TextBox;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class RibbonLoadedWorkbookSmokeTests
{
    [TestMethod]
    [Timeout(60_000)]
    public void LoadedWorkbookShouldRetainTheCompleteRibbonShellAndExistingSession()
        => RunSta(VerifyLoadedShell);

    [TestMethod]
    [Timeout(60_000)]
    public void WorksheetTabsShouldRemainHorizontalAndRevealTheActiveSheetAfterResize()
        => RunSta(VerifyWorksheetTabs);

    [TestMethod]
    [Timeout(60_000)]
    public void ImportedSplitShellShouldPreservePaneStateHistoryAndSingleInputTopology()
        => RunSta(VerifyImportedSplitShell);

    private static void VerifyImportedSplitShell()
    {
        var source = new SpreadsheetSession(new Workbook());
        source.ActiveWorksheet.SetValue(default, "Synthetic split import");
        source.Workbook.AddWorksheet("Unsplit");
        var hiddenSheet = source.Workbook.AddWorksheet("Hidden pane offsets");
        var state = new SpreadsheetSplitViewState(SpreadsheetSplitViewMode.Both, 300.5d, 110.25d,
            SpreadsheetSplitViewPane.BottomRight, new(12.25d, 20.5d), new(31.75d, 41.125d),
            new(51.5d, 61.75d), new(71.25d, 81.5d));
        var hiddenState = state.WithTopology(SpreadsheetSplitViewMode.Vertical, 270.125d, null);
        source.View.SetSplitState(state);
        source.View.SetSplitState(hiddenSheet, hiddenState);
        var session = RoundTrip(source);
        var first = session.ActiveWorksheet;
        var beforeVersion = first.Version;
        var beforeUsed = first.UsedCellCount;
        var selection = session.Selection.Capture();
        session.View.SetFrozenPanes(1, 1);
        session.View.ExecuteSplitViewChange(state.WithPaneScroll(SpreadsheetSplitViewPane.TopLeft, 99d, 88d), "Synthetic prior view change");
        Assert.IsTrue(session.View.UndoSplitViewChange());
        var undo = session.View.SplitViewUndoCount;
        var redo = session.View.SplitViewRedoCount;
        var viewVersion = session.View.Version;
        using var window = new RibbonPreviewWindow(session, "Loaded split workbook") { ShowInTaskbar = false };
        try
        {
            window.Show();
            PumpSplit(window);
            var grid = Descendants(window).OfType<NeraSpreadsheetControl>().Single();
            Assert.IsTrue(grid.TryGetSplitPaneController(out var split));
            Assert.AreSame(session, split.Session);
            Assert.IsTrue(split.IsAttached);
            split.RenderNow();
            Assert.AreEqual(4, split.LastFrame?.Panes.Count);
            Assert.AreEqual(8, split.LastFrame?.ScrollBars.Bars.Count);
            Assert.AreEqual(state, session.View.SplitState);
            Assert.AreEqual(undo, session.View.SplitViewUndoCount);
            Assert.AreEqual(redo, session.View.SplitViewRedoCount);
            Assert.AreEqual(viewVersion, session.View.Version, "Opening a split shell must not publish a view mutation.");
            Assert.AreEqual(selection.ActiveCell, session.Selection.ActiveCell);
            Assert.AreEqual(selection.Version, session.Selection.Version);
            Assert.IsFalse(grid.IsHitTestVisible);
            Assert.IsFalse(grid.Focusable);
            Assert.IsTrue(NavigationBars(window).All(bar => bar.Visibility == Visibility.Collapsed && !bar.IsEnabled));
            var splitBodyWidth = split.LastFrame!.Layout.ViewportSize.Width;
            Assert.IsTrue(Field<RibbonRuntimeController>(window, "_runtime").TryActivateAsync("Sample.Headers").AsTask().GetAwaiter().GetResult());
            PumpSplit(window);
            Assert.IsTrue(split.LastFrame!.Layout.ViewportSize.Width > splitBodyWidth + 10d,
                "A header command must invalidate the active split renderer as well as the standalone control.");
            Assert.AreEqual(state, session.View.SplitState);

            // Drive the existing integrated native scrollbar state machine, not an
            // optional second overlay or a synthetic controller in the test.
            var frame = split.LastFrame!;
            Assert.IsTrue(frame.ScrollBars.TryGetBar(SpreadsheetPaneId.BottomRight,
                SpreadsheetScrollBarOrientation.Horizontal, out var bar));
            var adorner = Field<object>(split, "_adorner");
            var chrome = SpreadsheetChromeGeometry.Calculate(grid.ActualWidth, grid.ActualHeight, grid.RenderTheme);
            var pointX = bar.IncreaseButtonBounds.Left + bar.IncreaseButtonBounds.Width / 2d + chrome.RowHeaderWidth;
            var pointY = bar.IncreaseButtonBounds.Top + bar.IncreaseButtonBounds.Height / 2d + chrome.ColumnHeaderHeight;
            Assert.AreSame(adorner, window.InputHitTest(grid.TranslatePoint(new System.Windows.Point(pointX, pointY), window)),
                "Native hit testing must route the visible pane scrollbar to the split adorner.");
            var begin = adorner.GetType().GetMethod("TryBeginScrollBarInteraction", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(begin);
            Assert.AreEqual(true, begin.Invoke(adorner, [pointX, pointY]));
            PumpSplit(window);
            var after = session.View.SplitState;
            Assert.AreEqual(state.TopLeftScroll, after.TopLeftScroll);
            Assert.AreEqual(state.TopRightScroll, after.TopRightScroll);
            Assert.AreEqual(state.BottomLeftScroll, after.BottomLeftScroll);
            Assert.AreEqual(state.BottomRightScroll.OffsetY, after.BottomRightScroll.OffsetY);
            Assert.AreEqual(state.BottomRightScroll.OffsetX + grid.RenderTheme.ScrollBarLineStep,
                after.BottomRightScroll.OffsetX, 0.01d);
            Assert.AreEqual(beforeVersion, first.Version);
            Assert.AreEqual(beforeUsed, first.UsedCellCount);
            Assert.AreEqual(0, session.History.UndoCount);

            session.ActivateWorksheet(session.Workbook.Worksheets[1]);
            PumpSplit(window);
            Assert.IsFalse(grid.TryGetSplitPaneController(out _));
            Assert.IsTrue(grid.IsHitTestVisible && grid.Focusable);
            Assert.IsTrue(NavigationBars(window).All(item => item.Visibility == Visibility.Visible && item.IsEnabled));
            Assert.IsTrue(grid.UseAdaptiveNavigationExtent);
            Assert.AreEqual(0, session.ActiveWorksheet.UsedCellCount, "An empty loaded worksheet must remain empty.");
            session.ActivateWorksheet(session.Workbook.Worksheets[2]);
            PumpSplit(window);
            Assert.IsTrue(grid.TryGetSplitPaneController(out split));
            split.RenderNow();
            Assert.AreEqual(2, split.LastFrame?.Panes.Count);
            Assert.AreEqual(hiddenState, session.View.SplitState, "Hidden pane offsets must survive shell activation.");
            session.ActivateWorksheet(first);
            PumpSplit(window);
            Assert.AreEqual(after, session.View.SplitState);
            Assert.AreEqual(1, session.View.FrozenRows);
            Assert.AreEqual(1, session.View.FrozenColumns);
            Assert.IsTrue(session.View.ClearSplitPanes());
            PumpSplit(window);
            Assert.IsFalse(grid.TryGetSplitPaneController(out _));
            Assert.AreEqual(after.TopLeftScroll.OffsetX, grid.ScrollSnapshot.OffsetX, 1e-9);
            session.View.SetSplitState(after);
            PumpSplit(window);
            Assert.IsTrue(grid.TryGetSplitPaneController(out split));
            Assert.AreEqual(after, session.View.SplitState);
            var reopened = RoundTrip(session);
            Assert.AreEqual(after, reopened.View.SplitState);
            Assert.AreEqual(hiddenState, reopened.View.GetSplitState(reopened.Workbook.Worksheets[2]));
            Assert.AreEqual(beforeUsed, reopened.ActiveWorksheet.UsedCellCount);
        }
        finally { window.Close(); Pump(window); }
    }

    private static SpreadsheetSession RoundTrip(SpreadsheetSession session)
    {
        var serializer = new NeraOpenXmlSpreadsheetSessionSerializer();
        using var stream = new MemoryStream();
        serializer.SaveSessionAsync(session, stream, new OpenXmlExportOptions()).GetAwaiter().GetResult();
        stream.Position = 0;
        return serializer.LoadSessionAsync(stream, new OpenXmlImportOptions()).GetAwaiter().GetResult();
    }

    private static System.Windows.Controls.Primitives.ScrollBar[] NavigationBars(Window window) =>
        Descendants(window).OfType<System.Windows.Controls.Primitives.ScrollBar>().Where(bar =>
            System.Windows.Automation.AutomationProperties.GetAutomationId(bar)
                .StartsWith("preview-worksheet-scroll-", StringComparison.Ordinal)).ToArray();

    private static void PumpSplit(Window window)
    {
        window.UpdateLayout();
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.ApplicationIdle)
        { Interval = TimeSpan.FromMilliseconds(100d) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
        window.UpdateLayout();
    }

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
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(45)), "Loaded workbook shell timed out.");
        failure?.Throw();
    }

    private static void VerifyWorksheetTabs()
    {
        var workbook = new Workbook();
        for (var index = 1; index < 40; index++) workbook.AddWorksheet($"Worksheet {index:00}");
        var session = new SpreadsheetSession(workbook);
        using var window = new RibbonPreviewWindow(session) { ShowInTaskbar = false };
        try
        {
            window.Show();
            Pump(window);
            var tabs = Descendants(window).OfType<ListBox>()
                .Single(list => ReferenceEquals(list.ItemsSource, workbook.Worksheets));
            var panel = Descendants(tabs).OfType<VirtualizingStackPanel>().Single();
            var scroller = Descendants(tabs).OfType<ScrollViewer>().Single();
            Assert.AreEqual(System.Windows.Controls.Orientation.Horizontal, panel.Orientation);
            Assert.IsTrue(scroller.ScrollableWidth > 0, "Many sheets must scroll, not wrap into extra rows.");
            Assert.IsTrue(Descendants(tabs).OfType<ListBoxItem>().Count() < workbook.Worksheets.Count,
                "The sheet row must not realize every offscreen worksheet tab.");
            var last = workbook.Worksheets[^1];
            session.ActivateWorksheet(last);
            Pump(window);
            Assert.AreSame(last, tabs.SelectedItem);
            AssertVisibleSelectedTab(tabs);
            Assert.IsTrue(Descendants(tabs).OfType<ListBoxItem>().Count() < workbook.Worksheets.Count,
                "Selecting a distant sheet must preserve tab virtualization.");
            var initialWidth = tabs.ActualWidth;
            window.Width = 640;
            Pump(window);
            Assert.IsTrue(tabs.ActualWidth < initialWidth - 100, "The loaded shell did not actually shrink.");
            AssertVisibleSelectedTab(tabs);
            Assert.AreEqual(0d, scroller.ScrollableHeight, 0.1, "Sheet tabs must stay in one row.");

            var grid = Descendants(window).OfType<NeraSpreadsheetControl>().Single();
            grid.BeginEdit("=S");
            var editor = Field<TextBox>(grid, "_editor");
            editor.AppendText("U");
            Pump(window);
            var popup = Field<System.Windows.Controls.Primitives.Popup>(grid, "_formulaSuggestionPopup");
            Assert.AreEqual("=SU", editor.Text);
            Assert.AreEqual(Visibility.Visible, editor.Visibility);
            Assert.IsTrue(popup.IsOpen && grid.CurrentFormulaSuggestions.Count > 0,
                "The tab-switch regression must start with a real changed native draft and completion popup.");
            var before = last.GetCell(default);
            var history = session.History.UndoCount;
            tabs.SelectedItem = workbook.Worksheets[0];
            Pump(window);
            Assert.IsFalse(session.Editor.IsEditing, "Sheet activation must use the session's existing cancellation semantics.");
            Assert.AreEqual(Visibility.Collapsed, editor.Visibility);
            Assert.IsFalse(popup.IsOpen);
            Assert.AreEqual(0, grid.CurrentFormulaSuggestions.Count);
            Assert.AreEqual(before, last.GetCell(default));
            Assert.AreEqual(history, session.History.UndoCount);
            Assert.AreSame(workbook.Worksheets[0], session.ActiveWorksheet);
            AssertVisibleSelectedTab(tabs);
        }
        finally { window.Close(); Pump(window); }
    }

    private static T Field<T>(object target, string name) =>
        (T)(target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target)
            ?? throw new InvalidOperationException($"Expected native field {name} was not found."));

    private static void AssertVisibleSelectedTab(ListBox tabs)
    {
        var item = tabs.ItemContainerGenerator.ContainerFromItem(tabs.SelectedItem) as ListBoxItem;
        Assert.IsNotNull(item, "The active worksheet tab must have a realized native container.");
        var viewport = Descendants(tabs).OfType<ScrollContentPresenter>().Single();
        var origin = item.TranslatePoint(default, viewport);
        Assert.IsTrue(origin.X >= -1 && origin.X + item.ActualWidth <= viewport.ActualWidth + 1,
            "The active tab was clipped outside the horizontal viewport.");
        Assert.IsTrue(origin.Y >= -1 && origin.Y + item.ActualHeight <= viewport.ActualHeight + 1,
            "The tab row changed height or clipped its selected label.");
    }

    private static void VerifyLoadedShell()
    {
        var workbook = new Workbook();
        workbook.RenameWorksheet(workbook.Worksheets[0], "Imported");
        var selectedSheet = workbook.AddWorksheet("Second");
        var session = new SpreadsheetSession(workbook);
        session.ActivateWorksheet(selectedSheet);
        var address = new CellAddress(5, 7);
        session.SetFormula(address, "=1+2");
        session.Selection.SetActiveCell(address);
        var beforeCell = selectedSheet.GetCell(address);
        var beforeHistory = session.History.UndoCount;
        using var window = new RibbonPreviewWindow(session, "Synthetic loaded workbook") { ShowInTaskbar = false };
        try
        {
            window.Show();
            Pump(window);
            var visuals = Descendants(window).ToArray();
            var grid = visuals.OfType<NeraSpreadsheetControl>().Single();
            var ribbon = visuals.OfType<NeraRibbonControl>().Single();
            Assert.AreSame(session, grid.Session, "Opening a workbook must not substitute the synthetic preview session.");
            Assert.AreSame(selectedSheet, session.ActiveWorksheet);
            Assert.AreEqual(address, session.Selection.ActiveCell);
            Assert.AreEqual(beforeCell, selectedSheet.GetCell(address));
            Assert.AreEqual(beforeHistory, session.History.UndoCount);
            Assert.HasCount(2, workbook.Worksheets);
            Assert.IsTrue(ribbon.IsLoaded && ribbon.LayoutSnapshot.Tabs.Count > 1);
            Assert.IsTrue(visuals.OfType<System.Windows.Controls.TextBox>().Any(text => text.Text == "=1+2"), "The loaded shell lost its formula bar.");
            Assert.IsTrue(visuals.OfType<TextBlock>().Any(text => text.Text == "Synthetic loaded workbook"));
            var selector = visuals.OfType<ListBox>().Single(list => ReferenceEquals(list.ItemsSource, workbook.Worksheets));
            Assert.AreSame(selectedSheet, selector.SelectedItem);
            selector.SelectedItem = workbook.Worksheets[0];
            Pump(window);
            Assert.AreSame(workbook.Worksheets[0], session.ActiveWorksheet);
            Assert.AreSame(session, grid.Session);
            Assert.IsTrue(ribbon.IsLoaded);
            Assert.AreEqual(beforeHistory, session.History.UndoCount, "Sheet navigation must not mutate workbook history.");
            session.ActivateWorksheet(selectedSheet);
            Pump(window);
            Assert.AreSame(selectedSheet, selector.SelectedItem, "Tabs must follow activation outside the tab row.");
        }
        finally { window.Close(); Pump(window); }
    }

    private static void Pump(Window window) => window.Dispatcher.Invoke(
        System.Windows.Threading.DispatcherPriority.ApplicationIdle, static () => { });

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            foreach (var child in Descendants(VisualTreeHelper.GetChild(root, index))) yield return child;
        }
    }
}
