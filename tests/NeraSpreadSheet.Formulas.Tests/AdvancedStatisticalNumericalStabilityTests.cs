using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class AdvancedStatisticalNumericalStabilityTests
{
    [TestMethod]
    public void OnlinePairedMomentsRemainStableWithLargeOffsets()
    {
        var values = new Dictionary<CellAddress, CellValue>();
        for (var index = 0; index < 5; index++)
        {
            var x = 1_000_000_000_000d + index + 1d;
            values[new CellAddress(index, 0)] = CellValue.FromNumber(x);
            values[new CellAddress(index, 1)] =
                CellValue.FromNumber((3d * x) - 7d);
        }
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        Assert.AreEqual(
            1d,
            EvaluateNumber(engine, "=CORREL(A1:A5,B1:B5)", context),
            1e-12d);
        Assert.AreEqual(
            3d,
            EvaluateNumber(engine, "=SLOPE(B1:B5,A1:A5)", context),
            1e-12d);
        Assert.AreEqual(
            6d,
            EvaluateNumber(
                engine,
                "=COVARIANCE.P(A1:A5,B1:B5)",
                context),
            1e-12d);
    }

    [TestMethod]
    public void StandardNormalInverseRoundTripsCentralAndTailProbabilities()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();
        var probabilities = new[]
        {
            0.000001d,
            0.001d,
            0.025d,
            0.5d,
            0.975d,
            0.999d,
            0.999999d,
        };

        foreach (var probability in probabilities)
        {
            var text = probability.ToString(
                "R",
                CultureInfo.InvariantCulture);
            var inverse = EvaluateNumber(
                engine,
                $"=NORM.S.INV({text})",
                context);
            var roundTrip = EvaluateNumber(
                engine,
                $"=NORM.S.DIST(NORM.S.INV({text}),TRUE())",
                context);
            Assert.AreEqual(
                probability,
                roundTrip,
                5e-8d,
                $"Round-trip failed for probability {text} and z={inverse:R}.");
        }

        var lower = EvaluateNumber(
            engine,
            "=NORM.S.INV(0.000001)",
            context);
        var upper = EvaluateNumber(
            engine,
            "=NORM.S.INV(0.999999)",
            context);
        Assert.AreEqual(0d, lower + upper, 2e-7d);
    }

    [TestMethod]
    public void DiscreteDistributionBoundaryProbabilitiesAreExact()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            1d,
            EvaluateNumber(engine, "=BINOM.DIST(0,10,0,FALSE())", context));
        Assert.AreEqual(
            1d,
            EvaluateNumber(engine, "=BINOM.DIST(7,10,0,TRUE())", context));
        Assert.AreEqual(
            0d,
            EvaluateNumber(engine, "=BINOM.DIST(9,10,1,TRUE())", context));
        Assert.AreEqual(
            1d,
            EvaluateNumber(engine, "=BINOM.DIST(10,10,1,TRUE())", context));
        Assert.AreEqual(
            1d,
            EvaluateNumber(engine, "=BINOM.DIST(10,10,1,FALSE())", context));
        Assert.AreEqual(
            0d,
            EvaluateNumber(engine, "=BINOM.DIST(9,10,1,FALSE())", context));
    }

    [TestMethod]
    public void PoissonCumulativeIsMonotonicAndBounded()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();
        var previous = 0d;

        for (var events = 0; events <= 20; events++)
        {
            var current = EvaluateNumber(
                engine,
                $"=POISSON.DIST({events},3,TRUE())",
                context);
            Assert.IsTrue(current >= previous - 1e-14d);
            Assert.IsTrue(current is >= 0d and <= 1d);
            previous = current;
        }
        Assert.IsTrue(previous > 0.999999d);
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
