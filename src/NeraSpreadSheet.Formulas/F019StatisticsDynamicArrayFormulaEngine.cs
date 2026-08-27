using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

public sealed partial class NeraDynamicArrayFormulaEngine
{
    private FormulaArrayEvaluationResult EvaluateF019GroupBDynamic(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        var name = FormulaFunctionName.Normalize(function.Name);
        return name switch
        {
            "GROWTH" => EvaluateF019RegressionArray(function, context, dependencies, exponential: true, coefficientsOnly: false),
            "LINEST" => EvaluateF019RegressionArray(function, context, dependencies, exponential: false, coefficientsOnly: true),
            "LOGEST" => EvaluateF019RegressionArray(function, context, dependencies, exponential: true, coefficientsOnly: true),
            "MINVERSE" => EvaluateF019MatrixInverse(function, context, dependencies),
            "MMULT" => EvaluateF019MatrixMultiply(function, context, dependencies),
            "MODE.MULT" => EvaluateF019ModeMultiple(function, context, dependencies),
            "RANDARRAY" => EvaluateF019RandomArray(function, context, dependencies),
            "TEXTSPLIT" => EvaluateF019TextSplit(function, context, dependencies),
            "TREND" => EvaluateF019RegressionArray(function, context, dependencies, exponential: false, coefficientsOnly: false),
            "STOCKHISTORY" => EvaluateF019StockHistory(function, context, dependencies),
            _ => Failure("#NAME?", FormulaErrorCode.InvalidName, dependencies),
        };
    }

