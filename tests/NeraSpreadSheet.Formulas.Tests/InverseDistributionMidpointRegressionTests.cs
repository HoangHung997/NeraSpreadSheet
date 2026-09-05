using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class InverseDistributionMidpointRegressionTests
{
    [TestMethod]
    public void InverseSearchesReturnAcceptedMidpointBeforeNarrowingBracket()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            3d,
            EvaluateNumber(
                engine,
                "=BETA.INV(BETA.DIST(3,2,3,TRUE(),2,6),2,3,2,6)",
                context),
            1e-12d);
        Assert.AreEqual(
            3d,
            EvaluateNumber(
                engine,
                "=GAMMA.INV(GAMMA.DIST(3,2,3,TRUE()),2,3)",
                context),
            1e-12d);
        Assert.AreEqual(
            2d,
            EvaluateNumber(
                engine,
                "=CHISQ.INV(CHISQ.DIST(2,4,TRUE()),4)",
                context),
            1e-12d);
        Assert.AreEqual(
            0.5d,
            EvaluateNumber(
                engine,
                "=T.INV(T.DIST(0.5,10,TRUE()),10)",
                context),
            1e-12d);
        Assert.AreEqual(
            1d,
            EvaluateNumber(
                engine,
                "=F.INV(F.DIST(1,6,6,TRUE()),6,6)",
                context),
            1e-12d);
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
