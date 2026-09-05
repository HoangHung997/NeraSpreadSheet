using System.Globalization;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static class XMatchFormulaEvaluation
{
    public static bool TryMatch(
        CellValue lookupValue,
        IReadOnlyList<CellValue> values,
        int matchMode,
        int searchMode,
        out int position,
        out CellValue error)
    {
        ArgumentNullException.ThrowIfNull(values);
        position = default;

        if (lookupValue.Kind == CellValueKind.Error)
        {
            error = lookupValue;
            return false;
        }
        if (values.Count == 0 ||
            matchMode is not 0 and not -1 and not 1 and not 2 ||
            searchMode is not 1 and not -1 and not 2 and not -2)
        {
            error = CellValue.FromError("#VALUE!");
            return false;
        }

        var reverse = searchMode is -1 or -2;
        IEnumerable<int> indexes = reverse
            ? Enumerable.Range(0, values.Count).Reverse()
            : Enumerable.Range(0, values.Count);

        if (matchMode == 2)
        {
            var pattern = FormulaValueCoercion.ToText(lookupValue);
            foreach (var index in indexes)
            {
                var candidate = values[index];
                if (candidate.Kind == CellValueKind.Error)
                {
                    continue;
                }
                if (WildcardEquals(
                        pattern,
                        FormulaValueCoercion.ToText(candidate)))
                {
                    position = index + 1;
                    error = default;
                    return true;
                }
            }

            error = CellValue.FromError("#N/A");
            return false;
        }

        foreach (var index in indexes)
        {
            var candidate = values[index];
            if (candidate.Kind == CellValueKind.Error)
            {
                continue;
            }
            if (ValuesEqual(lookupValue, candidate))
            {
                position = index + 1;
                error = default;
                return true;
            }
        }

        if (matchMode == 0)
        {
            error = CellValue.FromError("#N/A");
            return false;
        }

        var hasBest = false;
        var bestIndex = -1;
        var bestValue = default(CellValue);
        foreach (var index in indexes)
        {
            var candidate = values[index];
            if (candidate.Kind == CellValueKind.Error ||
                !TryCompare(candidate, lookupValue, out var relation))
            {
                continue;
            }

            var eligible = matchMode == -1
                ? relation <= 0
                : relation >= 0;
            if (!eligible)
            {
                continue;
            }

            if (!hasBest)
            {
                hasBest = true;
                bestIndex = index;
                bestValue = candidate;
                continue;
            }

            if (!TryCompare(candidate, bestValue, out var bestRelation))
            {
                continue;
            }
            var improves = matchMode == -1
                ? bestRelation > 0
                : bestRelation < 0;
            if (improves)
            {
                bestIndex = index;
                bestValue = candidate;
            }
        }

        if (hasBest)
        {
            position = bestIndex + 1;
            error = default;
            return true;
        }

        error = CellValue.FromError("#N/A");
        return false;
    }

    public static bool ValuesEqual(CellValue left, CellValue right)
    {
        if (left.Kind == CellValueKind.Error ||
            right.Kind == CellValueKind.Error)
        {
            return left.Kind == CellValueKind.Error &&
                   right.Kind == CellValueKind.Error &&
                   string.Equals(
                       Convert.ToString(
                           left.RawValue,
                           CultureInfo.InvariantCulture),
                       Convert.ToString(
                           right.RawValue,
                           CultureInfo.InvariantCulture),
                       StringComparison.OrdinalIgnoreCase);
        }

        if (FormulaValueCoercion.TryNumber(left, out var leftNumber) &&
            FormulaValueCoercion.TryNumber(right, out var rightNumber))
        {
            return leftNumber.Equals(rightNumber);
        }

        return string.Equals(
            FormulaValueCoercion.ToText(left),
            FormulaValueCoercion.ToText(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryCompare(
        CellValue left,
        CellValue right,
        out int result)
    {
        if (FormulaValueCoercion.TryNumber(left, out var leftNumber) &&
            FormulaValueCoercion.TryNumber(right, out var rightNumber))
        {
            result = leftNumber.CompareTo(rightNumber);
            return true;
        }

        if (left.Kind == CellValueKind.Error ||
            right.Kind == CellValueKind.Error)
        {
            result = default;
            return false;
        }

        result = string.Compare(
            FormulaValueCoercion.ToText(left),
            FormulaValueCoercion.ToText(right),
            StringComparison.OrdinalIgnoreCase);
        return true;
    }

    private static bool WildcardEquals(string pattern, string value)
    {
        var memo = new Dictionary<(int Pattern, int Value), bool>();

        bool Match(int patternIndex, int valueIndex)
        {
            var key = (patternIndex, valueIndex);
            if (memo.TryGetValue(key, out var cached))
            {
                return cached;
            }

            bool matched;
            if (patternIndex == pattern.Length)
            {
                matched = valueIndex == value.Length;
            }
            else if (pattern[patternIndex] == '~')
            {
                if (patternIndex + 1 >= pattern.Length)
                {
                    matched = valueIndex < value.Length &&
                              value[valueIndex] == '~' &&
                              Match(patternIndex + 1, valueIndex + 1);
                }
                else
                {
                    matched = valueIndex < value.Length &&
                              char.ToUpperInvariant(
                                  pattern[patternIndex + 1]) ==
                              char.ToUpperInvariant(value[valueIndex]) &&
                              Match(patternIndex + 2, valueIndex + 1);
                }
            }
            else if (pattern[patternIndex] == '*')
            {
                matched = Match(patternIndex + 1, valueIndex) ||
                          valueIndex < value.Length &&
                          Match(patternIndex, valueIndex + 1);
            }
            else if (pattern[patternIndex] == '?')
            {
                matched = valueIndex < value.Length &&
                          Match(patternIndex + 1, valueIndex + 1);
            }
            else
            {
                matched = valueIndex < value.Length &&
                          char.ToUpperInvariant(pattern[patternIndex]) ==
                          char.ToUpperInvariant(value[valueIndex]) &&
                          Match(patternIndex + 1, valueIndex + 1);
            }

            memo[key] = matched;
            return matched;
        }

        return Match(0, 0);
    }
}

public sealed partial class NeraFormulaEngine
{
    private CellValue EvaluateIfError(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count != 2)
        {
            return CellValue.FromError("#VALUE!");
        }

        var value = EvaluateNode(
            function.Arguments[0],
            context,
            dependencies);
        return value.Kind == CellValueKind.Error
            ? EvaluateNode(
                function.Arguments[1],
                context,
                dependencies)
            : value;
    }

    private CellValue EvaluateIfNa(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count != 2)
        {
            return CellValue.FromError("#VALUE!");
        }

        var value = EvaluateNode(
            function.Arguments[0],
            context,
            dependencies);
        return IsError(value, "#N/A")
            ? EvaluateNode(
                function.Arguments[1],
                context,
                dependencies)
            : value;
    }

    private CellValue EvaluateSwitch(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count is < 3 or > 254)
        {
            return CellValue.FromError("#VALUE!");
        }

        var expression = EvaluateNode(
            function.Arguments[0],
            context,
            dependencies);
        if (expression.Kind == CellValueKind.Error)
        {
            return expression;
        }

        var hasDefault = function.Arguments.Count % 2 == 0;
        var pairLimit = hasDefault
            ? function.Arguments.Count - 1
            : function.Arguments.Count;
        for (var index = 1; index + 1 < pairLimit; index += 2)
        {
            var candidate = EvaluateNode(
                function.Arguments[index],
                context,
                dependencies);
            if (candidate.Kind == CellValueKind.Error)
            {
                return candidate;
            }
            if (!XMatchFormulaEvaluation.ValuesEqual(
                    expression,
                    candidate))
            {
                continue;
            }

            return EvaluateNode(
                function.Arguments[index + 1],
                context,
                dependencies);
        }

        return hasDefault
            ? EvaluateNode(
                function.Arguments[^1],
                context,
                dependencies)
            : CellValue.FromError("#N/A");
    }

    private CellValue EvaluateXMatch(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count is < 2 or > 4)
        {
            return CellValue.FromError("#VALUE!");
        }

        var lookupValue = EvaluateNode(
            function.Arguments[0],
            context,
            dependencies);
        if (lookupValue.Kind == CellValueKind.Error)
        {
            return lookupValue;
        }

        if (!TryReadXMatchValues(
                function.Arguments[1],
                context,
                dependencies,
                out var values,
                out var arrayError))
        {
            return arrayError;
        }

        if (!TryReadXMatchMode(
                function,
                2,
                0,
                context,
                dependencies,
                out var matchMode,
                out var modeError) ||
            !TryReadXMatchMode(
                function,
                3,
                1,
                context,
                dependencies,
                out var searchMode,
                out modeError))
        {
            return modeError;
        }

        return XMatchFormulaEvaluation.TryMatch(
                lookupValue,
                values,
                matchMode,
                searchMode,
                out var position,
                out var error)
            ? CellValue.FromNumber(position)
            : error;
    }

    private bool TryReadXMatchValues(
        FormulaNode node,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        out CellValue[] values,
        out CellValue error)
    {
        switch (node)
        {
            case RangeNode range:
                if (range.Range.RowCount > 1 &&
                    range.Range.ColumnCount > 1)
                {
                    values = [];
                    error = CellValue.FromError("#VALUE!");
                    return false;
                }

                dependencies.Add(new FormulaDependency(
                    range.WorksheetName,
                    range.Range));
                var result = new List<CellValue>(
                    checked(
                        range.Range.RowCount *
                        range.Range.ColumnCount));
                for (var row = range.Range.Top;
                     row <= range.Range.Bottom;
                     row++)
                {
                    for (var column = range.Range.Left;
                         column <= range.Range.Right;
                         column++)
                    {
                        result.Add(context.GetCellValue(
                            range.WorksheetName,
                            new CellAddress(row, column)));
                    }
                }
                values = result.ToArray();
                error = default;
                return true;

            case CellNode cell:
                values =
                [
                    EvaluateNode(
                        cell,
                        context,
                        dependencies),
                ];
                error = values[0].Kind == CellValueKind.Error
                    ? values[0]
                    : default;
                return values[0].Kind != CellValueKind.Error;

            default:
                var scalar = EvaluateNode(node, context, dependencies);
                if (scalar.Kind == CellValueKind.Error)
                {
                    values = [];
                    error = scalar;
                    return false;
                }

                values = [scalar];
                error = default;
                return true;
        }
    }

    private bool TryReadXMatchMode(
        FunctionNode function,
        int index,
        int defaultValue,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        out int value,
        out CellValue error)
    {
        if (function.Arguments.Count <= index ||
            function.Arguments[index] is MissingArgumentNode)
        {
            value = defaultValue;
            error = default;
            return true;
        }

        var modeValue = EvaluateNode(
            function.Arguments[index],
            context,
            dependencies);
        if (modeValue.Kind == CellValueKind.Error)
        {
            value = default;
            error = modeValue;
            return false;
        }
        if (!FormulaValueCoercion.TryNumber(
                modeValue,
                out var number,
                allowText: true) ||
            !double.IsFinite(number) ||
            number < int.MinValue ||
            number > int.MaxValue)
        {
            value = default;
            error = CellValue.FromError("#VALUE!");
            return false;
        }

        value = checked((int)Math.Truncate(number));
        error = default;
        return true;
    }

    private static bool IsError(CellValue value, string code) =>
        value.Kind == CellValueKind.Error &&
        string.Equals(
            Convert.ToString(
                value.RawValue,
                CultureInfo.InvariantCulture),
            code,
            StringComparison.OrdinalIgnoreCase);
}
