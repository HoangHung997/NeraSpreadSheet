using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Headless;
using global::Avalonia.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Avalonia.Tests;

[TestClass]
public sealed class FormulaCrossPaneDragTests
{
    [TestMethod]
    public Task DragStartingInEditorPaneShouldUseDestinationPaneCoordinates() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        fixture.Right.ScrollTo(720.25, 400.75);
        RunDrag(fixture, fixture.Left, new Point(180, 65), fixture.Right, new Point(240, 130));
    });

    [TestMethod]
    public Task DragStartingInAnotherPaneShouldReturnThroughEditorPane() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        fixture.Right.ScrollTo(720.25, 400.75);
        RunDrag(fixture, fixture.Right, new Point(240, 130), fixture.Left, new Point(180, 65));
    });

    [TestMethod]
    public Task SamePaneReferenceDragShouldNotChangeSelectionOrWorkbook() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        RunDrag(fixture, fixture.Left, new Point(180, 65), fixture.Left, new Point(300, 160));
    });

    [TestMethod]
    public Task NativeEditorMouseSelectionShouldNotBecomeReferencePointing() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        fixture.Left.BeginEdit("=A1+B1");
        fixture.Window.UpdateLayout();
        var native = (TextBox)fixture.Left.Children[0];
        var start = native.TranslatePoint(new Point(4, 8), fixture.Window);
        var end = native.TranslatePoint(new Point(Math.Min(40, native.Bounds.Width - 2), 8), fixture.Window);
        Assert.IsNotNull(start);
        Assert.IsNotNull(end);
        fixture.Window.MouseDown(start.Value, MouseButton.Left);
        fixture.Window.MouseMove(end.Value, RawInputModifiers.LeftMouseButton);
        fixture.Window.MouseUp(end.Value, MouseButton.Left);
        Assert.IsTrue(fixture.Left.IsEditing);
        Assert.IsFalse(fixture.Left.IsFormulaPointMode);
        Assert.AreEqual("=A1+B1", fixture.Left.EditorText);
        Assert.IsTrue(fixture.Session.ActiveWorksheet.GetCell(default).IsEmpty);
    });

    [TestMethod]
    public Task CancelledDraftShouldNotReceiveFurtherCapturedDragUpdates() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        fixture.Left.BeginEdit("=");
        var start = fixture.Left.TranslatePoint(new Point(180, 65), fixture.Window);
        var end = fixture.Right.TranslatePoint(new Point(260, 120), fixture.Window);
        Assert.IsNotNull(start);
        Assert.IsNotNull(end);
        var version = fixture.Session.Workbook.Version;
        fixture.Window.MouseDown(start.Value, MouseButton.Left);
        Assert.IsTrue(fixture.Left.IsFormulaPointMode);
        Assert.IsTrue(fixture.Left.CancelEditor());
        fixture.Window.MouseMove(end.Value, RawInputModifiers.LeftMouseButton);
        fixture.Window.MouseUp(end.Value, MouseButton.Left);
        Assert.IsFalse(fixture.Left.IsFormulaPointMode);
        Assert.IsFalse(fixture.Session.Editor.IsEditing);
        Assert.AreEqual(version, fixture.Session.Workbook.Version);
        Assert.IsTrue(fixture.Session.ActiveWorksheet.GetCell(default).IsEmpty);
    });

    private static void RunDrag(Fixture fixture, NeraSpreadsheetControl from, Point fromPoint,
        NeraSpreadsheetControl to, Point toPoint)
    {
        var version = fixture.Session.Workbook.Version;
        var selection = fixture.Session.Selection.Capture().Version;
        fixture.Left.BeginEdit("=");
        Assert.IsTrue(from.TryHitFormulaCell(fromPoint, out var first));
        Assert.IsTrue(to.TryHitFormulaCell(toPoint, out var last));
        Assert.AreNotEqual(first, last);
        var start = from.TranslatePoint(fromPoint, fixture.Window);
        var finish = to.TranslatePoint(toPoint, fixture.Window);
        Assert.IsNotNull(start);
        Assert.IsNotNull(finish);
        fixture.Window.MouseDown(start.Value, MouseButton.Left);
        Assert.IsTrue(fixture.Left.IsFormulaPointMode);
        fixture.Window.MouseMove(finish.Value, RawInputModifiers.LeftMouseButton);
        fixture.Window.MouseUp(finish.Value, MouseButton.Left);
        var range = new CellRange(first, last);
        Assert.AreEqual($"={range.TopLeft.ToA1()}:{range.BottomRight.ToA1()}", fixture.Left.EditorText);
        Assert.AreSame(fixture.Left, fixture.Control.EditingSpreadsheet);
        Assert.IsFalse(fixture.Left.IsFormulaPointMode);
        Assert.AreEqual(SpreadsheetSplitViewPane.TopLeft, fixture.Control.State.ActivePane);
        Assert.AreEqual(selection, fixture.Session.Selection.Capture().Version);
        Assert.AreEqual(version, fixture.Session.Workbook.Version);
        Assert.IsTrue(fixture.Session.ActiveWorksheet.GetCell(default).IsEmpty);
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Session = new SpreadsheetSession(new Workbook());
            Control = new NeraSpreadsheetSplitControl { Session = Session };
            Window = new Window { Width = 1000, Height = 550, Content = Control };
            Window.Show();
            Window.UpdateLayout();
            Control.SetMode(SpreadsheetSplitViewMode.Vertical);
            Window.UpdateLayout();
        }
        public SpreadsheetSession Session { get; }
        public NeraSpreadsheetSplitControl Control { get; }
        public Window Window { get; }
        public NeraSpreadsheetControl Left => Control.GetPane(SpreadsheetSplitViewPane.TopLeft);
        public NeraSpreadsheetControl Right => Control.GetPane(SpreadsheetSplitViewPane.TopRight);
        public void Dispose() { Window.Content = null; Window.Close(); Control.Dispose(); }
    }
}
