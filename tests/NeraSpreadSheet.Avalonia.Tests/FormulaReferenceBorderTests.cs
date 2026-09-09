using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Headless;
using global::Avalonia.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Avalonia.Tests;

[TestClass]
public sealed class FormulaReferenceBorderTests
{
    [TestMethod]
    public void LocatorShouldIgnoreStringsFunctionNamesAndStructuredIdentifiers()
    {
        var references = EditableFormulaReference.Locate("=LOG10(100)+A1Name+Table1[A1]+\"B2\"+D4");
        Assert.HasCount(1, references); Assert.AreEqual(new CellRange(new CellAddress(3, 3), new CellAddress(3, 3)), references[0].Range);
    }
    [TestMethod]
    public void FormattingMovedReferenceShouldPreserveMixedAbsoluteMarkers()
    {
        var reference = EditableFormulaReference.Locate("='O''Brien'!$B2:C$3").Single();
        Assert.AreEqual("'O''Brien'!$D4:E$5", reference.Format(new CellRange(new CellAddress(3, 3), new CellAddress(4, 4))));
    }
    [TestMethod]
    public void DuplicateReferencesShouldRetainSeparateSourceSpans()
    {
        var references = EditableFormulaReference.Locate("=B2+B2");
        Assert.HasCount(2, references); Assert.AreNotEqual(references[0].Span, references[1].Span);
        Assert.AreEqual(references[0].Range, references[1].Range);
    }
    [TestMethod]
    public void ReversedReferencesShouldNotBeSilentlyNormalizedForDragging()
    {
        Assert.HasCount(0, EditableFormulaReference.Locate("=C3:B2"));
    }
    [TestMethod]
    public void FrozenOverlayShouldClipScrollableReferenceEdgesWithoutFalseSeam()
    {
        var layout = new ViewportLayoutEngine(new SparseAxisMetricIndex(100, 20), new SparseAxisMetricIndex(50, 80))
            .Compute(new ViewportRequest(50, 0, new SizeD(400, 200), 64, 1, 1));
        var edges = FormulaReferenceOutlineGeometry.Edges(layout, new CellRange(new CellAddress(0, 1), new CellAddress(2, 1)));
        Assert.IsTrue(edges.Count > 0);
        Assert.IsTrue(edges.All(edge => edge.Start.X >= 80 && edge.End.X >= 80));
        Assert.IsFalse(edges.Any(edge => edge.Start.X == 80 && edge.End.X == 80));
    }
    [TestMethod]
    public void ViewportClippingShouldNotCreateArtificialDraggableTopOrLeftEdges()
    {
        var layout = new ViewportLayoutEngine(new SparseAxisMetricIndex(100, 20), new SparseAxisMetricIndex(50, 80))
            .Compute(new ViewportRequest(100, 100, new SizeD(400, 180), 64));
        var edges = FormulaReferenceOutlineGeometry.Edges(layout, new CellRange(default, new CellAddress(10, 5)));
        Assert.IsTrue(edges.Count > 0);
        Assert.IsFalse(edges.Any(edge => edge.Start.X == 0 && edge.End.X == 0));
        Assert.IsFalse(edges.Any(edge => edge.Start.Y == 0 && edge.End.Y == 0));
    }
    [TestMethod]
    public void OutlineCompositionShouldRetainBodyDisplayListByReference()
    {
        var body = new DisplayListBuilder().Build();
        var layout = new ViewportLayoutEngine(new SparseAxisMetricIndex(100, 20), new SparseAxisMetricIndex(50, 80))
            .Compute(new ViewportRequest(0, 0, new SizeD(400, 200), 64));
        var result = FormulaReferenceOutlineGeometry.Compose(body, layout,
            [new SpreadsheetFormulaReferenceHighlight(new CellRange(default, new CellAddress(1, 1)), new ColorRgba(255, 0, 0))], 2);
        Assert.AreSame(body, result.Commands.OfType<DrawDisplayListCommand>().Single().DisplayList);
    }
    [TestMethod]
    public Task RoutedBorderMoveShouldChangeOnlyTheTargetToken() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture("=$B$2:C3+D5");
        var start = fixture.Point(1, 1.5); var end = fixture.Point(2, 2.5);
        fixture.Drag(start, end);
        Assert.AreEqual("=$C$3:D4+D5", fixture.Sheet.EditorText);
        Assert.IsTrue(fixture.Session.ActiveWorksheet.GetCell(default).IsEmpty);
        Assert.IsFalse(fixture.Session.Undo());
    });
    [TestMethod]
    public Task RoutedBottomRightCornerShouldResizeInsteadOfMoveWholeRange() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture("=B2:C3+D5");
        var corner = fixture.Point(3, 3) - new Vector(0.2, 0.2);
        fixture.Drag(corner, fixture.Point(4.5, 4.5));
        Assert.AreEqual("=B2:E5+D5", fixture.Sheet.EditorText);
    });
    [TestMethod]
    public Task CancelledBorderGestureShouldRestoreOriginalDraft() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture("=B2:C3+D5");
        var start = fixture.Point(1, 1.5); var end = fixture.Point(2, 2.5);
        fixture.Window.MouseDown(start, MouseButton.Left);
        Assert.IsTrue(fixture.Sheet.IsDraggingFormulaReferenceBorder);
        fixture.Window.MouseMove(end, RawInputModifiers.LeftMouseButton);
        fixture.Sheet.AdvanceFormulaPointerFrame(TimeSpan.Zero);
        Assert.AreEqual("=C3:D4+D5", fixture.Sheet.EditorText);
        fixture.Sheet.EndPointReference(false);
        fixture.Window.MouseUp(end, MouseButton.Left);
        Assert.AreEqual("=B2:C3+D5", fixture.Sheet.EditorText);
        Assert.IsTrue(fixture.Sheet.IsEditing);
        Assert.IsFalse(fixture.Sheet.IsDraggingFormulaReferenceBorder);
    });
    [TestMethod]
    public Task BorderMoveShouldCommitThroughExactlyOneExistingHistoryOperation() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture("=B2:C3+D5");
        fixture.Drag(fixture.Point(1, 1.5), fixture.Point(2, 2.5));
        Assert.IsTrue(fixture.Sheet.CommitEditor());
        Assert.AreEqual("=C3:D4+D5", fixture.Session.ActiveWorksheet.GetCell(default).Formula);
        Assert.IsTrue(fixture.Session.Undo()); Assert.IsTrue(fixture.Session.ActiveWorksheet.GetCell(default).IsEmpty);
        Assert.IsFalse(fixture.Session.Undo());
    });
    [TestMethod]
    public Task OptOutShouldPreventReferenceBorderGesture() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        using var fixture = new Fixture("=B2:C3+D5"); fixture.Sheet.ShowFormulaReferenceHighlights = false;
        var start = fixture.Point(1, 1.5);
        fixture.Window.MouseDown(start, MouseButton.Left);
        Assert.IsFalse(fixture.Sheet.IsDraggingFormulaReferenceBorder);
        fixture.Window.MouseUp(start, MouseButton.Left);
        Assert.AreEqual("=B2:C3+D5", fixture.Sheet.EditorText);
    });
    private sealed class Fixture : IDisposable
    {
        public Fixture(string formula)
        {
            Session = new SpreadsheetSession(new Workbook()); Sheet = new NeraSpreadsheetControl { Session = Session };
            Window = new Window { Width = 900, Height = 550, Content = Sheet }; Window.Show(); Window.UpdateLayout(); Sheet.BeginEdit(formula); Window.UpdateLayout();
        }
        public SpreadsheetSession Session { get; }
        public NeraSpreadsheetControl Sheet { get; }
        public Window Window { get; }
        public Point Point(double column, double row) => Sheet.TranslatePoint(new Point(Sheet.RenderTheme.RowHeaderWidth + column * Session.ActiveWorksheet.Dimensions.DefaultColumnWidth,
            Sheet.RenderTheme.ColumnHeaderHeight + row * Session.ActiveWorksheet.Dimensions.DefaultRowHeight), Window)!.Value;
        public void Drag(Point start, Point end)
        {
            var selection = Session.Selection.Capture().Version; var version = Session.Workbook.Version;
            Window.MouseDown(start, MouseButton.Left); Assert.IsTrue(Sheet.IsDraggingFormulaReferenceBorder);
            Window.MouseMove(end, RawInputModifiers.LeftMouseButton); Window.MouseUp(end, MouseButton.Left);
            Assert.IsFalse(Sheet.IsDraggingFormulaReferenceBorder); Assert.IsTrue(Sheet.IsEditing);
            Assert.AreEqual(selection, Session.Selection.Capture().Version); Assert.AreEqual(version, Session.Workbook.Version);
        }
        public void Dispose() { Window.Content = null; Window.Close(); Sheet.Dispose(); }
    }
}
