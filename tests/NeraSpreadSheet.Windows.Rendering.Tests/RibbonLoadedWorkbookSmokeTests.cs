using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Wpf;
using NeraSpreadSheet.Wpf.Sample;
using ComboBox = System.Windows.Controls.ComboBox;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class RibbonLoadedWorkbookSmokeTests
{
    [TestMethod]
    [Timeout(60_000)]
    public void LoadedWorkbookShouldRetainTheCompleteRibbonShellAndExistingSession()
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try { VerifyLoadedShell(); }
            catch (Exception exception) { failure = ExceptionDispatchInfo.Capture(exception); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(45)), "Loaded workbook shell timed out.");
        failure?.Throw();
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
            var selector = visuals.OfType<ComboBox>().Single(combo => ReferenceEquals(combo.ItemsSource, workbook.Worksheets));
            Assert.AreSame(selectedSheet, selector.SelectedItem);
            selector.SelectedItem = workbook.Worksheets[0];
            Pump(window);
            Assert.AreSame(workbook.Worksheets[0], session.ActiveWorksheet);
            Assert.AreSame(session, grid.Session);
            Assert.IsTrue(ribbon.IsLoaded);
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
