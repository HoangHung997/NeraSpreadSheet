using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class DataValidationRenderingTests
{
    [TestMethod]
    public void InvalidVisibleCellProducesSharedDisplayListOutline()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(default, -1d);
        worksheet.AddDataValidationRule(new DataValidationRule(
            Guid.NewGuid(),
            [new CellRange(default, default)],
            DataValidationType.Decimal,
            DataValidationOperator.GreaterThan,
            "0"));
        var invalidColor = new ColorRgba(211, 40, 40);
        var theme = new SpreadsheetRenderTheme
        {
            InvalidCell = invalidColor,
            InvalidCellStrokeWidth = 3d,
        };
        var layout = new ViewportLayoutEngine(
            new SparseAxisMetricIndex(10, 20d),
            new SparseAxisMetricIndex(10, 80d)).Compute(
            new ViewportRequest(
                0d,
                0d,
                new SizeD(160d, 80d),
                0d));

        var displayList = SpreadsheetDisplayListComposer.Compose(
            WorksheetSnapshot.Capture(worksheet),
            layout,
            theme: theme,
            styles: workbook.Styles);

        var diagnosticLines = displayList.Commands
            .OfType<DrawLineCommand>()
            .Where(command =>
                command.Color == invalidColor &&
                command.StrokeWidth == 3d)
            .ToArray();
        Assert.AreEqual(4, diagnosticLines.Length);
    }

    [TestMethod]
    public void ValidCellAndDisabledDiagnosticsProduceNoInvalidOutline()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(default, 5d);
        worksheet.AddDataValidationRule(new DataValidationRule(
            Guid.NewGuid(),
            [new CellRange(default, default)],
            DataValidationType.Whole,
            DataValidationOperator.Between,
            "1",
            "10"));
        var invalidColor = new ColorRgba(211, 40, 40);
        var layout = new ViewportLayoutEngine(
            new SparseAxisMetricIndex(10, 20d),
            new SparseAxisMetricIndex(10, 80d)).Compute(
            new ViewportRequest(
                0d,
                0d,
                new SizeD(160d, 80d),
                0d));

        var valid = SpreadsheetDisplayListComposer.Compose(
            WorksheetSnapshot.Capture(worksheet),
            layout,
            theme: new SpreadsheetRenderTheme
            {
                InvalidCell = invalidColor,
            },
            styles: workbook.Styles);
        Assert.IsFalse(valid.Commands
            .OfType<DrawLineCommand>()
            .Any(command => command.Color == invalidColor));

        worksheet.SetValue(default, -1d);
        var disabled = SpreadsheetDisplayListComposer.Compose(
            WorksheetSnapshot.Capture(worksheet),
            layout,
            theme: new SpreadsheetRenderTheme
            {
                InvalidCell = invalidColor,
                ShowValidationErrors = false,
            },
            styles: workbook.Styles);
        Assert.IsFalse(disabled.Commands
            .OfType<DrawLineCommand>()
            .Any(command => command.Color == invalidColor));
    }
}
