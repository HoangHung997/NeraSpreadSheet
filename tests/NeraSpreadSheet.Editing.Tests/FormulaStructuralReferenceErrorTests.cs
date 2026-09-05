using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class FormulaStructuralReferenceErrorTests
{
    [TestMethod]
    public void DeletedQualifiedReferenceBecomesStandaloneRefError()
    {
        var change = new WorksheetStructuralChange(
            WorksheetAxis.Row,
            WorksheetStructuralChangeKind.Delete,
            index: 0,
            count: 1);

        var rewritten = FormulaStructuralReferenceRewriter.Rewrite(
            "=Data!A1",
            formulaWorksheetName: "Summary",
            changedWorksheetName: "Data",
            change);

        Assert.AreEqual("=#REF!", rewritten);
    }

    [TestMethod]
    public void DeletedQuotedQualifiedRangeBecomesStandaloneRefError()
    {
        var change = new WorksheetStructuralChange(
            WorksheetAxis.Column,
            WorksheetStructuralChangeKind.Delete,
            index: 0,
            count: 2);

        var rewritten = FormulaStructuralReferenceRewriter.Rewrite(
            "=SUM('Input Data'!A1:B3)",
            formulaWorksheetName: "Summary",
            changedWorksheetName: "Input Data",
            change);

        Assert.AreEqual("=SUM(#REF!)", rewritten);
    }
}
