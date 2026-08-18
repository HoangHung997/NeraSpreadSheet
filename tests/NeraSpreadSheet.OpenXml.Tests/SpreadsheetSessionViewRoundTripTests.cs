using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraWorkbook = NeraSpreadSheet.Core.Workbook;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class SpreadsheetSessionViewRoundTripTests
{
    private const string NeraViewStateContentType = "application/vnd.neraspreadsheet.view-state+xml";

    [TestMethod]
    public async Task NativeRoundTripPreservesPerWorksheetTopologyActivePaneAndAllOffsets()
    {
        var workbook = new NeraWorkbook();
        var first = workbook.Worksheets[0];
        first.SetValue(default, "first");
        first.Dimensions.SetColumnWidth(0, 120d);
        first.Dimensions.SetRowHeight(0, 32d);
        var second = workbook.AddWorksheet("Second");
        second.SetValue(default, "second");
        var session = new SpreadsheetSession(workbook);
        var firstState = new SpreadsheetSplitViewState(
            SpreadsheetSplitViewMode.Both,
            280.5d,
            170.25d,
            SpreadsheetSplitViewPane.BottomRight,
            topLeftScroll: new SpreadsheetPaneScrollOffset(11.25d, 21.5d),
            topRightScroll: new SpreadsheetPaneScrollOffset(111.75d, 31.125d),
            bottomLeftScroll: new SpreadsheetPaneScrollOffset(41.5d, 221.25d),
            bottomRightScroll: new SpreadsheetPaneScrollOffset(141.875d, 261.625d));
        var secondState = new SpreadsheetSplitViewState(
            SpreadsheetSplitViewMode.Horizontal,
            null,
            190.75d,
            SpreadsheetSplitViewPane.BottomLeft,
            topLeftScroll: new SpreadsheetPaneScrollOffset(7d, 8d),
            bottomLeftScroll: new SpreadsheetPaneScrollOffset(17.5d, 92.25d));
        session.View.SetSplitState(first, firstState);
        session.View.SetSplitState(second, secondState);
        var serializer = new NeraOpenXmlSpreadsheetSessionSerializer();
        await using var stream = new MemoryStream();

        await serializer.SaveSessionAsync(session, stream, new OpenXmlExportOptions());

        stream.Position = 0L;
        using (var document = SpreadsheetDocument.Open(stream, false))
        {
            var workbookPart = document.WorkbookPart;
            Assert.IsNotNull(workbookPart);
            var sheets = workbookPart.Workbook.GetFirstChild<Sheets>()?.Elements<Sheet>().ToArray();
            Assert.IsNotNull(sheets);
            Assert.AreEqual(2, sheets.Length);
            var firstPart = (WorksheetPart)workbookPart.GetPartById(sheets[0].Id!.Value!);
            var pane = firstPart.Worksheet
                .GetFirstChild<SheetViews>()?
                .Elements<SheetView>()
                .Single()
                .GetFirstChild<Pane>();
            Assert.IsNotNull(pane);
            Assert.AreEqual(PaneStateValues.Split, pane.State?.Value);
            Assert.AreEqual(PaneValues.BottomRight, pane.ActivePane?.Value);
            Assert.AreEqual(280.5d * 15d, pane.HorizontalSplit?.Value ?? 0d, 1e-9);
            Assert.AreEqual(170.25d * 15d, pane.VerticalSplit?.Value ?? 0d, 1e-9);
            Assert.IsFalse(string.IsNullOrWhiteSpace(pane.TopLeftCell?.Value));
            Assert.IsTrue(workbookPart.CustomXmlParts.Any(part => string.Equals(
                part.ContentType,
                NeraViewStateContentType,
                StringComparison.OrdinalIgnoreCase)));
        }

        stream.Position = 0L;
        var loaded = await serializer.LoadSessionAsync(
            stream,
            new OpenXmlImportOptions());

        Assert.AreEqual("first", loaded.Workbook.Worksheets[0].GetValue(default));
        Assert.AreEqual("second", loaded.Workbook.Worksheets[1].GetValue(default));
        Assert.AreEqual(firstState, loaded.View.GetSplitState(loaded.Workbook.Worksheets[0]));
        Assert.AreEqual(secondState, loaded.View.GetSplitState(loaded.Workbook.Worksheets[1]));
    }

    [TestMethod]
    public async Task StandardSplitPaneWithoutNativeMetadataImportsCompatibleViewState()
    {
        var workbook = new NeraWorkbook();
        workbook.Worksheets[0].SetValue(new CellAddress(20, 10), "extent");
        var workbookSerializer = new NeraOpenXmlWorkbookSerializer();
        await using var stream = new MemoryStream();
        await workbookSerializer.SaveAsync(workbook, stream, new OpenXmlExportOptions());

        stream.Position = 0L;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var workbookPart = document.WorkbookPart;
            Assert.IsNotNull(workbookPart);
            var sheet = workbookPart.Workbook.GetFirstChild<Sheets>()?.Elements<Sheet>().Single();
            Assert.IsNotNull(sheet);
            var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!.Value!);
            var pane = new Pane
            {
                State = PaneStateValues.Split,
                ActivePane = PaneValues.BottomRight,
                HorizontalSplit = 1500d,
                VerticalSplit = 900d,
                TopLeftCell = "C4",
            };
            var sheetView = new SheetView { WorkbookViewId = 0U };
            sheetView.Append(pane);
            worksheetPart.Worksheet.PrependChild(new SheetViews(sheetView));
            worksheetPart.Worksheet.Save();
        }

        stream.Position = 0L;
        var sessionSerializer = new NeraOpenXmlSpreadsheetSessionSerializer();
        var loaded = await sessionSerializer.LoadSessionAsync(
            stream,
            new OpenXmlImportOptions());
        var state = loaded.View.SplitState;

        Assert.AreEqual(SpreadsheetSplitViewMode.Both, state.Mode);
        Assert.AreEqual(100d, state.SplitX);
        Assert.AreEqual(60d, state.SplitY);
        Assert.AreEqual(SpreadsheetSplitViewPane.BottomRight, state.ActivePane);
        Assert.AreEqual(
            new SpreadsheetPaneScrollOffset(160d, 0d),
            state.TopRightScroll);
        Assert.AreEqual(
            new SpreadsheetPaneScrollOffset(0d, 60d),
            state.BottomLeftScroll);
        Assert.AreEqual(
            new SpreadsheetPaneScrollOffset(160d, 60d),
            state.BottomRightScroll);
    }

    [TestMethod]
    public async Task DefaultSessionDoesNotEmitNativeOrStandardSplitMetadata()
    {
        var session = new SpreadsheetSession(new NeraWorkbook());
        var serializer = new NeraOpenXmlSpreadsheetSessionSerializer();
        await using var stream = new MemoryStream();

        await serializer.SaveSessionAsync(session, stream, new OpenXmlExportOptions());

        stream.Position = 0L;
        using var document = SpreadsheetDocument.Open(stream, false);
        var workbookPart = document.WorkbookPart;
        Assert.IsNotNull(workbookPart);
        Assert.IsFalse(workbookPart.CustomXmlParts.Any(part => string.Equals(
            part.ContentType,
            NeraViewStateContentType,
            StringComparison.OrdinalIgnoreCase)));
        var sheet = workbookPart.Workbook.GetFirstChild<Sheets>()?.Elements<Sheet>().Single();
        Assert.IsNotNull(sheet);
        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!.Value!);
        Assert.IsNull(worksheetPart.Worksheet.GetFirstChild<SheetViews>());
    }
}
