using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Headless;
using global::Avalonia.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Avalonia.Tests;

[TestClass]
public sealed class SplitFormulaTests
{
    [TestMethod]
    public Task ReferenceClickInAnotherPaneShouldKeepOriginalDraftOwner() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        var editor = fixture.Control.GetPane(SpreadsheetSplitViewPane.TopLeft);
        var target = fixture.Control.GetPane(SpreadsheetSplitViewPane.TopRight);
        var version = fixture.Session.Workbook.Version;
        editor.BeginEdit("=");
        var local = new Point(180, 65);
        Assert.IsTrue(target.TryHitFormulaCell(local, out var address));
        var screen = target.TranslatePoint(local, fixture.Window);
        Assert.IsNotNull(screen);
        fixture.Window.MouseDown(screen.Value, MouseButton.Left);
        fixture.Window.MouseUp(screen.Value, MouseButton.Left);
        Assert.AreSame(editor, fixture.Control.EditingSpreadsheet);
        Assert.IsTrue(editor.IsEditing);
        Assert.AreEqual("=" + address, editor.EditorText);
        Assert.AreEqual(SpreadsheetSplitViewPane.TopLeft, fixture.Control.State.ActivePane);
        Assert.AreEqual(version, fixture.Session.Workbook.Version);
        Assert.IsTrue(fixture.Session.ActiveWorksheet.GetCell(default).IsEmpty);
    });

    [TestMethod]
    public Task FormulaHighlightProjectionShouldBeSharedButRespectEachPanesOptOut() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture();
        var editor = fixture.Control.ActiveSpreadsheet;
        var other = fixture.Control.GetPane(SpreadsheetSplitViewPane.TopRight);
        editor.BeginEdit("=");
        editor.InsertFormulaReference(new CellRange(new CellAddress(0, 1), new CellAddress(2, 1)));
        Assert.HasCount(1, other.CurrentFormulaReferenceHighlights);
        other.ShowFormulaReferenceHighlights = false;
        Assert.HasCount(0, other.CurrentFormulaReferenceHighlights);
        Assert.HasCount(1, editor.CurrentFormulaReferenceHighlights);
        other.ShowFormulaReferenceHighlights = true;
        Assert.HasCount(1, other.CurrentFormulaReferenceHighlights);
        editor.CancelEditor();
        Assert.HasCount(0, other.CurrentFormulaReferenceHighlights);
    });

    [TestMethod]
    public Task ActivePaneChangeShouldRaiseOneNotification() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture(); var count = 0;
        fixture.Control.ActivePaneChanged += (_, _) => count++;
        fixture.Control.ActivatePane(SpreadsheetSplitViewPane.TopRight);
        Assert.AreEqual(1, count);
        fixture.Control.ActivatePane(SpreadsheetSplitViewPane.TopRight);
        Assert.AreEqual(1, count);
    });

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Session = new SpreadsheetSession(new Workbook());
            Control = new NeraSpreadsheetSplitControl { Session = Session };
            Window = new Window { Width = 1000, Height = 550, Content = Control };
            Window.Show(); Window.UpdateLayout(); Control.SetMode(SpreadsheetSplitViewMode.Vertical); Window.UpdateLayout();
        }
        public SpreadsheetSession Session { get; }
        public NeraSpreadsheetSplitControl Control { get; }
        public Window Window { get; }
        public void Dispose() { Window.Content = null; Window.Close(); Control.Dispose(); }
    }
}
