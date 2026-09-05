using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class SpreadsheetFormulaReferenceDisplayListComposerTests
{
    [TestMethod]
    public void ComposeShouldOutlineEveryVisibleReferenceWithItsColor()
    {
        var body = new DisplayListBuilder().Build();
        var layout = new ViewportLayoutEngine(
            new SparseAxisMetricIndex(20, 20d),
            new SparseAxisMetricIndex(20, 80d)).Compute(
            new ViewportRequest(
                0d,
                0d,
                new SizeD(320d, 120d),
                0d));
        var blue = new ColorRgba(33, 115, 201);
        var red = new ColorRgba(196, 62, 62);

        var result = SpreadsheetFormulaReferenceDisplayListComposer.Compose(
            body,
            layout,
            [
                new SpreadsheetFormulaReferenceHighlight(
                    new CellRange(
                        new CellAddress(0, 0),
                        new CellAddress(1, 1)),
                    blue),
                new SpreadsheetFormulaReferenceHighlight(
                    new CellRange(
                        new CellAddress(2, 2),
                        new CellAddress(2, 2)),
                    red),
            ],
            2d);

        Assert.IsTrue(result.Commands.OfType<DrawDisplayListCommand>().Any(
            command => ReferenceEquals(command.DisplayList, body)));
        Assert.AreEqual(
            4,
            result.Commands.OfType<DrawLineCommand>().Count(
                command => command.Color == blue));
        Assert.AreEqual(
            4,
            result.Commands.OfType<DrawLineCommand>().Count(
                command => command.Color == red));
    }

    [TestMethod]
    public void ComposeShouldKeepBodyWhenNoReferencesExist()
    {
        var body = new DisplayListBuilder().Build();
        var layout = new ViewportLayout(
            0d,
            0d,
            new SizeD(100d, 100d),
            100d,
            100d,
            0d,
            0d,
            Array.Empty<AxisSlot>(),
            Array.Empty<AxisSlot>());

        var result = SpreadsheetFormulaReferenceDisplayListComposer.Compose(
            body,
            layout,
            Array.Empty<SpreadsheetFormulaReferenceHighlight>(),
            2d);

        Assert.AreSame(body, result);
    }
}
