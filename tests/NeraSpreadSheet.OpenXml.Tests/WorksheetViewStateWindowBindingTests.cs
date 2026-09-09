using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraWorkbook = NeraSpreadSheet.Core.Workbook;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class WorksheetViewStateWindowBindingTests
{
    [TestMethod]
    public async Task ReversedSheetViewOrderShouldStillImportOneWorkbookWindow()
    {
        await using var source = await CreateFixture();

        var session = await new NeraOpenXmlSpreadsheetSessionSerializer()
            .LoadSessionAsync(source, new OpenXmlImportOptions());

        var first = session.Workbook.Worksheets[0];
        var second = session.Workbook.Worksheets[1];
        Assert.AreSame(second, session.ActiveWorksheet);
        Assert.AreEqual(1.3d, session.View.GetWorksheetState(first).Zoom);
        Assert.AreEqual(new CellAddress(2, 2), session.View.GetWorksheetState(first).Selection.ActiveCell);
        Assert.AreEqual(1.6d, session.View.GetWorksheetState(second).Zoom);
        Assert.AreEqual(new CellAddress(5, 5), session.Selection.ActiveCell);
        Assert.AreEqual(80d, session.View.WorksheetState.OffsetX);
        Assert.AreEqual(20d, session.View.WorksheetState.OffsetY);
    }

    [TestMethod]
    public async Task SavingChosenWindowShouldPreserveOtherWindowOnEverySheet()
    {
        await using var source = await CreateFixture();
        string firstSibling;
        string secondSibling;
        using (var document = SpreadsheetDocument.Open(source, false))
        {
            firstSibling = View(document, 0, 0U).OuterXml;
            secondSibling = View(document, 1, 0U).OuterXml;
        }
        source.Position = 0;
        var serializer = new NeraOpenXmlSpreadsheetSessionSerializer();
        var session = await serializer.LoadSessionAsync(source, new OpenXmlImportOptions());
        session.Selection.SetActiveCell(new CellAddress(8, 8));
        session.View.SetWorksheetViewport(session.ActiveWorksheet, 41.25d, 82.5d, 1.75d);
        await using var result = new MemoryStream();

        await serializer.SaveSessionAsync(session, result, new OpenXmlExportOptions());

        result.Position = 0;
        using var saved = SpreadsheetDocument.Open(result, false);
        Assert.AreEqual(firstSibling, View(saved, 0, 0U).OuterXml);
        Assert.AreEqual(secondSibling, View(saved, 1, 0U).OuterXml);
        Assert.AreEqual(175U, View(saved, 1, 1U).ZoomScale!.Value);
        Assert.AreEqual("I9", View(saved, 1, 1U).Elements<Selection>().Single().ActiveCell!.Value);
        var windows = saved.WorkbookPart!.Workbook.GetFirstChild<BookViews>()!.Elements<WorkbookView>().ToArray();
        Assert.AreEqual(0U, windows[0].ActiveTab!.Value);
        Assert.AreEqual(1U, windows[1].ActiveTab!.Value);
        AssertSchema(saved);
    }

    [TestMethod]
    public async Task MissingChosenViewShouldNotBorrowAnotherWindowsSelection()
    {
        await using var source = await CreateFixture(includeSecondWindowOnSecondSheet: false);

        var session = await new NeraOpenXmlSpreadsheetSessionSerializer()
            .LoadSessionAsync(source, new OpenXmlImportOptions());

        Assert.AreSame(session.Workbook.Worksheets[1], session.ActiveWorksheet);
        Assert.AreEqual(1d, session.View.WorksheetState.Zoom);
        Assert.AreEqual(default(CellAddress), session.Selection.ActiveCell);
        Assert.AreEqual(0d, session.View.WorksheetState.OffsetX);
        Assert.AreEqual(0d, session.View.WorksheetState.OffsetY);
    }

    [TestMethod]
    public async Task EditingMissingChosenViewShouldCreateMatchingIdWithoutOverwritingSibling()
    {
        await using var source = await CreateFixture(includeSecondWindowOnSecondSheet: false);
        string sibling;
        using (var document = SpreadsheetDocument.Open(source, false)) sibling = View(document, 1, 0U).OuterXml;
        source.Position = 0;
        var serializer = new NeraOpenXmlSpreadsheetSessionSerializer();
        var session = await serializer.LoadSessionAsync(source, new OpenXmlImportOptions());
        session.Selection.SetActiveCell(new CellAddress(3, 4));
        session.View.SetWorksheetViewport(session.ActiveWorksheet, 25.5d, 60.25d, 1.5d);
        await using var result = new MemoryStream();

        await serializer.SaveSessionAsync(session, result, new OpenXmlExportOptions());

        result.Position = 0;
        using var saved = SpreadsheetDocument.Open(result, false);
        Assert.AreEqual(sibling, View(saved, 1, 0U).OuterXml);
        var views = Part(saved, 1).Worksheet.GetFirstChild<SheetViews>()!.Elements<SheetView>().ToArray();
        Assert.AreEqual(2, views.Length);
        Assert.AreEqual(1U, views[1].WorkbookViewId!.Value);
        Assert.AreEqual(150U, views[1].ZoomScale!.Value);
        Assert.AreEqual("E4", views[1].Elements<Selection>().Single().ActiveCell!.Value);
        AssertSchema(saved);
    }

    [TestMethod]
    public async Task SplitPaneImportShouldUseChosenWindowRatherThanLastSheetView()
    {
        await using var source = await CreateFixture();
        using (var document = SpreadsheetDocument.Open(source, true))
        {
            var view = View(document, 1, 1U);
            view.AddChild(new Pane
            {
                State = PaneStateValues.Split,
                HorizontalSplit = 2250d,
                ActivePane = PaneValues.TopRight,
                TopLeftCell = "D4",
            }, true);
            view.Elements<Selection>().Single().Pane = PaneValues.TopRight;
            Part(document, 1).Worksheet.Save();
        }
        source.Position = 0;

        var session = await new NeraOpenXmlSpreadsheetSessionSerializer()
            .LoadSessionAsync(source, new OpenXmlImportOptions());

        Assert.AreEqual(SpreadsheetSplitViewMode.Vertical, session.View.SplitState.Mode);
        Assert.AreEqual(150d, session.View.SplitState.SplitX);
        Assert.AreEqual(SpreadsheetSplitViewPane.TopRight, session.View.SplitState.ActivePane);
        Assert.AreEqual(new SpreadsheetPaneScrollOffset(240d, 0d), session.View.SplitState.TopRightScroll);
        Assert.AreEqual(new CellAddress(5, 5), session.Selection.ActiveCell);
        Assert.AreEqual(1.6d, session.View.WorksheetState.Zoom);
    }

    [TestMethod]
    public async Task TwoRoundTripsShouldKeepChosenWindowAndIndependentExactOffsets()
    {
        await using var source = await CreateFixture();
        var serializer = new NeraOpenXmlSpreadsheetSessionSerializer();
        var session = await serializer.LoadSessionAsync(source, new OpenXmlImportOptions());
        session.View.SetWorksheetViewport(session.Workbook.Worksheets[0], 17.125d, 23.25d, 1.325d);
        session.View.SetWorksheetViewport(session.Workbook.Worksheets[1], 61.375d, 92.625d, 1.675d);
        await using var once = new MemoryStream();
        await serializer.SaveSessionAsync(session, once, new OpenXmlExportOptions());
        once.Position = 0;
        var firstLoad = await serializer.LoadSessionAsync(once, new OpenXmlImportOptions());
        await using var twice = new MemoryStream();
        await serializer.SaveSessionAsync(firstLoad, twice, new OpenXmlExportOptions());
        twice.Position = 0;

        var secondLoad = await serializer.LoadSessionAsync(twice, new OpenXmlImportOptions());

        Assert.AreSame(secondLoad.Workbook.Worksheets[1], secondLoad.ActiveWorksheet);
        var first = secondLoad.View.GetWorksheetState(secondLoad.Workbook.Worksheets[0]);
        var second = secondLoad.View.GetWorksheetState(secondLoad.Workbook.Worksheets[1]);
        Assert.AreEqual(1.325d, first.Zoom);
        Assert.AreEqual(17.125d, first.OffsetX);
        Assert.AreEqual(23.25d, first.OffsetY);
        Assert.AreEqual(new CellAddress(2, 2), first.Selection.ActiveCell);
        Assert.AreEqual(1.675d, second.Zoom);
        Assert.AreEqual(61.375d, second.OffsetX);
        Assert.AreEqual(92.625d, second.OffsetY);
        Assert.AreEqual(new CellAddress(5, 5), second.Selection.ActiveCell);
        twice.Position = 0;
        using var saved = SpreadsheetDocument.Open(twice, false);
        Assert.AreEqual(70U, View(saved, 0, 0U).ZoomScale!.Value);
        Assert.AreEqual(80U, View(saved, 1, 0U).ZoomScale!.Value);
        AssertSchema(saved);
    }

    [TestMethod]
    public async Task UnboundWorkbookViewIdShouldBeRejectedDuringLoad()
    {
        await using var source = await CreateFixture();
        using (var document = SpreadsheetDocument.Open(source, true))
        {
            View(document, 0, 1U).WorkbookViewId = 7U;
            Part(document, 0).Worksheet.Save();
        }
        source.Position = 0;

        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
            await new NeraOpenXmlSpreadsheetSessionSerializer().LoadSessionAsync(source, new OpenXmlImportOptions()));
    }

    [TestMethod]
    public async Task DuplicateSheetViewsCollectionsShouldBeRejectedDuringLoad()
    {
        await using var source = await CreateFixture();
        using (var document = SpreadsheetDocument.Open(source, true))
        {
            Part(document, 1).Worksheet.Append(new SheetViews(CreateView(1U, 125U, "A1")));
            Part(document, 1).Worksheet.Save();
        }
        source.Position = 0;

        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
            await new NeraOpenXmlSpreadsheetSessionSerializer().LoadSessionAsync(source, new OpenXmlImportOptions()));
    }

    private static async Task<MemoryStream> CreateFixture(bool includeSecondWindowOnSecondSheet = true)
    {
        var workbook = new NeraWorkbook();
        workbook.Worksheets[0].SetValue(default, "first");
        workbook.AddWorksheet("Second").SetValue(default, "second");
        var stream = new MemoryStream();
        await new NeraOpenXmlWorkbookSerializer().SaveAsync(workbook, stream, new OpenXmlExportOptions());
        stream.Position = 0;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var xml = document.WorkbookPart!.Workbook;
            xml.GetFirstChild<BookViews>()?.Remove();
            xml.AddChild(new BookViews(new WorkbookView { ActiveTab = 0U }, new WorkbookView { ActiveTab = 1U }), true);
            var first = Part(document, 0);
            var second = Part(document, 1);
            first.Worksheet.GetFirstChild<SheetViews>()?.Remove();
            second.Worksheet.GetFirstChild<SheetViews>()?.Remove();
            first.Worksheet.AddChild(new SheetViews(CreateView(0U, 70U, "B2"), CreateView(1U, 130U, "C3")), true);
            // Deliberately use the opposite order in the second sheet.
            var secondViews = new SheetViews();
            if (includeSecondWindowOnSecondSheet) secondViews.Append(CreateView(1U, 160U, "F6"));
            secondViews.Append(CreateView(0U, 80U, "K11"));
            second.Worksheet.AddChild(secondViews, true);
            first.Worksheet.Save();
            second.Worksheet.Save();
            xml.Save();
        }
        stream.Position = 0;
        return stream;
    }

    private static SheetView CreateView(uint id, uint zoom, string active)
    {
        var view = new SheetView { WorkbookViewId = id, ZoomScale = zoom, TopLeftCell = "B2", ShowZeros = false };
        view.Append(new Selection
        {
            ActiveCell = active,
            ActiveCellId = 0U,
            SequenceOfReferences = new ListValue<StringValue> { InnerText = active },
        });
        return view;
    }

    private static WorksheetPart Part(SpreadsheetDocument document, int index)
    {
        var workbookPart = document.WorkbookPart!;
        var sheet = workbookPart.Workbook.GetFirstChild<Sheets>()!.Elements<Sheet>().ElementAt(index);
        return (WorksheetPart)workbookPart.GetPartById(sheet.Id!.Value!);
    }

    private static SheetView View(SpreadsheetDocument document, int sheetIndex, uint viewId) =>
        Part(document, sheetIndex).Worksheet.GetFirstChild<SheetViews>()!.Elements<SheetView>()
            .Single(view => view.WorkbookViewId!.Value == viewId);

    private static void AssertSchema(SpreadsheetDocument document)
    {
        var errors = new OpenXmlValidator().Validate(document).ToArray();
        Assert.AreEqual(0, errors.Length, string.Join("; ", errors.Select(error => error.Description)));
    }
}