    private FormulaArrayEvaluationResult EvaluateF019RegressionArray(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        bool exponential,
        bool coefficientsOnly)
    {
        if (function.Arguments.Count is < 1 or > 4)
        {
            return Failure("#VALUE!", FormulaErrorCode.InvalidValue, dependencies);
        }
        var knownY = EvaluateNodeAsArray(function.Arguments[0], context, dependencies);
        if (!knownY.IsSuccess || !TryF019Numbers(knownY.Value!, out var y))
        {
            return knownY.IsSuccess
                ? Failure("#VALUE!", FormulaErrorCode.InvalidValue, dependencies)
                : knownY;
        }
        FormulaArrayValue knownX;
        if (function.Arguments.Count >= 2 && function.Arguments[1] is not MissingArgumentNode)
        {
            var xResult = EvaluateNodeAsArray(function.Arguments[1], context, dependencies);
            if (!xResult.IsSuccess)
            {
                return xResult;
            }
            knownX = xResult.Value!;
        }
        else
        {
            knownX = FormulaArrayValue.Create(y.Length, 1, (row, _) => CellValue.FromNumber(row + 1d));
        }
        if (!TryF019Numbers(knownX, out var x) || x.Length != y.Length || x.Length < 2)
        {
            return Failure("#N/A", FormulaErrorCode.NotAvailable, dependencies);
        }
        var transformedY = y;
        if (exponential)
        {
            if (y.Any(static value => value <= 0d))
            {
                return Failure("#NUM!", FormulaErrorCode.InvalidValue, dependencies);
            }
            transformedY = y.Select(static value => Math.Log(value)).ToArray();
        }
        F019StatisticsMatrixAndExternalFormulaFunctions.LinearFit(x, transformedY, out var slope, out var intercept);
        if (coefficientsOnly)
        {
            var first = exponential ? Math.Exp(slope) : slope;
            var second = exponential ? Math.Exp(intercept) : intercept;
            if (!double.IsFinite(first) || !double.IsFinite(second))
            {
                return Failure("#NUM!", FormulaErrorCode.InvalidValue, dependencies);
            }
            return FormulaArrayEvaluationResult.Success(
                new FormulaArrayValue(1, 2, [CellValue.FromNumber(first), CellValue.FromNumber(second)]),
                DistinctDependencies(dependencies));
        }

        FormulaArrayValue newX;
        if (function.Arguments.Count >= 3 && function.Arguments[2] is not MissingArgumentNode)
        {
            var newResult = EvaluateNodeAsArray(function.Arguments[2], context, dependencies);
            if (!newResult.IsSuccess)
            {
                return newResult;
            }
            newX = newResult.Value!;
        }
        else
        {
            newX = knownX;
        }
        return FormulaArrayEvaluationResult.Success(
            FormulaArrayValue.Create(
                newX.RowCount,
                newX.ColumnCount,
                (row, column) =>
                {
                    var value = newX[row, column];
                    if (!F019StatisticsMatrixAndExternalFormulaFunctions.TryRangeNumber(value, out var input))
                    {
                        return CellValue.FromError("#VALUE!");
                    }
                    var predicted = (slope * input) + intercept;
                    if (exponential)
                    {
                        predicted = Math.Exp(predicted);
                    }
                    return double.IsFinite(predicted)
                        ? CellValue.FromNumber(predicted)
                        : CellValue.FromError("#NUM!");
                }),
            DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateF019MatrixInverse(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count != 1)
        {
            return Failure("#VALUE!", FormulaErrorCode.InvalidValue, dependencies);
        }
        var sourceResult = EvaluateNodeAsArray(function.Arguments[0], context, dependencies);
        if (!sourceResult.IsSuccess)
        {
            return sourceResult;
        }
        var source = sourceResult.Value!;
        if (source.RowCount != source.ColumnCount || source.RowCount > 256)
        {
            return Failure("#VALUE!", FormulaErrorCode.InvalidValue, dependencies);
        }
        var n = source.RowCount;
        var matrix = new double[n, n * 2];
        for (var row = 0; row < n; row++)
        {
            for (var column = 0; column < n; column++)
            {
                if (!F019StatisticsMatrixAndExternalFormulaFunctions.TryRangeNumber(source[row, column], out matrix[row, column]))
                {
                    return Failure("#VALUE!", FormulaErrorCode.InvalidValue, dependencies);
                }
            }
            matrix[row, n + row] = 1d;
        }
        for (var pivot = 0; pivot < n; pivot++)
        {
            var best = pivot;
            for (var row = pivot + 1; row < n; row++)
            {
                if (Math.Abs(matrix[row, pivot]) > Math.Abs(matrix[best, pivot]))
                {
                    best = row;
                }
            }
            if (Math.Abs(matrix[best, pivot]) <= 1e-14)
            {
                return Failure("#NUM!", FormulaErrorCode.InvalidValue, dependencies);
            }
            if (best != pivot)
            {
                for (var column = 0; column < n * 2; column++)
                {
                    (matrix[pivot, column], matrix[best, column]) = (matrix[best, column], matrix[pivot, column]);
                }
            }
            var divisor = matrix[pivot, pivot];
            for (var column = 0; column < n * 2; column++)
            {
                matrix[pivot, column] /= divisor;
            }
            for (var row = 0; row < n; row++)
            {
                if (row == pivot)
                {
                    continue;
                }
                var factor = matrix[row, pivot];
                for (var column = 0; column < n * 2; column++)
                {
                    matrix[row, column] -= factor * matrix[pivot, column];
                }
            }
        }
        return FormulaArrayEvaluationResult.Success(
            FormulaArrayValue.Create(n, n, (row, column) => CellValue.FromNumber(matrix[row, n + column])),
            DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateF019MatrixMultiply(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count != 2)
        {
            return Failure("#VALUE!", FormulaErrorCode.InvalidValue, dependencies);
        }
        var leftResult = EvaluateNodeAsArray(function.Arguments[0], context, dependencies);
        if (!leftResult.IsSuccess)
        {
            return leftResult;
        }
        var rightResult = EvaluateNodeAsArray(function.Arguments[1], context, dependencies);
        if (!rightResult.IsSuccess)
        {
            return rightResult;
        }
        var left = leftResult.Value!;
        var right = rightResult.Value!;
        if (left.ColumnCount != right.RowCount ||
            (long)left.RowCount * right.ColumnCount > FormulaArrayValue.MaximumCellCount)
        {
            return Failure("#VALUE!", FormulaErrorCode.InvalidValue, dependencies);
        }
        return FormulaArrayEvaluationResult.Success(
            FormulaArrayValue.Create(
                left.RowCount,
                right.ColumnCount,
                (row, column) =>
                {
                    var sum = 0d;
                    for (var inner = 0; inner < left.ColumnCount; inner++)
                    {
                        if (!F019StatisticsMatrixAndExternalFormulaFunctions.TryRangeNumber(left[row, inner], out var a) ||
                            !F019StatisticsMatrixAndExternalFormulaFunctions.TryRangeNumber(right[inner, column], out var b))
                        {
                            return CellValue.FromError("#VALUE!");
                        }
                        sum += a * b;
                    }
                    return double.IsFinite(sum) ? CellValue.FromNumber(sum) : CellValue.FromError("#NUM!");
                }),
            DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateF019ModeMultiple(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count < 1)
        {
            return Failure("#VALUE!", FormulaErrorCode.InvalidValue, dependencies);
        }
        var values = new List<double>();
        foreach (var argument in function.Arguments)
        {
            var result = EvaluateNodeAsArray(argument, context, dependencies);
            if (!result.IsSuccess)
            {
                return result;
            }
            foreach (var value in result.Value!.ToArray())
            {
                if (F019StatisticsMatrixAndExternalFormulaFunctions.TryRangeNumber(value, out var number))
                {
                    values.Add(number);
                }
            }
        }
        var modes = values.GroupBy(static value => value)
            .Select(static group => new { Value = group.Key, Count = group.Count() })
            .ToArray();
        if (modes.Length == 0)
        {
            return Failure("#N/A", FormulaErrorCode.NotAvailable, dependencies);
        }
        var maximum = modes.Max(static item => item.Count);
        if (maximum < 2)
        {
            return Failure("#N/A", FormulaErrorCode.NotAvailable, dependencies);
        }
        var winners = modes.Where(item => item.Count == maximum).Select(item => item.Value).OrderBy(static value => value).ToArray();
        return FormulaArrayEvaluationResult.Success(
            new FormulaArrayValue(winners.Length, 1, winners.Select(CellValue.FromNumber)),
            DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateF019RandomArray(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count > 5)
        {
            return Failure("#VALUE!", FormulaErrorCode.InvalidValue, dependencies);
        }
        if (!TryF019OptionalInt(function, 0, context, dependencies, 1, out var rows) ||
            !TryF019OptionalInt(function, 1, context, dependencies, 1, out var columns) ||
            rows < 1 || columns < 1 ||
            (long)rows * columns > FormulaArrayValue.MaximumCellCount)
        {
            return Failure("#NUM!", FormulaErrorCode.InvalidValue, dependencies);
        }
        var minimum = TryF019OptionalNumber(function, 2, context, dependencies, 0d, out var min) ? min : double.NaN;
        var maximum = TryF019OptionalNumber(function, 3, context, dependencies, 1d, out var max) ? max : double.NaN;
        var whole = TryF019OptionalBoolean(function, 4, context, dependencies, false, out var wholeNumber) && wholeNumber;
        if (!double.IsFinite(minimum) || !double.IsFinite(maximum) || minimum >= maximum)
        {
            return Failure("#VALUE!", FormulaErrorCode.InvalidValue, dependencies);
        }
        if (whole && Math.Ceiling(minimum) > Math.Floor(maximum))
        {
            return Failure("#VALUE!", FormulaErrorCode.InvalidValue, dependencies);
        }
        return FormulaArrayEvaluationResult.Success(
            FormulaArrayValue.Create(rows, columns, (_, _) =>
            {
                var random = Random.Shared.NextDouble();
                var value = minimum + (random * (maximum - minimum));
                if (whole)
                {
                    var lower = checked((long)Math.Ceiling(minimum));
                    var upper = checked((long)Math.Floor(maximum));
                    value = lower + Math.Floor(random * ((upper - lower) + 1d));
                }
                return CellValue.FromNumber(value);
            }),
            DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateF019TextSplit(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count is < 2 or > 6)
        {
            return Failure("#VALUE!", FormulaErrorCode.InvalidValue, dependencies);
        }
        var textValue = EvaluateScalarNode(function.Arguments[0], context, dependencies);
        var columnValue = EvaluateScalarNode(function.Arguments[1], context, dependencies);
        var text = FormulaValueCoercion.ToText(textValue);
        var columnDelimiter = FormulaValueCoercion.ToText(columnValue);
        var rowDelimiter = function.Arguments.Count >= 3 && function.Arguments[2] is not MissingArgumentNode
            ? FormulaValueCoercion.ToText(EvaluateScalarNode(function.Arguments[2], context, dependencies))
            : string.Empty;
        var ignoreEmpty = function.Arguments.Count >= 4 && function.Arguments[3] is not MissingArgumentNode &&
                          FormulaValueCoercion.TryBoolean(EvaluateScalarNode(function.Arguments[3], context, dependencies), out var ignore) && ignore;
        var comparison = function.Arguments.Count >= 5 && function.Arguments[4] is not MissingArgumentNode &&
                         FormulaValueCoercion.TryNumber(EvaluateScalarNode(function.Arguments[4], context, dependencies), out var mode, allowText: true) && mode != 0d
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var pad = function.Arguments.Count >= 6 && function.Arguments[5] is not MissingArgumentNode
            ? EvaluateScalarNode(function.Arguments[5], context, dependencies)
            : CellValue.FromError("#N/A");
        if (columnDelimiter.Length == 0 && rowDelimiter.Length == 0)
        {
            return Failure("#VALUE!", FormulaErrorCode.InvalidValue, dependencies);
        }
        var rowTexts = rowDelimiter.Length == 0
            ? [text]
            : SplitF019(text, rowDelimiter, ignoreEmpty, comparison);
        var rows = new List<string[]>();
        foreach (var rowText in rowTexts)
        {
            rows.Add(columnDelimiter.Length == 0
                ? [rowText]
                : SplitF019(rowText, columnDelimiter, ignoreEmpty, comparison));
        }
        var columnCount = rows.Max(static row => row.Length);
        if (rows.Count == 0 || columnCount == 0 || (long)rows.Count * columnCount > FormulaArrayValue.MaximumCellCount)
        {
            return Failure("#NUM!", FormulaErrorCode.InvalidValue, dependencies);
        }
        return FormulaArrayEvaluationResult.Success(
            FormulaArrayValue.Create(rows.Count, columnCount, (row, column) =>
                column < rows[row].Length ? CellValue.FromText(rows[row][column]) : pad),
            DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateF019StockHistory(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count is < 2 or > 7 || context is not IFormulaExternalFunctionContext external)
        {
            return Failure("#N/A", FormulaErrorCode.NotAvailable, dependencies);
        }
        var arguments = new List<CellValue>();
        foreach (var argument in function.Arguments)
        {
            arguments.Add(EvaluateScalarNode(argument, context, dependencies));
        }
        return external.TryEvaluateExternalArrayFunction("STOCKHISTORY", arguments, out var value)
            ? FormulaArrayEvaluationResult.Success(value, DistinctDependencies(dependencies))
            : Failure("#N/A", FormulaErrorCode.NotAvailable, dependencies);
    }

    private static bool TryF019Numbers(FormulaArrayValue array, out double[] values)
    {
        values = new double[array.Count];
        var index = 0;
        foreach (var cell in array.ToArray())
        {
            if (!F019StatisticsMatrixAndExternalFormulaFunctions.TryRangeNumber(cell, out values[index++]))
            {
                values = [];
                return false;
            }
        }
        return true;
    }

    private bool TryF019OptionalInt(FunctionNode function, int index, IFormulaEvaluationContext context, List<FormulaDependency> dependencies, int fallback, out int value)
    {
        if (function.Arguments.Count <= index || function.Arguments[index] is MissingArgumentNode)
        {
            value = fallback;
            return true;
        }
        var cell = EvaluateScalarNode(function.Arguments[index], context, dependencies);
        if (!FormulaValueCoercion.TryNumber(cell, out var number, allowText: true) || !double.IsFinite(number) || number < int.MinValue || number > int.MaxValue)
        {
            value = default;
            return false;
        }
        value = checked((int)Math.Truncate(number));
        return true;
    }

    private bool TryF019OptionalNumber(FunctionNode function, int index, IFormulaEvaluationContext context, List<FormulaDependency> dependencies, double fallback, out double value)
    {
        if (function.Arguments.Count <= index || function.Arguments[index] is MissingArgumentNode)
        {
            value = fallback;
            return true;
        }
        var cell = EvaluateScalarNode(function.Arguments[index], context, dependencies);
        return FormulaValueCoercion.TryNumber(cell, out value, allowText: true) && double.IsFinite(value);
    }

    private bool TryF019OptionalBoolean(FunctionNode function, int index, IFormulaEvaluationContext context, List<FormulaDependency> dependencies, bool fallback, out bool value)
    {
        if (function.Arguments.Count <= index || function.Arguments[index] is MissingArgumentNode)
        {
            value = fallback;
            return true;
        }
        return FormulaValueCoercion.TryBoolean(EvaluateScalarNode(function.Arguments[index], context, dependencies), out value);
    }

    private static string[] SplitF019(string text, string delimiter, bool ignoreEmpty, StringComparison comparison)
    {
        var parts = new List<string>();
        var start = 0;
        while (start <= text.Length)
        {
            var index = text.IndexOf(delimiter, start, comparison);
            var end = index < 0 ? text.Length : index;
            var part = text[start..end];
            if (!ignoreEmpty || part.Length > 0)
            {
                parts.Add(part);
            }
            if (index < 0)
            {
                break;
            }
            start = index + delimiter.Length;
        }
        return parts.ToArray();
    }
}
