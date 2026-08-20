using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class SpreadsheetTableTests
{
    [TestMethod]
    public void CatalogEnforcesWorkbookWideNamesAndWorksheetOverlap()
    {
        var workbook = new Workbook();
        var second = workbook.AddWorksheet("Data Sheet");
        var catalog = new WorkbookTableCatalog(workbook);
        var firstTable = CreateTable(
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(4, 1)));
        catalog.Add(workbook.Worksheets[0], firstTable);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            catalog.Add(
                second,
                CreateTable(
                    "sales",
                    new CellRange(
                        new CellAddress(0, 0),
                        new CellAddress(3, 1)))));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            catalog.Add(
                workbook.Worksheets[0],
                CreateTable(
                    "Other",
                    new CellRange(
                        new CellAddress(3, 1),
                        new CellAddress(6, 2)))));
        Assert.AreEqual(1, catalog.Count);
    }

    [TestMethod]
    public void TableRangesAndColumnIdentityRemainStable()
    {
        var amountId = Guid.NewGuid();
        var table = new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(5, 1)),
            [
                new SpreadsheetTableColumn(Guid.NewGuid(), "Category"),
                new SpreadsheetTableColumn(amountId, "Amount"),
            ],
            hasHeaderRow: true,
            hasTotalsRow: true);

        Assert.AreEqual("A1:B1", table.HeaderRange?.ToString());
        Assert.AreEqual("A2:B5", table.DataRange?.ToString());
        Assert.AreEqual("A6:B6", table.TotalsRange?.ToString());

        var renamed = table.RenameColumn(amountId, "NetAmount");
        Assert.AreEqual(amountId, renamed.Columns[1].Id);
        Assert.AreEqual("NetAmount", renamed.Columns[1].Name);
        Assert.AreEqual("Amount", table.Columns[1].Name);

        var insertedId = Guid.NewGuid();
        var inserted = renamed.InsertColumns(
            1,
            [new SpreadsheetTableColumn(insertedId, "Quantity")]);
        Assert.AreEqual("A1:C6", inserted.Range.ToString());
        Assert.AreEqual(insertedId, inserted.Columns[1].Id);
        Assert.AreEqual(amountId, inserted.Columns[2].Id);
    }

    [TestMethod]
    public void StructuralMappingExpandsRowsAndRejectsInternalPermutation()
    {
        var table = CreateTable(
            "Sales",
            new CellRange(
                new CellAddress(2, 2),
                new CellAddress(6, 3)));

        var shifted = table.MapInsert(WorksheetAxis.Row, 1, 2);
        Assert.AreEqual("C5:D9", shifted.Range.ToString());

        var expanded = table.MapInsert(WorksheetAxis.Row, 4, 3);
        Assert.AreEqual("C3:D10", expanded.Range.ToString());

        var translated = table.MapMove(new WorksheetAxisMove(
            WorksheetAxis.Row,
            2,
            5,
            12));
        Assert.AreEqual("C8:D12", translated.Range.ToString());

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            table.MapMove(new WorksheetAxisMove(
                WorksheetAxis.Row,
                3,
                1,
                7)));
    }

    [TestMethod]
    public void StructuredReferencesExpandWithoutTouchingStringLiterals()
    {
        var workbook = new Workbook();
        var dataSheet = workbook.AddWorksheet("Data Sheet");
        var catalog = new WorkbookTableCatalog(workbook);
        var table = CreateTable(
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(4, 1)));
        catalog.Add(dataSheet, table);

        var expanded = SpreadsheetStructuredReferenceResolver.ResolveFormula(
            "=SUM(Sales[Amount])+LEN(\"Sales[Amount]\")",
            catalog,
            workbook.Worksheets[0],
            default);
        Assert.AreEqual(
            "=SUM('Data Sheet'!B2:B5)+LEN(\"Sales[Amount]\")",
            expanded);

        var header = SpreadsheetStructuredReferenceResolver.ResolveFormula(
            "=Sales[[#Headers],[Amount]]",
            catalog,
            workbook.Worksheets[0],
            default);
        Assert.AreEqual("='Data Sheet'!B1", header);

        var thisRow = SpreadsheetStructuredReferenceResolver.ResolveFormula(
            "=[@Amount]*2",
            catalog,
            dataSheet,
            new CellAddress(2, 0));
        Assert.AreEqual("=B3*2", thisRow);
    }

    [TestMethod]
    public void AutoFilterCombinesCriteriaWithoutMaterializingBlankRows()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var categoryId = Guid.NewGuid();
        var amountId = Guid.NewGuid();
        var filter = new SpreadsheetAutoFilter()
            .Set(
                categoryId,
                new SpreadsheetFilterCriterion(
                    SpreadsheetFilterOperator.Equal,
                    CellValue.FromText("A")))
            .Set(
                amountId,
                new SpreadsheetFilterCriterion(
                    SpreadsheetFilterOperator.GreaterThan,
                    CellValue.FromNumber(10d)));
        var table = new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(3, 1)),
            [
                new SpreadsheetTableColumn(categoryId, "Category"),
                new SpreadsheetTableColumn(amountId, "Amount"),
            ],
            autoFilter: filter);
        var catalog = new WorkbookTableCatalog(workbook);
        catalog.Add(worksheet, table);

        worksheet.SetValue(new CellAddress(1, 0), "A");
        worksheet.SetValue(new CellAddress(1, 1), 5d);
        worksheet.SetValue(new CellAddress(2, 0), "B");
        worksheet.SetValue(new CellAddress(2, 1), 30d);
        worksheet.SetValue(new CellAddress(3, 0), "A");
        worksheet.SetValue(new CellAddress(3, 1), 20d);

        var hidden = catalog.GetFilteredOutRows(
            WorksheetSnapshot.Capture(worksheet),
            table);
        CollectionAssert.AreEqual(new[] { 1, 2 }, hidden.ToArray());
        Assert.IsTrue(WorkbookTableCatalog.IsRowVisible(
            WorksheetSnapshot.Capture(worksheet),
            table,
            3));
    }

    private static SpreadsheetTable CreateTable(
        string name,
        CellRange range) =>
        new(
            Guid.NewGuid(),
            name,
            range,
            [
                new SpreadsheetTableColumn(Guid.NewGuid(), "Category"),
                new SpreadsheetTableColumn(Guid.NewGuid(), "Amount"),
            ]);
}
