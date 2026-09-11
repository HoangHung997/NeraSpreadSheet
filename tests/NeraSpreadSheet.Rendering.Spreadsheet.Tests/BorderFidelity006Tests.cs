using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class BorderFidelity006Tests
{
    [TestMethod]
    public void ExplicitBordersRenderAfterGridAndPreservePatternSemantics()
    {
        var workbook = new Workbook();
        var borderColor = new ColorRgba(12, 23, 34);
        var gridColor = new ColorRgba(201, 202, 203);
        var style = CellStyle.Default with
        {
            Border = new CellBorderStyle
            {
                Top = new CellBorderSide
                {
                    Style = CellBorderLineStyle.Dotted,
                    Color = borderColor,
                    Width = 1d,
                },
                Bottom = new CellBorderSide
                {
                    Style = CellBorderLineStyle.DoubleLine,
                    Color = borderColor,
                    Width = 1d,
                },
                Left = new CellBorderSide
                {
                    Style = CellBorderLineStyle.Dashed,
                    Color = borderColor,
                    Width = 1d,
                },
            },
        };
        var styleId = workbook.Styles.Intern(style);
        workbook.Worksheets[0].SetCell(
            new CellAddress(0, 0),
            new CellData(CellValue.FromText("Border"), styleId: styleId));

        var layout = new ViewportLayoutEngine(
                new SparseAxisMetricIndex(4, 20d),
                new SparseAxisMetricIndex(4, 80d))
            .Compute(new ViewportRequest(0d, 0d, new SizeD(180d, 80d), 0d));
        var theme = new SpreadsheetRenderTheme
        {
            GridLine = gridColor,
            GridStrokeWidth = 1d,
        };

        var displayList = SpreadsheetDisplayListComposer.Compose(
            WorksheetSnapshot.Capture(workbook.Worksheets[0]),
            layout,
            theme: theme,
            styles: workbook.Styles);
        var commands = displayList.Commands.ToArray();
        var gridIndexes = commands
            .Select((command, index) => (command, index))
            .Where(pair => pair.command is DrawLineCommand line && line.Color == gridColor)
            .Select(pair => pair.index)
            .ToArray();
        var borderIndexes = commands
            .Select((command, index) => (command, index))
            .Where(pair => pair.command is DrawLineCommand line && line.Color == borderColor)
            .Select(pair => pair.index)
            .ToArray();

        Assert.IsTrue(gridIndexes.Length > 0, "Expected worksheet grid lines.");
        Assert.IsTrue(borderIndexes.Length >= 6, "Dotted, dashed and double borders must expand to multiple line primitives.");
        Assert.IsTrue(borderIndexes.Min() > gridIndexes.Max(), "Explicit cell borders must render after grid lines.");

        var borderLines = commands
            .OfType<DrawLineCommand>()
            .Where(line => line.Color == borderColor)
            .ToArray();
        var horizontalLines = borderLines
            .Where(line => Math.Abs(line.Start.Y - line.End.Y) < 0.001d)
            .ToArray();
        Assert.IsTrue(horizontalLines.Length >= 4, "Dotted top plus double bottom should produce multiple horizontal primitives.");
        Assert.IsTrue(horizontalLines.Select(line => Math.Round(line.Start.Y, 3)).Distinct().Count() >= 3,
            "Double border must render as two separated parallel strokes, not one thick stroke.");
    }
}
