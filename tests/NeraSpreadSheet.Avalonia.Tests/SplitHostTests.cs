using global::Avalonia.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Avalonia.Tests;

[TestClass]
public sealed class SplitHostTests
{
    [TestMethod]
    public Task AllSplitModesShouldProjectSharedTopology() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        foreach (var mode in Enum.GetValues<SpreadsheetSplitViewMode>())
        {
            fixture.Control.SetMode(mode); fixture.Window.UpdateLayout();
            Assert.AreEqual(mode, fixture.Session.View.SplitState.Mode);
            Assert.AreEqual(mode == SpreadsheetSplitViewMode.None ? 1 : mode == SpreadsheetSplitViewMode.Both ? 4 : 2, fixture.Control.Children.Count(child => child.IsVisible));
        }
    });
    [TestMethod]
    public Task PanesShouldUseOneCallerOwnedSession() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        foreach (var pane in Enum.GetValues<SpreadsheetSplitViewPane>()) Assert.AreSame(fixture.Session, fixture.Control.GetPane(pane).Session);
    });
    [TestMethod]
    public Task PaneScrollShouldRemainIndependentAndFractional() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture(); fixture.Control.SetMode(SpreadsheetSplitViewMode.Both); fixture.Window.UpdateLayout();
        fixture.Control.GetPane(SpreadsheetSplitViewPane.TopRight).ScrollTo(10.25, 40.75);
        Assert.AreEqual(10.25, fixture.Session.View.SplitState.TopRightScroll.OffsetX);
        Assert.AreEqual(40.75, fixture.Session.View.SplitState.TopRightScroll.OffsetY);
        Assert.AreEqual(0d, fixture.Control.GetPane(SpreadsheetSplitViewPane.TopLeft).ScrollSnapshot.OffsetX);
    });
    [TestMethod]
    public Task ResizeShouldRetainNativePaneIdentityAndDraft() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture(); fixture.Control.SetMode(SpreadsheetSplitViewMode.Vertical); fixture.Window.UpdateLayout();
        var right = fixture.Control.GetPane(SpreadsheetSplitViewPane.TopRight); right.BeginEdit("Draft");
        fixture.Window.Width = 1100; fixture.Window.UpdateLayout();
        Assert.AreSame(right, fixture.Control.GetPane(SpreadsheetSplitViewPane.TopRight)); Assert.AreEqual("Draft", right.EditorText);
        Assert.IsTrue(right.IsEditing); Assert.IsTrue(fixture.Session.ActiveWorksheet.GetCell(default).IsEmpty);
    });
    [TestMethod]
    public Task HidingEditingPaneShouldCancelWithoutWritingCell() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture(); fixture.Control.SetMode(SpreadsheetSplitViewMode.Both); fixture.Window.UpdateLayout();
        fixture.Control.GetPane(SpreadsheetSplitViewPane.BottomRight).BeginEdit("Discard"); fixture.Control.SetMode(SpreadsheetSplitViewMode.None);
        Assert.IsFalse(fixture.Session.Editor.IsEditing); Assert.IsTrue(fixture.Session.ActiveWorksheet.GetCell(default).IsEmpty);
    });
    [TestMethod]
    public Task SplitTopologyShouldUseSharedUndoHistory() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture(); fixture.Control.SetMode(SpreadsheetSplitViewMode.Vertical); fixture.Control.SetMode(SpreadsheetSplitViewMode.Both);
        Assert.IsTrue(fixture.Control.UndoSplit()); Assert.AreEqual(SpreadsheetSplitViewMode.Vertical, fixture.Control.State.Mode);
        Assert.IsTrue(fixture.Control.RedoSplit()); Assert.AreEqual(SpreadsheetSplitViewMode.Both, fixture.Control.State.Mode);
    });
    [TestMethod]
    public Task WorksheetSwitchShouldRestoreItsOwnSplitState() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture(); var first = fixture.Session.ActiveWorksheet; var second = fixture.Session.Workbook.AddWorksheet("Second");
        fixture.Control.SetMode(SpreadsheetSplitViewMode.Vertical); fixture.Session.ActivateWorksheet(second);
        Assert.AreEqual(SpreadsheetSplitViewMode.None, fixture.Control.State.Mode);
        fixture.Control.SetMode(SpreadsheetSplitViewMode.Horizontal); fixture.Session.ActivateWorksheet(first);
        Assert.AreEqual(SpreadsheetSplitViewMode.Vertical, fixture.Control.State.Mode);
    });
    [TestMethod]
    public Task ActivePaneShouldCommitThePreviousOwnedDraftOnce() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture(); fixture.Control.SetMode(SpreadsheetSplitViewMode.Both); fixture.Window.UpdateLayout();
        fixture.Control.ActiveSpreadsheet.BeginEdit("21"); Assert.IsTrue(fixture.Control.ActivatePane(SpreadsheetSplitViewPane.BottomRight));
        Assert.AreEqual("21", fixture.Session.ActiveWorksheet.GetCell(default).Value.ToString());
        Assert.AreSame(fixture.Control.GetPane(SpreadsheetSplitViewPane.BottomRight), fixture.Control.ActiveSpreadsheet);
        Assert.IsTrue(fixture.Session.Undo()); Assert.IsTrue(fixture.Session.ActiveWorksheet.GetCell(default).IsEmpty);
    });
    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Session = new SpreadsheetSession(new Workbook()); Control = new NeraSpreadsheetSplitControl { Session = Session };
            Window = new Window { Width = 900, Height = 600, Content = Control }; Window.Show(); Window.UpdateLayout();
        }
        public SpreadsheetSession Session { get; }
        public NeraSpreadsheetSplitControl Control { get; }
        public Window Window { get; }
        public void Dispose() { Window.Content = null; Window.Close(); Control.Dispose(); }
    }
}
