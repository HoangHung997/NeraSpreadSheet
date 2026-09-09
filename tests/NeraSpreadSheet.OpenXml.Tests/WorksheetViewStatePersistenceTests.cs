using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Interaction;
using NeraWorkbook = NeraSpreadSheet.Core.Workbook;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class WorksheetViewStatePersistenceTests
{
    private const string NativeContentType = "application/vnd.neraspreadsheet.view-state+xml";
    private const string TestNamespace = "urn:nera:test:preserved-views";

    [TestMethod]
    public async Task UnsplitRoundTripShouldKeepDirectedRangesExactZoomOffsetsAndActiveSheet()
    {
        var workbook = new NeraWorkbook();
        var second = workbook.AddWorksheet("Second");
        var session = new SpreadsheetSession(workbook);
        var ranges = new[] { new CellRange(new CellAddress(2, 2), new CellAddress(4, 4)), new CellRange(new CellAddress(8, 8), new CellAddress(9, 9)) };
        session.Selection.Restore(new SelectionSnapshot(new CellAddress(4, 2), new CellAddress(2, 4), ranges, 0));
        session.View.SetWorksheetViewport(session.ActiveWorksheet, 15.125, 81.875, 1.3333333333);
        var firstState = session.View.WorksheetState;
        session.ActivateWorksheet(second);
        session.Selection.SetActiveCell(new CellAddress(20, 10));
        session.View.SetWorksheetViewport(second, 201.375, 701.625, 0.875);
        var secondState = session.View.WorksheetState;
        await using var firstSave = new MemoryStream();
        await new NeraOpenXmlSpreadsheetSessionSerializer().SaveSessionAsync(session, firstSave, new OpenXmlExportOptions());
        firstSave.Position = 0;
        var loaded = await new NeraOpenXmlSpreadsheetSessionSerializer().LoadSessionAsync(firstSave, new OpenXmlImportOptions());
        Assert.AreEqual("Second", loaded.ActiveWorksheet.Name);
        AssertState(firstState, loaded.View.GetWorksheetState(loaded.Workbook!.Worksheets[0]));
        AssertState(secondState, loaded.View.WorksheetState);
        await using var secondSave = new MemoryStream();
        await new NeraOpenXmlSpreadsheetSessionSerializer().SaveSessionAsync(loaded, secondSave, new OpenXmlExportOptions());
        secondSave.Position = 0;
        var reloaded = await new NeraOpenXmlSpreadsheetSessionSerializer().LoadSessionAsync(secondSave, new OpenXmlImportOptions());
        AssertState(firstState, reloaded.View.GetWorksheetState(reloaded.Workbook!.Worksheets[0]));
        AssertState(secondState, reloaded.View.WorksheetState);
        Assert.IsFalse(reloaded.View.HasSplitPanes);
    }

    [TestMethod]
    public async Task ExistingUnsplitViewsShouldRetainSiblingIdsUnknownAttributesAndChildren()
    {
        await using var source = await CreateStandardFixture();
        var serializer = new NeraOpenXmlSpreadsheetSessionSerializer();
        var session = await serializer.LoadSessionAsync(source, new OpenXmlImportOptions { PreserveUnknownParts = true });
        Assert.AreEqual(new CellAddress(8, 7), session.Selection.ActiveCell);
        Assert.AreEqual(1.35, session.View.WorksheetState.Zoom);
        await using var destination = new MemoryStream();
        await serializer.SaveSessionAsync(session, destination, new OpenXmlExportOptions { PreserveUnknownParts = true });
        destination.Position = 0;
        using (var document = SpreadsheetDocument.Open(destination, false))
        {
            var views = FirstPart(document).Worksheet!.GetFirstChild<SheetViews>()!.Elements<SheetView>().ToArray();
            Assert.AreEqual(2, views.Length);
            Assert.AreEqual(0U, views[0].WorkbookViewId!.Value);
            Assert.AreEqual(70U, views[0].ZoomScale!.Value);
            Assert.AreEqual("C3", views[0].Elements<Selection>().Single().SequenceOfReferences!.InnerText);
            Assert.AreEqual(1U, views[1].WorkbookViewId!.Value);
            Assert.AreEqual("keep", views[1].GetAttribute("marker", TestNamespace).Value);
            Assert.IsTrue(views[1].GetFirstChild<ExtensionList>()!.OuterXml.Contains("payload", StringComparison.Ordinal));
            Assert.AreEqual(2, document.WorkbookPart!.Workbook!.GetFirstChild<BookViews>()!.Elements<WorkbookView>().Count());
        }
        destination.Position = 0;
        var loaded = await serializer.LoadSessionAsync(destination, new OpenXmlImportOptions { PreserveUnknownParts = true });
        Assert.AreEqual(new CellAddress(8, 7), loaded.Selection.ActiveCell);
        Assert.AreEqual(2, loaded.Selection.Ranges.Count);
    }

    [TestMethod]
    public async Task EditingChosenViewShouldNotResetOtherWorkbookViewOrUnknownExtensions()
    {
        await using var source = await CreateStandardFixture();
        var serializer = new NeraOpenXmlSpreadsheetSessionSerializer();
        var session = await serializer.LoadSessionAsync(source, new OpenXmlImportOptions { PreserveUnknownParts = true });
        session.View.SetWorksheetViewport(session.ActiveWorksheet, 35.5, 45.25, 1.6);
        session.View.SetSplitTopology(SpreadsheetSplitViewMode.Vertical, 200.5, null);
        session.View.SetSplitPaneScroll(SpreadsheetSplitViewPane.TopRight, 123.75, 99.5);
        await using var result = new MemoryStream();
        await serializer.SaveSessionAsync(session, result, new OpenXmlExportOptions { PreserveUnknownParts = true });
        result.Position = 0;
        using var document = SpreadsheetDocument.Open(result, false);
        var views = FirstPart(document).Worksheet!.GetFirstChild<SheetViews>()!.Elements<SheetView>().ToArray();
        Assert.AreEqual(70U, views[0].ZoomScale!.Value);
        Assert.IsNull(views[0].GetFirstChild<Pane>());
        Assert.AreEqual(1U, views[1].WorkbookViewId!.Value);
        Assert.AreEqual(160U, views[1].ZoomScale!.Value);
        Assert.AreEqual(200.5 * 15d, views[1].GetFirstChild<Pane>()!.HorizontalSplit!.Value);
        Assert.AreEqual("keep", views[1].GetAttribute("marker", TestNamespace).Value);
        Assert.IsNotNull(views[1].GetFirstChild<ExtensionList>());
    }

    [TestMethod]
    public async Task ClearingSplitShouldRemoveOnlyOwnedPaneAndKeepViews()
    {
        await using var source = await CreateStandardFixture();
        using (var document = SpreadsheetDocument.Open(source, true))
        {
            var view = FirstPart(document).Worksheet!.GetFirstChild<SheetViews>()!.Elements<SheetView>().Last();
            view.AddChild(new Pane { State = PaneStateValues.Split, HorizontalSplit = 1500, ActivePane = PaneValues.TopRight, TopLeftCell = "C3" }, true);
            view.Elements<Selection>().Single().Pane = PaneValues.TopRight;
            FirstPart(document).Worksheet!.Save();
        }
        source.Position = 0;
        var serializer = new NeraOpenXmlSpreadsheetSessionSerializer();
        var session = await serializer.LoadSessionAsync(source, new OpenXmlImportOptions { PreserveUnknownParts = true });
        Assert.IsTrue(session.View.HasSplitPanes);
        session.View.ClearSplitPanes();
        await using var result = new MemoryStream();
        await serializer.SaveSessionAsync(session, result, new OpenXmlExportOptions { PreserveUnknownParts = true });
        result.Position = 0;
        using var saved = SpreadsheetDocument.Open(result, false);
        var views = FirstPart(saved).Worksheet!.GetFirstChild<SheetViews>()!.Elements<SheetView>().ToArray();
        Assert.AreEqual(2, views.Length);
        Assert.IsNull(views[1].GetFirstChild<Pane>());
        Assert.AreEqual("keep", views[1].GetAttribute("marker", TestNamespace).Value);
        Assert.AreEqual(70U, views[0].ZoomScale!.Value);
    }

    [TestMethod]
    public async Task StandardFrozenViewWithoutNativeMetadataShouldImportFreezeAndBodyOffsets()
    {
        await using var source = await CreateStandardFixture();
        using (var document = SpreadsheetDocument.Open(source, true))
        {
            var part = FirstPart(document);
            var view = part.Worksheet!.GetFirstChild<SheetViews>()!.Elements<SheetView>().Last();
            view.AddChild(new Pane { State = PaneStateValues.Frozen, HorizontalSplit = 1, VerticalSplit = 2,
                ActivePane = PaneValues.BottomRight, TopLeftCell = "D6" }, true);
            view.Elements<Selection>().Single().Pane = PaneValues.BottomRight;
            part.Worksheet!.Save();
        }
        source.Position = 0;
        var session = await new NeraOpenXmlSpreadsheetSessionSerializer().LoadSessionAsync(source, new OpenXmlImportOptions());
        Assert.AreEqual(2, session.View.FrozenRows);
        Assert.AreEqual(1, session.View.FrozenColumns);
        Assert.AreEqual(160d, session.View.WorksheetState.OffsetX);
        Assert.AreEqual(60d, session.View.WorksheetState.OffsetY);
        Assert.IsFalse(session.View.HasSplitPanes);
    }

    [TestMethod]
    public async Task NewViewMarkupShouldRespectSchemaOrderAndValidate()
    {
        var session = new SpreadsheetSession(new NeraWorkbook());
        session.View.SetWorksheetViewport(session.ActiveWorksheet, 50.5, 77.25, 1.25);
        session.View.SetFrozenPanes(2, 1);
        await using var stream = new MemoryStream();
        await new NeraOpenXmlSpreadsheetSessionSerializer().SaveSessionAsync(session, stream, new OpenXmlExportOptions());
        stream.Position = 0;
        using var document = SpreadsheetDocument.Open(stream, false);
        var errors = new OpenXmlValidator().Validate(document).ToArray();
        Assert.AreEqual(0, errors.Length, string.Join("; ", errors.Select(error => error.Description)));
        var children = FirstPart(document).Worksheet!.ChildElements.ToArray();
        Assert.IsTrue(Array.FindIndex(children, element => element is SheetViews) < Array.FindIndex(children, element => element is SheetData));
    }

    [TestMethod]
    public async Task LegacyNativePaneMetadataShouldNotEraseStandardSelectionOrZoom()
    {
        var session = new SpreadsheetSession(new NeraWorkbook());
        session.Selection.SetActiveCell(new CellAddress(3, 3));
        session.View.SetSplitTopology(SpreadsheetSplitViewMode.Vertical, 200, null);
        session.View.SetWorksheetViewport(session.ActiveWorksheet, 0, 0, 1.5);
        await using var stream = new MemoryStream();
        var serializer = new NeraOpenXmlSpreadsheetSessionSerializer();
        await serializer.SaveSessionAsync(session, stream, new OpenXmlExportOptions());
        stream.Position = 0;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var part = document.WorkbookPart!.CustomXmlParts.Single(part => part.ContentType == NativeContentType);
            XDocument xml;
            using (var input = part.GetStream(FileMode.Open, FileAccess.Read)) xml = XDocument.Load(input);
            foreach (var worksheet in xml.Root!.Elements())
            {
                worksheet.Attribute("zoom")?.Remove();
                worksheet.Attribute("frozenRows")?.Remove();
                worksheet.Attribute("frozenColumns")?.Remove();
                worksheet.Elements().Where(element => element.Name.LocalName == "selection").Remove();
            }
            using var output = part.GetStream(FileMode.Create, FileAccess.Write);
            xml.Save(output);
        }
        stream.Position = 0;
        var loaded = await serializer.LoadSessionAsync(stream, new OpenXmlImportOptions());
        Assert.AreEqual(new CellAddress(3, 3), loaded.Selection.ActiveCell);
        Assert.AreEqual(1.5, loaded.View.WorksheetState.Zoom);
        Assert.AreEqual(SpreadsheetSplitViewMode.Vertical, loaded.View.SplitState.Mode);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task OpaqueSiblingRetentionShouldRequireBothImportAndExportOptIn(bool capture, bool preserve)
    {
        Assert.IsFalse(new OpenXmlImportOptions().PreserveUnknownParts);
        Assert.IsFalse(new OpenXmlExportOptions().PreserveUnknownParts);
        await using var source = await CreateStandardFixture();
        var serializer = new NeraOpenXmlSpreadsheetSessionSerializer();
        var session = await serializer.LoadSessionAsync(source, new OpenXmlImportOptions { PreserveUnknownParts = capture });
        Assert.AreEqual(new CellAddress(8, 7), session.Selection.ActiveCell);
        await using var result = new MemoryStream();
        await serializer.SaveSessionAsync(session, result, new OpenXmlExportOptions { PreserveUnknownParts = preserve });
        result.Position = 0;
        using var document = SpreadsheetDocument.Open(result, false);
        var views = FirstPart(document).Worksheet!.GetFirstChild<SheetViews>()!.Elements<SheetView>().ToArray();
        Assert.AreEqual(capture && preserve ? 2 : 1, views.Length);
        if (capture && preserve)
        {
            Assert.AreEqual(70U, views[0].ZoomScale!.Value);
            Assert.AreEqual("keep", views[1].GetAttribute("marker", TestNamespace).Value);
            Assert.IsNotNull(views[1].GetFirstChild<ExtensionList>());
        }
        Assert.AreEqual("H9", views[^1].Elements<Selection>().Single().ActiveCell!.Value);
    }

    private static async Task<MemoryStream> CreateStandardFixture()
    {
        var stream = new MemoryStream();
        await new NeraOpenXmlWorkbookSerializer().SaveAsync(new NeraWorkbook(), stream, new OpenXmlExportOptions());
        stream.Position = 0;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var workbook = document.WorkbookPart!.Workbook ?? throw new AssertFailedException("Expected workbook fixture markup.");
            workbook.GetFirstChild<BookViews>()?.Remove();
            workbook.AddChild(new BookViews(new WorkbookView { ActiveTab = 0U }, new WorkbookView { ActiveTab = 0U }), true);
            var part = FirstPart(document);
            part.Worksheet!.GetFirstChild<SheetViews>()?.Remove();
            if (part.Worksheet!.GetFirstChild<SheetProperties>() is null)
                part.Worksheet!.AddChild(new SheetProperties(), true);
            var first = new SheetView { WorkbookViewId = 0U, ZoomScale = 70U, ShowGridLines = false, TopLeftCell = "C3" };
            first.Append(new Selection { ActiveCell = "C3", SequenceOfReferences = new ListValue<StringValue> { InnerText = "C3" } });
            var second = new SheetView { WorkbookViewId = 1U, ZoomScale = 135U, ShowZeros = false, TopLeftCell = "D5" };
            second.AddNamespaceDeclaration("test", TestNamespace);
            second.MCAttributes = new MarkupCompatibilityAttributes { Ignorable = "test" };
            second.SetAttribute(new OpenXmlAttribute("test", "marker", TestNamespace, "keep"));
            second.Append(new Selection { ActiveCell = "H9", ActiveCellId = 1U,
                SequenceOfReferences = new ListValue<StringValue> { InnerText = "C5:D7 H9" } });
            second.Append(new ExtensionList { InnerXml = "<ext xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" uri=\"{41701995-DC4D-4E67-A662-34F3F2355814}\"><test:payload xmlns:test=\"urn:nera:test:preserved-views\" value=\"untouched\" /></ext>" });
            part.Worksheet!.AddChild(new SheetViews(first, second), true);
            part.Worksheet!.Save();
            workbook.Save();
        }
        stream.Position = 0;
        return stream;
    }

    private static WorksheetPart FirstPart(SpreadsheetDocument document)
    {
        var part = document.WorkbookPart!;
        var sheet = part.Workbook!.GetFirstChild<Sheets>()!.Elements<Sheet>().First();
        return (WorksheetPart)part.GetPartById(sheet.Id!.Value!);
    }

    private static void AssertState(SpreadsheetWorksheetViewState expected, SpreadsheetWorksheetViewState actual)
    {
        Assert.AreEqual(expected.Zoom, actual.Zoom);
        Assert.AreEqual(expected.SplitState, actual.SplitState);
        Assert.AreEqual(expected.FrozenRows, actual.FrozenRows);
        Assert.AreEqual(expected.FrozenColumns, actual.FrozenColumns);
        Assert.AreEqual(expected.Selection.ActiveCell, actual.Selection.ActiveCell);
        Assert.AreEqual(expected.Selection.AnchorCell, actual.Selection.AnchorCell);
        Assert.IsTrue(expected.Selection.Ranges.SequenceEqual(actual.Selection.Ranges));
    }
}
