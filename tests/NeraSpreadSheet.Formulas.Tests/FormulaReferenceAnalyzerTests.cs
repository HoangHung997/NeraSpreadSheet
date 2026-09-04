using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class FormulaReferenceAnalyzerTests
{
    [TestMethod]
    public void TryGetReferencesShouldExtractLocalAndCrossSheetRanges()
    {
        var parsed = FormulaReferenceAnalyzer.TryGetReferences(
            "=SUM(A1:B2,'Sales Data'!C3)+D4",
            out var references);

        Assert.IsTrue(parsed);
        CollectionAssert.AreEquivalent(
            new[]
            {
                new FormulaDependency(
                    null,
                    new CellRange(
                        new CellAddress(0, 0),
                        new CellAddress(1, 1))),
                new FormulaDependency(
                    "Sales Data",
                    new CellRange(
                        new CellAddress(2, 2),
                        new CellAddress(2, 2))),
                new FormulaDependency(
                    null,
                    new CellRange(
                        new CellAddress(3, 3),
                        new CellAddress(3, 3))),
            },
            references.ToArray());
    }

    [TestMethod]
    public void TryGetReferencesShouldRejectIncompleteFormulaWithoutThrowing()
    {
        var parsed = FormulaReferenceAnalyzer.TryGetReferences(
            "=SUM(A1:",
            out var references);

        Assert.IsFalse(parsed);
        Assert.AreEqual(0, references.Count);
    }
}
