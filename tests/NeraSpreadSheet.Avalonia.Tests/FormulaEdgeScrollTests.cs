using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Headless;
using global::Avalonia.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Avalonia.Tests;

[TestClass]
public sealed class FormulaEdgeScrollTests
{
    [TestMethod]
    public Task RawPointerMovesShouldBeCoalescedUntilFrameAdvance() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        fixture.Begin();
        var original = fixture.Sheet.EditorText;
        for (var index = 0; index < 100; index++) fixture.Sheet.TrackFormulaPointer(fixture.Sheet, new Point(fixture.Sheet.Bounds.Width - 7.25, 150));
        Assert.AreEqual(0d, fixture.Sheet.ScrollSnapshot.OffsetX);
        Assert.AreEqual(original, fixture.Sheet.EditorText);
        Assert.IsTrue(fixture.Sheet.AdvanceFormulaPointerFrame(TimeSpan.FromSeconds(1d / 60d)));
        Assert.IsTrue(fixture.Sheet.ScrollSnapshot.OffsetX > 0);
        Assert.AreNotEqual(original, fixture.Sheet.EditorText);
        Assert.IsTrue(fixture.Session.ActiveWorksheet.GetCell(default).IsEmpty);
    });
    [TestMethod]
    public Task EdgeScrollShouldKeepFractionalOffsetsAndBoundStalledFrames() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        fixture.Begin();
        fixture.Sheet.TrackFormulaPointer(fixture.Sheet, new Point(fixture.Sheet.Bounds.Width - 7.25, 150));
        fixture.Sheet.AdvanceFormulaPointerFrame(TimeSpan.FromSeconds(1d / 60d));
        var first = fixture.Sheet.ScrollSnapshot.OffsetX;
        Assert.IsTrue(first > 0 && Math.Abs(first - Math.Round(first)) > 0.01);
        fixture.Sheet.AdvanceFormulaPointerFrame(TimeSpan.FromSeconds(5));
        Assert.IsTrue(fixture.Sheet.ScrollSnapshot.OffsetX - first <= 48.001);
    });
    [TestMethod]
    public Task StationaryPointerAtEdgeShouldContinueExtendingRange() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        fixture.Begin();
        fixture.Sheet.TrackFormulaPointer(fixture.Sheet, new Point(fixture.Sheet.Bounds.Width + 30, 150));
        fixture.Sheet.AdvanceFormulaPointerFrame(TimeSpan.FromMilliseconds(50));
        var before = fixture.Sheet.EditorText;
        for (var frame = 0; frame < 10; frame++) fixture.Sheet.AdvanceFormulaPointerFrame(TimeSpan.FromMilliseconds(50));
        Assert.AreNotEqual(before, fixture.Sheet.EditorText);
        Assert.IsTrue(fixture.Sheet.ScrollSnapshot.OffsetX > 400);
        Assert.IsFalse(fixture.Session.Undo());
    });
    [TestMethod]
    public Task PointerInBodyCenterShouldNotAutoscroll() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture(); fixture.Begin();
        fixture.Sheet.TrackFormulaPointer(fixture.Sheet, new Point(300, 200));
        fixture.Sheet.AdvanceFormulaPointerFrame(TimeSpan.FromMilliseconds(50));
        Assert.AreEqual(0d, fixture.Sheet.ScrollSnapshot.OffsetX);
        Assert.AreEqual(0d, fixture.Sheet.ScrollSnapshot.OffsetY);
    });
    [TestMethod]
    public Task CancelShouldStopSubsequentEdgeFrames() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture(); fixture.Begin();
        fixture.Sheet.TrackFormulaPointer(fixture.Sheet, new Point(fixture.Sheet.Bounds.Width + 30, 150));
        fixture.Sheet.AdvanceFormulaPointerFrame(TimeSpan.FromMilliseconds(20));
        fixture.Sheet.CancelEditor();
        var snapshot = fixture.Sheet.ScrollSnapshot;
        Assert.IsFalse(fixture.Sheet.AdvanceFormulaPointerFrame(TimeSpan.FromSeconds(1)));
        Assert.AreEqual(snapshot.OffsetX, fixture.Sheet.ScrollSnapshot.OffsetX);
        Assert.IsFalse(fixture.Session.Editor.IsEditing);
    });
    [TestMethod]
    public Task FrozenColumnBandShouldNotMoveScrollableColumns() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        fixture.Session.View.SetFrozenPanes(1, 1);
        fixture.Begin(); fixture.Sheet.ScrollTo(200.25, 0);
        fixture.Sheet.TrackFormulaPointer(fixture.Sheet, new Point(fixture.Sheet.RenderTheme.RowHeaderWidth + 10, 200));
        fixture.Sheet.AdvanceFormulaPointerFrame(TimeSpan.FromMilliseconds(50));
        Assert.AreEqual(200.25, fixture.Sheet.ScrollSnapshot.OffsetX);
    });
    [TestMethod]
    public Task SplitDragShouldScrollOnlyItsHoveredPane() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var session = new SpreadsheetSession(new Workbook());
        using var split = new NeraSpreadsheetSplitControl { Session = session };
        var window = new Window { Width = 1000, Height = 550, Content = split };
        window.Show(); window.UpdateLayout(); split.SetMode(SpreadsheetSplitViewMode.Vertical); window.UpdateLayout();
        try
        {
            var editor = split.GetPane(SpreadsheetSplitViewPane.TopLeft);
            var target = split.GetPane(SpreadsheetSplitViewPane.TopRight);
            editor.BeginEdit("="); editor.BeginPointReference(new CellAddress(1, 1));
            editor.TrackFormulaPointer(target, new Point(target.Bounds.Width + 20, 150));
            editor.AdvanceFormulaPointerFrame(TimeSpan.FromMilliseconds(25));
            Assert.AreEqual(0d, editor.ScrollSnapshot.OffsetX);
            Assert.IsTrue(target.ScrollSnapshot.OffsetX > 0);
            Assert.AreEqual(SpreadsheetSplitViewPane.TopLeft, split.State.ActivePane);
            Assert.AreSame(editor, split.EditingSpreadsheet);
            Assert.IsTrue(session.ActiveWorksheet.GetCell(default).IsEmpty);
        }
        finally { window.Content = null; window.Close(); }
    });
    [TestMethod]
    public Task RoutedStandaloneDragShouldAutoscrollAndStopOnRelease() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        fixture.Sheet.BeginEdit("=");
        var start = fixture.Sheet.TranslatePoint(new Point(180, 65), fixture.Window);
        var outside = fixture.Sheet.TranslatePoint(new Point(fixture.Sheet.Bounds.Width + 20, 150), fixture.Window);
        Assert.IsNotNull(start); Assert.IsNotNull(outside);
        fixture.Window.MouseDown(start.Value, MouseButton.Left);
        fixture.Window.MouseMove(outside.Value, RawInputModifiers.LeftMouseButton);
        Assert.IsTrue(fixture.Sheet.IsFormulaPointMode);
        fixture.Sheet.AdvanceFormulaPointerFrame(TimeSpan.FromMilliseconds(25));
        Assert.IsTrue(fixture.Sheet.ScrollSnapshot.OffsetX > 0);
        fixture.Window.MouseUp(outside.Value, MouseButton.Left);
        Assert.IsFalse(fixture.Sheet.IsFormulaPointMode);
        var offset = fixture.Sheet.ScrollSnapshot.OffsetX;
        Assert.IsFalse(fixture.Sheet.AdvanceFormulaPointerFrame(TimeSpan.FromMilliseconds(25)));
        Assert.AreEqual(offset, fixture.Sheet.ScrollSnapshot.OffsetX);
    });
    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Session = new SpreadsheetSession(new Workbook());
            Sheet = new NeraSpreadsheetControl { Session = Session };
            Window = new Window { Width = 800, Height = 500, Content = Sheet };
            Window.Show(); Window.UpdateLayout();
        }
        public SpreadsheetSession Session { get; }
        public NeraSpreadsheetControl Sheet { get; }
        public Window Window { get; }
        public void Begin() { Sheet.BeginEdit("="); Sheet.BeginPointReference(new CellAddress(1, 1)); }
        public void Dispose() { Window.Content = null; Window.Close(); Sheet.Dispose(); }
    }
}
