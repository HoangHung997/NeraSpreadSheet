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
}
