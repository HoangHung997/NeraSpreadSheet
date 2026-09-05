using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Commands.Tests;

[TestClass]
public sealed class TableDesignCommandTests
{
    [TestMethod]
    public async Task DefaultRibbonShouldExposeContextualTableCommandsThroughSessionRegistry()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(default, "Item");
        worksheet.SetValue(new CellAddress(0, 1), "Amount");
        worksheet.SetValue(new CellAddress(1, 0), "A");
        worksheet.SetValue(new CellAddress(1, 1), 2d);
        var session = new SpreadsheetSession(workbook);
        session.Selection.Select(new CellRange(default, new CellAddress(1, 1)));
        var definition = RibbonProductionCommandCatalog.CreateDefaultDefinition();
        var runtime = new RibbonRuntimeController(definition, session.Commands);

        RibbonCommandCatalogAudit.ValidateExact(
            session.Commands,
            definition,
            RibbonProductionCommandCatalog.CommandIds);
        Assert.AreEqual(6, runtime.Snapshot.Tabs.Count);
        Assert.IsTrue(await runtime.TryActivateAsync(SpreadsheetTableCommandIds.Create));
        var table = worksheet.Tables.Single();
        var state = session.TableDesign.Refresh();
        runtime.SetSelectionContext(new RibbonSelectionContext(
            state.HasSelection,
            state.IsInTable));

        Assert.AreEqual(7, runtime.Snapshot.Tabs.Count);
        Assert.IsTrue(await runtime.TryActivateAsync(SpreadsheetTableCommandIds.FirstColumn));
        Assert.IsTrue(worksheet.Tables.Single().ShowFirstColumn);
        Assert.IsTrue(await runtime.TryActivateItemAsync(
            SpreadsheetTableCommandIds.Style,
            "TableStyleDark1"));
        Assert.AreEqual("TableStyleDark1", worksheet.Tables.Single().StyleName);
        Assert.AreEqual(table.Id, worksheet.Tables.Single().Id);
        Assert.IsTrue(await runtime.TryActivateAsync(
            SpreadsheetTableCommandIds.ConvertToRange));
        state = session.TableDesign.Refresh();
        runtime.SetSelectionContext(new RibbonSelectionContext(
            state.HasSelection,
            state.IsInTable));
        Assert.AreEqual(6, runtime.Snapshot.Tabs.Count);
        Assert.AreEqual(0, worksheet.TableCount);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(table.Id, worksheet.Tables.Single().Id);
    }
}
