using System.Globalization;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static class AggregateLogicalFormulaFunctions
{
    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return FormulaFunctionFactory.Create(
            "SUM",
            0,
            int.MaxValue,
            static (arguments, _) => Aggregate(
                arguments,
                AggregateKind.Sum),
            propagateErrors: false);
        yield return FormulaFunctionFactory.Create(
            "AVERAGE",
            0,
            int.MaxValue,
            static (arguments, _) => Aggregate(
                arguments,
                AggregateKind.Average),
            propagateErrors: false);
        yield return FormulaFunctionFactory.Create(
            "MIN",
            0,
            int.MaxValue,
            static (arguments, _) => Aggregate(
                arguments,
                AggregateKind.Minimum),
            propagateErrors: false);
        yield return FormulaFunctionFactory.Create(
            "MAX",
            0,
            int.MaxValue,
            static (arguments, _) => Aggregate(
                arguments,
                AggregateKind.Maximum),
            propagateErrors: false);
        yield return FormulaFunctionFactory.Create(
            "COUNT",
            0,
            int.MaxValue,
            static (arguments, _) => CellValue.FromNumber(
                arguments.Count(IsNumericValue)),
            propagateErrors: false);
        yield return FormulaFunctionFactory.Create(
            "COUNTA",
            0,
            int.MaxValue,
            static (arguments, _) => CellValue.FromNumber(
                arguments.Count(static value => !value.IsBlank)),
            propagateErrors: false);
        yield return FormulaFunctionFactory.Create(
            "COUNTBLANK",
            1,
            int.MaxValue,
            static (arguments, _) => CellValue.FromNumber(
                arguments.Count(static value => value.IsBlank)),
            propagateErrors: false);
        yield return FormulaFunctionFactory.Create(
            "PRODUCT",
            0,
            int.MaxValue,
            static (arguments, _) => Product(arguments),
            propagateErrors: false);
        yield return FormulaFunctionFactory.Create(
            "SUMSQ",
            0,
            int.MaxValue,
            static (arguments, _) => SumSquares(arguments),
            propagateErrors: false);
        yield return FormulaFunctionFactory.Create(
            "AND",
            1,
            int.MaxValue,
            static (arguments, _) => Logical(arguments, LogicalKind.And));
        yield return FormulaFunctionFactory.Create(
            "OR",
            1,
            int.MaxValue,
            static (arguments, _) => Logical(arguments, LogicalKind.Or));
        yield return FormulaFunctionFactory.Create(
            "XOR",
            1,
            int.MaxValue,
            static (arguments, _) => Logical(arguments, LogicalKind.Xor));
        yield return FormulaFunctionFactory.Create(
            "NOT",
            1,
            1,
            static (arguments, _) =>
                FormulaValueCoercion.TryBoolean(
                    arguments[0],
                    out var value)
                    ? CellValue.FromBoolean(!value)
                    : FormulaValueCoercion.Error("#VALUE!"));
        yield return FormulaFunctionFactory.Create(
            "TRUE",
            0,
            0,
            static (_, _) => CellValue.FromBoolean(true));
        yield return FormulaFunctionFactory.Create(
            "FALSE",
            0,
            0,
            static (_, _) => CellValue.FromBoolean(false));
        yield return FormulaFunctionFactory.Create(
            "NA",
            0,
            0,
            static (_, _) => FormulaValueCoercion.Error("#N/A"),
            propagateErrors: false);
        yield return FormulaFunctionFactory.Create(
            "ISBLANK",
            1,
            1,
            static (arguments, _) =>
                CellValue.FromBoolean(arguments[0].IsBlank),
            propagateErrors: false);
        yield return FormulaFunctionFactory.Create(
            "ISNUMBER",
            1,
            1,
            static (arguments, _) => CellValue.FromBoolean(
                arguments[0].Kind is
                    CellValueKind.Number or CellValueKind.DateTime),
            propagateErrors: false);
        yield return FormulaFunctionFactory.Create(
            "ISTEXT",
            1,
            1,
            static (arguments, _) => CellValue.FromBoolean(
                arguments[0].Kind == CellValueKind.Text),
            propagateErrors: false);
        yield return FormulaFunctionFactory.Create(
            "ISLOGICAL",
            1,
            1,
            static (arguments, _) => CellValue.FromBoolean(
                arguments[0].Kind == CellValueKind.Boolean),
            propagateErrors: false);
        yield return FormulaFunctionFactory.Create(
            "ISERROR",
            1,
            1,
            static (arguments, _) => CellValue.FromBoolean(
                arguments[0].Kind == CellValueKind.Error),
            propagateErrors: false);
        yield return FormulaFunctionFactory.Create(
            "ISERR",
            1,
            1,
            static (arguments, _) => CellValue.FromBoolean(
                arguments[0].Kind == CellValueKind.Error &&
                !IsError(arguments[0], "#N/A")),
            propagateErrors: false);
        yield return FormulaFunctionFactory.Create(
            "ISNA",
            1,
            1,
            static (arguments, _) => CellValue.FromBoolean(
                IsError(arguments[0], "#N/A")),
            propagateErrors: false);
        yield return FormulaFunctionFactory.Create(
            "N",
            1,
            1,
            static (arguments, _) => ConvertToNumber(arguments[0]));
        yield return FormulaFunctionFactory.Create(
            "T",
            1,
            1,
            static (arguments, _) =>
                arguments[0].Kind == CellValueKind.Text
                    ? arguments[0]
                    : CellValue.Blank);
    }

    private static CellValue Aggregate(
        IReadOnlyList<CellValue> arguments,
        AggregateKind kind)
    {
        var numbers = arguments
            .Where(IsNumericValue)
            .Select(ToNumericValue)
            .ToArray();
        if (kind == AggregateKind.Sum)
        {
            return FormulaValueCoercion.SafeNumber(numbers.Sum());
        }
        if (numbers.Length == 0)
        {
            return FormulaValueCoercion.Error("#DIV/0!");
        }

        var result = kind switch
        {
            AggregateKind.Average => numbers.Average(),
            AggregateKind.Minimum => numbers.Min(),
            AggregateKind.Maximum => numbers.Max(),
            _ => throw new InvalidOperationException(
                "Unknown aggregate kind."),
        };
        return FormulaValueCoercion.SafeNumber(result);
    }

    private static CellValue Product(IReadOnlyList<CellValue> arguments)
    {
        var result = 1d;
        var count = 0;
        foreach (var argument in arguments)
        {
            if (!IsNumericValue(argument))
            {
                continue;
            }
            result *= ToNumericValue(argument);
            count++;
            if (!double.IsFinite(result))
            {
                return FormulaValueCoercion.Error("#NUM!");
            }
        }
        return CellValue.FromNumber(count == 0 ? 0d : result);
    }

    private static CellValue SumSquares(
        IReadOnlyList<CellValue> arguments)
    {
        var result = 0d;
        foreach (var argument in arguments)
        {
            if (!IsNumericValue(argument))
            {
                continue;
            }
            var number = ToNumericValue(argument);
            result += number * number;
            if (!double.IsFinite(result))
            {
                return FormulaValueCoercion.Error("#NUM!");
            }
        }
        return CellValue.FromNumber(result);
    }

    private static CellValue Logical(
        IReadOnlyList<CellValue> arguments,
        LogicalKind kind)
    {
        var logicalCount = 0;
        var trueCount = 0;
        foreach (var argument in arguments)
        {
            if (argument.Kind == CellValueKind.Text &&
                !bool.TryParse(
                    (string)argument.RawValue!,
                    out _))
            {
                continue;
            }
            if (!FormulaValueCoercion.TryBoolean(
                    argument,
                    out var value))
            {
                return FormulaValueCoercion.Error("#VALUE!");
            }
            logicalCount++;
            if (value)
            {
                trueCount++;
            }
        }
        if (logicalCount == 0)
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }

        return CellValue.FromBoolean(kind switch
        {
            LogicalKind.And => trueCount == logicalCount,
            LogicalKind.Or => trueCount > 0,
            LogicalKind.Xor => (trueCount & 1) == 1,
            _ => throw new InvalidOperationException(
                "Unknown logical kind."),
        });
    }

    private static CellValue ConvertToNumber(CellValue value)
    {
        if (value.Kind == CellValueKind.Text || value.IsBlank)
        {
            return CellValue.FromNumber(0d);
        }
        return FormulaValueCoercion.TryNumber(
                value,
                out var number)
            ? FormulaValueCoercion.SafeNumber(number)
            : FormulaValueCoercion.Error("#VALUE!");
    }

    private static bool IsNumericValue(CellValue value) =>
        value.Kind is CellValueKind.Number or CellValueKind.DateTime;

    private static double ToNumericValue(CellValue value)
    {
        if (!FormulaValueCoercion.TryNumber(value, out var number))
        {
            throw new InvalidOperationException(
                "The value was expected to be numeric.");
        }
        return number;
    }

    private static bool IsError(CellValue value, string code) =>
        value.Kind == CellValueKind.Error &&
        string.Equals(
            Convert.ToString(
                value.RawValue,
                CultureInfo.InvariantCulture),
            code,
            StringComparison.OrdinalIgnoreCase);

    private enum AggregateKind
    {
        Sum,
        Average,
        Minimum,
        Maximum,
    }

    private enum LogicalKind
    {
        And,
        Or,
        Xor,
    }
}
