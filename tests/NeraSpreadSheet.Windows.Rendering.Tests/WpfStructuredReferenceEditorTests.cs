using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Wpf;
using ListBox = System.Windows.Controls.ListBox;
using TextBox = System.Windows.Controls.TextBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WpfStructuredReferenceEditorTests
{
    [TestMethod]
    [Timeout(60_000)]
    public void PointModeCommitShouldMoveFromEditedCellAndKeepUndoRedoValues()
    {
        RunLoaded((control, session, editor) =>
        {
            var target = session.Selection.ActiveCell;
            control.BeginEdit("=SUM(");
            var range = new CellRange(new CellAddress(1, 1), new CellAddress(3, 1));
            control.InsertFormulaReference(range);
            session.Selection.Select(range);
            editor.AppendText(")");
            Press(editor, Key.Enter);
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
    public void LoadedEditorShouldAcceptColumnWithTabAndCommitThroughHistory()
    {
        RunLoaded((control, session, editor) =>
        {
            control.BeginEdit("=SUM(Sales[Am");
            Assert.AreEqual("Amount", control.CurrentStructuredReferenceSuggestions.Single().DisplayText);
            Assert.IsTrue(Press(editor, Key.Tab));
            Assert.AreEqual("=SUM(Sales[[#Data],[Amount]]", control.CurrentEditText);
            Assert.IsTrue(control.IsEditing);
            Assert.AreEqual(0, session.History.UndoCount);
            editor.AppendText(")");
            Assert.AreEqual(new CellRange(new CellAddress(1, 1), new CellAddress(3, 1)),
                control.CurrentFormulaReferenceHighlights.Single().Range);
            Assert.IsTrue(Press(editor, Key.Enter));
            Assert.IsFalse(control.IsEditing);
            Assert.AreEqual(60d, session.ActiveWorksheet.GetValue(new CellAddress(6, 0)));
            Assert.AreEqual(1, session.History.UndoCount);
            Assert.IsTrue(session.Undo());
            Assert.IsNull(session.ActiveWorksheet.GetFormula(new CellAddress(6, 0)));
            Assert.IsTrue(session.Redo());
            Assert.AreEqual(60d, session.ActiveWorksheet.GetValue(new CellAddress(6, 0)));
        });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(60_000)]
    public void LoadedEditorShouldResolveRenamedPopupIdentityAndConsumeDeletedCandidate(bool deleteTable)
    {
        RunLoaded((control, session, editor) =>
        {
            control.BeginEdit("=Sales[Am");
            var candidate = control.CurrentStructuredReferenceSuggestions.Single();
            session.Tables.RenameTable(candidate.TableId, "ĐơnHàng");
            session.Tables.RenameColumn(candidate.TableId, candidate.ColumnId!.Value, "Tiền [#'@]");
            var count = session.History.UndoCount;
            Assert.IsTrue(Press(editor, Key.Tab));
            Assert.AreEqual("=ĐơnHàng[[#Data],[Tiền '['#'''@']]]", control.CurrentEditText);
            Assert.AreEqual(count, session.History.UndoCount);
            control.CancelEditor();
            control.BeginEdit("=ĐơnHàng[Ti");
            Assert.AreEqual(1, control.CurrentStructuredReferenceSuggestions.Count);
            if (deleteTable) session.Tables.Remove(candidate.TableId);
            else session.Tables.DeleteColumn(candidate.TableId, candidate.ColumnId.Value);
            count = session.History.UndoCount;
            Assert.IsTrue(Press(editor, Key.Tab));
            Assert.IsTrue(control.IsEditing);
            Assert.AreEqual("=ĐơnHàng[Ti", control.CurrentEditText);
            Assert.AreEqual(0, control.CurrentStructuredReferenceSuggestions.Count);
            Assert.AreEqual(count, session.History.UndoCount);
        });
    }

    [TestMethod]
    [Timeout(60_000)]
    public void LoadedEditorShouldAcceptMouseCandidateAndEscapeCancelWithPopupOpen()
    {
        RunLoaded((control, session, editor) =>
        {
            control.BeginEdit("=Sa");
            var list = GetField<ListBox>(control, "_formulaSuggestionList");
            list.UpdateLayout();
            var item = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
            Assert.IsNotNull(item);
            item.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            {
                RoutedEvent = Mouse.PreviewMouseDownEvent,
            });
            Assert.AreEqual("=Sales[#Data]", control.CurrentEditText);
            Assert.AreEqual(0, session.History.UndoCount);
            control.CancelEditor();
            control.BeginEdit("=Sales[Am");
            Assert.AreEqual(1, control.CurrentStructuredReferenceSuggestions.Count);
            Assert.IsTrue(Press(editor, Key.Escape));
            Assert.IsFalse(control.IsEditing);
            Assert.AreEqual(0, control.CurrentStructuredReferenceSuggestions.Count);
            Assert.AreEqual(0, session.History.UndoCount);
        });
    }

    [TestMethod]
    [Timeout(60_000)]
    public void DraftPointModeShouldReplaceProvisionalSpanWithoutChangingCachedCells()
    {
        RunLoaded((control, session, editor) =>
        {
            session.ActiveWorksheet.SetCell(new CellAddress(20, 0), new CellData(CellValue.FromNumber(999d), "=1+1"));
            var before = session.ActiveWorksheet.EnumerateUsedCells().ToArray();
            control.BeginEdit("=SUM(");
            Assert.IsTrue(control.InsertFormulaReference(new CellRange(new CellAddress(1, 1), new CellAddress(1, 1))));
            Assert.AreEqual("=SUM(B2", control.CurrentEditText);
            Assert.IsTrue(control.InsertFormulaReference(new CellRange(new CellAddress(1, 1), new CellAddress(3, 1))));
            Assert.AreEqual("=SUM(Sales[[#Data],[Amount]]", control.CurrentEditText);
            Assert.AreEqual(new CellRange(new CellAddress(1, 1), new CellAddress(3, 1)),
                control.CurrentFormulaReferenceHighlights.Single().Range);
            Assert.IsTrue(control.InsertFormulaReference(new CellRange(new CellAddress(1, 0), new CellAddress(2, 1))));
            Assert.AreEqual("=SUM(A2:B3", control.CurrentEditText);
            Assert.AreEqual(0, session.History.UndoCount);
            CollectionAssert.AreEqual(before, session.ActiveWorksheet.EnumerateUsedCells().ToArray());
            editor.CaretIndex = 1;
            Assert.IsTrue(control.InsertFormulaReference(new CellRange(new CellAddress(4, 0), new CellAddress(4, 0))));
            Assert.AreEqual("=A5SUM(A2:B3", control.CurrentEditText);
        });
    }

    [TestMethod]
    [Timeout(60_000)]
    public void LoadedEditorShouldUseOwningRowAndCrossSheetTableMetadata()
    {
        RunLoaded((control, session, editor) =>
        {
            session.Selection.SetActiveCell(new CellAddress(2, 0));
            control.BeginEdit("=[@Am");
            Assert.IsTrue(Press(editor, Key.Tab));
            Assert.AreEqual("=Sales[[#This Row],[Amount]]", control.CurrentEditText);
            Assert.AreEqual(new CellRange(new CellAddress(2, 1), new CellAddress(2, 1)),
                control.CurrentFormulaReferenceHighlights.Single().Range);
            control.CancelEditor();
            var sheet = session.Workbook.AddWorksheet("Other sheet");
            sheet.AddTable(new SpreadsheetTable(Guid.NewGuid(), "ForeignSales", new CellRange(default, new CellAddress(3, 1)),
                [new SpreadsheetTableColumn(Guid.NewGuid(), "Item"), new SpreadsheetTableColumn(Guid.NewGuid(), "Amount")]));
            control.BeginEdit("=SUM(");
            Assert.IsTrue(control.InsertFormulaReference(new CellRange(new CellAddress(1, 1), new CellAddress(3, 1)), sheet.Name));
            Assert.AreEqual("=SUM(ForeignSales[[#Data],[Amount]]", control.CurrentEditText);
            Assert.AreEqual(0, control.CurrentFormulaReferenceHighlights.Count);
            Assert.IsTrue(control.InsertFormulaReference(new CellRange(new CellAddress(1, 1), new CellAddress(2, 1)), sheet.Name));
            Assert.AreEqual("=SUM('Other sheet'!B2:B3", control.CurrentEditText);
            Assert.IsFalse(control.InsertFormulaReference(new CellRange(default, default), "Missing"));
            Assert.AreEqual(0, session.History.UndoCount);
        });
    }

    [TestMethod]
    [DataRow("=\"Sales[Am")]
    [DataRow("=Sales[[#Data],[Am")]
    [DataRow("='Sales[Am")]
    [Timeout(60_000)]
    public void LoadedEditorShouldRejectPointModeInsideUnsupportedFragment(string text)
    {
        RunLoaded((control, session, _) =>
        {
            control.BeginEdit(text);
            Assert.AreEqual(0, control.CurrentStructuredReferenceSuggestions.Count);
            Assert.IsFalse(control.InsertFormulaReference(new CellRange(default, default)));
            Assert.AreEqual(text, control.CurrentEditText);
            Assert.AreEqual(0, control.CurrentFormulaReferenceHighlights.Count);
            Assert.AreEqual(0, session.History.UndoCount);
        });
    }

    private static T GetField<T>(NeraSpreadsheetControl control, string name) =>
        (T)(typeof(NeraSpreadsheetControl).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(control)!);

    private static bool Press(TextBox editor, Key key)
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(editor)!, Environment.TickCount, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        };
        editor.RaiseEvent(args);
        return args.Handled;
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
                using var control = new NeraSpreadsheetControl { Session = session, Width = 600d, Height = 400d };
                var window = new Window { Content = control, ShowInTaskbar = false, Left = -32_000d, Top = -32_000d };
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    Assert.IsTrue(control.IsLoaded);
                    action(control, session, GetField<TextBox>(control, "_editor"));
                    control.CancelEditor();
                }
                finally { window.Close(); }
            }
            catch (Exception exception) { failure = exception; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(45d)), "Loaded Table editor smoke timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
