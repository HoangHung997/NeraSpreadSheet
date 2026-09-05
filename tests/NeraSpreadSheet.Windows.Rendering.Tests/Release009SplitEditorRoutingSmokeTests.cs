using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Viewport;
using NeraSpreadSheet.Wpf;
using ListBox = System.Windows.Controls.ListBox;
using TextBox = System.Windows.Controls.TextBox;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class Release009SplitEditorRoutingSmokeTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(90_000)]
    public void OwnerMetadataShouldDescribeTheActualNativeDraftAndNestedArgumentHelp(bool useSplit)
    {
        RunLoaded(useSplit, host =>
        {
            var owner = host.Owner;
            var session = host.Session;
            owner.BeginEdit("=SU");
            var list = Field<ListBox>(host.Surface, "_formulaSuggestionList");
            CollectionAssert.AreEqual(owner.CurrentFormulaSuggestions.ToArray(), list.Items.Cast<FormulaFunctionSuggestion>().ToArray());
            Assert.IsTrue(owner.CurrentFormulaSuggestions.Any(candidate => candidate.Name == "SUM"));
            const string nested = "=SUM(1,IF(2,3,4))";
            var caret = nested.IndexOf('4');
            Assert.IsTrue(owner.UpdateEditorDraft(nested, caret, 0));
            Assert.AreEqual("IF", owner.CurrentFormulaHelp!.Function.Name);
            Assert.AreEqual(2, owner.CurrentFormulaHelp.ActiveArgumentIndex);
            var helpText = Field<TextBlock>(host.Surface, "_formulaHelpText");
            StringAssert.Contains(helpText.Text, owner.CurrentFormulaHelp.ActiveArgument!.Name);
            StringAssert.Contains(helpText.Text, "Đối số 3:");
            Assert.AreEqual(Visibility.Collapsed, list.Visibility);
            var popup = Field<Popup>(host.Surface, "_formulaSuggestionPopup");
            Assert.IsTrue(popup.IsOpen, "Argument help must remain visible when there are no completion candidates.");
            host.Window.UpdateLayout();
            if (useSplit)
            {
                Capture(host.Window, "release009-split-formula-help-host");
                Capture((FrameworkElement)popup.Child!, "release009-split-formula-help-popup");
            }
            Assert.IsTrue(owner.UpdateEditorDraft(nested, nested.IndexOf("IF", StringComparison.Ordinal), 0));
            Assert.AreEqual("SUM", owner.CurrentFormulaHelp!.Function.Name);
            Assert.AreEqual(1, owner.CurrentFormulaHelp.ActiveArgumentIndex);

            const string structured = "=SUM(Sales[Am";
            Assert.IsTrue(owner.UpdateEditorDraft(structured, structured.Length, 0));
            var candidate = owner.CurrentStructuredReferenceSuggestions.Single();
            Assert.AreSame(candidate, list.Items[0]);
            Assert.AreEqual("Amount", candidate.DisplayText);
            var canonical = session.Editor.State;
            session.Tables.RenameTable(session.Workbook.Tables.Single().Id, "Orders");
            Press(host.Editor, Key.Tab);
            Assert.AreEqual("=SUM(Orders[[#Data],[Amount]]", owner.CurrentEditorDraft!.Text);
            Assert.AreSame(canonical, session.Editor.State);
            Assert.AreEqual(1, session.History.UndoCount, "Only the independent rename has entered workbook history.");
            Assert.IsTrue(owner.CancelEditor());
            AssertMetadataCleared(owner);
            Assert.IsFalse(popup.IsOpen);
            Assert.AreEqual(string.Empty, helpText.Text);

            owner.BeginEdit("=SUM(B2:B4)");
            var expected = new CellRange(new CellAddress(1, 1), new CellAddress(3, 1));
            Assert.AreEqual(expected, owner.CurrentFormulaReferenceHighlights.Single().Range);
            owner.ShowFormulaReferenceHighlights = false;
            Assert.AreEqual(0, owner.CurrentFormulaReferenceHighlights.Count);
            owner.ShowFormulaReferenceHighlights = true;
            Assert.AreEqual(expected, owner.CurrentFormulaReferenceHighlights.Single().Range);
            var second = session.Workbook.AddWorksheet("Other");
            session.ActivateWorksheet(second);
            AssertMetadataCleared(owner);
            Assert.AreEqual(0, owner.CurrentFormulaReferenceHighlights.Count);
            Assert.IsFalse(popup.IsOpen);
            Assert.IsNull(owner.CurrentEditorDraft);
        });
    }

    [TestMethod]
    [Timeout(90_000)]
    public void PublicVisibilityShouldPreserveOtherPanesFractionsFrozenAxesAndHistory()
    {
        RunLoaded(true, host =>
        {
            var split = host.Split!;
            var session = host.Session;
            session.View.SetFrozenPanes(1, 1);
            split.RenderNow();
            var offsets = SnapshotOffsets(split);
            var history = session.View.SplitViewUndoCount;
            var selection = session.Selection.Capture();
            var target = new CellAddress(30, 12);
            Assert.IsTrue(host.Owner.ScrollCellIntoView(target));
            AssertVisible(host, target);
            AssertOtherOffsets(split, offsets);
            var visible = split.GetPaneScroll(split.ActivePane);
            Assert.IsTrue(visible.X != Math.Truncate(visible.X) || visible.Y != Math.Truncate(visible.Y));
            Assert.IsFalse(host.Owner.ScrollCellIntoView(target), "Repeated reveal must not drift or oscillate.");
            Assert.AreEqual(visible, split.GetPaneScroll(split.ActivePane));
            Assert.AreEqual(selection.Version, session.Selection.Version);
            Assert.AreEqual(selection.ActiveCell, session.Selection.ActiveCell);
            CollectionAssert.AreEqual(selection.Ranges.ToArray(), session.Selection.Capture().Ranges.ToArray());
            Assert.AreEqual(history, session.View.SplitViewUndoCount);
            Assert.AreEqual(0, session.History.UndoCount);
            Assert.IsFalse(host.Owner.ScrollCellIntoView(default), "Both frozen axes stay pinned.");
            Assert.AreEqual(visible, split.GetPaneScroll(split.ActivePane));
            Assert.IsTrue(host.Owner.ScrollCellIntoView(new CellAddress(50, 0)));
            Assert.AreEqual(visible.X, split.GetPaneScroll(split.ActivePane).X);
            var frozenColumn = split.GetPaneScroll(split.ActivePane);
            Assert.IsTrue(host.Owner.ScrollCellIntoView(new CellAddress(0, 25)));
            Assert.AreEqual(frozenColumn.Y, split.GetPaneScroll(split.ActivePane).Y);
            AssertOtherOffsets(split, offsets);

            var merge = new CellRange(new CellAddress(55, 20), new CellAddress(56, 21));
            session.ActiveWorksheet.MergeCells(merge);
            Assert.IsTrue(host.Owner.ScrollCellIntoView(merge.BottomRight));
            AssertVisible(host, merge.TopLeft);
            Assert.IsFalse(split.ScrollCellIntoView(merge.BottomRight));
            session.ActiveWorksheet.Dimensions.SetColumnWidth(22, 1500.75d);
            Assert.IsTrue(host.Owner.ScrollCellIntoView(new CellAddress(55, 22)));
            var oversized = split.GetPaneScroll(split.ActivePane);
            Assert.IsFalse(host.Owner.ScrollCellIntoView(new CellAddress(55, 22)));
            Assert.AreEqual(oversized, split.GetPaneScroll(split.ActivePane));
            Assert.AreEqual(history, session.View.SplitViewUndoCount);
            Assert.AreEqual(0, session.History.UndoCount);
        });
    }

    [TestMethod]
    [DataRow(Key.Enter)]
    [DataRow(Key.Tab)]
    [Timeout(90_000)]
    public void NativeCommitShouldRevealTheNextVisibleCellInOnlyTheActivePane(Key key)
    {
        RunLoaded(true, host =>
        {
            var session = host.Session;
            var split = host.Split!;
            session.View.SetFrozenPanes(1, 1);
            var target = new CellAddress(30, 12);
            if (key == Key.Enter) session.ActiveWorksheet.Dimensions.HideRows(31, 3);
            else session.ActiveWorksheet.Dimensions.HideColumns(13, 3);
            session.Selection.SetActiveCell(target);
            Assert.IsTrue(host.Owner.ScrollCellIntoView(target));
            AssertVisible(host, target);
            var before = SnapshotOffsets(split);
            var history = session.View.SplitViewUndoCount;
            host.Owner.BeginEdit("42");
            Press(host.Editor, key);
            var next = key == Key.Enter ? new CellAddress(34, 12) : new CellAddress(30, 16);
            Assert.AreEqual(next, session.Selection.ActiveCell);
            AssertVisible(host, next);
            AssertOtherOffsets(split, before);
            var after = split.GetPaneScroll(split.ActivePane);
            if (key == Key.Enter)
            {
                Assert.IsTrue(after.Y > before[split.ActivePane].Y);
                Assert.AreEqual(before[split.ActivePane].X, after.X);
            }
            else
            {
                Assert.IsTrue(after.X > before[split.ActivePane].X);
                Assert.AreEqual(before[split.ActivePane].Y, after.Y);
            }
            Assert.AreEqual(42d, session.ActiveWorksheet.GetValue(target));
            Assert.IsNull(host.Owner.CurrentEditorDraft);
            Assert.AreEqual(1, session.History.UndoCount);
            Assert.AreEqual(history, session.View.SplitViewUndoCount);
            Assert.IsTrue(session.Undo());
            Assert.IsNull(session.ActiveWorksheet.GetValue(target));
            Assert.IsTrue(session.Redo());
            Assert.AreEqual(42d, session.ActiveWorksheet.GetValue(target));
            if (key == Key.Enter) Capture(host.Window, "release009-split-enter-pane-edge");
        });
    }

    private static void AssertMetadataCleared(NeraSpreadsheetControl owner)
    {
        Assert.AreEqual(0, owner.CurrentFormulaSuggestions.Count);
        Assert.AreEqual(0, owner.CurrentStructuredReferenceSuggestions.Count);
        Assert.IsNull(owner.CurrentFormulaHelp);
    }

    private static Dictionary<SpreadsheetPaneId, PointD> SnapshotOffsets(NeraSpreadsheetSplitController split) =>
        Enum.GetValues<SpreadsheetPaneId>().ToDictionary(pane => pane, split.GetPaneScroll);

    private static void AssertOtherOffsets(NeraSpreadsheetSplitController split, Dictionary<SpreadsheetPaneId, PointD> before)
    {
        foreach (var pair in before.Where(pair => pair.Key != split.ActivePane))
            Assert.AreEqual(pair.Value, split.GetPaneScroll(pair.Key));
    }

    private static void AssertVisible(Host host, CellAddress address)
    {
        host.Split!.RenderNow();
        host.Window.UpdateLayout();
        var engine = Field<SpreadsheetSplitViewportEngine>(host.Surface, "_engine");
        Assert.IsTrue(engine.TryGetCellBounds(host.Split.ActivePane, address, out var bounds));
        Assert.IsTrue(host.Split.LastFrame!.TryGetPane(host.Split.ActivePane, out var pane));
        Assert.IsTrue(bounds.Left >= pane.Pane.Bounds.Left + pane.ViewportFrame.Layout.FrozenWidth - 1e-7);
        Assert.IsTrue(bounds.Top >= pane.Pane.Bounds.Top + pane.ViewportFrame.Layout.FrozenHeight - 1e-7);
        Assert.IsTrue(bounds.Right <= pane.Pane.Bounds.Right + 1e-7);
        Assert.IsTrue(bounds.Bottom <= pane.Pane.Bounds.Bottom + 1e-7);
    }

    private static void Press(TextBox editor, Key key)
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(editor)!, Environment.TickCount, key)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        editor.RaiseEvent(args);
        Assert.IsTrue(args.Handled);
    }

    private static void Capture(FrameworkElement element, string name)
    {
        element.UpdateLayout();
        Assert.IsTrue(element.ActualWidth > 0 && element.ActualHeight > 0);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth),
            (int)Math.Ceiling(element.ActualHeight), 96d, 96d, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "NeraSpreadSheet.slnx")))
            directory = directory.Parent;
        Assert.IsNotNull(directory, "Native capture must be rooted in this source checkout.");
        var output = Path.Combine(directory.FullName, "artifacts", "ribbon-visual-011", "captures", "split-editor-routing");
        Directory.CreateDirectory(output);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(output, name + ".png"));
        encoder.Save(stream);
    }

    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    private sealed record Host(NeraSpreadsheetControl Owner, NeraSpreadsheetSplitController? Split,
        object Surface, TextBox Editor, Window Window, SpreadsheetSession Session);

    private static void RunLoaded(bool useSplit, Action<Host> action)
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
                sheet.Dimensions.SetColumnWidth(0, 80.25d);
                sheet.Dimensions.SetRowHeight(0, 24.5d);
                var session = new SpreadsheetSession(workbook);
                session.Selection.SetActiveCell(new CellAddress(6, 2));
                using var owner = new NeraSpreadsheetControl { Session = session };
                var window = new Window { Width = 800, Height = 620, ShowInTaskbar = false,
                    Content = new AdornerDecorator { Child = owner } };
                NeraSpreadsheetSplitController? split = null;
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    if (useSplit)
                    {
                        split = owner.EnableSplitPanes(SpreadsheetSplitPaneMode.Both);
                        split.SetSplit(260.5d, 260.25d);
                        split.RenderNow();
                        split.ScrollPaneTo(SpreadsheetPaneId.TopLeft, 13.25d, 17.5d);
                        split.ScrollPaneTo(SpreadsheetPaneId.TopRight, 80.375d, 62.625d);
                        split.ScrollPaneTo(SpreadsheetPaneId.BottomLeft, 37.75d, 43.125d);
                        split.SetActivePane(SpreadsheetPaneId.BottomRight);
                        split.RenderNow();
                        owner.Focusable = false;
                        owner.IsHitTestVisible = false;
                    }
                    object surface = split is null ? owner : Field<object>(split, "_adorner");
                    action(new Host(owner, split, surface, Field<TextBox>(surface, "_editor"), window, session));
                }
                finally { split?.Dispose(); window.Close(); }
            }
            catch (Exception exception) { failure = exception; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(65)), "Loaded split editor routing smoke timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
