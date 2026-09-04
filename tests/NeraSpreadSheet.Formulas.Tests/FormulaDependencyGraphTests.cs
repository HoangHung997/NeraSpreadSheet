using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class FormulaDependencyGraphTests
{
    [TestMethod]
    public void GetTransitiveDependentsTraversesFormulaChain()
    {
        var graph = new FormulaDependencyGraph();
        var b1 = new FormulaCellKey("Sheet1", new CellAddress(0, 1));
        var c1 = new FormulaCellKey("Sheet1", new CellAddress(0, 2));
        graph.Replace(b1, [new FormulaDependency(null, new CellRange(new CellAddress(0, 0), new CellAddress(0, 0)))]);
        graph.Replace(c1, [new FormulaDependency(null, new CellRange(new CellAddress(0, 1), new CellAddress(0, 1)))]);
        var dependents = graph.GetTransitiveDependents("Sheet1", new CellRange(new CellAddress(0, 0), new CellAddress(0, 0)));
        CollectionAssert.AreEquivalent(new[] { b1, c1 }, dependents.ToArray());
    }

    [TestMethod]
    public void ReverseIndexShouldFindNarrowAndWholeColumnDependencies()
    {
        var graph = new FormulaDependencyGraph();
        var narrow = new FormulaCellKey("Sheet1", new CellAddress(0, 1));
        var broad = new FormulaCellKey("Sheet2", new CellAddress(0, 0));
        graph.Replace(narrow, [new FormulaDependency(null, new CellRange(default, default))]);
        graph.Replace(
            broad,
            [new FormulaDependency(
                "Sheet1",
                new CellRange(
                    new CellAddress(0, 2),
                    new CellAddress(SpreadsheetLimits.MaxRows - 1, 2))) ]);

        CollectionAssert.AreEquivalent(
            new[] { narrow },
            graph.GetDirectDependents(
                    "Sheet1",
                    new CellRange(default, default))
                .ToArray());
        CollectionAssert.AreEquivalent(
            new[] { broad },
            graph.GetDirectDependents(
                    "Sheet1",
                    new CellRange(
                        new CellAddress(900_000, 2),
                        new CellAddress(900_000, 2)))
                .ToArray());
    }
}
