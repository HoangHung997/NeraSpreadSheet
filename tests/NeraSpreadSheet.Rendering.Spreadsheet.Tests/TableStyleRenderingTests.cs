using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class TableStyleRenderingTests
{
    [TestMethod]
    public void ComposeShouldEmitResolvedTableStyleAndRespectDirectAndConditionalOverrides()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var tableFill = new ColorRgba(220, 235, 250);
        var headerFill = new ColorRgba(30, 80, 140);
        var directFill = new ColorRgba(210, 70, 60);
        var conditionalFill = new ColorRgba(70, 180, 90);
        var borderColor = new ColorRgba(15, 40, 90);
        var style = new TableStyleDefinition(
            "custom:render",
            "RenderStyle",
            [
                new TableStyleElement(
                    TableStyleElementType.WholeTable,
                    new TableStyleFormat
                    {
                        FillColor = TableStyleColor.FromRgb(tableFill),
                        Border = new TableStyleBorder
                        {
                            Bottom = new TableStyleBorderSide
                            {
                                Color = TableStyleColor.FromRgb(borderColor),
                            },
                        },
                    }),
                new TableStyleElement(
                    TableStyleElementType.HeaderRow,
                    new TableStyleFormat
                    {
                        FillColor = TableStyleColor.FromRgb(headerFill),
                        FontColor = TableStyleColor.FromRgb(ColorRgba.White),
                        FontWeight = 700,
                    }),
            ]);
        workbook.TableStyles.AddOrReplaceCustom(style);
        worksheet.AddTable(CreateTable(style.Name, bottom: 4));
        worksheet.SetValue(new CellAddress(0, 0), "Header");
        worksheet.SetValue(new CellAddress(1, 0), "Table");

        var directStyleId = workbook.Styles.Intern(CellStyle.Default with
        {
            Fill = new CellFillStyle
            {
                IsVisible = true,
                Pattern = CellFillPattern.Solid,
                Color = directFill,
            },
        });
        worksheet.SetCell(
            new CellAddress(2, 0),
            new CellData(CellValue.FromText("Direct"), styleId: directStyleId));
        worksheet.SetValue(new CellAddress(3, 0), 12d);
        var dxfId = worksheet.DifferentialStyles.Intern(new CellStylePatch
        {
            Fill = new CellFillStyle
            {
                IsVisible = true,
                Pattern = CellFillPattern.Solid,
                Color = conditionalFill,
            },
        });
        worksheet.AddConditionalFormattingRule(new ConditionalFormattingRule(
            Guid.NewGuid(),
            [new CellRange(new CellAddress(3, 0), new CellAddress(3, 0))],
            ConditionalFormattingRuleType.CellIs,
            ConditionalFormattingOperator.GreaterThan,
            formula1: "=10",
            formula2: null,
            dxfId,
            priority: 1));

        var displayList = SpreadsheetDisplayListComposer.Compose(
            WorksheetSnapshot.Capture(worksheet),
            CreateLayout(rows: 5, columns: 2),
            styles: workbook.Styles);

        AssertFill(displayList, 0, headerFill);
        AssertFill(displayList, 1, tableFill);
        AssertFill(displayList, 2, directFill);
        AssertFill(displayList, 3, conditionalFill);
        var headerText = displayList.Commands
            .OfType<DrawTextCommand>()
            .Single(static command => command.Text == "Header");
        Assert.AreEqual(700, headerText.Style.FontWeight);
        Assert.AreEqual(ColorRgba.White, headerText.Style.Color);
        Assert.IsTrue(displayList.Commands
            .OfType<DrawLineCommand>()
            .Any(command => command.Color == borderColor));
    }

    [TestMethod]
    public void ComposeShouldRemainBoundedToVisibleSlotsForMillionRowTable()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.AddTable(CreateTable(
            "TableStyleMedium2",
            SpreadsheetLimits.MaxRows - 1));
        worksheet.SetValue(default, "Header");

        var displayList = SpreadsheetDisplayListComposer.Compose(
            WorksheetSnapshot.Capture(worksheet),
            CreateLayout(rows: 4, columns: 3),
            styles: workbook.Styles);

        Assert.AreEqual(1, worksheet.UsedCellCount);
        Assert.IsTrue(displayList.Count < 250, $"Unexpected command count: {displayList.Count}.");
    }

    [TestMethod]
    public void FilterButtonVisualShouldUseResolvedElementWithThemeFallbacks()
    {
        var workbook = new Workbook();
        var fill = new ColorRgba(240, 230, 210);
        var glyph = new ColorRgba(15, 65, 120);
        var border = new ColorRgba(80, 90, 100);
        var definition = new TableStyleDefinition(
            "custom:button",
            "ButtonStyle",
            [new TableStyleElement(
                TableStyleElementType.FilterButton,
                new TableStyleFormat
                {
                    FillColor = TableStyleColor.FromRgb(fill),
                    FontColor = TableStyleColor.FromRgb(glyph),
                    Border = new TableStyleBorder
                    {
                        Bottom = new TableStyleBorderSide
                        {
                            Color = TableStyleColor.FromRgb(border),
                        },
                    },
                })]);
        workbook.TableStyles.AddOrReplaceCustom(definition);
        var table = CreateTable(definition.Name, bottom: 4);
        var theme = new SpreadsheetRenderTheme
        {
            TableFilterButtonFilteredBackground = new ColorRgba(190, 220, 200),
        };

        var visual = SpreadsheetTableStyleVisuals.ResolveFilterButton(
            workbook,
            table,
            theme);

        Assert.AreEqual(fill, visual.Background);
        Assert.AreEqual(glyph, visual.Glyph);
        Assert.AreEqual(border, visual.Border);
        Assert.AreEqual(theme.TableFilterButtonFilteredBackground, visual.ActiveBackground);
    }

    private static SpreadsheetTable CreateTable(string styleName, int bottom) =>
        new(
            Guid.NewGuid(),
            "Sales",
            new CellRange(default, new CellAddress(bottom, 2)),
            [
                new SpreadsheetTableColumn(Guid.NewGuid(), "A"),
                new SpreadsheetTableColumn(Guid.NewGuid(), "B"),
                new SpreadsheetTableColumn(Guid.NewGuid(), "C"),
            ],
            styleName: styleName);

    private static ViewportLayout CreateLayout(int rows, int columns) =>
        new ViewportLayoutEngine(
                new SparseAxisMetricIndex(SpreadsheetLimits.MaxRows, 20d),
                new SparseAxisMetricIndex(SpreadsheetLimits.MaxColumns, 80d))
            .Compute(new ViewportRequest(
                0d,
                0d,
                new SizeD(columns * 80d, rows * 20d),
                0d));

    private static void AssertFill(
        DisplayList displayList,
        int row,
        ColorRgba expected)
    {
        Assert.IsTrue(displayList.Commands
            .OfType<FillRectangleCommand>()
            .Any(command =>
                command.Bounds == new RectD(0d, row * 20d, 80d, 20d) &&
                command.Color == expected));
    }
}
