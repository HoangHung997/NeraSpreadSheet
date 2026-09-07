using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Headless;
using global::Avalonia.Input;
using global::Avalonia.Themes.Fluent;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

[assembly: DoNotParallelize]

namespace NeraSpreadSheet.Avalonia.Tests;

[TestClass]
public sealed class SpreadsheetHostTests
{
    [TestMethod]
    public Task HostShouldUseTheCallerSessionAndOneReusableEditor() => OnUi(() =>
    {
        using var fixture = new HostFixture();
        Assert.AreSame(fixture.Session, fixture.Control.Session);
        fixture.Session.SetValue(new CellAddress(999999, 100), "Sparse tail");
        Assert.HasCount(1, fixture.Control.Children);
        Assert.IsInstanceOfType<TextBox>(fixture.Control.Children[0]);
    });

    [TestMethod]
    public Task CommitShouldRecalculateThroughCanonicalHistory() => OnUi(() =>
    {
        using var fixture = new HostFixture();
        fixture.Session.SetValue(default, 3d);
        fixture.Session.SetFormula(new CellAddress(0, 1), "=A1*2");
        Assert.IsTrue(fixture.Control.BeginEdit("21"));
        Assert.IsTrue(fixture.Control.CommitEditor());
        Assert.AreEqual("42", fixture.Session.ActiveWorksheet.GetCell(new CellAddress(0, 1)).Value.ToString());
        Assert.IsTrue(fixture.Session.Undo());
        Assert.AreEqual("3", fixture.Session.ActiveWorksheet.GetCell(default).Value.ToString());
        Assert.IsTrue(fixture.Session.Redo());
        Assert.AreEqual("21", fixture.Session.ActiveWorksheet.GetCell(default).Value.ToString());
    });

    [TestMethod]
    public Task CancelShouldNotMutateWorkbookOrSelection() => OnUi(() =>
    {
        using var fixture = new HostFixture();
        fixture.Session.SetValue(default, "Original");
        var version = fixture.Session.Workbook.Version;
        fixture.Control.BeginEdit("Discard");
        Assert.IsTrue(fixture.Control.CancelEditor());
        Assert.AreEqual(version, fixture.Session.Workbook.Version);
        Assert.AreEqual("Original", fixture.Session.ActiveWorksheet.GetCell(default).Value.ToString());
        Assert.AreEqual(default(CellAddress), fixture.Session.Selection.ActiveCell);
        Assert.IsFalse(fixture.Control.CancelEditor());
    });

    [TestMethod]
    public Task CanonicalSheetSwitchShouldClearNativeDraftWithoutWritingOldOrNewSheet() => OnUi(() =>
    {
        using var fixture = new HostFixture();
        var first = fixture.Session.ActiveWorksheet;
        var second = fixture.Session.Workbook.AddWorksheet("Second");
        fixture.Session.Selection.SetActiveCell(new CellAddress(5, 4));
        fixture.Control.BeginEdit("Must not be saved");
        fixture.Session.ActivateWorksheet(second);
        Assert.IsFalse(fixture.Control.IsEditing);
        Assert.IsFalse(fixture.Control.CancelEditor());
        Assert.AreEqual(default(CellAddress), fixture.Session.Selection.ActiveCell);
        Assert.IsTrue(first.GetCell(new CellAddress(5, 4)).IsEmpty);
        Assert.IsTrue(second.GetCell(default).IsEmpty);
        Assert.IsFalse(((TextBox)fixture.Control.Children[0]).IsVisible);
    });

    [TestMethod]
    public Task RebindingShouldCancelOnlyTheOldOwnedDraft() => OnUi(() =>
    {
        using var fixture = new HostFixture();
        fixture.Control.BeginEdit("Old draft");
        var next = new SpreadsheetSession(new Workbook());
        next.SetValue(default, "Next");
        fixture.Control.Session = next;
        Assert.IsFalse(fixture.Session.Editor.IsEditing);
        Assert.AreSame(next, fixture.Control.Session);
        Assert.AreEqual("Next", next.ActiveWorksheet.GetCell(default).Value.ToString());
        Assert.IsFalse(fixture.Control.IsEditing);
    });

    [TestMethod]
    public Task ViewMustNotStealOrCancelAnotherViewsEditor() => OnUi(() =>
    {
        using var first = new HostFixture();
        using var second = new HostFixture(first.Session);
        Assert.IsTrue(second.Control.BeginEdit("Second owns this"));
        Assert.IsFalse(first.Control.BeginEdit("Do not steal"));
        first.Control.Dispose();
        Assert.IsTrue(second.Control.IsEditing);
        Assert.AreEqual("Second owns this", second.Control.EditorText);
    });

    [TestMethod]
    public Task BeginningTheSameDraftAgainShouldPreserveUncommittedText() => OnUi(() =>
    {
        using var fixture = new HostFixture();
        fixture.Control.BeginEdit("Uncommitted");
        fixture.Control.BeginEdit(focusEditor: false);
        Assert.AreEqual("Uncommitted", fixture.Control.EditorText);
        Assert.IsTrue(fixture.Session.ActiveWorksheet.GetCell(default).IsEmpty);
    });

