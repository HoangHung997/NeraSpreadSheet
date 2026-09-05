using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Wpf;
using TextBox = System.Windows.Controls.TextBox;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class Table007EditorLifecycleSmokeTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(60_000)]
    public void NativeDraftAndPopupShouldCloseAfterCanonicalCancellationWithoutChangingSelectionOrHistory(bool activateWorksheet)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var workbook = new Workbook();
                var first = workbook.Worksheets[0];
                first.AddTable(new SpreadsheetTable(Guid.NewGuid(), "Sales",
                    new CellRange(default, new CellAddress(3, 1)),
                    [new SpreadsheetTableColumn(Guid.NewGuid(), "Item"), new SpreadsheetTableColumn(Guid.NewGuid(), "Amount")]));
                var second = workbook.AddWorksheet("Other");
                second.SetValue(default, "preserved");
                var session = new SpreadsheetSession(workbook);
                session.SetValue(new CellAddress(6, 0), "original");
                session.Selection.SetActiveCell(new CellAddress(6, 0));
                var history = session.History.UndoCount;
                var firstCells = first.EnumerateUsedCells().ToArray();
                var secondCells = second.EnumerateUsedCells().ToArray();
                using var control = new NeraSpreadsheetControl { Session = session, Width = 600, Height = 400 };
                var window = new Window { Content = control, ShowInTaskbar = false, Left = -32_000, Top = -32_000 };
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    Assert.IsTrue(control.IsLoaded);
                    control.BeginEdit("=SUM(");
                    var editor = Field<TextBox>(control, "_editor");
                    editor.AppendText("Sales[Am");
                    window.UpdateLayout();
                    var popup = Field<Popup>(control, "_formulaSuggestionPopup");
                    Assert.AreEqual("=SUM(Sales[Am", editor.Text);
                    Assert.AreEqual(Visibility.Visible, editor.Visibility);
                    Assert.IsTrue(popup.IsOpen);
                    Assert.AreEqual(1, control.CurrentStructuredReferenceSuggestions.Count);

                    if (activateWorksheet)
                    {
                        session.ActivateWorksheet(second);
                        Assert.AreEqual(Visibility.Collapsed, editor.Visibility,
                            "Worksheet activation must hide the native draft before any caller cleanup.");
                        Assert.IsFalse(popup.IsOpen);
                    }
                    else
                    {
                        Assert.IsTrue(session.Editor.Cancel());
                        session.Selection.SetActiveCell(new CellAddress(11, 5));
                    }
                    var version = session.Selection.Version;
                    Assert.IsFalse(control.CancelEditor());
                    window.UpdateLayout();
                    Assert.AreEqual(Visibility.Collapsed, editor.Visibility);
                    Assert.IsFalse(popup.IsOpen);
                    Assert.IsFalse(control.IsEditing);
                    Assert.AreEqual(0, control.CurrentStructuredReferenceSuggestions.Count);
                    Assert.AreEqual(0, control.CurrentFormulaReferenceHighlights.Count);
                    Assert.AreEqual(activateWorksheet ? default : new CellAddress(11, 5), session.Selection.ActiveCell);
                    Assert.AreEqual(version, session.Selection.Version);
                    Assert.AreEqual(history, session.History.UndoCount);
                    Assert.AreEqual(0, session.History.RedoCount);
                    CollectionAssert.AreEqual(firstCells, first.EnumerateUsedCells().ToArray());
                    CollectionAssert.AreEqual(secondCells, second.EnumerateUsedCells().ToArray());
                }
                finally { window.Close(); }
            }
            catch (Exception exception) { failure = exception; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(45)), "Native editor lifecycle smoke timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
}
