using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Viewport.Tests;

[TestClass]
public sealed class SpreadsheetViewportEngineTests
{
    [TestMethod]
    public void ComposePreservesFractionalPixelOffset()
    {
        var workbook = new Workbook();
        workbook.Worksheets[0].SetValue(default, "Nera");
        var engine = new SpreadsheetViewportEngine(new SpreadsheetSession(workbook));

        var frame = engine.Compose(13.25d, 7.75d, 320d, 180d, 0d);

        Assert.AreEqual(13.25d, frame.Layout.ScrollX, 1e-9);
        Assert.AreEqual(7.75d, frame.Layout.ScrollY, 1e-9);
        Assert.IsTrue(EnumerateCommands(frame.DisplayList)
            .OfType<DrawTextCommand>()
            .Any(command => command.Text == "Nera"));
    }

    [TestMethod]
    public void HitTestAccountsForScrollAndDimensionOverrides()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.Dimensions.SetRowHeight(0, 40d);
        sheet.Dimensions.SetColumnWidth(0, 100d);
        var engine = new SpreadsheetViewportEngine(new SpreadsheetSession(workbook));

        var hit = engine.TryHitTest(10d, 10d, 100d, 40d, out var address);

        Assert.IsTrue(hit);
        Assert.AreEqual(new CellAddress(1, 1), address);
    }

    [TestMethod]
    public void ContentExtentReflectsSparseDimensionOverrides()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        var before = new SpreadsheetViewportEngine(new SpreadsheetSession(workbook)).GetContentExtent();
        sheet.Dimensions.SetColumnWidth(0, sheet.Dimensions.DefaultColumnWidth + 25d);
        var engine = new SpreadsheetViewportEngine(new SpreadsheetSession(workbook));

        var after = engine.GetContentExtent();

        Assert.AreEqual(before.Width + 25d, after.Width, 1e-9);
    }

    [TestMethod]
    public void AdaptiveNavigationExtentExpandsForActiveCellAndContractsOnReturn()
    {
        var workbook = new Workbook();
        var engine = new SpreadsheetViewportEngine(
            new SpreadsheetSession(workbook));
        var viewport = new SizeD(320d, 180d);

        var initial = engine.GetAdaptiveNavigationExtent(
            new CellAddress(0, 0),
            viewport);
        var expanded = engine.GetAdaptiveNavigationExtent(
            new CellAddress(50, 20),
            viewport);
        var returned = engine.GetAdaptiveNavigationExtent(
            new CellAddress(0, 0),
            viewport);
        workbook.Worksheets[0].SetValue(new CellAddress(50, 20), "kept");
        var retainedByData = engine.GetAdaptiveNavigationExtent(
            new CellAddress(0, 0),
            viewport);
        workbook.Worksheets[0].SetValue(new CellAddress(50, 20), null);
        var contractedAfterClear = engine.GetAdaptiveNavigationExtent(
            new CellAddress(0, 0),
            viewport);

        Assert.AreEqual(21d * 80d, initial.Width, 1e-9);
        Assert.AreEqual(101d * 20d, initial.Height, 1e-9);
        Assert.AreEqual(41d * 80d, expanded.Width, 1e-9);
        Assert.AreEqual(151d * 20d, expanded.Height, 1e-9);
        Assert.AreEqual(initial, returned);
        Assert.AreEqual(expanded, retainedByData);
        Assert.AreEqual(initial, contractedAfterClear);
    }

    [TestMethod]
    public void AdaptiveNavigationExtentIncludesSparseWorksheetArtifacts()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(9, 5), "used");
        sheet.MergeCells(new CellRange(
            new CellAddress(11, 1),
            new CellAddress(12, 7)));
        sheet.Dimensions.SetColumnWidth(10, 120d);
        var engine = new SpreadsheetViewportEngine(
            new SpreadsheetSession(workbook));

        var extent = engine.GetAdaptiveNavigationExtent(
            new CellAddress(0, 0),
            new SizeD(0d, 0d),
            default,
            trailingRowCount: 0,
            trailingColumnCount: 0);

        Assert.AreEqual(920d, extent.Width, 1e-9);
        Assert.AreEqual(260d, extent.Height, 1e-9);
    }

    [TestMethod]
    public void AdaptiveNavigationExtentRetainsScrolledViewportWithoutCompoundingTail()
    {
        var workbook = new Workbook();
        var engine = new SpreadsheetViewportEngine(
            new SpreadsheetSession(workbook));

        var extent = engine.GetAdaptiveNavigationExtent(
            new CellAddress(0, 0),
            new SizeD(320d, 180d),
            new PointD(2400d, 3200d));

        Assert.AreEqual(2720d, extent.Width, 1e-9);
        Assert.AreEqual(3380d, extent.Height, 1e-9);
    }

    [TestMethod]
    public void FilteredTableRowsAreRemovedFromLayoutHitTestingAndExtent()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        var statusColumnId = Guid.NewGuid();
        sheet.SetValue(new CellAddress(1, 1), "Open");
        sheet.SetValue(new CellAddress(2, 1), "Closed");
        sheet.SetValue(new CellAddress(3, 1), "Open");
        sheet.AddTable(new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(3, 1)),
            [
                new SpreadsheetTableColumn(Guid.NewGuid(), "Item"),
                new SpreadsheetTableColumn(statusColumnId, "Status"),
            ],
            autoFilter: new TableAutoFilter([
                new TableFilterColumn(
                    statusColumnId,
                    [CellValue.FromText("Open")]),
            ])));
        var engine = new SpreadsheetViewportEngine(
            new SpreadsheetSession(workbook));
        var rowHeight = sheet.Dimensions.DefaultRowHeight;

        var frame = engine.Compose(
            0d,
            0d,
            240d,
            rowHeight * 5d,
            overscan: 0d);

        Assert.IsFalse(frame.Layout.Rows.Any(static row => row.Index == 2));
        Assert.AreEqual(
            (SpreadsheetLimits.MaxRows - 1d) * rowHeight,
            frame.Layout.ContentHeight,
            1e-6);
        Assert.IsTrue(engine.TryHitTestRow(
            rowHeight * 2.25d,
            0d,
            out var hitRow));
        Assert.AreEqual(3, hitRow);

        sheet.SetValue(new CellAddress(2, 1), "Open");
        var refreshed = engine.Compose(
            0d,
            0d,
            240d,
            rowHeight * 5d,
            overscan: 0d);

        Assert.IsTrue(refreshed.Layout.Rows.Any(static row => row.Index == 2));
        Assert.AreEqual(
            SpreadsheetLimits.MaxRows * rowHeight,
            refreshed.Layout.ContentHeight,
            1e-6);
    }

    private static IEnumerable<RenderCommand> EnumerateCommands(DisplayList displayList)
    {
        foreach (var command in displayList.Commands)
        {
            yield return command;
            if (command is not DrawDisplayListCommand nested)
            {
                continue;
            }
            foreach (var child in EnumerateCommands(nested.DisplayList))
            {
                yield return child;
            }
        }
    }
}
