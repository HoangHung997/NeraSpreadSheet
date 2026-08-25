using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static class LookupFormulaFunctions
{
    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return new FormulaFunctionDefinition(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity(
                    "NERA.BUILTIN",
                    "LOOKUP"),
                new FormulaFunctionVersion(1, 0, 0),
                FormulaFunctionApiVersion.Current,
                2,
                3,
                FormulaFunctionCapabilities.ScalarArguments |
                FormulaFunctionCapabilities.RangeArguments |
                FormulaFunctionCapabilities.ReturnsScalar,
                propagateArgumentErrors: false,
                argumentCountPolicy:
                    FormulaFunctionArgumentCountPolicy.LogicalArguments),
            EvaluateLookup);
    }

    private static FormulaEvaluationResult EvaluateLookup(
        FormulaFunctionInvocation invocation)
    {
        var lookupArgument = invocation.Arguments[0];
        if (lookupArgument.Values.Count != 1)
        {
            return InvalidValue();
        }

        var lookupValue = lookupArgument.Values[0];
        if (lookupValue.Kind == CellValueKind.Error)
        {
            return FromValue(lookupValue);
        }
        if (!TryNormalize(lookupValue, out var normalizedLookup))
        {
            return InvalidValue();
        }

        return invocation.Arguments.Count == 3
            ? EvaluateVectorForm(
                normalizedLookup,
                invocation.Arguments[1],
                invocation.Arguments[2])
            : EvaluateArrayForm(
                normalizedLookup,
                invocation.Arguments[1]);
    }

    private static FormulaEvaluationResult EvaluateVectorForm(
        LookupComparable lookupValue,
        FormulaFunctionArgument lookupVector,
        FormulaFunctionArgument resultVector)
    {
        if (!TryGetVector(lookupVector, out var lookupValues) ||
            !TryGetVector(resultVector, out var resultValues) ||
            lookupValues.Count != resultValues.Count)
        {
            return NotAvailable();
        }
        if (!TryFindApproximateIndex(
                lookupValue,
                lookupValues,
                out var index))
        {
            return NotAvailable();
        }

        return FromValue(resultValues[index]);
    }

    private static FormulaEvaluationResult EvaluateArrayForm(
        LookupComparable lookupValue,
        FormulaFunctionArgument array)
    {
        if (!TryGetShape(array, out var rows, out var columns) ||
            array.Values.Count != checked(rows * columns))
        {
            return InvalidValue();
        }

        var searchFirstRow = columns > rows;
        var candidateCount = searchFirstRow ? columns : rows;
        var candidates = new CellValue[candidateCount];
        for (var index = 0; index < candidateCount; index++)
        {
            candidates[index] = searchFirstRow
                ? array.Values[index]
                : array.Values[checked(index * columns)];
        }
        if (!TryFindApproximateIndex(
                lookupValue,
                candidates,
                out var match))
        {
            return NotAvailable();
        }

        var resultIndex = searchFirstRow
            ? checked(((rows - 1) * columns) + match)
            : checked((match * columns) + columns - 1);
        return FromValue(array.Values[resultIndex]);
    }

    private static bool TryGetVector(
        FormulaFunctionArgument argument,
        out IReadOnlyList<CellValue> values)
    {
        if (!TryGetShape(argument, out var rows, out var columns) ||
            (rows != 1 && columns != 1))
        {
            values = Array.Empty<CellValue>();
            return false;
        }

        values = argument.Values;
        return true;
    }

    private static bool TryGetShape(
        FormulaFunctionArgument argument,
        out int rows,
        out int columns)
    {
        switch (argument.Kind)
        {
            case FormulaFunctionArgumentKind.Scalar:
                rows = 1;
                columns = 1;
                return true;
            case FormulaFunctionArgumentKind.Range
                when argument.SourceDependency is { } dependency:
                rows = dependency.Range.RowCount;
                columns = dependency.Range.ColumnCount;
                return true;
            default:
                rows = default;
                columns = default;
                return false;
        }
    }

    private static bool TryFindApproximateIndex(
        LookupComparable lookupValue,
        IReadOnlyList<CellValue> candidates,
        out int index)
    {
        index = default;
        var found = false;
        LookupComparable best = default;
        for (var candidateIndex = 0;
             candidateIndex < candidates.Count;
             candidateIndex++)
        {
            var candidateValue = candidates[candidateIndex];
            if (candidateValue.Kind == CellValueKind.Error ||
                !TryNormalize(candidateValue, out var candidate) ||
                candidate.Kind != lookupValue.Kind ||
                Compare(candidate, lookupValue) > 0)
            {
                continue;
            }

            if (!found || Compare(candidate, best) >= 0)
            {
                found = true;
                best = candidate;
                index = candidateIndex;
            }
        }

        return found;
    }

    private static bool TryNormalize(
        CellValue value,
        out LookupComparable comparable)
    {
        switch (value.Kind)
        {
            case CellValueKind.Number:
                comparable = LookupComparable.FromNumber(
                    (double)value.RawValue!);
                return true;
            case CellValueKind.DateTime:
                try
                {
                    var number = ((DateTime)value.RawValue!).ToOADate();
                    if (double.IsFinite(number))
                    {
                        comparable = LookupComparable.FromNumber(number);
                        return true;
                    }
                }
                catch (OverflowException)
                {
                    // Fall through to an unsupported comparison value.
                }
                break;
            case CellValueKind.Blank:
                comparable = LookupComparable.FromNumber(0d);
                return true;
            case CellValueKind.Text:
                comparable = LookupComparable.FromText(
                    (string)value.RawValue!);
                return true;
            case CellValueKind.Boolean:
                comparable = LookupComparable.FromBoolean(
                    (bool)value.RawValue!);
                return true;
        }

        comparable = default;
        return false;
    }

    private static int Compare(
        LookupComparable left,
        LookupComparable right) =>
        left.Kind switch
        {
            LookupComparableKind.Number =>
                left.Number.CompareTo(right.Number),
            LookupComparableKind.Text => string.Compare(
                left.Text,
                right.Text,
                StringComparison.OrdinalIgnoreCase),
            LookupComparableKind.Boolean =>
                left.Boolean.CompareTo(right.Boolean),
            _ => 0,
        };

    private static FormulaEvaluationResult FromValue(CellValue value) =>
        value.Kind == CellValueKind.Error
            ? new FormulaEvaluationResult(
                value,
                FormulaErrorMapping.ToErrorCode(value),
                Array.Empty<FormulaDependency>())
            : FormulaEvaluationResult.Success(value);

    private static FormulaEvaluationResult InvalidValue() =>
        FormulaEvaluationResult.Failure(FormulaErrorCode.InvalidValue);

    private static FormulaEvaluationResult NotAvailable() =>
        FormulaEvaluationResult.Failure(FormulaErrorCode.NotAvailable);

    private enum LookupComparableKind
    {
        Number = 0,
        Text,
        Boolean,
    }

    private readonly record struct LookupComparable(
        LookupComparableKind Kind,
        double Number,
        string? Text,
        bool Boolean)
    {
        public static LookupComparable FromNumber(double value) =>
            new(LookupComparableKind.Number, value, null, false);

        public static LookupComparable FromText(string value) =>
            new(LookupComparableKind.Text, 0d, value, false);

        public static LookupComparable FromBoolean(bool value) =>
            new(LookupComparableKind.Boolean, 0d, null, value);
    }
}
