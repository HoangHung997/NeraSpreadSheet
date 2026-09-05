using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

/// <summary>
/// First-generation beta, gamma, chi-square, Student-t and F distribution
/// functions. Inverse functions use bounded searches and return #N/A when the
/// numerical primitive cannot converge within its contract.
/// </summary>
internal static class ContinuousDistributionFormulaFunctions
{
    private const double MaximumDegreesOfFreedom = 10_000_000_000d;

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return CreateScalarDefinition("BETA.DIST", 4, 6, EvaluateBetaDistribution);
        yield return CreateScalarDefinition("BETA.INV", 3, 5, EvaluateBetaInverse);
        yield return CreateScalarDefinition("GAMMA.DIST", 4, 4, EvaluateGammaDistribution);
        yield return CreateScalarDefinition("GAMMA.INV", 3, 3, EvaluateGammaInverse);

        yield return CreateScalarDefinition("CHISQ.DIST", 3, 3, EvaluateChiSquareDistribution);
        yield return CreateScalarDefinition("CHISQ.DIST.RT", 2, 2, EvaluateChiSquareRightTail);
        yield return CreateScalarDefinition("CHISQ.INV", 2, 2, EvaluateChiSquareInverse);
        yield return CreateScalarDefinition("CHISQ.INV.RT", 2, 2, EvaluateChiSquareInverseRightTail);

        yield return CreateScalarDefinition("T.DIST", 3, 3, EvaluateStudentTDistribution);
        yield return CreateScalarDefinition("T.DIST.RT", 2, 2, EvaluateStudentTRightTail);
        yield return CreateScalarDefinition("T.DIST.2T", 2, 2, EvaluateStudentTTwoTail);
        yield return CreateScalarDefinition("T.INV", 2, 2, EvaluateStudentTInverse);
        yield return CreateScalarDefinition("T.INV.2T", 2, 2, EvaluateStudentTInverseTwoTail);

