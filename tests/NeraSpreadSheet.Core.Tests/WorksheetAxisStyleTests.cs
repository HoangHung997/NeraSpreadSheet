using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class WorksheetAxisStyleTests
{
    [TestMethod]
    public void RowStyleAffectsBlankCellsWithoutMaterializingThem()
    {
        var worksheet = new Workbook().Worksheets[0];
        var styles = new CellStyleCatalog();
        var fill = new ColorRgba(24, 96, 180);

        worksheet.ApplyAxisStyle(
            WorksheetAxis.Row,
            7,
            7,
            CreateFillPatch(fill));

        Assert.AreEqual(0, worksheet.UsedCellCount);
        Assert.AreEqual(1, worksheet.RowStyleSpanCount);
        Assert.AreEqual(
            fill,
            worksheet.GetEffectiveStyle(
                new CellAddress(7, 12_000),
                styles).Fill.Color);
        Assert.IsFalse(worksheet.GetEffectiveStyle(
            new CellAddress(8, 12_000),
            styles).Fill.IsVisible);
    }

    [TestMethod]
    public void RowAndColumnStylesComposeInGlobalApplicationOrder()
    {
        var worksheet = new Workbook().Worksheets[0];
        var styles = new CellStyleCatalog();
        var red = new ColorRgba(210, 30, 30);
        var blue = new ColorRgba(30, 80, 210);
        var green = new ColorRgba(40, 170, 80);

        worksheet.ApplyAxisStyle(
            WorksheetAxis.Row,
            2,
            2,
            CreateFillPatch(red));
        worksheet.ApplyAxisStyle(
            WorksheetAxis.Column,
            3,
            3,
            CreateFillPatch(blue));

        Assert.AreEqual(
            blue,
            worksheet.GetEffectiveStyle(
                new CellAddress(2, 3),
                styles).Fill.Color);
        Assert.AreEqual(
            red,
            worksheet.GetEffectiveStyle(
                new CellAddress(2, 4),
                styles).Fill.Color);

        worksheet.ApplyAxisStyle(
            WorksheetAxis.Row,
            2,
            2,
            CreateFillPatch(green));

        Assert.AreEqual(
            green,
            worksheet.GetEffectiveStyle(
                new CellAddress(2, 3),
                styles).Fill.Color);
    }

    [TestMethod]
    public void SnapshotRetainsIndependentAxisStyleState()
    {
        var worksheet = new Workbook().Worksheets[0];
        var styles = new CellStyleCatalog();
        var original = new ColorRgba(220, 180, 40);
        var replacement = new ColorRgba(120, 40, 190);
        worksheet.ApplyAxisStyle(
            WorksheetAxis.Row,
            4,
            4,
            CreateFillPatch(original));
        var snapshot = WorksheetSnapshot.Capture(worksheet);

        worksheet.ApplyAxisStyle(
            WorksheetAxis.Row,
            4,
            4,
            CreateFillPatch(replacement));

        Assert.AreEqual(1, snapshot.RowStyleSpanCount);
        Assert.AreEqual(
            original,
            snapshot.GetEffectiveStyle(
                new CellAddress(4, 100),
                styles).Fill.Color);
        Assert.AreEqual(
            replacement,
            worksheet.GetEffectiveStyle(
                new CellAddress(4, 100),
                styles).Fill.Color);
    }

    [TestMethod]
    public void SnapshotReusesAxisStyleCompositionAcrossEquivalentCells()
    {
        var worksheet = new Workbook().Worksheets[0];
        var styles = new CellStyleCatalog();
        worksheet.ApplyAxisStyle(
            WorksheetAxis.Row,
            9,
            9,
            CreateFillPatch(new ColorRgba(40, 160, 110)));
        var snapshot = WorksheetSnapshot.Capture(worksheet);

        var first = snapshot.GetEffectiveStyle(
            new CellAddress(9, 1),
            styles);
        var second = snapshot.GetEffectiveStyle(
            new CellAddress(9, 12_000),
            styles);

        Assert.AreSame(first, second);
        Assert.AreEqual(1, snapshot.AxisStyleCacheEntryCount);
    }

    [TestMethod]
    public void StructuralInsertMovesSparseRowStyleSpan()
    {
        var worksheet = new Workbook().Worksheets[0];
        var styles = new CellStyleCatalog();
        var fill = new ColorRgba(18, 140, 160);
        worksheet.ApplyAxisStyle(
            WorksheetAxis.Row,
            5,
            5,
            CreateFillPatch(fill));

        worksheet.ApplyStructuralChange(new WorksheetStructuralChange(
            WorksheetAxis.Row,
            WorksheetStructuralChangeKind.Insert,
            index: 3,
            count: 2));

        Assert.IsFalse(worksheet.GetEffectiveStyle(
            new CellAddress(3, 0),
            styles).Fill.IsVisible);
        Assert.IsFalse(worksheet.GetEffectiveStyle(
            new CellAddress(5, 0),
            styles).Fill.IsVisible);
        Assert.AreEqual(
            fill,
            worksheet.GetEffectiveStyle(
                new CellAddress(7, 0),
                styles).Fill.Color);
        Assert.AreEqual(0, worksheet.UsedCellCount);
    }

    [TestMethod]
    public void FullAxisStyleIsInheritedAndClippedOnInsert()
    {
        var worksheet = new Workbook().Worksheets[0];
        var styles = new CellStyleCatalog();
        var fill = new ColorRgba(75, 115, 185);
        worksheet.ApplyAxisStyle(
            WorksheetAxis.Row,
            0,
            SpreadsheetLimits.MaxRows - 1,
            CreateFillPatch(fill));

        worksheet.ApplyStructuralChange(new WorksheetStructuralChange(
            WorksheetAxis.Row,
            WorksheetStructuralChangeKind.Insert,
            index: 2,
            count: 3));

        Assert.AreEqual(1, worksheet.RowStyleSpanCount);
        Assert.AreEqual(
            fill,
            worksheet.GetEffectiveStyle(
                new CellAddress(1, 0),
                styles).Fill.Color);
        Assert.AreEqual(
            fill,
            worksheet.GetEffectiveStyle(
                new CellAddress(2, 0),
                styles).Fill.Color);
        Assert.AreEqual(
            fill,
            worksheet.GetEffectiveStyle(
                new CellAddress(4, 0),
                styles).Fill.Color);
        Assert.AreEqual(
            fill,
            worksheet.GetEffectiveStyle(
                new CellAddress(5, 0),
                styles).Fill.Color);
        Assert.AreEqual(
            fill,
            worksheet.GetEffectiveStyle(
                new CellAddress(
                    SpreadsheetLimits.MaxRows - 1,
                    0),
                styles).Fill.Color);
    }

    [TestMethod]
    public void AxisMoveMapsStyleSpanWithoutMaterializingCells()
    {
        var worksheet = new Workbook().Worksheets[0];
        var styles = new CellStyleCatalog();
        var fill = new ColorRgba(90, 130, 220);
        worksheet.ApplyAxisStyle(
            WorksheetAxis.Row,
            2,
            2,
            CreateFillPatch(fill));

        worksheet.ApplyAxisMove(new WorksheetAxisMove(
            WorksheetAxis.Row,
            sourceIndex: 2,
            count: 1,
            destinationBoundary: 6));

        Assert.IsFalse(worksheet.GetEffectiveStyle(
            new CellAddress(2, 0),
            styles).Fill.IsVisible);
        Assert.AreEqual(
            fill,
            worksheet.GetEffectiveStyle(
                new CellAddress(5, 0),
                styles).Fill.Color);
        Assert.AreEqual(0, worksheet.UsedCellCount);
    }

    private static CellStylePatch CreateFillPatch(
        ColorRgba color) => new()
    {
        Fill = new CellFillStyle
        {
            IsVisible = true,
            Color = color,
        },
    };
}
