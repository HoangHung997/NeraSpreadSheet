using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Avalonia.Tests;

[TestClass]
public sealed class FormulaEditingTests
{
    [TestMethod]
    public Task CompletionShouldUpdateOnlyCanonicalDraft() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture(); var version = fixture.Session.Workbook.Version;
        fixture.Control.BeginEdit("=SU");
        var index = fixture.Control.CurrentStructuredReferenceSuggestions.Count + fixture.Control.CurrentFormulaSuggestions.ToList().FindIndex(item => item.Name == "SUM");
        Assert.IsTrue(index >= 0); Assert.IsTrue(fixture.Control.ApplyFormulaSuggestion(index));
        Assert.AreEqual("=SUM(", fixture.Control.EditorText);
        Assert.AreEqual(version, fixture.Session.Workbook.Version); Assert.IsTrue(fixture.Session.ActiveWorksheet.GetCell(default).IsEmpty);
    });

    [TestMethod]
    public Task NestedArgumentHelpShouldFollowNativeCaret() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture(); fixture.Control.BeginEdit("=SUM(IF(A1,");
        Assert.AreEqual("IF", fixture.Control.CurrentFormulaHelp!.Function.Name);
        Assert.AreEqual(1, fixture.Control.CurrentFormulaHelp.ActiveArgumentIndex);
        const string text = "=SUM(IF(A1,2,3),";
        fixture.Control.UpdateEditorDraft(text, text.Length, text.Length);
        Assert.AreEqual("SUM", fixture.Control.CurrentFormulaHelp!.Function.Name);
        Assert.AreEqual(1, fixture.Control.CurrentFormulaHelp.ActiveArgumentIndex);
    });

    [TestMethod]
    public Task CompletionAndPointModeShouldNotModifyQuotedLiterals() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture(); fixture.Control.BeginEdit("=\"SU");
        Assert.HasCount(0, fixture.Control.CurrentFormulaSuggestions);
        Assert.IsFalse(fixture.Control.InsertFormulaReference(new CellRange(new CellAddress(0, 1), new CellAddress(2, 1))));
        Assert.AreEqual("=\"SU", fixture.Control.EditorText);
    });

    [TestMethod]
    public Task PointDragShouldReplaceProvisionalSpanWithoutChangingSelectionOrHistory() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture(); var version = fixture.Session.Workbook.Version;
        fixture.Control.BeginEdit("=SUM(");
        Assert.IsTrue(fixture.Control.InsertFormulaReference(new CellRange(new CellAddress(0, 1), new CellAddress(0, 1))));
        Assert.IsTrue(fixture.Control.InsertFormulaReference(new CellRange(new CellAddress(0, 1), new CellAddress(2, 1))));
        Assert.AreEqual("=SUM(B1:B3", fixture.Control.EditorText);
        Assert.AreEqual(default(CellAddress), fixture.Session.Selection.ActiveCell);
        Assert.AreEqual(version, fixture.Session.Workbook.Version);
        Assert.HasCount(1, fixture.Control.CurrentFormulaReferenceHighlights);
    });

    [TestMethod]
    public Task QueuedTextChangedShouldNotInvalidateProgrammaticReferenceSpan() => AvaloniaAsyncTest.Run(async () =>
    {
        using var fixture = new Fixture(); fixture.Control.BeginEdit("=");
        fixture.Control.InsertFormulaReference(new CellRange(new CellAddress(0, 1), new CellAddress(0, 1)));
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
        Assert.IsTrue(fixture.Control.InsertFormulaReference(new CellRange(new CellAddress(0, 1), new CellAddress(2, 1))));
        Assert.AreEqual("=B1:B3", fixture.Control.EditorText);
    });

    [TestMethod]
    public Task IdenticalDraftEchoShouldPreserveBackwardSelectionWithoutAnEvent() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture(); fixture.Control.BeginEdit("=SUM(A1,2)");
        fixture.Control.UpdateEditorDraft(fixture.Control.EditorText, 8, 2);
        var before = fixture.Control.CurrentEditorDraft; var notifications = 0;
        fixture.Control.EditorDraftChanged += (_, _) => notifications++;
        Assert.IsTrue(fixture.Control.UpdateEditorDraft(before!.Text, before.SelectionStart, before.SelectionEnd));
        Assert.AreEqual(before, fixture.Control.CurrentEditorDraft); Assert.AreEqual(0, notifications);
        Assert.IsTrue(before.SelectionStart > before.SelectionEnd);
    });

    [TestMethod]
    public Task SelectedTextShouldNotBeReplacedByCompletionOrPointMode() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture(); fixture.Control.BeginEdit("=SUM(A1)");
        fixture.Control.UpdateEditorDraft("=SUM(A1)", 5, 7);
        Assert.HasCount(0, fixture.Control.CurrentFormulaSuggestions);
        Assert.IsFalse(fixture.Control.ApplyFormulaSuggestion(0));
        Assert.IsFalse(fixture.Control.InsertFormulaReference(new CellRange(new CellAddress(0, 2), new CellAddress(0, 2))));
        Assert.AreEqual("=SUM(A1)", fixture.Control.EditorText);
    });

    [TestMethod]
    public Task CanonicalSheetSwitchShouldDismissAssistanceAndLeaveCellsUnchanged() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture(); var second = fixture.Session.Workbook.AddWorksheet("Second");
        fixture.Control.BeginEdit("=SUM("); Assert.IsTrue(fixture.Control.IsFormulaAssistanceOpen);
        fixture.Session.ActivateWorksheet(second);
        Assert.IsFalse(fixture.Control.IsEditing); Assert.IsFalse(fixture.Control.IsFormulaAssistanceOpen);
        Assert.HasCount(0, fixture.Control.CurrentFormulaSuggestions); Assert.IsNull(fixture.Control.CurrentFormulaHelp);
        Assert.IsTrue(second.GetCell(default).IsEmpty); Assert.IsTrue(fixture.Session.Workbook.Worksheets[0].GetCell(default).IsEmpty);
    });

    [TestMethod]
    public Task HighlightsShouldRespectOptOutAndUseSharedFormulaReferences() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture(); fixture.Session.SetFormula(default, "=SUM(B1:B3)");
        Assert.HasCount(1, fixture.Control.CurrentFormulaReferenceHighlights);
        Assert.AreEqual(new CellRange(new CellAddress(0, 1), new CellAddress(2, 1)), fixture.Control.CurrentFormulaReferenceHighlights[0].Range);
        fixture.Control.ShowFormulaReferenceHighlights = false; Assert.HasCount(0, fixture.Control.CurrentFormulaReferenceHighlights);
        fixture.Control.ShowFormulaReferenceHighlights = true; Assert.HasCount(1, fixture.Control.CurrentFormulaReferenceHighlights);
    });

    [TestMethod]
    public Task TabShouldAcceptSuggestionInsteadOfCommittingIncompleteFormula() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture(); fixture.Control.BeginEdit("=SU");
        Assert.IsTrue(fixture.Control.TryHandleFormulaAssistanceKey(Key.Tab, KeyModifiers.None));
        Assert.IsTrue(fixture.Control.IsEditing); Assert.IsTrue(fixture.Session.ActiveWorksheet.GetCell(default).IsEmpty);
        Assert.IsTrue(fixture.Control.EditorText.StartsWith("=SU", StringComparison.Ordinal));
    });

    [TestMethod]
    public Task ReferenceToAnotherSheetShouldUseSharedQuoting() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture(); fixture.Session.Workbook.AddWorksheet("Dữ liệu khác"); fixture.Control.BeginEdit("=");
        Assert.IsTrue(fixture.Control.InsertFormulaReference(new CellRange(new CellAddress(0, 0), new CellAddress(0, 0)), "Dữ liệu khác"));
        Assert.AreEqual("='Dữ liệu khác'!A1", fixture.Control.EditorText);
        Assert.HasCount(0, fixture.Control.CurrentFormulaReferenceHighlights);
    });

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Session = new SpreadsheetSession(new Workbook()); Control = new NeraSpreadsheetControl { Session = Session };
            Window = new Window { Width = 900, Height = 550, Content = Control }; Window.Show(); Window.UpdateLayout();
        }
        public SpreadsheetSession Session { get; }
        public NeraSpreadsheetControl Control { get; }
        public Window Window { get; }
        public void Dispose() { Window.Content = null; Window.Close(); Control.Dispose(); }
    }
}
