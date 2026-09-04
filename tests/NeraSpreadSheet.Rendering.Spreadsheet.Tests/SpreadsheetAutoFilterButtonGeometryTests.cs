using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class SpreadsheetAutoFilterButtonGeometryTests
{
    [TestMethod]
    public void CombinesTableAndWorksheetFilterButtons()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var tableId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        var amountId = Guid.NewGuid();
        worksheet.AddTable(new SpreadsheetTable(
            tableId,
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(3, 1)),
            [
                new SpreadsheetTableColumn(statusId, "Status"),
                new SpreadsheetTableColumn(amountId, "Amount"),
            ],
            autoFilter: new TableAutoFilter([
                new TableFilterColumn(
                    statusId,
                    [CellValue.FromText("Open")]),
            ])));
        worksheet.SetAutoFilter(new WorksheetAutoFilter(
            new CellRange(
                new CellAddress(0, 3),
                new CellAddress(3, 4)),
            [new WorksheetAutoFilterColumn(
                1,
                [CellValue.FromText("A")])]));

        var buttons = SpreadsheetAutoFilterButtonGeometry
            .GetVisibleButtons(
                WorksheetSnapshot.Capture(worksheet),
                CreateLayout());

        Assert.AreEqual(4, buttons.Count);
        Assert.AreEqual(
            2,
            buttons.Count(button =>
                button.OwnerKind ==
                SpreadsheetAutoFilterButtonOwnerKind.Table));
        Assert.AreEqual(
            2,
            buttons.Count(button =>
                button.OwnerKind ==
                SpreadsheetAutoFilterButtonOwnerKind.Worksheet));
        var tableButton = buttons.Single(button =>
            button.TableColumnId == statusId);
        Assert.AreEqual(tableId, tableButton.TableId);
        Assert.AreEqual(new CellRange(
            new CellAddress(0, 0),
            new CellAddress(3, 1)),
            tableButton.FilterRange);
        Assert.IsTrue(tableButton.IsFiltered);
        var worksheetButton = buttons.Single(button =>
            button.OwnerKind ==
                SpreadsheetAutoFilterButtonOwnerKind.Worksheet &&
            button.ColumnOffset == 1);
        Assert.AreEqual(4, worksheetButton.WorksheetColumnIndex);
        Assert.IsTrue(worksheetButton.IsFiltered);
    }

    [TestMethod]
    public void HitTestReturnsTheCorrectOwner()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetAutoFilter(new WorksheetAutoFilter(
            new CellRange(
                new CellAddress(0, 2),
                new CellAddress(3, 2))));
        var snapshot = WorksheetSnapshot.Capture(worksheet);
        var layout = CreateLayout();
        var button = SpreadsheetAutoFilterButtonGeometry
            .GetVisibleButtons(snapshot, layout)
            .Single();

        Assert.IsTrue(SpreadsheetAutoFilterButtonGeometry.TryHitTest(
            snapshot,
            layout,
            button.Bounds.X + 1d,
            button.Bounds.Y + 1d,
            theme: null,
            out var hit));
        Assert.AreEqual(
            SpreadsheetAutoFilterButtonOwnerKind.Worksheet,
            hit.OwnerKind);
        Assert.IsFalse(SpreadsheetAutoFilterButtonGeometry.TryHitTest(
            snapshot,
            layout,
            1d,
            1d,
            theme: null,
            out _));
    }

    [TestMethod]
    public void MetadataOverloadMatchesSnapshotWithoutCapturingCellPayload()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var table = new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(new CellAddress(0, 0), new CellAddress(3, 1)),
            [
                new SpreadsheetTableColumn(Guid.NewGuid(), "Status"),
                new SpreadsheetTableColumn(Guid.NewGuid(), "Amount"),
            ]);
        worksheet.AddTable(table);
        worksheet.SetAutoFilter(new WorksheetAutoFilter(
            new CellRange(new CellAddress(0, 3), new CellAddress(3, 4))));
        var layout = CreateLayout();

        var fromSnapshot = SpreadsheetAutoFilterButtonGeometry.GetVisibleButtons(
            WorksheetSnapshot.Capture(worksheet),
            layout);
        var fromMetadata = SpreadsheetAutoFilterButtonGeometry.GetVisibleButtons(
            worksheet.Tables,
            worksheet.AutoFilter,
            layout);

        CollectionAssert.AreEqual(fromSnapshot.ToArray(), fromMetadata.ToArray());
    }

    [TestMethod]
    public void HeaderButtonsExposeFilteredSortedAndCombinedStates()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var tableId = Guid.NewGuid();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        worksheet.AddTable(new SpreadsheetTable(
            tableId,
            "Sales",
            new CellRange(new CellAddress(0, 0), new CellAddress(3, 1)),
            [
                new SpreadsheetTableColumn(firstId, "Status"),
                new SpreadsheetTableColumn(secondId, "Amount"),
            ],
            autoFilter: new TableAutoFilter(
                [new TableFilterColumn(firstId, [CellValue.FromText("Open")])],
                new SpreadsheetFilterSortState([
                    new SpreadsheetFilterSortCondition(0, descending: true),
                    new SpreadsheetFilterSortCondition(1),
                ]))));

        var buttons = SpreadsheetAutoFilterButtonGeometry.GetVisibleButtons(
            worksheet.Tables,
            worksheet.AutoFilter,
            CreateLayout());

        var combined = buttons.Single(button => button.TableColumnId == firstId);
        Assert.AreEqual(SpreadsheetFilterHeaderState.FilteredAndSorted, combined.HeaderState);
        Assert.IsTrue(combined.SortDescending);
        var sorted = buttons.Single(button => button.TableColumnId == secondId);
        Assert.AreEqual(SpreadsheetFilterHeaderState.Sorted, sorted.HeaderState);
        Assert.IsFalse(sorted.SortDescending);
    }

    [TestMethod]
    public void HitRecordsPreservePreFilter007ConstructorAndDeconstructShapes()
    {
        var range = new CellRange(default, new CellAddress(2, 2));
        var address = new CellAddress(0, 1);
        var bounds = new RectD(1, 2, 3, 4);
        var tableId = Guid.NewGuid();
        var columnId = Guid.NewGuid();

        var combined = new SpreadsheetAutoFilterButtonHit(
            SpreadsheetAutoFilterButtonOwnerKind.Table,
            tableId,
            columnId,
            range,
            1,
            1,
            address,
            bounds,
            true);
        var (_, _, _, _, _, _, _, _, combinedFiltered) = combined;
        Assert.IsTrue(combinedFiltered);
        Assert.IsFalse(combined.IsSorted);

        var table = new SpreadsheetTableFilterButtonHit(
            tableId, columnId, address, bounds, true);
        var (_, _, _, _, tableFiltered) = table;
        Assert.IsTrue(tableFiltered);
        Assert.IsFalse(table.IsSorted);

        var worksheet = new SpreadsheetWorksheetFilterButtonHit(
            range, 1, 1, address, bounds, true);
        var (_, _, _, _, _, worksheetFiltered) = worksheet;
        Assert.IsTrue(worksheetFiltered);
        Assert.IsFalse(worksheet.IsSorted);

        Assert.IsNotNull(typeof(SpreadsheetAutoFilterButtonHit).GetConstructor([
            typeof(SpreadsheetAutoFilterButtonOwnerKind),
            typeof(Guid?),
            typeof(Guid?),
            typeof(CellRange),
            typeof(int),
            typeof(int),
            typeof(CellAddress),
            typeof(RectD),
            typeof(bool),
        ]));
        Assert.IsNotNull(typeof(SpreadsheetTableFilterButtonHit).GetConstructor([
            typeof(Guid), typeof(Guid), typeof(CellAddress), typeof(RectD), typeof(bool),
        ]));
        Assert.IsNotNull(typeof(SpreadsheetWorksheetFilterButtonHit).GetConstructor([
            typeof(CellRange), typeof(int), typeof(int), typeof(CellAddress),
            typeof(RectD), typeof(bool),
        ]));
    }

    private static ViewportLayout CreateLayout() =>
        new(
            0d,
            0d,
            new SizeD(400d, 80d),
            400d,
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
                new AxisSlot(2, 160d, 80d),
                new AxisSlot(3, 240d, 80d),
                new AxisSlot(4, 320d, 80d),
            ]);
}
