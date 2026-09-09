using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class SettingsDraftRoundTripTests
{
    [TestMethod]
    public async Task ConfirmedFormattingShouldRoundTripWithoutReinterpretingNumericCells()
    {
        var workbook = new Workbook(); var sheet = workbook.Worksheets[0];
        sheet.SetValue(default, 1234.5);
        sheet.SetFormula(new CellAddress(0, 1), "=A1+1");
        var session = new SpreadsheetSession(workbook);
        session.Selection.Select(new CellRange(default, new CellAddress(0, 1)));
        using (var draft = new SpreadsheetFormatCellsDraft(session))
        {
            Assert.IsTrue(draft.Apply(new CellStylePatch
            {
                NumberFormatCode = "#,##0.000", FontWeight = 700,
                FontColor = new ColorRgba(33, 115, 70), WrapText = true,
            }));
        }
        Assert.AreEqual(1, session.History.UndoCount);
        var serializer = new NeraOpenXmlWorkbookSerializer();
        using var bytes = new MemoryStream();
        await serializer.SaveAsync(workbook, bytes, new OpenXmlExportOptions());
        bytes.Position = 0;
        var loaded = await serializer.LoadAsync(bytes, new OpenXmlImportOptions());
        var loadedSheet = loaded.Worksheets[0];
        Assert.AreEqual(1234.5, loadedSheet.GetValue(default));
        Assert.AreEqual("=A1+1", loadedSheet.GetFormula(new CellAddress(0, 1)));
        for (var column = 0; column < 2; column++)
        {
            var style = loadedSheet.GetEffectiveStyle(new CellAddress(0, column), loaded.Styles);
            Assert.AreEqual("#,##0.000", style.NumberFormat.FormatCode);
            Assert.AreEqual(700, style.Font.Weight);
            Assert.AreEqual(new ColorRgba(33, 115, 70), style.Font.Color);
            Assert.IsTrue(style.Alignment.WrapText);
        }
        session.Undo();
        Assert.AreEqual("General", session.Styles.ActiveCellStyle.NumberFormat.FormatCode);
        Assert.AreEqual(1234.5, sheet.GetValue(default));
    }
}
