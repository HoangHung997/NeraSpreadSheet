using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.WinForms;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WinFormsStructuredReferenceEditorTests
{
    [TestMethod]
    [DataRow(Keys.Up)]
    [DataRow(Keys.Down)]
    [DataRow(Keys.PageUp)]
    [DataRow(Keys.PageDown)]
    [DataRow(Keys.None)]
    [Timeout(60_000)]
    public void PointModeShouldInsertAtMovedMultilineCaretAndKeepPreviousReference(Keys caretKey)
    {
        RunLoaded((control, session, editor) =>
        {
            session.ActiveWorksheet.SetCell(new CellAddress(20, 0), new CellData(CellValue.FromNumber(999d), "=1+1"));
            var before = session.ActiveWorksheet.EnumerateUsedCells().ToArray();
            control.BeginEdit("=SUM(" + Environment.NewLine);
            Assert.IsTrue(control.InsertFormulaReference(new CellRange(new CellAddress(1, 1), new CellAddress(2, 1))));
            Assert.AreEqual("=SUM(" + Environment.NewLine + "B2:B3", control.CurrentEditText);
            Assert.IsFalse(GetField<ListBox>(control, "_formulaSuggestionList").Visible);
            // Model the native caret change before KeyUp. None covers callers
            // that move the caret programmatically without a keyboard event.
            editor.Select(5, 0);
            if (caretKey != Keys.None) Raise(editor, "OnKeyUp", new KeyEventArgs(caretKey));
            Assert.IsTrue(control.InsertFormulaReference(new CellRange(new CellAddress(4, 0), new CellAddress(4, 0))));
            Assert.AreEqual("=SUM(A5" + Environment.NewLine + "B2:B3", control.CurrentEditText);
            Assert.IsTrue(control.InsertFormulaReference(new CellRange(new CellAddress(4, 0), new CellAddress(5, 0))));
            Assert.AreEqual("=SUM(A5:A6" + Environment.NewLine + "B2:B3", control.CurrentEditText);
            Assert.AreEqual(0, session.History.UndoCount);
            CollectionAssert.AreEqual(before, session.ActiveWorksheet.EnumerateUsedCells().ToArray());
        });
    }

    [TestMethod]
    [Timeout(60_000)]
    public void PointModeCommitShouldRestoreEditedCellAndPreserveClippedEditorWidth()
    {
        RunLoaded((control, session, editor) =>
        {
            var target = session.Selection.ActiveCell;
            control.BeginEdit("=SUM(");
            var width = editor.Width;
            control.ScrollTo(30d, 0d);
            Assert.AreEqual(width, editor.Width);
            Assert.IsNotNull(editor.Region);
            var range = new CellRange(new CellAddress(1, 1), new CellAddress(3, 1));
            control.InsertFormulaReference(range);
            session.Selection.Select(range);
            editor.AppendText(")");
            Press(editor, Keys.Enter);
            Assert.AreEqual(60d, session.ActiveWorksheet.GetValue(target));
            Assert.AreEqual(new CellAddress(target.RowIndex + 1, target.ColumnIndex), session.Selection.ActiveCell);
            Assert.IsTrue(session.Undo());
            Assert.IsNull(session.ActiveWorksheet.GetFormula(target));
            Assert.IsTrue(session.Redo());
            Assert.AreEqual(60d, session.ActiveWorksheet.GetValue(target));
        });
    }

    [TestMethod]
    [Timeout(60_000)]
    public void LoadedEditorShouldCompleteAndCommitTableFormulaThroughHistory()
    {
        RunLoaded((control, session, editor) =>
        {
            control.BeginEdit("=SUM(Sales[Am");
            Assert.AreEqual("Amount", control.CurrentStructuredReferenceSuggestions.Single().DisplayText);
            Press(editor, Keys.Tab);
            Assert.AreEqual("=SUM(Sales[[#Data],[Amount]]", control.CurrentEditText);
            Assert.IsTrue(control.IsEditing);
            Assert.AreEqual(0, session.History.UndoCount);
            editor.AppendText(")");
            Assert.AreEqual(new CellRange(new CellAddress(1, 1), new CellAddress(3, 1)),
                control.CurrentFormulaReferenceHighlights.Single().Range);
            Press(editor, Keys.Enter);
            Assert.IsFalse(control.IsEditing);
            Assert.AreEqual(60d, session.ActiveWorksheet.GetValue(new CellAddress(6, 0)));
            Assert.AreEqual(1, session.History.UndoCount);
            Assert.IsTrue(session.Undo());
            Assert.IsTrue(session.Redo());
            Assert.AreEqual(60d, session.ActiveWorksheet.GetValue(new CellAddress(6, 0)));
        });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(60_000)]
    public void LoadedEditorShouldAcceptMouseAndResolveOrRejectChangedPopupIdentity(bool deleteTable)
    {
        RunLoaded((control, session, editor) =>
        {
            control.BeginEdit("=Sa");
            var list = GetField<ListBox>(control, "_formulaSuggestionList");
            Raise(list, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, 4, 4, 0));
            Assert.AreEqual("=Sales[#Data]", control.CurrentEditText);
            control.CancelEditor();
            control.BeginEdit("=Sales[Am");
            var candidate = control.CurrentStructuredReferenceSuggestions.Single();
            session.Tables.RenameTable(candidate.TableId, "ĐơnHàng");
            session.Tables.RenameColumn(candidate.TableId, candidate.ColumnId!.Value, "Tiền");
            var history = session.History.UndoCount;
            Press(editor, Keys.Tab);
            Assert.AreEqual("=ĐơnHàng[[#Data],[Tiền]]", control.CurrentEditText);
            Assert.AreEqual(history, session.History.UndoCount);
            control.CancelEditor();
            control.BeginEdit("=ĐơnHàng[Ti");
            if (deleteTable) session.Tables.Remove(candidate.TableId);
            else session.Tables.DeleteColumn(candidate.TableId, candidate.ColumnId.Value);
            history = session.History.UndoCount;
            Press(editor, Keys.Tab);
            Assert.AreEqual("=ĐơnHàng[Ti", control.CurrentEditText);
            Assert.IsTrue(control.IsEditing);
            Assert.AreEqual(history, session.History.UndoCount);
            Press(editor, Keys.Escape);
            Assert.IsFalse(control.IsEditing);
        });
    }

    [TestMethod]
    [Timeout(60_000)]
    public void LoadedEditorShouldReplacePointModeAndKeepCurrentRowAndCrossSheetContext()
    {
        RunLoaded((control, session, editor) =>
        {
            session.ActiveWorksheet.SetCell(new CellAddress(20, 0), new CellData(CellValue.FromNumber(999d), "=1+1"));
            var cells = session.ActiveWorksheet.EnumerateUsedCells().ToArray();
            control.BeginEdit("=SUM(");
            Assert.IsTrue(control.InsertFormulaReference(new CellRange(new CellAddress(1, 1), new CellAddress(3, 1))));
            Assert.AreEqual("=SUM(Sales[[#Data],[Amount]]", control.CurrentEditText);
            Assert.AreEqual(new CellRange(new CellAddress(1, 1), new CellAddress(3, 1)), control.CurrentFormulaReferenceHighlights.Single().Range);
            Assert.IsTrue(control.InsertFormulaReference(new CellRange(new CellAddress(1, 1), new CellAddress(2, 1))));
            Assert.AreEqual("=SUM(B2:B3", control.CurrentEditText);
            CollectionAssert.AreEqual(cells, session.ActiveWorksheet.EnumerateUsedCells().ToArray());
            control.CancelEditor();
            session.Selection.SetActiveCell(new CellAddress(2, 0));
            control.BeginEdit("=[@Am");
            Press(editor, Keys.Tab);
            Assert.AreEqual("=Sales[[#This Row],[Amount]]", control.CurrentEditText);
            Assert.AreEqual(new CellRange(new CellAddress(2, 1), new CellAddress(2, 1)), control.CurrentFormulaReferenceHighlights.Single().Range);
            control.CancelEditor();
            var other = session.Workbook.AddWorksheet("Other sheet");
            other.AddTable(new SpreadsheetTable(Guid.NewGuid(), "ForeignSales", new CellRange(default, new CellAddress(3, 1)),
                [new SpreadsheetTableColumn(Guid.NewGuid(), "Item"), new SpreadsheetTableColumn(Guid.NewGuid(), "Amount")]));
            control.BeginEdit("=SUM(");
            Assert.IsTrue(control.InsertFormulaReference(new CellRange(new CellAddress(1, 1), new CellAddress(3, 1)), other.Name));
            Assert.AreEqual("=SUM(ForeignSales[[#Data],[Amount]]", control.CurrentEditText);
            Assert.AreEqual(0, control.CurrentFormulaReferenceHighlights.Count);
            Assert.AreEqual(0, session.History.UndoCount);
        });
    }

    [TestMethod]
    [Timeout(60_000)]
    public void LoadedEditorShouldKeepNewlineAndCancelContractsWithSuggestionsVisible()
    {
        RunLoaded((control, session, editor) =>
        {
            control.BeginEdit("=Sales[Am");
            Press(editor, Keys.Alt | Keys.Enter);
            Assert.IsTrue(control.IsEditing);
            Assert.AreEqual("=Sales[Am" + Environment.NewLine, control.CurrentEditText);
            Press(editor, Keys.Escape);
            Assert.IsFalse(control.IsEditing);
            control.BeginEdit("=Sa");
            Press(editor, Keys.Escape);
            Assert.IsFalse(control.IsEditing);
            Assert.AreEqual(0, session.History.UndoCount);
            control.BeginEdit("=Sa");
            Press(editor, Keys.Enter);
            Assert.IsFalse(control.IsEditing);
            Assert.AreEqual("=Sa", session.ActiveWorksheet.GetFormula(new CellAddress(6, 0)));
        });
    }

    [TestMethod]
    [DataRow("=\"Sales[Am")]
    [DataRow("=Sales[[#Data],[Am")]
    [DataRow("='Sales[Am")]
    [Timeout(60_000)]
    public void LoadedEditorShouldRejectUnsupportedDraftWithoutMutation(string text)
    {
        RunLoaded((control, session, _) =>
        {
            control.BeginEdit(text);
            Assert.AreEqual(0, control.CurrentStructuredReferenceSuggestions.Count);
            Assert.IsFalse(control.InsertFormulaReference(new CellRange(default, default)));
            Assert.AreEqual(text, control.CurrentEditText);
            Assert.AreEqual(0, session.History.UndoCount);
        });
    }

    private static T GetField<T>(NeraSpreadsheetControl control, string name) =>
        (T)typeof(NeraSpreadsheetControl).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(control)!;

    private static void Raise(Control control, string method, EventArgs args) =>
        typeof(Control).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, [args]);

    private static void Press(TextBox editor, Keys key)
    {
        var args = new KeyEventArgs(key);
        Raise(editor, "OnKeyDown", args);
        Assert.IsTrue(args.Handled);
        Assert.IsTrue(args.SuppressKeyPress);
    }

    private static void RunLoaded(Action<NeraSpreadsheetControl, SpreadsheetSession, TextBox> action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var workbook = new Workbook();
                var sheet = workbook.Worksheets[0];
                sheet.AddTable(new SpreadsheetTable(Guid.NewGuid(), "Sales", new CellRange(default, new CellAddress(3, 1)),
                    [new SpreadsheetTableColumn(Guid.NewGuid(), "Item"), new SpreadsheetTableColumn(Guid.NewGuid(), "Amount")]));
                for (var row = 1; row <= 3; row++) sheet.SetValue(new CellAddress(row, 1), row * 10d);
                var session = new SpreadsheetSession(workbook);
                session.Selection.SetActiveCell(new CellAddress(6, 0));
                using var control = new NeraSpreadsheetControl { Session = session, Dock = DockStyle.Fill };
                using var form = new Form { Width = 600, Height = 400, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Left = -32_000, Top = -32_000 };
                form.Controls.Add(control);
                form.Show();
                Application.DoEvents();
                Assert.IsTrue(control.IsHandleCreated);
                action(control, session, GetField<TextBox>(control, "_editor"));
                control.CancelEditor();
                form.Close();
            }
            catch (Exception exception) { failure = exception; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(45d)), "Loaded WinForms Table editor smoke timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
