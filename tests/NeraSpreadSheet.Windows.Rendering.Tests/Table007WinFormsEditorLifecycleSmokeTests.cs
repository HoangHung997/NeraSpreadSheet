using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.WinForms;
using Forms = System.Windows.Forms;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class Table007WinFormsEditorLifecycleSmokeTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(60_000)]
    public void NativeDraftAndSuggestionsShouldCloseAfterCanonicalCancellationWithoutChangingSelectionOrHistory(bool activateWorksheet)
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
                using var control = new NeraSpreadsheetControl { Session = session, Dock = Forms.DockStyle.Fill };
                using var form = new Forms.Form
                {
                    Width = 640, Height = 440, ShowInTaskbar = false,
                    StartPosition = Forms.FormStartPosition.Manual, Left = -32_000, Top = -32_000,
                };
                form.Controls.Add(control);
                form.Show();
                Forms.Application.DoEvents();
                Assert.IsTrue(control.IsHandleCreated && control.Visible);
                control.BeginEdit("=SUM(Sales[A");
                var editor = Field<Forms.TextBox>(control, "_editor");
                editor.AppendText("m");
                Forms.Application.DoEvents();
                var suggestions = Field<Forms.ListBox>(control, "_formulaSuggestionList");
                Assert.AreEqual("=SUM(Sales[Am", editor.Text);
                Assert.IsTrue(editor.Visible);
                Assert.IsTrue(suggestions.Visible);
                Assert.AreEqual(1, control.CurrentStructuredReferenceSuggestions.Count);

                if (activateWorksheet)
                {
                    session.ActivateWorksheet(second);
                    Assert.IsFalse(editor.Visible,
                        "Worksheet activation must hide the native draft before any caller cleanup.");
                    Assert.IsFalse(suggestions.Visible);
                }
                else
                {
                    Assert.IsTrue(session.Editor.Cancel());
                    session.Selection.SetActiveCell(new CellAddress(11, 5));
                }
                var version = session.Selection.Version;
                Assert.IsFalse(control.CancelEditor());
                Forms.Application.DoEvents();
                Assert.IsFalse(editor.Visible);
                Assert.IsFalse(suggestions.Visible);
                Assert.IsFalse(control.IsEditing);
                Assert.AreEqual(0, control.CurrentStructuredReferenceSuggestions.Count);
                Assert.AreEqual(0, control.CurrentFormulaReferenceHighlights.Count);
                Assert.AreEqual(activateWorksheet ? default : new CellAddress(11, 5), session.Selection.ActiveCell);
                Assert.AreEqual(version, session.Selection.Version);
                Assert.AreEqual(history, session.History.UndoCount);
                Assert.AreEqual(0, session.History.RedoCount);
                CollectionAssert.AreEqual(firstCells, first.EnumerateUsedCells().ToArray());
                CollectionAssert.AreEqual(secondCells, second.EnumerateUsedCells().ToArray());
                form.Close();
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
