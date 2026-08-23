using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class FinancialIrrRootSelectionTests
{
    private static readonly Dictionary<CellAddress, CellValue>
        MultipleRootCashFlows = new()
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(17d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(116d),
            [new CellAddress(2, 0)] = CellValue.FromNumber(-473d),
            [new CellAddress(3, 0)] = CellValue.FromNumber(74d),
        };

    [TestMethod]
    public void IrrChoosesConvergedRootNearestTheSuppliedGuess()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(MultipleRootCashFlows);

        var negativeRoot = EvaluateNumber(
            engine,
            "=IRR(A1:A4,-0.62)",
            context);
        var positiveRoot = EvaluateNumber(
            engine,
            "=IRR(A1:A4,1.5)",
            context);

        Assert.AreEqual(
            -0.8368694674176768d,
            negativeRoot,
            1e-8d);
        Assert.AreEqual(
            1.742625940800664d,
            positiveRoot,
            1e-8d);
        Assert.IsTrue(
            Math.Abs(negativeRoot + 0.62d) <
            Math.Abs(positiveRoot + 0.62d));
        Assert.IsTrue(
            Math.Abs(positiveRoot - 1.5d) <
            Math.Abs(negativeRoot - 1.5d));
    }

    [TestMethod]
    public void MultipleRootSelectionIsDeterministicAcrossEvaluations()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(MultipleRootCashFlows);
        var expected = EvaluateNumber(
            engine,
            "=IRR(A1:A4,-0.62)",
            context);

        for (var iteration = 0; iteration < 20; iteration++)
        {
            Assert.AreEqual(
                expected,
                EvaluateNumber(
                    engine,
                    "=IRR(A1:A4,-0.62)",
                    context),
                1e-12d);
        }
    }

    private static double EvaluateNumber(
        NeraFormulaEngine engine,
        string formula,
        IFormulaEvaluationContext context)
    {
        var result = engine.Evaluate(formula, context);
        Assert.IsTrue(
            result.IsSuccess,
            $"Expected success for {formula}, but received {result.Value}.");
        Assert.AreEqual(CellValueKind.Number, result.Value.Kind);
        return (double)result.Value.RawValue!;
    }
}
