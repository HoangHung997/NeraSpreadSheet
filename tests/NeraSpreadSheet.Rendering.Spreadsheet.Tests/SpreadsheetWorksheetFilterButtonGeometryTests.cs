using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class SpreadsheetWorksheetFilterButtonGeometryTests
{
    [TestMethod]
    public void ReturnsOneButtonPerVisibleDirectFilterColumn()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetAutoFilter(new WorksheetAutoFilter(
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(4, 1)),
            [
                new WorksheetAutoFilterColumn(
                    0,
                    [CellValue.FromText("Open")]),
            ]));
        var layout = CreateLayout();

        var buttons =
            SpreadsheetWorksheetFilterButtonGeometry.GetVisibleButtons(
                WorksheetSnapshot.Capture(worksheet),
                layout,
                new SpreadsheetRenderTheme());

        Assert.AreEqual(2, buttons.Count);
        Assert.IsTrue(buttons[0].IsFiltered);
        Assert.IsFalse(buttons[1].IsFiltered);
        Assert.AreEqual(0, buttons[0].ColumnOffset);
        Assert.AreEqual(1, buttons[1].WorksheetColumnIndex);
        Assert.AreEqual(
            new CellAddress(0, 1),
            buttons[1].HeaderCell);
    }

    [TestMethod]
    public void HitTestUsesOnlyDirectFilterButtonBounds()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetAutoFilter(new WorksheetAutoFilter(
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(4, 0))));
        var snapshot = WorksheetSnapshot.Capture(worksheet);
        var layout = CreateLayout();
        var button =
            SpreadsheetWorksheetFilterButtonGeometry.GetVisibleButtons(
                snapshot,
                layout)[0];

        Assert.IsTrue(
            SpreadsheetWorksheetFilterButtonGeometry.TryHitTest(
                snapshot,
                layout,
                button.Bounds.X + 1d,
                button.Bounds.Y + 1d,
                theme: null,
                out var hit));
        Assert.AreEqual(0, hit.ColumnOffset);
        Assert.IsFalse(
            SpreadsheetWorksheetFilterButtonGeometry.TryHitTest(
                snapshot,
                layout,
                4d,
                4d,
                theme: null,
                out _));
    }

    [TestMethod]
    public void MissingHeaderInvisibleRowAndDisabledThemeReturnNoButtons()
    {
        var workbook = new Workbook();
        var noHeader = workbook.Worksheets[0];
        noHeader.SetAutoFilter(new WorksheetAutoFilter(
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(4, 0)),
            hasHeaderRow: false));
        var outside = workbook.AddWorksheet("Outside");
        outside.SetAutoFilter(new WorksheetAutoFilter(
            new CellRange(
                new CellAddress(5, 0),
                new CellAddress(8, 0))));
        var layout = CreateLayout();

        Assert.AreEqual(
            0,
            SpreadsheetWorksheetFilterButtonGeometry.GetVisibleButtons(
                WorksheetSnapshot.Capture(noHeader),
                layout).Count);
        Assert.AreEqual(
            0,
            SpreadsheetWorksheetFilterButtonGeometry.GetVisibleButtons(
                WorksheetSnapshot.Capture(outside),
                layout).Count);

        var visible = workbook.AddWorksheet("Visible");
        visible.SetAutoFilter(new WorksheetAutoFilter(
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(4, 0))));
        Assert.AreEqual(
            0,
            SpreadsheetWorksheetFilterButtonGeometry.GetVisibleButtons(
                WorksheetSnapshot.Capture(visible),
                layout,
                new SpreadsheetRenderTheme
                {
                    ShowTableFilterButtons = false,
                }).Count);
    }

    private static ViewportLayout CreateLayout() =>
        new(
            0d,
            0d,
            new SizeD(160d, 80d),
            160d,
            80d,
            0d,
            0d,
            [
                new AxisSlot(0, 0d, 20d),
                new AxisSlot(1, 20d, 20d),
                new AxisSlot(2, 40d, 20d),
                new AxisSlot(3, 60d, 20d),
            ],
            [
                new AxisSlot(0, 0d, 80d),
                new AxisSlot(1, 80d, 80d),
            ]);
}
