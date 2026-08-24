using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class ContinuousDistributionFunctionTests
{
    [TestMethod]
    public void BetaAndGammaFamiliesMatchReferenceValuesAndRoundTrip()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            0.5248d,
            EvaluateNumber(
                engine,
                "=BETA.DIST(0.4,2,3,TRUE())",
                context),
            2e-12d);
        Assert.AreEqual(
            1.728d,
            EvaluateNumber(
                engine,
                "=BETA.DIST(0.4,2,3,FALSE())",
                context),
            2e-12d);
        Assert.AreEqual(
            0.26171875d,
            EvaluateNumber(
                engine,
                "=BETA.DIST(3,2,3,TRUE(),2,6)",
                context),
            2e-12d);
        Assert.AreEqual(
            3d,
            EvaluateNumber(
                engine,
                "=BETA.INV(BETA.DIST(3,2,3,TRUE(),2,6),2,3,2,6)",
                context),
            2e-9d);

        Assert.AreEqual(
            0.38494001106330406d,
            EvaluateNumber(
                engine,
                "=GAMMA.DIST(4,2,3,TRUE())",
                context),
            2e-12d);
        Assert.AreEqual(
            0.11715428360698966d,
            EvaluateNumber(
                engine,
                "=GAMMA.DIST(4,2,3,FALSE())",
                context),
            2e-12d);
        Assert.AreEqual(
            4d,
            EvaluateNumber(
                engine,
                "=GAMMA.INV(GAMMA.DIST(4,2,3,TRUE()),2,3)",
                context),
            2e-9d);
    }

    [TestMethod]
    public void ChiSquareFamiliesMatchReferenceValuesAndComplementEachOther()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        var cumulative = EvaluateNumber(
            engine,
            "=CHISQ.DIST(5,4,TRUE())",
            context);
        var rightTail = EvaluateNumber(
            engine,
            "=CHISQ.DIST.RT(5,4)",
            context);

        Assert.AreEqual(0.7127025048163542d, cumulative, 2e-12d);
        Assert.AreEqual(0.2872974951836458d, rightTail, 2e-12d);
        Assert.AreEqual(1d, cumulative + rightTail, 2e-12d);
        Assert.AreEqual(
            9.487729036781154d,
            EvaluateNumber(
                engine,
                "=CHISQ.INV(0.95,4)",
                context),
            2e-8d);
        Assert.AreEqual(
            9.487729036781154d,
            EvaluateNumber(
                engine,
                "=CHISQ.INV.RT(0.05,4)",
                context),
            2e-8d);
    }

    [TestMethod]
    public void StudentTAndFFamiliesMatchReferenceValuesAndInverseTails()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            0.9177463367772799d,
            EvaluateNumber(
                engine,
                "=T.DIST(1.5,10,TRUE())",
                context),
            2e-12d);
        Assert.AreEqual(
            0.1274447942870917d,
            EvaluateNumber(
                engine,
                "=T.DIST(1.5,10,FALSE())",
                context),
            2e-12d);
        Assert.AreEqual(
            0.08225366322272007d,
            EvaluateNumber(
                engine,
                "=T.DIST.RT(1.5,10)",
                context),
            2e-12d);
        Assert.AreEqual(
            0.16450732644544014d,
            EvaluateNumber(
                engine,
                "=T.DIST.2T(1.5,10)",
                context),
            4e-12d);
        Assert.AreEqual(
            2.228138851986274d,
            EvaluateNumber(
                engine,
                "=T.INV(0.975,10)",
                context),
            2e-8d);
        Assert.AreEqual(
            2.228138851986274d,
            EvaluateNumber(
                engine,
                "=T.INV.2T(0.05,10)",
                context),
            2e-8d);

        Assert.AreEqual(
            0.8358050491002611d,
            EvaluateNumber(
                engine,
                "=F.DIST(2,5,10,TRUE())",
                context),
            2e-12d);
        Assert.AreEqual(
            0.16200574218011515d,
            EvaluateNumber(
                engine,
                "=F.DIST(2,5,10,FALSE())",
                context),
            2e-12d);
        Assert.AreEqual(
            0.16419495089973887d,
            EvaluateNumber(
                engine,
                "=F.DIST.RT(2,5,10)",
                context),
            2e-12d);
        Assert.AreEqual(
            3.3258345304130104d,
            EvaluateNumber(
                engine,
                "=F.INV(0.95,5,10)",
                context),
            2e-8d);
        Assert.AreEqual(
            3.3258345304130104d,
            EvaluateNumber(
                engine,
                "=F.INV.RT(0.05,5,10)",
                context),
            2e-8d);
    }

    [TestMethod]
    public void EndpointProbabilitiesAndDegreesOfFreedomAreDeterministic()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            6d,
            EvaluateNumber(
                engine,
                "=BETA.INV(1,2,3,2,6)",
                context));
        Assert.AreEqual(
            0d,
            EvaluateNumber(
                engine,
                "=GAMMA.INV(0,2,3)",
                context));
        Assert.AreEqual(
            0d,
            EvaluateNumber(
                engine,
                "=CHISQ.INV(0,4)",
                context));
        Assert.AreEqual(
            0d,
            EvaluateNumber(
                engine,
                "=CHISQ.INV.RT(1,4)",
                context));
        Assert.AreEqual(
            0d,
            EvaluateNumber(
                engine,
                "=T.INV.2T(1,10)",
                context));
        Assert.AreEqual(
            0d,
            EvaluateNumber(
                engine,
                "=F.INV(0,5,10)",
                context));
        Assert.AreEqual(
            0d,
            EvaluateNumber(
                engine,
                "=F.INV.RT(1,5,10)",
                context));

        Assert.AreEqual(
            EvaluateNumber(
                engine,
                "=T.DIST(1.5,10,TRUE())",
                context),
            EvaluateNumber(
                engine,
                "=T.DIST(1.5,10.9,TRUE())",
                context),
            1e-15d);
    }

    [TestMethod]
    public void ContinuousDistributionDomainsAndScalarContractsAreExplicit()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(0.25d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(0.75d),
        };
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        AssertNumericError(
            engine,
            "=BETA.DIST(0.5,0,2,TRUE())",
            context);
        AssertNumericError(
            engine,
            "=BETA.DIST(3,2,3,TRUE(),6,2)",
            context);
        AssertNumericError(
            engine,
            "=BETA.INV(0,2,3)",
            context);
        AssertNumericError(
            engine,
            "=GAMMA.DIST(-1,2,3,TRUE())",
            context);
        AssertNumericError(
            engine,
            "=GAMMA.INV(1,2,3)",
            context);
        AssertNumericError(
            engine,
            "=CHISQ.DIST(-1,4,TRUE())",
            context);
        AssertNumericError(
            engine,
            "=CHISQ.INV(1,4)",
            context);
        AssertNumericError(
            engine,
            "=T.DIST.2T(-1,10)",
            context);
        AssertNumericError(
            engine,
            "=T.INV(0,10)",
            context);
        AssertNumericError(
            engine,
            "=F.DIST(-1,5,10,TRUE())",
            context);
        AssertNumericError(
            engine,
            "=F.INV(1,5,10)",
            context);
        AssertNumericError(
            engine,
            "=F.INV.RT(0,5,10)",
            context);

        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            engine.Evaluate(
                "=T.DIST(A1:A2,10,TRUE())",
                context).ErrorCode);
    }

    private static void AssertNumericError(
        NeraFormulaEngine engine,
        string formula,
        IFormulaEvaluationContext context)
    {
        var result = engine.Evaluate(formula, context);
        Assert.AreEqual("#NUM!", result.Value.RawValue, formula);
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
