using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class TableStyleEngineTests
{
    [TestMethod]
    public void ThemeTintShouldResolveLightAndShadeWithoutChangingAlpha()
    {
        var theme = WorkbookTheme.Office with
        {
            Accent1 = new ColorRgba(0, 0, 0, 173),
            Accent2 = new ColorRgba(255, 255, 255, 91),
        };

        var light = TableStyleColor.FromTheme(
            WorkbookThemeColor.Accent1,
            0.5d).Resolve(theme);
        var shade = TableStyleColor.FromTheme(
            WorkbookThemeColor.Accent2,
            -0.5d).Resolve(theme);

        Assert.AreEqual(new ColorRgba(128, 128, 128, 173), light);
        Assert.AreEqual(new ColorRgba(128, 128, 128, 91), shade);
    }

    [TestMethod]
    public void ResolverShouldComposeElementsInLockedPrecedenceOrder()
    {
        var definition = CreatePrecedenceStyle();
        var resolved = TableStyleResolver.Resolve(
            definition,
            WorkbookTheme.Office);
        var table = CreateTable(definition.Name);

        Assert.AreEqual(
            Color(70),
            resolved.ResolveCell(table, new CellAddress(0, 0)).Fill.Color,
            "Header row must override first-column and stripe layers.");
        Assert.AreEqual(
            Color(50),
            resolved.ResolveCell(table, new CellAddress(1, 0)).Fill.Color,
            "First column must override row and column stripes.");
        Assert.AreEqual(
            Color(45),
            resolved.ResolveCell(table, new CellAddress(1, 1)).Fill.Color,
            "Column stripe must override row stripe.");
        Assert.AreEqual(
            Color(60),
            resolved.ResolveCell(table, new CellAddress(1, 2)).Fill.Color,
            "Last column must override stripes.");
        Assert.AreEqual(
            Color(80),
            resolved.ResolveCell(table, new CellAddress(5, 2)).Fill.Color,
            "Totals row must be the highest cell Table-style layer.");
    }

    [TestMethod]
    public void ResolverShouldHonorIndependentStripeSizes()
    {
        var definition = new TableStyleDefinition(
            "custom:stripe-size",
            "StripeSize",
            [
                Element(TableStyleElementType.FirstRowStripe, 10, stripeSize: 2),
                Element(TableStyleElementType.SecondRowStripe, 20, stripeSize: 1),
            ]);
        var table = CreateTable(definition.Name);
        var resolved = TableStyleResolver.Resolve(definition, WorkbookTheme.Office);

        Assert.AreEqual(Color(10), resolved.ResolveCell(table, new CellAddress(1, 1)).Fill.Color);
        Assert.AreEqual(Color(10), resolved.ResolveCell(table, new CellAddress(2, 1)).Fill.Color);
        Assert.AreEqual(Color(20), resolved.ResolveCell(table, new CellAddress(3, 1)).Fill.Color);
        Assert.AreEqual(Color(10), resolved.ResolveCell(table, new CellAddress(4, 1)).Fill.Color);
    }

    [TestMethod]
    public void BuiltInGalleryShouldExposeStableUniqueIdentityAndBoundedPreview()
    {
        var catalog = new TableStyleCatalog();
        var first = catalog.BuiltInGallery.ToArray();
        var second = new TableStyleCatalog().BuiltInGallery.ToArray();

        Assert.AreEqual(60, first.Length);
        Assert.AreEqual(first.Length, first.Select(static entry => entry.Id).Distinct().Count());
        CollectionAssert.AreEqual(first, second);
        Assert.AreEqual(
            "builtin:TableStyleMedium2",
            first.Single(static entry => entry.Name == "TableStyleMedium2").Id);
        var preview = TableStylePreview.Create(
            catalog.Get("TableStyleMedium2"),
            WorkbookTheme.Office,
            rows: TableStylePreview.MaximumRows,
            columns: TableStylePreview.MaximumColumns);
        Assert.AreEqual(144, preview.Count);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            TableStylePreview.Create(
                catalog.Get("TableStyleMedium2"),
                WorkbookTheme.Office,
                rows: TableStylePreview.MaximumRows + 1));
    }

    [TestMethod]
    public void CatalogShouldRejectRenameOntoAnotherCustomStyle()
    {
        var catalog = new TableStyleCatalog();
        catalog.AddOrReplaceCustom(new TableStyleDefinition(
            "custom:first",
            "FirstStyle",
            []));
        catalog.AddOrReplaceCustom(new TableStyleDefinition(
            "custom:second",
            "SecondStyle",
            []));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            catalog.AddOrReplaceCustom(new TableStyleDefinition(
                "custom:first",
                "SecondStyle",
                [])));
        Assert.AreEqual("custom:first", catalog.Get("FirstStyle").Id);
        Assert.AreEqual("custom:second", catalog.Get("SecondStyle").Id);
    }

    [TestMethod]
    public void SnapshotShouldRetainCapturedThemeAndStyleDefinition()
    {
        var workbook = new Workbook();
        var definition = new TableStyleDefinition(
            "custom:snapshot",
            "SnapshotStyle",
            [new TableStyleElement(
                TableStyleElementType.WholeTable,
                new TableStyleFormat
                {
                    FillColor = TableStyleColor.FromTheme(WorkbookThemeColor.Accent1),
                })]);
        workbook.TableStyles.AddOrReplaceCustom(definition);
        workbook.Theme = WorkbookTheme.Office with
        {
            Accent1 = new ColorRgba(11, 22, 33),
        };
        var table = CreateTable(definition.Name);
        workbook.Worksheets[0].AddTable(table);
        var snapshot = WorksheetSnapshot.Capture(workbook.Worksheets[0]);

        workbook.Theme = workbook.Theme with
        {
            Accent1 = new ColorRgba(200, 210, 220),
        };
        workbook.TableStyles.AddOrReplaceCustom(new TableStyleDefinition(
            definition.Id,
            definition.Name,
            [Element(TableStyleElementType.WholeTable, 99)]));

        Assert.IsTrue(snapshot.TryGetResolvedTableStyle(definition.Name, out var resolved));
        Assert.AreEqual(
            new ColorRgba(11, 22, 33),
            resolved!.ResolveCell(table, new CellAddress(1, 1)).Fill.Color);
    }

    private static TableStyleDefinition CreatePrecedenceStyle() =>
        new(
            "custom:precedence",
            "PrecedenceStyle",
            [
                Element(TableStyleElementType.WholeTable, 10),
                Element(TableStyleElementType.FirstRowStripe, 20),
                Element(TableStyleElementType.SecondRowStripe, 30),
                Element(TableStyleElementType.FirstColumnStripe, 40),
                Element(TableStyleElementType.SecondColumnStripe, 45),
                Element(TableStyleElementType.FirstColumn, 50),
                Element(TableStyleElementType.LastColumn, 60),
                Element(TableStyleElementType.HeaderRow, 70),
                Element(TableStyleElementType.TotalsRow, 80),
                Element(TableStyleElementType.FilterButton, 90),
            ]);

    private static TableStyleElement Element(
        TableStyleElementType type,
        byte color,
        int stripeSize = 1) =>
        new(
            type,
            new TableStyleFormat
            {
                FillColor = TableStyleColor.FromRgb(Color(color)),
            },
            stripeSize);

    private static SpreadsheetTable CreateTable(string styleName) =>
        new(
            Guid.NewGuid(),
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(5, 2)),
            [
                new SpreadsheetTableColumn(Guid.NewGuid(), "First"),
                new SpreadsheetTableColumn(Guid.NewGuid(), "Middle"),
                new SpreadsheetTableColumn(Guid.NewGuid(), "Last"),
            ],
            hasTotalsRow: true,
            styleName: styleName,
            showFirstColumn: true,
            showLastColumn: true,
            showRowStripes: true,
            showColumnStripes: true);

    private static ColorRgba Color(byte value) => new(value, 0, 0);
}
