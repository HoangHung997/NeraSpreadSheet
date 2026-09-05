using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Viewport;
using NeraSpreadSheet.Wpf;
using NeraSpreadSheet.WinForms;
using NativeWpf = System.Windows;
using WpfControls = System.Windows.Controls;
using WpfInput = System.Windows.Input;
using Forms = System.Windows.Forms;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class Table007SplitEditorSmokeTests
{
    [TestMethod]
    [Timeout(90_000)]
    public void LoadedWpfSplitEditorShouldCompleteRenameCommitAndClipFullCell()
    {
        RunInSta(() =>
        {
            var session = CreateSession();
            using var owner = new NeraSpreadSheet.Wpf.NeraSpreadsheetControl { Session = session };
            var window = new NativeWpf.Window { Width = 760, Height = 540, ShowInTaskbar = false,
                Content = new System.Windows.Documents.AdornerDecorator { Child = owner } };
            try
            {
                window.Show();
                window.UpdateLayout();
                using var split = owner.EnableSplitPanes(NeraSpreadSheet.Wpf.SpreadsheetSplitPaneMode.Both);
                split.SetSplit(240, 260);
                split.RenderNow();
                var surface = Field<object>(split, "_adorner");
                var editor = Field<WpfControls.TextBox>(surface, "_editor");
                var list = Field<WpfControls.ListBox>(surface, "_formulaSuggestionList");
                foreach (var draft in new[] { string.Empty, "literal", "=SUM(" })
                {
                    Invoke(surface, "BeginEdit", draft);
                    split.RenderNow();
                    window.UpdateLayout();
                    Assert.AreEqual(0, owner.CurrentFormulaReferenceHighlights.Count,
                        "The hidden standalone editor must not analyze its empty draft during a split edit.");
                    Assert.AreEqual(draft, editor.Text);
                    PressWpf(editor, WpfInput.Key.Escape);
                }
                Assert.AreEqual(0, session.History.UndoCount);
                Invoke(surface, "BeginEdit", "=SUM(Sales[Am");
                window.UpdateLayout();
                Assert.AreEqual("Amount", ((FormulaStructuredReferenceSuggestion)list.Items[0]).DisplayText);
                var table = session.Workbook.Tables.Single();
                session.Tables.RenameTable(table.Id, "Orders");
                PressWpf(editor, WpfInput.Key.Tab);
                Assert.AreEqual("=SUM(Orders[[#Data],[Amount]]", editor.Text);
                Assert.AreEqual(1, session.History.UndoCount);
                editor.AppendText(")");
                PressWpf(editor, WpfInput.Key.Enter);
                Assert.AreEqual(60d, session.ActiveWorksheet.GetValue(new CellAddress(6, 0)));
                Assert.AreEqual(new CellAddress(7, 0), session.Selection.ActiveCell);
                Assert.IsTrue(session.Undo());
                Assert.IsTrue(session.Redo());
                session.Selection.SetActiveCell(new CellAddress(6, 0));
                Invoke(surface, "BeginEdit", "line one line two line three");
                window.UpdateLayout();
                var full = Field<NativeWpf.Rect>(surface, "_editorBounds");
                var clip = Field<NativeWpf.Rect>(surface, "_editorClipBounds");
                Assert.AreEqual(360d, full.Width, 0.01d);
                Assert.IsTrue(clip.Width < full.Width);
                Assert.AreEqual(full.Width, editor.ActualWidth, 0.01d);
                split.SetActivePane(SpreadsheetPaneId.TopRight);
                split.ScrollPaneTo(SpreadsheetPaneId.TopRight, 300d, 0d);
                split.RenderNow();
                window.UpdateLayout();
                Assert.AreEqual(full, Field<NativeWpf.Rect>(surface, "_editorBounds"),
                    "Scrolling another pane must not move the active editor into that pane.");
                PressWpf(editor, WpfInput.Key.Escape);
                Assert.IsFalse(session.Editor.IsEditing);
                Assert.AreSame(editor, Field<WpfControls.TextBox>(surface, "_editor"));
                split.SetActivePane(SpreadsheetPaneId.TopLeft);
                Invoke(surface, "BeginEdit", "=SUM(");
                var input = (NativeWpf.UIElement)surface;
                var start = ReferencePoint(surface, 1, owner.RenderTheme);
                var end = ReferencePoint(surface, 3, owner.RenderTheme);
                Assert.IsTrue((bool)Invoke(surface, "TryInsertFormulaReference", new NativeWpf.Point(start.X, start.Y))!);
                Assert.IsTrue((bool)Invoke(surface, "UpdateFormulaReferencePointer", new NativeWpf.Point(end.X, end.Y), false)!);
                Assert.AreEqual("=SUM(Orders[[#Data],[Amount]]", editor.Text);
                Assert.IsTrue(input.IsMouseCaptured);
                input.ReleaseMouseCapture();
                var released = editor.Text;
                Assert.IsFalse((bool)Invoke(surface, "UpdateFormulaReferencePointer", new NativeWpf.Point(start.X, start.Y), false)!);
                Assert.AreEqual(released, editor.Text);
                Assert.IsTrue((bool)Invoke(surface, "TryInsertFormulaReference", new NativeWpf.Point(start.X, start.Y))!);
                PressWpf(editor, WpfInput.Key.Escape);
                Assert.IsFalse(input.IsMouseCaptured);
            }
            finally { window.Close(); }
        });
    }

    [TestMethod]
    [Timeout(90_000)]
    public void LoadedWinFormsSplitEditorShouldRejectStaleCaretKeepNewlinesAndClipFullCell()
    {
        RunInSta(() =>
        {
            var session = CreateSession();
            using var owner = new NeraSpreadSheet.WinForms.NeraSpreadsheetControl { Session = session, Dock = Forms.DockStyle.Fill };
            using var form = new Forms.Form { Width = 760, Height = 540, ShowInTaskbar = false };
            form.Controls.Add(owner);
            form.Show();
            Forms.Application.DoEvents();
            using var split = owner.EnableSplitPanes(NeraSpreadSheet.WinForms.SpreadsheetSplitPaneMode.Both);
            split.SetSplit(240, 260);
            split.RenderNow();
            var surface = Field<object>(split, "_surface");
            var editor = Field<Forms.TextBox>(surface, "_editor");
            foreach (var draft in new[] { string.Empty, "literal", "=SUM(" })
            {
                Invoke(surface, "BeginEdit", draft);
                split.RenderNow();
                Forms.Application.DoEvents();
                Assert.AreEqual(0, owner.CurrentFormulaReferenceHighlights.Count,
                    "The hidden standalone editor must not analyze its empty draft during a split edit.");
                Assert.AreEqual(draft, editor.Text);
                PressForms(editor, Forms.Keys.Escape);
            }
            Assert.AreEqual(0, session.History.UndoCount);
            Invoke(surface, "BeginEdit", "=SUM(Sales[Am");
            var list = Field<Forms.ListBox>(surface, "_formulaSuggestionList");
            Assert.AreEqual(1, list.Items.Count);
            editor.Select(1, 0);
            PressForms(editor, Forms.Keys.Tab);
            Assert.AreEqual("=SUM(Sales[Am", editor.Text);
            Assert.IsTrue(session.Editor.IsEditing);
            Assert.AreEqual(0, session.History.UndoCount);
            PressForms(editor, Forms.Keys.Escape);
            Invoke(surface, "BeginEdit", "=SUM(Sales[Am");
            PressForms(editor, Forms.Keys.Tab);
            editor.AppendText(")");
            PressForms(editor, Forms.Keys.Enter);
            Assert.AreEqual(60d, session.ActiveWorksheet.GetValue(new CellAddress(6, 0)));
            session.Selection.SetActiveCell(new CellAddress(6, 0));
            Invoke(surface, "BeginEdit", "first");
            PressForms(editor, Forms.Keys.Alt | Forms.Keys.Enter);
            Assert.AreEqual("first" + Environment.NewLine, editor.Text);
            Assert.IsTrue(session.Editor.IsEditing);
            Assert.AreEqual(360, editor.Width);
            Assert.IsNotNull(editor.Region);
            using (var graphics = editor.CreateGraphics()) Assert.IsTrue(editor.Region.GetBounds(graphics).Width < editor.Width);
            var bounds = editor.Bounds;
            split.SetActivePane(SpreadsheetPaneId.TopRight);
            split.ScrollPaneTo(SpreadsheetPaneId.TopRight, 300d, 0d);
            split.RenderNow();
            Forms.Application.DoEvents();
            Assert.AreEqual(bounds, editor.Bounds, "Scrolling another pane must not move the active editor.");
            PressForms(editor, Forms.Keys.Escape);
            Assert.IsFalse(session.Editor.IsEditing);
            Assert.AreEqual(1, session.History.UndoCount);
            Assert.AreSame(editor, Field<Forms.TextBox>(surface, "_editor"));
            split.SetActivePane(SpreadsheetPaneId.TopLeft);
            Invoke(surface, "BeginEdit", "=SUM(");
            var input = (Forms.Control)surface;
            var start = ReferencePoint(surface, 1, owner.RenderTheme);
            var end = ReferencePoint(surface, 3, owner.RenderTheme);
            Assert.IsTrue((bool)Invoke(surface, "TryInsertFormulaReference", (int)start.X, (int)start.Y)!);
            Assert.IsTrue((bool)Invoke(surface, "UpdateFormulaReferencePointer", (int)end.X, (int)end.Y, false)!);
            Assert.AreEqual("=SUM(Sales[[#Data],[Amount]]", editor.Text);
            Assert.IsTrue(input.Capture);
            input.Capture = false;
            var released = editor.Text;
            Assert.IsFalse((bool)Invoke(surface, "UpdateFormulaReferencePointer", (int)start.X, (int)start.Y, false)!);
            Assert.AreEqual(released, editor.Text);
            editor.Select(1, 0);
            Assert.IsTrue((bool)Invoke(surface, "TryInsertFormulaReference", (int)start.X, (int)start.Y)!);
            Assert.IsTrue(editor.Text.EndsWith(released[1..], StringComparison.Ordinal),
                "Moved native caret must invalidate the old provisional span.");
            PressForms(editor, Forms.Keys.Escape);
            Assert.IsFalse(input.Capture);
        });
    }

    private static SpreadsheetSession CreateSession()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.AddTable(new SpreadsheetTable(Guid.NewGuid(), "Sales", new CellRange(default, new CellAddress(3, 1)),
            [new SpreadsheetTableColumn(Guid.NewGuid(), "Item"), new SpreadsheetTableColumn(Guid.NewGuid(), "Amount")]));
        for (var row = 1; row <= 3; row++) sheet.SetValue(new CellAddress(row, 1), row * 10d);
        sheet.Dimensions.SetColumnWidth(0, 360d);
        sheet.Dimensions.SetRowHeight(6, 80d);
        var session = new SpreadsheetSession(workbook);
        session.Selection.SetActiveCell(new CellAddress(6, 0));
        return session;
    }

    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
    private static object? Invoke(object target, string name, params object?[] args) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);
    private static NeraSpreadSheet.Foundation.PointD ReferencePoint(object surface, int row, SpreadsheetRenderTheme theme)
    {
        var engine = Field<SpreadsheetSplitViewportEngine>(surface, "_engine");
        Assert.IsTrue(engine.TryGetCellBounds(SpreadsheetPaneId.TopRight, new CellAddress(row, 1), out var bounds));
        var width = surface is NativeWpf.FrameworkElement wpf ? wpf.ActualWidth : ((Forms.Control)surface).ClientSize.Width;
        var height = surface is NativeWpf.FrameworkElement wpfHost ? wpfHost.ActualHeight : ((Forms.Control)surface).ClientSize.Height;
        var chrome = SpreadsheetChromeGeometry.Calculate(width, height, theme);
        return new(bounds.X + bounds.Width / 2d + chrome.RowHeaderWidth,
            bounds.Y + bounds.Height / 2d + chrome.ColumnHeaderHeight);
    }
    private static void PressWpf(WpfControls.TextBox editor, WpfInput.Key key)
    {
        var source = NativeWpf.PresentationSource.FromVisual(editor)!;
        var args = new WpfInput.KeyEventArgs(WpfInput.Keyboard.PrimaryDevice, source, Environment.TickCount, key)
            { RoutedEvent = WpfInput.Keyboard.PreviewKeyDownEvent };
        editor.RaiseEvent(args);
        Assert.IsTrue(args.Handled);
    }
    private static void PressForms(Forms.TextBox editor, Forms.Keys key)
    {
        var args = new Forms.KeyEventArgs(key);
        typeof(Forms.Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(editor, [args]);
        Assert.IsTrue(args.Handled && args.SuppressKeyPress);
    }
    private static void RunInSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = exception; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(65)));
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
