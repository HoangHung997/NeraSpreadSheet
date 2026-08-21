using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class SpreadsheetTableFilterButtonGeometryTests
{
    [TestMethod]
    public void ReturnsOneButtonPerVisibleTableHeaderColumn()
    {
        var worksheet = new Worksheet("Sheet1");
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var table = new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(new CellAddress(0, 0), new CellAddress(3, 1)),
            [
                new SpreadsheetTableColumn(firstId, "Status"),
                new SpreadsheetTableColumn(secondId, "Amount"),
            ],
            autoFilter: new TableAutoFilter([
                new TableFilterColumn(
                    firstId,
                    [CellValue.FromText("Open")]),
            ]));
        worksheet.AddTable(table);
        var layout = CreateLayout();

        var buttons = SpreadsheetTableFilterButtonGeometry.GetVisibleButtons(
            WorksheetSnapshot.Capture(worksheet),
            layout,
            new SpreadsheetRenderTheme());

        Assert.AreEqual(2, buttons.Count);
        Assert.IsTrue(buttons.Single(button =>
            button.ColumnId == firstId).IsFiltered);
        Assert.IsFalse(buttons.Single(button =>
            button.ColumnId == secondId).IsFiltered);
        Assert.AreEqual(new CellAddress(0, 1), buttons[1].HeaderCell);
    }

    [TestMethod]
    public void HitTestUsesButtonBoundsNotWholeHeaderCell()
    {
        var worksheet = new Worksheet("Sheet1");
        var columnId = Guid.NewGuid();
        worksheet.AddTable(new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(new CellAddress(0, 0), new CellAddress(2, 0)),
            [new SpreadsheetTableColumn(columnId, "Status")]));
        var snapshot = WorksheetSnapshot.Capture(worksheet);
        var layout = CreateLayout();
        var button = SpreadsheetTableFilterButtonGeometry.GetVisibleButtons(
            snapshot,
            layout)[0];

        Assert.IsTrue(SpreadsheetTableFilterButtonGeometry.TryHitTest(
            snapshot,
            layout,
            button.Bounds.X + 1d,
            button.Bounds.Y + 1d,
            theme: null,
            out var hit));
        Assert.AreEqual(columnId, hit.ColumnId);
        Assert.IsFalse(SpreadsheetTableFilterButtonGeometry.TryHitTest(
            snapshot,
            layout,
            4d,
            4d,
            theme: null,
            out _));
    }

    [TestMethod]
    public void HiddenThemeAndHeaderOutsideViewportProduceNoButtons()
    {
        var worksheet = new Worksheet("Sheet1");
        worksheet.AddTable(new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(new CellAddress(5, 0), new CellAddress(8, 0)),
            [new SpreadsheetTableColumn(Guid.NewGuid(), "Status")]));
        var layout = CreateLayout();

        Assert.AreEqual(
            0,
            SpreadsheetTableFilterButtonGeometry.GetVisibleButtons(
                WorksheetSnapshot.Capture(worksheet),
                layout,
                new SpreadsheetRenderTheme
                {
                    ShowTableFilterButtons = false,
                }).Count);
        Assert.AreEqual(
            0,
            SpreadsheetTableFilterButtonGeometry.GetVisibleButtons(
                WorksheetSnapshot.Capture(worksheet),
                layout).Count);
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
