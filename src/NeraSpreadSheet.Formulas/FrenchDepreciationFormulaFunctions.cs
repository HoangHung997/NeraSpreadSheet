using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

/// <summary>
/// French-accounting linear and accelerated depreciation functions.
/// </summary>
internal static class FrenchDepreciationFormulaFunctions
{
    public const int MaximumAcceleratedPeriods = 100_000;

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return CreateDefinition(
            "AMORLINC",
            EvaluateLinearDepreciation);
        yield return CreateDefinition(
            "AMORDEGRC",
            EvaluateAcceleratedDepreciation);
    }

    private static FormulaFunctionDefinition CreateDefinition(
        string name,
        Func<FormulaFunctionInvocation, FormulaEvaluationResult> evaluator) =>
        new(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity("NERA.BUILTIN", name),
                new FormulaFunctionVersion(1, 0, 0),
                FormulaFunctionApiVersion.Current,
                6,
                7,
                FormulaFunctionCapabilities.ScalarArguments |
                FormulaFunctionCapabilities.ReturnsScalar,
                argumentCountPolicy:
                    FormulaFunctionArgumentCountPolicy.LogicalArguments),
            evaluator);

    private static FormulaEvaluationResult EvaluateLinearDepreciation(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetArguments(
                invocation,
                out var arguments,
                out var error))
        {
            return error;
        }

        var firstDepreciation =
            arguments.Cost *
            arguments.Rate *
            FinancialDateMath.GetYearFraction(
                arguments.PurchaseDate,
                arguments.FirstPeriod,
                arguments.Basis);
        var fullPeriodDepreciation =
            arguments.Cost * arguments.Rate;
        var depreciableAmount =
            arguments.Cost - arguments.Salvage;
        if (!double.IsFinite(firstDepreciation) ||
            !double.IsFinite(fullPeriodDepreciation) ||
            !double.IsFinite(depreciableAmount))
        {
            return NumericError();
        }

        if (arguments.Period == 0)
        {
            return Number(Math.Max(0d, firstDepreciation));
        }

        var remainingAfterFirst =
            depreciableAmount - firstDepreciation;
        if (remainingAfterFirst <= 0d)
        {
            return Number(0d);
        }

        var fullPeriods = Math.Floor(
            remainingAfterFirst / fullPeriodDepreciation);
        if (!double.IsFinite(fullPeriods) || fullPeriods < 0d)
        {
            return NumericError();
        }

        if (arguments.Period <= fullPeriods)
        {
            return Number(fullPeriodDepreciation);
        }
        if (arguments.Period == fullPeriods + 1d)
        {
            var finalDepreciation =
                depreciableAmount -
                (fullPeriodDepreciation * fullPeriods) -
                firstDepreciation;
            return Number(Math.Max(0d, finalDepreciation));
        }

        return Number(0d);
    }

    private static FormulaEvaluationResult EvaluateAcceleratedDepreciation(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetArguments(
                invocation,
                out var arguments,
                out var error))
        {
            return error;
        }

        var usefulLife = 1d / arguments.Rate;
        if (!double.IsFinite(usefulLife) ||
            usefulLife < 3d ||
            (usefulLife > 4d && usefulLife < 5d))
        {
            return NumericError();
        }

        var coefficient = usefulLife switch
        {
            < 5d => 1.5d,
            <= 6d => 2d,
            _ => 2.5d,
        };
        var acceleratedRate = arguments.Rate * coefficient;
        var firstDepreciation = RoundCurrency(
            FinancialDateMath.GetYearFraction(
                arguments.PurchaseDate,
                arguments.FirstPeriod,
                arguments.Basis) *
            acceleratedRate *
            arguments.Cost);
        if (!double.IsFinite(acceleratedRate) ||
            !double.IsFinite(firstDepreciation))
        {
            return NumericError();
        }
        if (arguments.Period == 0)
        {
            return Number(firstDepreciation);
        }

        var currentCost = arguments.Cost - firstDepreciation;
        var remainingDepreciable =
            currentCost - arguments.Salvage;
        var depreciation = firstDepreciation;
        for (var periodIndex = 0;
             periodIndex < arguments.Period;
             periodIndex++)
        {
            if (periodIndex >= MaximumAcceleratedPeriods)
            {
                return NumericError();
            }

            depreciation = RoundCurrency(
                acceleratedRate * currentCost);
            if (!double.IsFinite(depreciation))
            {
                return NumericError();
            }

            remainingDepreciable -= depreciation;
            if (remainingDepreciable < 0d)
            {
                return Number(
                    arguments.Period - periodIndex <= 1
                        ? RoundCurrency(currentCost * 0.5d)
                        : 0d);
            }

            currentCost -= depreciation;
            if (!double.IsFinite(currentCost))
            {
                return NumericError();
            }
        }

        return Number(depreciation);
    }

    private static bool TryGetArguments(
        FormulaFunctionInvocation invocation,
        out DepreciationArguments arguments,
        out FormulaEvaluationResult error)
    {
        arguments = default;
        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out var cost,
                out error) ||
            !TryGetScalarDate(
                invocation.Arguments[1],
                out var purchaseDate,
                out error) ||
            !TryGetScalarDate(
                invocation.Arguments[2],
                out var firstPeriod,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[3],
                out var salvage,
                out error) ||
            !TryGetTruncatedInteger(
                invocation.Arguments[4],
                out var period,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[5],
                out var rate,
                out error))
        {
            return false;
        }

        var basis = 0;
        if (invocation.Arguments.Count == 7 &&
            !TryGetTruncatedInteger(
                invocation.Arguments[6],
                out basis,
                out error))
        {
            return false;
        }

        if (cost <= 0d ||
            salvage < 0d ||
            salvage > cost ||
            purchaseDate > firstPeriod ||
            period < 0 ||
            rate <= 0d ||
            !IsSupportedBasis(basis))
        {
            error = NumericError();
            return false;
        }

        arguments = new DepreciationArguments(
            cost,
            purchaseDate,
            firstPeriod,
            salvage,
            period,
            rate,
            (FinancialDayCountBasis)basis);
        error = default!;
        return true;
    }

    private static bool IsSupportedBasis(int basis) =>
        basis is 0 or 1 or 3 or 4;

    private static bool TryGetScalarDate(
        FormulaFunctionArgument argument,
        out DateTime date,
        out FormulaEvaluationResult error)
    {
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar ||
            !FormulaValueCoercion.TryDateTime(
                argument.ScalarValue,
                out date,
                allowText: true))
        {
            date = default;
            error = InvalidValue();
            return false;
        }

        date = date.Date;
        error = default!;
        return true;
    }

    private static bool TryGetScalarNumber(
        FormulaFunctionArgument argument,
        out double value,
        out FormulaEvaluationResult error)
    {
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar ||
            !FormulaValueCoercion.TryNumber(
                argument.ScalarValue,
                out value,
                allowText: true) ||
            !double.IsFinite(value))
        {
            value = default;
            error = InvalidValue();
            return false;
        }

        error = default!;
        return true;
    }

    private static bool TryGetTruncatedInteger(
        FormulaFunctionArgument argument,
        out int value,
        out FormulaEvaluationResult error)
    {
        if (!TryGetScalarNumber(argument, out var number, out error))
        {
            value = default;
            return false;
        }

        var truncated = Math.Truncate(number);
        if (truncated < int.MinValue ||
            truncated > int.MaxValue)
        {
            value = default;
            error = NumericError();
            return false;
        }

        value = checked((int)truncated);
        error = default!;
        return true;
    }

    private static double RoundCurrency(double value) =>
        Math.Round(
            value,
            0,
            MidpointRounding.AwayFromZero);

    private static FormulaEvaluationResult Number(double value) =>
        double.IsFinite(value)
            ? FormulaEvaluationResult.Success(
                CellValue.FromNumber(value))
            : NumericError();

    private static FormulaEvaluationResult InvalidValue() =>
        FormulaEvaluationResult.Failure(
            FormulaErrorCode.InvalidValue);

    private static FormulaEvaluationResult NumericError() =>
        new(
            CellValue.FromError("#NUM!"),
            FormulaErrorCode.InvalidValue,
            Array.Empty<FormulaDependency>());

    private readonly record struct DepreciationArguments(
        double Cost,
        DateTime PurchaseDate,
        DateTime FirstPeriod,
        double Salvage,
        int Period,
        double Rate,
        FinancialDayCountBasis Basis);
}
