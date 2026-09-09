using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Headless;
using global::Avalonia.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Avalonia.Tests;

[TestClass]
public sealed class FormulaReferencePickerTests
{
    [TestMethod]
    public Task BrowsingAnotherWorksheetShouldKeepOriginalCanonicalDraft() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        var state = fixture.Session.Editor.State;
        var selection = fixture.Session.Selection.Capture().Version;
        fixture.Picker.SelectWorksheet(fixture.Other);
        Assert.AreSame(fixture.First, fixture.Session.ActiveWorksheet);
        Assert.AreSame(state, fixture.Session.Editor.State);
        Assert.AreEqual("=SUM(", fixture.Owner.EditorText);
        Assert.AreEqual(selection, fixture.Session.Selection.Capture().Version);
        Assert.AreSame(fixture.Other, fixture.Picker.SelectedWorksheet);
    });
    [TestMethod]
    public Task ApplyShouldInsertEscapedSheetRangeWithoutCommittingCell() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        var version = fixture.Session.Workbook.Version;
        fixture.Picker.SelectWorksheet(fixture.Other);
        fixture.Picker.SelectRange(new CellRange(new CellAddress(1, 2), new CellAddress(3, 2)));
        Assert.IsTrue(fixture.Picker.TryApply());
        Assert.AreEqual("=SUM('O''Brien'!C2:C4", fixture.Owner.EditorText);
        Assert.AreEqual(version, fixture.Session.Workbook.Version);
        Assert.AreSame(fixture.First, fixture.Session.ActiveWorksheet);
        Assert.IsTrue(fixture.First.GetCell(default).IsEmpty);
        Assert.IsFalse(fixture.Session.Undo());
        Assert.IsFalse(fixture.Picker.TryApply());
    });
    [TestMethod]
    public Task CancelShouldKeepDraftTextAndDirectedSelection() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        var draft = fixture.Owner.CurrentEditorDraft;
        fixture.Picker.SelectWorksheet(fixture.Other);
        fixture.Picker.SelectRange(new CellRange(new CellAddress(1, 1), new CellAddress(5, 5)));
        fixture.Picker.Cancel();
        Assert.AreEqual(draft, fixture.Owner.CurrentEditorDraft);
        Assert.IsFalse(fixture.Picker.TryApply());
    });
    [TestMethod]
    public Task ChangedDraftShouldRejectStalePickerApply() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        fixture.Owner.UpdateEditorDraft("=MAX(", 5, 5);
        Assert.IsFalse(fixture.Picker.TryApply());
        Assert.AreEqual("=MAX(", fixture.Owner.EditorText);
    });
    [TestMethod]
    public Task ChangedWorkbookShouldRejectStaleSnapshotReference() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        fixture.Other.SetValue(new CellAddress(2, 2), 9d);
        Assert.IsFalse(fixture.Picker.TryApply());
        Assert.AreEqual("=SUM(", fixture.Owner.EditorText);
    });
    [TestMethod]
    public Task CancelledCanonicalEditorShouldNotBeResurrectedByPicker() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        fixture.Owner.CancelEditor();
        Assert.IsFalse(fixture.Picker.TryApply());
        Assert.IsFalse(fixture.Session.Editor.IsEditing);
        Assert.IsTrue(fixture.First.GetCell(default).IsEmpty);
    });
    [TestMethod]
    public Task EscapeInPickerShouldCancelOnlyPickerNotOriginalDraft() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        var cancelled = false;
        fixture.Picker.Cancelled += (_, _) => cancelled = true;
        fixture.Picker.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        Assert.IsTrue(cancelled);
        Assert.IsTrue(fixture.Owner.IsEditing);
        Assert.AreEqual("=SUM(", fixture.Owner.EditorText);
    });
    [TestMethod]
    public Task RoutedRangeDragOnAnotherSheetShouldOnlyUpdatePickerSelection() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        fixture.Picker.SelectWorksheet(fixture.Other); fixture.Window.UpdateLayout();
        var surface = fixture.Picker.PreviewSurface;
        var first = surface.TranslatePoint(new Point(150, 70), fixture.Window);
        var last = surface.TranslatePoint(new Point(340, 190), fixture.Window);
        Assert.IsNotNull(first); Assert.IsNotNull(last);
        fixture.Window.MouseDown(first.Value, MouseButton.Left);
        fixture.Window.MouseMove(last.Value, RawInputModifiers.LeftMouseButton);
        fixture.Window.MouseUp(last.Value, MouseButton.Left);
        Assert.IsTrue(fixture.Picker.SelectedRange.RowCount > 1);
        Assert.IsTrue(fixture.Picker.SelectedRange.ColumnCount > 1);
        Assert.AreEqual("=SUM(", fixture.Owner.EditorText);
        Assert.AreSame(fixture.First, fixture.Session.ActiveWorksheet);
        Assert.IsTrue(fixture.Picker.TryApply());
        Assert.IsTrue(fixture.Owner.EditorText.Contains("'O''Brien'!", StringComparison.Ordinal));
    });
    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            var workbook = new Workbook(); First = workbook.Worksheets[0]; Other = workbook.AddWorksheet("O'Brien");
            Session = new SpreadsheetSession(workbook); Owner = new NeraSpreadsheetControl { Session = Session };
            OwnerWindow = new Window { Width = 800, Height = 500, Content = Owner }; OwnerWindow.Show(); OwnerWindow.UpdateLayout();
            Owner.BeginEdit("=SUM("); Picker = new NeraFormulaReferencePicker(Owner);
            Window = new Window { Width = 900, Height = 650, Content = Picker }; Window.Show(); Window.UpdateLayout();
        }
        public Worksheet First { get; }
        public Worksheet Other { get; }
        public SpreadsheetSession Session { get; }
        public NeraSpreadsheetControl Owner { get; }
        public Window OwnerWindow { get; }
        public NeraFormulaReferencePicker Picker { get; }
        public Window Window { get; }
        public void Dispose() { Window.Content = null; Window.Close(); Picker.Dispose(); OwnerWindow.Content = null; OwnerWindow.Close(); Owner.Dispose(); }
    }
}
