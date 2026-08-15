using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class TabSeparatedClipboardTests
{
    [TestMethod]
    public void NativeClipboardExportsQuotedTabSeparatedText()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(default, "A\tB");
        sheet.SetValue(new CellAddress(0, 1), "say \"hello\"");
        sheet.SetFormula(new CellAddress(1, 0), "=A1");
        var session = new SpreadsheetSession(workbook);
        session.Selection.Select(new CellRange(default, new CellAddress(1, 1)));

        var package = session.Clipboard.CopyPrimarySelection();
        var text = package.ToTabSeparatedText();

        StringAssert.Contains(text, "\"A\tB\"");
        StringAssert.Contains(text, "\"say \"\"hello\"\"\"");
        StringAssert.Contains(text, "=A1");
    }

    [TestMethod]
    public void ImportedTextPreservesQuotedNewlinesAndBlankCells()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        workbook.Worksheets[0].SetValue(new CellAddress(1, 1), "clear me");

        session.Clipboard.ImportTabSeparatedText("\"line1\r\nline2\"\t\r\n=1+2\ttrue");
        Assert.IsTrue(session.Clipboard.Paste(new CellAddress(0, 0)));

        Assert.AreEqual("line1\r\nline2", workbook.Worksheets[0].GetCell(default).Value.RawValue);
        Assert.IsTrue(workbook.Worksheets[0].GetCell(new CellAddress(0, 1)).IsEmpty);
        Assert.AreEqual(3d, workbook.Worksheets[0].GetCell(new CellAddress(1, 0)).Value.RawValue);
        Assert.AreEqual(true, workbook.Worksheets[0].GetCell(new CellAddress(1, 1)).Value.RawValue);
    }

    [TestMethod]
    public void ExternalFormulaTextIsNotRelocatedAsNativeCopyReference()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        session.Clipboard.ImportTabSeparatedText("=A1+1");

        Assert.IsTrue(session.Clipboard.Paste(new CellAddress(4, 4)));

        Assert.AreEqual("=A1+1", workbook.Worksheets[0].GetCell(new CellAddress(4, 4)).Formula);
    }
}
