using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Wpf;
using TextBox = System.Windows.Controls.TextBox;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class Table007WpfEditorDraftSmokeTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(60_000)]
    public void PublicDraftBridgeShouldUseTheNativeEditorWithoutRestartingHistoryOrTakingFormulaBarFocus(bool useSplit)
    {
        RunLoaded(useSplit, (control, split, editor, formulaBar, session) =>
        {
            var snapshots = new List<SpreadsheetEditorDraft?>();
            control.EditorDraftChanged += (_, _) => snapshots.Add(control.CurrentEditorDraft);
            Assert.IsNull(control.CurrentEditorDraft);
            Assert.IsFalse(control.UpdateEditorDraft("42", 2, 0));
            Assert.IsFalse(control.FocusEditor());
            control.BeginEdit();
            var canonical = session.Editor.State;
            Assert.IsNotNull(canonical);
            Assert.AreEqual("original", control.CurrentEditorDraft!.Text);
            Assert.AreEqual(1, snapshots.Count);
            Assert.AreEqual(editor.SelectionLength, control.CurrentEditorDraft.SelectionLength);
            Assert.IsTrue(formulaBar.Focus());
            var beforeUpdate = snapshots.Count;
            Assert.IsTrue(control.UpdateEditorDraft("=20+22", 1, 2));
            Assert.AreSame(formulaBar, Keyboard.FocusedElement);
            Assert.AreSame(canonical, session.Editor.State, "Draft updates must not restart the canonical edit.");
            Assert.AreEqual("=20+22", editor.Text);
            Assert.AreEqual(1, editor.SelectionStart);
            Assert.AreEqual(2, editor.SelectionLength);
            Assert.AreEqual(beforeUpdate + 1, snapshots.Count, "One bridge update must publish one complete snapshot.");
            Assert.AreEqual(editor.CaretIndex, snapshots[^1]!.CaretIndex);
            Assert.AreEqual(0, session.History.UndoCount);
            Assert.AreEqual("original", session.ActiveWorksheet.GetValue(default));
            Assert.IsTrue(control.FocusEditor());
            Assert.AreSame(editor, Keyboard.FocusedElement);
            Assert.AreEqual(1, editor.SelectionStart);
            Assert.AreEqual(2, editor.SelectionLength);
            editor.Select(6, 0);
            Assert.AreEqual(6, control.CurrentEditorDraft!.SelectionStart);
            editor.AppendText("+1");
            Assert.AreEqual("=20+22+1", snapshots[^1]!.Text);
            Assert.AreEqual(editor.Text, control.CurrentEditText);
            if (split is not null) Assert.AreEqual(split.CurrentEditorDraft, control.CurrentEditorDraft);
            var draft = control.CurrentEditorDraft;
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => control.UpdateEditorDraft("bad", 2, 2));
            Assert.AreEqual(draft, control.CurrentEditorDraft);
            Assert.IsTrue(control.CommitEditor());
            Assert.IsNull(control.CurrentEditorDraft);
            Assert.IsNull(snapshots[^1]);
            Assert.AreEqual(Visibility.Collapsed, editor.Visibility);
            Assert.AreEqual(43d, session.ActiveWorksheet.GetValue(default));
            Assert.AreEqual(1, session.History.UndoCount);
            Assert.IsFalse(control.CommitEditor());
            Assert.AreEqual(1, session.History.UndoCount);
            Assert.IsTrue(session.Undo());
            Assert.AreEqual("original", session.ActiveWorksheet.GetValue(default));
            Assert.IsTrue(session.Redo());
            Assert.AreEqual(43d, session.ActiveWorksheet.GetValue(default));
            control.BeginEdit("temporary");
            Assert.IsTrue(control.CancelEditor());
            Assert.IsNull(snapshots[^1]);
            Assert.AreEqual(1, session.History.UndoCount);
        });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(60_000)]
    public void ValidationFailureShouldRetainNativeDraftSelectionAndFormulaBarFocus(bool useSplit)
    {
        RunLoaded(useSplit, (control, _, editor, formulaBar, session) =>
        {
            session.ActiveWorksheet.AddDataValidationRule(new DataValidationRule(
                Guid.NewGuid(), [new CellRange(default, default)], DataValidationType.Whole,
                DataValidationOperator.Between, "1", "10", allowBlank: false, showErrorMessage: true));
            control.BeginEdit();
            Assert.IsTrue(formulaBar.Focus());
            Assert.IsTrue(control.UpdateEditorDraft("20", 0, 1));
            var snapshot = control.CurrentEditorDraft;
            var canonical = session.Editor.State;
            var notifications = 0;
            control.EditorDraftChanged += (_, _) => notifications++;
            Assert.IsFalse(control.CommitEditor());
            Assert.AreEqual(snapshot, control.CurrentEditorDraft);
            Assert.AreSame(canonical, session.Editor.State);
            Assert.AreSame(formulaBar, Keyboard.FocusedElement);
            Assert.AreEqual(Visibility.Visible, editor.Visibility);
            Assert.AreEqual(0, notifications);
            Assert.AreEqual(0, session.History.UndoCount);
            Assert.IsTrue(control.UpdateEditorDraft("5", 1, 0));
            Assert.IsTrue(control.CommitEditor());
            Assert.AreEqual(5d, session.ActiveWorksheet.GetValue(default));
            Assert.AreEqual(1, session.History.UndoCount);
        });
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    [Timeout(60_000)]
    public void CanonicalCancellationShouldNotifyAndClearNativeDraftPopupWithoutMovingSelection(bool useSplit, bool switchWorksheet)
    {
        RunLoaded(useSplit, (control, split, editor, formulaBar, session) =>
        {
            var first = session.ActiveWorksheet;
            first.AddTable(new SpreadsheetTable(Guid.NewGuid(), "Sales",
                new CellRange(new CellAddress(2, 0), new CellAddress(5, 1)),
                [new SpreadsheetTableColumn(Guid.NewGuid(), "Item"), new SpreadsheetTableColumn(Guid.NewGuid(), "Amount")]));
            var second = session.Workbook.AddWorksheet("Other");
            var firstCells = first.EnumerateUsedCells().ToArray();
            var secondCells = second.EnumerateUsedCells().ToArray();
            control.BeginEdit("=SUM(Sales[Am");
            object surface = split is null ? control : Field<object>(split, "_adorner");
            var popup = Field<Popup>(surface, "_formulaSuggestionPopup");
            Assert.IsTrue(popup.IsOpen);
            var notifications = 0;
            control.EditorDraftChanged += (_, _) => notifications++;
            Assert.IsTrue(formulaBar.Focus());
            if (switchWorksheet) session.ActivateWorksheet(second);
            else Assert.IsTrue(session.Editor.Cancel());
            Assert.AreEqual(1, notifications);
            Assert.IsNull(control.CurrentEditorDraft);
            Assert.AreEqual(Visibility.Collapsed, editor.Visibility);
            Assert.IsFalse(popup.IsOpen);
            session.Selection.SetActiveCell(new CellAddress(11, 5));
            var version = session.Selection.Version;
            Assert.IsFalse(control.CancelEditor());
            Assert.AreSame(formulaBar, Keyboard.FocusedElement);
            Assert.AreEqual(version, session.Selection.Version);
            Assert.AreEqual(new CellAddress(11, 5), session.Selection.ActiveCell);
            Assert.AreEqual(0, session.History.UndoCount);
            CollectionAssert.AreEqual(firstCells, first.EnumerateUsedCells().ToArray());
            CollectionAssert.AreEqual(secondCells, second.EnumerateUsedCells().ToArray());
        });
    }

    private static void RunLoaded(bool useSplit,
        Action<NeraSpreadsheetControl, NeraSpreadsheetSplitController?, TextBox, TextBox, SpreadsheetSession> action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var workbook = new Workbook();
                workbook.Worksheets[0].SetValue(default, "original");
                var session = new SpreadsheetSession(workbook);
                using var control = new NeraSpreadsheetControl { Session = session };
                var formulaBar = new TextBox();
                var panel = new DockPanel();
                DockPanel.SetDock(formulaBar, Dock.Top);
                panel.Children.Add(formulaBar);
                panel.Children.Add(new AdornerDecorator { Child = control });
                var window = new Window { Width = 760, Height = 540, ShowInTaskbar = false, Content = panel };
                NeraSpreadsheetSplitController? split = null;
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    if (useSplit)
                    {
                        split = control.EnableSplitPanes(SpreadsheetSplitPaneMode.Both);
                        split.RenderNow();
                        control.Focusable = false;
                        control.IsHitTestVisible = false;
                    }
                    object surface = split is null ? control : Field<object>(split, "_adorner");
                    action(control, split, Field<TextBox>(surface, "_editor"), formulaBar, session);
                }
                finally { split?.Dispose(); window.Close(); }
            }
            catch (Exception exception) { failure = exception; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(45)), "Native draft bridge smoke timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
}
