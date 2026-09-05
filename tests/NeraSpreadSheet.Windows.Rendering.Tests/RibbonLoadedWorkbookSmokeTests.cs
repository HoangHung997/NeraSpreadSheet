using System.Runtime.ExceptionServices;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
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
            Assert.IsTrue(visuals.OfType<TextBlock>().Any(text => text.Text == "=1+2"), "The loaded shell lost its formula display.");
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
