using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static partial class DescriptiveCompatibilityStatisticalFormulaFunctions
{
    private static bool TryCollectNumbers(
        IReadOnlyList<FormulaFunctionArgument> arguments,
        CollectionMode mode,
        out double[] values,
        out FormulaEvaluationResult error)
    {
        var collected = new List<double>();
        foreach (var argument in arguments)
        {
            var direct = argument.Kind == FormulaFunctionArgumentKind.Scalar;
            foreach (var value in argument.Values)
            {
                if (!TryCollectValue(
                        value,
                        direct,
                        mode,
                        collected,
                        out error))
                {
                    values = Array.Empty<double>();
                    return false;
                }
            }
        }
        values = collected.ToArray();
        error = default!;
        return true;
    }

    private static bool TryCollectValue(
        CellValue value,
        bool direct,
        CollectionMode mode,
        List<double> collected,
        out FormulaEvaluationResult error)
    {
        double number;
        var include = false;
        switch (value.Kind)
        {
            case CellValueKind.Number:
            case CellValueKind.DateTime:
                include = FormulaValueCoercion.TryNumber(value, out number);
                break;
            case CellValueKind.Boolean:
                number = (bool)value.RawValue! ? 1d : 0d;
                include = mode == CollectionMode.ACompatible || direct;
                break;
            case CellValueKind.Text:
                if (mode == CollectionMode.ACompatible)
                {
                    number = 0d;
                    include = true;
                }
                else if (direct)
                {
                    if (!FormulaValueCoercion.TryNumber(
                            value,
                            out number,
                            allowText: true))
                    {
                        error = InvalidValue();
                        return false;
                    }
                    include = true;
                }
                else
                {
                    number = 0d;
                }
                break;
            case CellValueKind.Blank:
                number = 0d;
                break;
            default:
                number = 0d;
                error = InvalidValue();
                return false;
        }

        if (include)
        {
            if (!double.IsFinite(number))
            {
                error = NumericError();
                return false;
            }
            if (collected.Count >= MaximumValues)
            {
                error = NumericError();
                return false;
            }
            collected.Add(number);
        }
        error = default!;
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

    private static bool TryGetTruncatedInteger(
        FormulaFunctionArgument argument,
        out int result,
        out FormulaEvaluationResult error)
    {
        if (!TryGetScalarNumber(argument, out var number, out error))
        {
            result = default;
            return false;
        }
        if (number < int.MinValue || number > int.MaxValue)
        {
            result = default;
            error = NumericError();
            return false;
        }
        result = checked((int)Math.Truncate(number));
        return true;
    }

    private static bool ContainsNonPositive(double[] values)
    {
        foreach (var value in values)
        {
            if (value <= 0d)
            {
                return true;
            }
        }
        return false;
    }

    private static double Mean(double[] values)
    {
        var sum = 0d;
        var compensation = 0d;
        foreach (var value in values)
        {
            AddCompensated(value, ref sum, ref compensation);
        }
        return sum / values.Length;
    }

    private static double SumSquaredDeviations(double[] values)
    {
        var mean = Mean(values);
        var result = 0d;
        var compensation = 0d;
        foreach (var value in values)
        {
            var deviation = value - mean;
            AddCompensated(
                deviation * deviation,
                ref result,
                ref compensation);
        }
        return result;
    }

    private static void AddCompensated(
        double value,
        ref double sum,
        ref double compensation)
    {
        var corrected = value - compensation;
        var updated = sum + corrected;
        compensation = (updated - sum) - corrected;
        sum = updated;
    }

    private static FormulaEvaluationResult Number(double value) =>
        double.IsFinite(value)
            ? FormulaEvaluationResult.Success(CellValue.FromNumber(value))
            : NumericError();

    private static FormulaEvaluationResult InvalidValue() =>
        FormulaEvaluationResult.Failure(FormulaErrorCode.InvalidValue);

    private static FormulaEvaluationResult DivisionByZero() =>
        FormulaEvaluationResult.Failure(FormulaErrorCode.DivisionByZero);

    private static FormulaEvaluationResult NotAvailable() =>
        FormulaEvaluationResult.Failure(FormulaErrorCode.NotAvailable);

    private static FormulaEvaluationResult NumericError() =>
        new(
            CellValue.FromError("#NUM!"),
            FormulaErrorCode.InvalidValue,
            Array.Empty<FormulaDependency>());

    private enum CollectionMode
    {
        Standard = 0,
        ACompatible,
    }
}