    [TestMethod]
    public Task TextChangesShouldNotCreateHistoryBeforeCommit() => OnUi(() =>
    {
        using var fixture = new HostFixture();
        var version = fixture.Session.Workbook.Version;
        fixture.Control.BeginEdit();
        fixture.Control.SetEditorText("Bản nháp tiếng Việt");
        Assert.AreEqual(version, fixture.Session.Workbook.Version);
        Assert.AreEqual("Bản nháp tiếng Việt", fixture.Control.EditorText);
        Assert.IsTrue(fixture.Control.CommitEditor());
        Assert.AreEqual("Bản nháp tiếng Việt", fixture.Session.ActiveWorksheet.GetCell(default).Value.ToString());
    });

    [TestMethod]
    public Task PrecisionScrollShouldRetainFractionalOffsets() => OnUi(() =>
    {
        using var fixture = new HostFixture();
        fixture.Control.QueuePrecisionScroll(1.25, 2.75);
        fixture.Control.AdvanceFrame(TimeSpan.FromSeconds(1d / 60d));
        Assert.AreEqual(1.25, fixture.Control.ScrollSnapshot.OffsetX, 0.000001);
        Assert.AreEqual(2.75, fixture.Control.ScrollSnapshot.OffsetY, 0.000001);
    });

    [TestMethod]
    public Task ZoomShouldNotMutateWorkbookAndShouldRejectNonFiniteInput() => OnUi(() =>
    {
        using var fixture = new HostFixture();
        var version = fixture.Session.Workbook.Version;
        fixture.Control.Zoom = 1.5;
        Assert.AreEqual(version, fixture.Session.Workbook.Version);
        try { fixture.Control.Zoom = double.NaN; Assert.Fail("NaN must be rejected."); }
        catch (ArgumentOutOfRangeException) { }
        Assert.AreEqual(1.5, fixture.Control.Zoom);
    });

    [TestMethod]
    public Task HandledKeyboardEventShouldNotMoveSelection() => OnUi(() =>
    {
        using var fixture = new HostFixture();
        fixture.Control.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right, Handled = true,
        });
        Assert.AreEqual(default(CellAddress), fixture.Session.Selection.ActiveCell);
    });

    [TestMethod]
    public Task ArrowKeyShouldNavigateThroughSharedSelection() => OnUi(() =>
    {
        using var fixture = new HostFixture();
        fixture.Control.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right });
        Assert.AreEqual(new CellAddress(0, 1), fixture.Session.Selection.ActiveCell);
    });

    [TestMethod]
    public Task EnterInNativeEditorShouldCommitInsteadOfAddingANewline() => OnUi(() =>
    {
        using var fixture = new HostFixture();
        fixture.Control.BeginEdit("17");
        var editor = (TextBox)fixture.Control.Children[0];
        editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Assert.IsFalse(fixture.Control.IsEditing);
        Assert.AreEqual("17", fixture.Session.ActiveWorksheet.GetCell(default).Value.ToString());
        Assert.AreEqual(new CellAddress(1, 0), fixture.Session.Selection.ActiveCell);
    });

    [TestMethod]
    public Task DetachAndReattachShouldNotLeaveAStaleEditorOrLoseTheSession() => OnUi(() =>
    {
        using var fixture = new HostFixture();
        fixture.Control.BeginEdit("Discard on detach");
        fixture.Window.Content = null;
        Assert.IsFalse(fixture.Session.Editor.IsEditing);
        fixture.Window.Content = fixture.Control;
        fixture.Window.UpdateLayout();
        Assert.AreSame(fixture.Session, fixture.Control.Session);
        Assert.IsTrue(fixture.Control.BeginEdit("After reattach"));
        Assert.IsTrue(fixture.Control.CommitEditor());
        Assert.AreEqual("After reattach", fixture.Session.ActiveWorksheet.GetCell(default).Value.ToString());
    });

    private static async Task OnUi(Action action)
    {
        await using var headless = HeadlessUnitTestSession.StartNew(typeof(TestApplication));
        await headless.Dispatch(action, CancellationToken.None);
    }

    private sealed class HostFixture : IDisposable
    {
        public HostFixture(SpreadsheetSession? session = null)
        {
            Session = session ?? new SpreadsheetSession(new Workbook());
            Control = new NeraSpreadsheetControl { Session = Session };
            Window = new Window { Width = 800, Height = 500, Content = Control };
            Window.Show();
            Window.UpdateLayout();
            Control.Focus();
        }
        public SpreadsheetSession Session { get; }
        public NeraSpreadsheetControl Control { get; }
        public Window Window { get; }
        public void Dispose() { Window.Content = null; Window.Close(); Control.Dispose(); }
    }
}

public sealed class TestApplication : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApplication>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