        yield return CreateScalarDefinition("F.DIST", 4, 4, EvaluateFDistribution);
        yield return CreateScalarDefinition("F.DIST.RT", 3, 3, EvaluateFRightTail);
        yield return CreateScalarDefinition("F.INV", 3, 3, EvaluateFInverse);
        yield return CreateScalarDefinition("F.INV.RT", 3, 3, EvaluateFInverseRightTail);
    }

    private static FormulaFunctionDefinition CreateScalarDefinition(
        string name,
        int minimumArguments,
        int maximumArguments,
        Func<FormulaFunctionInvocation, FormulaEvaluationResult> evaluator) =>
        new(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity("NERA.BUILTIN", name),
                new FormulaFunctionVersion(1, 0, 0),
                FormulaFunctionApiVersion.Current,
                minimumArguments,
                maximumArguments,
                FormulaFunctionCapabilities.ScalarArguments |
                FormulaFunctionCapabilities.ReturnsScalar,
                argumentCountPolicy:
                    FormulaFunctionArgumentCountPolicy.LogicalArguments),
            evaluator);

    private static FormulaEvaluationResult EvaluateBetaDistribution(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(invocation.Arguments[0], out var x, out var error) ||
            !TryGetScalarNumber(invocation.Arguments[1], out var alpha, out error) ||
            !TryGetScalarNumber(invocation.Arguments[2], out var beta, out error) ||
            !TryGetScalarBoolean(invocation.Arguments[3], out var cumulative, out error) ||
            !TryGetBounds(invocation, 4, out var lower, out var upper, out error))
        {
            return error;
        }
        if (alpha <= 0d || beta <= 0d || upper <= lower ||
            x < lower || x > upper)
        {
            return NumericError();
        }
        var normalized = (x - lower) / (upper - lower);
        if (cumulative)
        {
            return AdvancedDistributionNumerics.TryRegularizedBeta(
                    alpha,
                    beta,
                    normalized,
                    out var probability)
                ? Number(probability)
                : NotAvailable();
        }
        return Number(BetaDensity(normalized, alpha, beta) /
                      (upper - lower));
    }

    private static FormulaEvaluationResult EvaluateBetaInverse(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out var probability,
                out var error) ||
            !TryGetScalarNumber(invocation.Arguments[1], out var alpha, out error) ||
            !TryGetScalarNumber(invocation.Arguments[2], out var beta, out error) ||
            !TryGetBounds(invocation, 3, out var lower, out var upper, out error))
        {
            return error;
        }
        if (probability <= 0d || probability > 1d ||
            alpha <= 0d || beta <= 0d || upper <= lower)
        {
            return NumericError();
        }
        if (probability == 1d)
        {
            return Number(upper);
        }
        return AdvancedDistributionNumerics.TryInverseRegularizedBeta(
                probability,
                alpha,
                beta,
                out var normalized)
            ? Number(lower + ((upper - lower) * normalized))
            : NotAvailable();
    }

    private static FormulaEvaluationResult EvaluateGammaDistribution(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(invocation.Arguments[0], out var x, out var error) ||
            !TryGetScalarNumber(invocation.Arguments[1], out var alpha, out error) ||
            !TryGetScalarNumber(invocation.Arguments[2], out var beta, out error) ||
            !TryGetScalarBoolean(invocation.Arguments[3], out var cumulative, out error))
        {
            return error;
        }
        if (x < 0d || alpha <= 0d || beta <= 0d)
        {
            return NumericError();
        }
        if (cumulative)
        {
            return AdvancedDistributionNumerics.TryRegularizedGammaP(
                    alpha,
                    x / beta,
                    out var probability)
                ? Number(probability)
                : NotAvailable();
        }
        return Number(GammaDensity(x, alpha, beta));
    }

    private static FormulaEvaluationResult EvaluateGammaInverse(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out var probability,
                out var error) ||
            !TryGetScalarNumber(invocation.Arguments[1], out var alpha, out error) ||
            !TryGetScalarNumber(invocation.Arguments[2], out var beta, out error))
        {
            return error;
        }
        if (probability < 0d || probability > 1d ||
            alpha <= 0d || beta <= 0d)
        {
            return NumericError();
        }
        if (probability == 0d)
        {
            return Number(0d);
        }
        if (probability == 1d)
        {
            return NumericError();
        }
        return AdvancedDistributionNumerics.TryInverseRegularizedGammaP(
                probability,
                alpha,
                out var standardized)
            ? Number(beta * standardized)
            : NotAvailable();
    }

    private static FormulaEvaluationResult EvaluateChiSquareDistribution(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(invocation.Arguments[0], out var x, out var error) ||
            !TryGetDegreesOfFreedom(invocation.Arguments[1], out var degrees, out error) ||
            !TryGetScalarBoolean(invocation.Arguments[2], out var cumulative, out error))
        {
            return error;
        }
        if (x < 0d)
        {
            return NumericError();
        }
        if (cumulative)
        {
            return AdvancedDistributionNumerics.TryRegularizedGammaP(
                    degrees / 2d,
                    x / 2d,
                    out var probability)
                ? Number(probability)
                : NotAvailable();
        }
        return Number(GammaDensity(x, degrees / 2d, 2d));
    }

    private static FormulaEvaluationResult EvaluateChiSquareRightTail(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(invocation.Arguments[0], out var x, out var error) ||
            !TryGetDegreesOfFreedom(invocation.Arguments[1], out var degrees, out error))
        {
            return error;
        }
        if (x < 0d)
        {
            return NumericError();
        }
        return StatisticalNumerics.TryRegularizedGammaQ(
                degrees / 2d,
                x / 2d,
                out var probability)
            ? Number(probability)
            : NotAvailable();
    }

    private static FormulaEvaluationResult EvaluateChiSquareInverse(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out var probability,
                out var error) ||
            !TryGetDegreesOfFreedom(invocation.Arguments[1], out var degrees, out error))
        {
            return error;
        }
        if (probability < 0d || probability > 1d)
        {
            return NumericError();
        }
        if (probability == 0d)
        {
            return Number(0d);
        }
        if (probability == 1d)
        {
            return NumericError();
        }
        return AdvancedDistributionNumerics.TryInverseRegularizedGammaP(
                probability,
                degrees / 2d,
                out var standardized)
            ? Number(2d * standardized)
            : NotAvailable();
    }

    private static FormulaEvaluationResult EvaluateChiSquareInverseRightTail(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out var probability,
                out var error) ||
            !TryGetDegreesOfFreedom(invocation.Arguments[1], out var degrees, out error))
        {
            return error;
        }
        if (probability < 0d || probability > 1d)
        {
            return NumericError();
        }
        if (probability == 1d)
        {
            return Number(0d);
        }
        if (probability == 0d)
        {
            return NumericError();
        }
        return AdvancedDistributionNumerics.TryInverseRegularizedGammaP(
                1d - probability,
                degrees / 2d,
                out var standardized)
            ? Number(2d * standardized)
            : NotAvailable();
    }

    private static FormulaEvaluationResult EvaluateStudentTDistribution(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(invocation.Arguments[0], out var x, out var error) ||
            !TryGetDegreesOfFreedom(invocation.Arguments[1], out var degrees, out error) ||
            !TryGetScalarBoolean(invocation.Arguments[2], out var cumulative, out error))
        {
            return error;
        }
        if (!cumulative)
        {
            return Number(AdvancedDistributionNumerics.StudentTDensity(x, degrees));
        }
        return AdvancedDistributionNumerics.TryStudentTCumulative(
                x,
                degrees,
                out var probability)
            ? Number(probability)
            : NotAvailable();
    }

    private static FormulaEvaluationResult EvaluateStudentTRightTail(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(invocation.Arguments[0], out var x, out var error) ||
            !TryGetDegreesOfFreedom(invocation.Arguments[1], out var degrees, out error))
        {
            return error;
        }
        return AdvancedDistributionNumerics.TryStudentTCumulative(
                x,
                degrees,
                out var cumulative)
            ? Number(1d - cumulative)
            : NotAvailable();
    }

    private static FormulaEvaluationResult EvaluateStudentTTwoTail(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(invocation.Arguments[0], out var x, out var error) ||
            !TryGetDegreesOfFreedom(invocation.Arguments[1], out var degrees, out error))
        {
            return error;
        }
        if (x < 0d)
        {
            return NumericError();
        }
        return AdvancedDistributionNumerics.TryStudentTCumulative(
                x,
                degrees,
                out var cumulative)
            ? Number(2d * (1d - cumulative))
            : NotAvailable();
    }

    private static FormulaEvaluationResult EvaluateStudentTInverse(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out var probability,
                out var error) ||
            !TryGetDegreesOfFreedom(invocation.Arguments[1], out var degrees, out error))
        {
            return error;
        }
        if (probability <= 0d || probability >= 1d)
        {
            return NumericError();
        }
        return AdvancedDistributionNumerics.TryInverseStudentT(
                probability,
                degrees,
                out var value)
            ? Number(value)
            : NotAvailable();
    }

    private static FormulaEvaluationResult EvaluateStudentTInverseTwoTail(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out var probability,
                out var error) ||
            !TryGetDegreesOfFreedom(invocation.Arguments[1], out var degrees, out error))
        {
            return error;
        }
        if (probability <= 0d || probability > 1d)
        {
            return NumericError();
        }
        return AdvancedDistributionNumerics.TryInverseStudentT(
                1d - (probability / 2d),
                degrees,
                out var value)
            ? Number(value)
            : NotAvailable();
    }

    private static FormulaEvaluationResult EvaluateFDistribution(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(invocation.Arguments[0], out var x, out var error) ||
            !TryGetDegreesOfFreedom(invocation.Arguments[1], out var degrees1, out error) ||
            !TryGetDegreesOfFreedom(invocation.Arguments[2], out var degrees2, out error) ||
            !TryGetScalarBoolean(invocation.Arguments[3], out var cumulative, out error))
        {
            return error;
        }
        if (x < 0d)
        {
            return NumericError();
        }
        if (!cumulative)
        {
            return Number(AdvancedDistributionNumerics.FDensity(
                x,
                degrees1,
                degrees2));
        }
        return AdvancedDistributionNumerics.TryFCumulative(
                x,
                degrees1,
                degrees2,
                out var probability)
            ? Number(probability)
            : NotAvailable();
    }

    private static FormulaEvaluationResult EvaluateFRightTail(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(invocation.Arguments[0], out var x, out var error) ||
            !TryGetDegreesOfFreedom(invocation.Arguments[1], out var degrees1, out error) ||
            !TryGetDegreesOfFreedom(invocation.Arguments[2], out var degrees2, out error))
        {
            return error;
        }
        if (x < 0d)
        {
            return NumericError();
        }
        return AdvancedDistributionNumerics.TryFCumulative(
                x,
                degrees1,
                degrees2,
                out var cumulative)
            ? Number(1d - cumulative)
            : NotAvailable();
    }

    private static FormulaEvaluationResult EvaluateFInverse(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out var probability,
                out var error) ||
            !TryGetDegreesOfFreedom(invocation.Arguments[1], out var degrees1, out error) ||
            !TryGetDegreesOfFreedom(invocation.Arguments[2], out var degrees2, out error))
        {
            return error;
        }
        if (probability < 0d || probability >= 1d)
        {
            return NumericError();
        }
        return AdvancedDistributionNumerics.TryInverseF(
                probability,
                degrees1,
                degrees2,
                out var value)
            ? Number(value)
            : NotAvailable();
    }

    private static FormulaEvaluationResult EvaluateFInverseRightTail(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out var probability,
                out var error) ||
            !TryGetDegreesOfFreedom(invocation.Arguments[1], out var degrees1, out error) ||
            !TryGetDegreesOfFreedom(invocation.Arguments[2], out var degrees2, out error))
        {
            return error;
        }
        if (probability < 0d || probability > 1d)
        {
            return NumericError();
        }
        if (probability == 1d)
        {
            return Number(0d);
        }
        if (probability == 0d)
        {
            return NumericError();
        }
        return AdvancedDistributionNumerics.TryInverseF(
                1d - probability,
                degrees1,
                degrees2,
                out var value)
            ? Number(value)
            : NotAvailable();
    }

    private static double BetaDensity(
        double x,
        double alpha,
        double beta)
    {
        if (x == 0d)
        {
            if (alpha > 1d)
            {
                return 0d;
            }
            return alpha == 1d ? beta : double.PositiveInfinity;
        }
        if (x == 1d)
        {
            if (beta > 1d)
            {
                return 0d;
            }
            return beta == 1d ? alpha : double.PositiveInfinity;
        }
        var logBeta = StatisticalNumerics.LogGamma(alpha) +
                      StatisticalNumerics.LogGamma(beta) -
                      StatisticalNumerics.LogGamma(alpha + beta);
        return Math.Exp(
            ((alpha - 1d) * Math.Log(x)) +
            ((beta - 1d) * Math.Log(1d - x)) -
            logBeta);
    }

    private static double GammaDensity(
        double x,
        double alpha,
        double beta)
    {
        if (x == 0d)
        {
            if (alpha > 1d)
            {
                return 0d;
            }
            return alpha == 1d
                ? 1d / beta
                : double.PositiveInfinity;
        }
        return Math.Exp(
            ((alpha - 1d) * Math.Log(x)) -
            (x / beta) -
            StatisticalNumerics.LogGamma(alpha) -
            (alpha * Math.Log(beta)));
    }

    private static bool TryGetBounds(
        FormulaFunctionInvocation invocation,
        int firstOptionalIndex,
        out double lower,
        out double upper,
        out FormulaEvaluationResult error)
    {
        lower = 0d;
        upper = 1d;
        if (invocation.Arguments.Count > firstOptionalIndex &&
            !TryGetScalarNumber(
                invocation.Arguments[firstOptionalIndex],
                out lower,
                out error))
        {
            return false;
        }
        if (invocation.Arguments.Count > firstOptionalIndex + 1 &&
            !TryGetScalarNumber(
                invocation.Arguments[firstOptionalIndex + 1],
                out upper,
                out error))
        {
            return false;
        }
        error = default!;
        return true;
    }

    private static bool TryGetDegreesOfFreedom(
        FormulaFunctionArgument argument,
        out double degrees,
        out FormulaEvaluationResult error)
    {
        if (!TryGetScalarNumber(argument, out var number, out error))
        {
            degrees = default;
            return false;
        }
        degrees = Math.Truncate(number);
        if (degrees < 1d || degrees > MaximumDegreesOfFreedom)
        {
            degrees = default;
            error = NumericError();
            return false;
        }
        return true;
    }

    private static bool TryGetScalarNumber(
        FormulaFunctionArgument argument,
        out double number,
        out FormulaEvaluationResult error)
    {
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar ||
            !FormulaValueCoercion.TryNumber(
                argument.ScalarValue,
                out number,
                allowText: true) ||
            !double.IsFinite(number))
        {
            number = default;
            error = InvalidValue();
            return false;
        }
        error = default!;
        return true;
    }

    private static bool TryGetScalarBoolean(
        FormulaFunctionArgument argument,
        out bool value,
        out FormulaEvaluationResult error)
    {
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar ||
            !FormulaValueCoercion.TryBoolean(argument.ScalarValue, out value))
        {
            value = default;
            error = InvalidValue();
            return false;
        }
        error = default!;
        return true;
    }

    private static FormulaEvaluationResult Number(double value)
    {
        if (!double.IsFinite(value))
        {
            return NumericError();
        }
        return FormulaEvaluationResult.Success(CellValue.FromNumber(value));
    }

    private static FormulaEvaluationResult InvalidValue() =>
        FormulaEvaluationResult.Failure(FormulaErrorCode.InvalidValue);

    private static FormulaEvaluationResult NotAvailable() =>
        FormulaEvaluationResult.Failure(FormulaErrorCode.NotAvailable);

    private static FormulaEvaluationResult NumericError() =>
        new(
            CellValue.FromError("#NUM!"),
            FormulaErrorCode.InvalidValue,
            Array.Empty<FormulaDependency>());
}
