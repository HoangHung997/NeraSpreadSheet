using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static partial class AdditionalFinancialFormulaFunctions
{
    private static double LogOnePlus(double value) =>
        double.LogP1(value);

    private static double ExponentialMinusOne(double value)
    {
        if (Math.Abs(value) > 1e-5d)
        {
            return Math.Exp(value) - 1d;
        }

        var term = value;
        var sum = value;
        for (var index = 2; index <= 32; index++)
        {
            term *= value / index;
            sum += term;
        }
        return sum;
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

    private static bool TryGetPaymentTiming(
        FormulaFunctionArgument argument,
        out int timing,
        out FormulaEvaluationResult error)
    {
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar ||
            !FormulaValueCoercion.TryInteger(
                argument.ScalarValue,
                out timing,
                allowText: true) ||
            timing is < 0 or > 1)
        {
            timing = default;
            error = InvalidValue();
            return false;
        }
        error = default!;
        return true;
    }

    private static bool IsValidRate(double rate) =>
        double.IsFinite(rate) && rate > -1d;

    private static bool IsValidSolverRate(double rate) =>
        double.IsFinite(rate) &&
        rate > -1d + MinimumRateBase &&
        rate <= MaximumRate;

    private static void AddCompensated(
        ref double sum,
        ref double compensation,
        double value)
    {
        var adjusted = value - compensation;
        var next = sum + adjusted;
        compensation = (next - sum) - adjusted;
        sum = next;
    }

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

    private delegate bool FinancialRootEvaluator(
        double rate,
        out double value,
        out double derivative);

    private readonly record struct ScheduledCashFlow(
        double Value,
        long DayNumber);

    private readonly record struct RootSample(
        double X,
        double Rate,
        double Value);

    private readonly record struct RootBracket(
        RootSample Left,
        RootSample Right,
        double DistanceFromGuess);
}
