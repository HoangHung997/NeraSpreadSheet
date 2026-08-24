using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Securities that accrue or discount one payment between two dates. All five
/// functions share the validated financial day-count layer.
/// </summary>
internal static class MaturitySecurityFormulaFunctions
{
    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return CreateDefinition(
            "ACCRINTM",
            3,
            5,
            EvaluateAccruedInterestAtMaturity);
        yield return CreateDefinition(
            "DISC",
            4,
            5,
            EvaluateDiscountRate);
        yield return CreateDefinition(
            "INTRATE",
            4,
            5,
            EvaluateInterestRate);
        yield return CreateDefinition(
            "RECEIVED",
            4,
            5,
            EvaluateReceivedAmount);
        yield return CreateDefinition(
            "PRICEDISC",
            4,
            5,
            EvaluateDiscountedPrice);
    }

    private static FormulaFunctionDefinition CreateDefinition(
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

    private static FormulaEvaluationResult EvaluateAccruedInterestAtMaturity(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarDate(
                invocation.Arguments[0],
                out var issue,
                out var error) ||
            !TryGetScalarDate(
                invocation.Arguments[1],
                out var settlement,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[2],
                out var rate,
                out error))
        {
            return error;
        }

        var par = 1000d;
        if (invocation.Arguments.Count >= 4 &&
            !TryGetScalarNumber(
                invocation.Arguments[3],
                out par,
                out error))
        {
            return error;
        }

        var basis = 0;
        if (invocation.Arguments.Count == 5 &&
            !TryGetBasis(
                invocation.Arguments[4],
                out basis,
                out error))
        {
            return error;
        }

        if (issue >= settlement || rate <= 0d || par <= 0d)
        {
            return NumericError();
        }

        var fraction = FinancialDateMath.GetYearFraction(
            issue,
            settlement,
            (FinancialDayCountBasis)basis);
        return Number(par * rate * fraction);
    }

    private static FormulaEvaluationResult EvaluateDiscountRate(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetMaturityArguments(
                invocation,
                out var settlement,
                out var maturity,
                out var price,
                out var redemption,
                out var basis,
                out var error))
        {
            return error;
        }

        if (price <= 0d || redemption <= 0d)
        {
            return NumericError();
        }

        var fraction = FinancialDateMath.GetYearFraction(
            settlement,
            maturity,
            (FinancialDayCountBasis)basis);
        return Divide(
            redemption - price,
            redemption * fraction);
    }

    private static FormulaEvaluationResult EvaluateInterestRate(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetMaturityArguments(
                invocation,
                out var settlement,
                out var maturity,
                out var investment,
                out var redemption,
                out var basis,
                out var error))
        {
            return error;
        }

        if (investment <= 0d || redemption <= 0d)
        {
            return NumericError();
        }

        var fraction = FinancialDateMath.GetYearFraction(
            settlement,
            maturity,
            (FinancialDayCountBasis)basis);
        return Divide(
            redemption - investment,
            investment * fraction);
    }

    private static FormulaEvaluationResult EvaluateReceivedAmount(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetMaturityArguments(
                invocation,
                out var settlement,
                out var maturity,
                out var investment,
                out var discount,
                out var basis,
                out var error))
        {
            return error;
        }

        if (investment <= 0d || discount <= 0d)
        {
            return NumericError();
        }

        var fraction = FinancialDateMath.GetYearFraction(
            settlement,
            maturity,
            (FinancialDayCountBasis)basis);
        var denominator = 1d - (discount * fraction);
        if (!double.IsFinite(denominator) || denominator <= 0d)
        {
            return NumericError();
        }

        return Number(investment / denominator);
    }

    private static FormulaEvaluationResult EvaluateDiscountedPrice(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetMaturityArguments(
                invocation,
                out var settlement,
                out var maturity,
                out var discount,
                out var redemption,
                out var basis,
                out var error))
        {
            return error;
        }

        if (discount <= 0d || redemption <= 0d)
        {
            return NumericError();
        }

        var fraction = FinancialDateMath.GetYearFraction(
            settlement,
            maturity,
            (FinancialDayCountBasis)basis);
        var price = redemption * (1d - (discount * fraction));
        return price > 0d ? Number(price) : NumericError();
    }

    private static bool TryGetMaturityArguments(
        FormulaFunctionInvocation invocation,
        out DateTime settlement,
        out DateTime maturity,
        out double firstValue,
        out double secondValue,
        out int basis,
        out FormulaEvaluationResult error)
    {
        settlement = default;
        maturity = default;
        firstValue = default;
        secondValue = default;
        basis = 0;

        if (!TryGetScalarDate(
                invocation.Arguments[0],
                out settlement,
                out error) ||
            !TryGetScalarDate(
                invocation.Arguments[1],
                out maturity,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[2],
                out firstValue,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[3],
                out secondValue,
                out error))
        {
            return false;
        }

        if (invocation.Arguments.Count == 5 &&
            !TryGetBasis(
                invocation.Arguments[4],
                out basis,
                out error))
        {
            return false;
        }

        if (settlement >= maturity)
        {
            error = NumericError();
            return false;
        }

        error = default!;
        return true;
    }

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

    private static bool TryGetBasis(
        FormulaFunctionArgument argument,
        out int basis,
        out FormulaEvaluationResult error)
    {
        if (!TryGetScalarNumber(argument, out var number, out error))
        {
            basis = default;
            return false;
        }

        var truncated = Math.Truncate(number);
        if (truncated < int.MinValue || truncated > int.MaxValue)
        {
            basis = default;
            error = NumericError();
            return false;
        }

        basis = checked((int)truncated);
        if (!FinancialDateMath.IsSupportedBasis(basis))
        {
            error = NumericError();
            return false;
        }

        error = default!;
        return true;
    }

    private static FormulaEvaluationResult Divide(
        double numerator,
        double denominator)
    {
        if (!double.IsFinite(denominator) || denominator == 0d)
        {
            return NumericError();
        }

        return Number(numerator / denominator);
    }

    private static FormulaEvaluationResult Number(double value) =>
        double.IsFinite(value)
            ? FormulaEvaluationResult.Success(CellValue.FromNumber(value))
            : NumericError();

    private static FormulaEvaluationResult InvalidValue() =>
        FormulaEvaluationResult.Failure(FormulaErrorCode.InvalidValue);

    private static FormulaEvaluationResult NumericError() =>
        new(
            CellValue.FromError("#NUM!"),
            FormulaErrorCode.InvalidValue,
            Array.Empty<FormulaDependency>());
}
