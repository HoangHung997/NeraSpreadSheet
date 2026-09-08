using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Headless;
using global::Avalonia.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Avalonia.Tests;

[TestClass]
public sealed class FormulaPointBoundaryTests
{
    [TestMethod]
    public Task CompleteOperandShouldRejectAdjacentReferenceInsertion() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture("=B2:C3+D5");
        Assert.IsFalse(fixture.Sheet.InsertFormulaReference(new CellRange(default, default)));
        Assert.AreEqual("=B2:C3+D5", fixture.Sheet.EditorText);
    });
    [TestMethod]
    public Task OperatorSlotShouldAcceptReference() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture("=B2+");
        Assert.IsTrue(fixture.Sheet.InsertFormulaReference(new CellRange(default, default)));
        Assert.AreEqual("=B2+A1", fixture.Sheet.EditorText);
    });
    [TestMethod]
    public Task MiddleOfIdentifierShouldRejectReferenceInsertion() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture("=ABC"); fixture.Sheet.UpdateEditorDraft("=ABC", 2, 2);
        Assert.IsFalse(fixture.Sheet.InsertFormulaReference(new CellRange(default, default)));
        Assert.AreEqual("=ABC", fixture.Sheet.EditorText);
    });
    [TestMethod]
    public Task EmptyArgumentBeforeClosingParenthesisShouldAcceptReference() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture("=SUM()"); fixture.Sheet.UpdateEditorDraft("=SUM()", 5, 5);
        Assert.IsTrue(fixture.Sheet.InsertFormulaReference(new CellRange(new CellAddress(1, 1), new CellAddress(3, 1))));
        Assert.AreEqual("=SUM(B2:B4)", fixture.Sheet.EditorText);
    });
    [TestMethod]
    public Task WorksheetMutationDuringBorderDragShouldRestoreOriginalText() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture("=B2:C3+D5");
        fixture.StartBorder();
        fixture.Session.ActiveWorksheet.SetValue(new CellAddress(20, 20), 5d);
        fixture.Window.MouseMove(fixture.End, RawInputModifiers.LeftMouseButton);
        fixture.Sheet.AdvanceFormulaPointerFrame(TimeSpan.Zero);
        fixture.Window.MouseUp(fixture.End, MouseButton.Left);
        Assert.AreEqual("=B2:C3+D5", fixture.Sheet.EditorText);
        Assert.IsFalse(fixture.Sheet.IsDraggingFormulaReferenceBorder);
    });
    [TestMethod]
    public Task ExternalDraftChangeShouldNotBeOverwrittenByOldCapturedPointer() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture("=B2:C3+D5"); fixture.StartBorder();
        fixture.Sheet.UpdateEditorDraft("=X1+2", 5, 5);
        fixture.Window.MouseMove(fixture.End, RawInputModifiers.LeftMouseButton);
        fixture.Window.MouseUp(fixture.End, MouseButton.Left);
        Assert.AreEqual("=X1+2", fixture.Sheet.EditorText);
        Assert.IsFalse(fixture.Sheet.IsDraggingFormulaReferenceBorder);
        Assert.IsTrue(fixture.Sheet.IsEditing);
    });
    private sealed class Fixture : IDisposable
    {
        public Fixture(string text)
        {
            Session = new SpreadsheetSession(new Workbook()); Sheet = new NeraSpreadsheetControl { Session = Session };
            Window = new Window { Width = 900, Height = 550, Content = Sheet }; Window.Show(); Window.UpdateLayout(); Sheet.BeginEdit(text); Window.UpdateLayout();
        }
        public SpreadsheetSession Session { get; }
        public NeraSpreadsheetControl Sheet { get; }
        public Window Window { get; }
        private Point Position(double column, double row) => Sheet.TranslatePoint(new Point(Sheet.RenderTheme.RowHeaderWidth + column * Session.ActiveWorksheet.Dimensions.DefaultColumnWidth,
            Sheet.RenderTheme.ColumnHeaderHeight + row * Session.ActiveWorksheet.Dimensions.DefaultRowHeight), Window)!.Value;
        public Point End => Position(2, 2.5);
        public void StartBorder()
        {
            Window.MouseDown(Position(1, 1.5), MouseButton.Left);
            Assert.IsTrue(Sheet.IsDraggingFormulaReferenceBorder);
        }
        public void Dispose() { Window.Content = null; Window.Close(); Sheet.Dispose(); }
    }
}
