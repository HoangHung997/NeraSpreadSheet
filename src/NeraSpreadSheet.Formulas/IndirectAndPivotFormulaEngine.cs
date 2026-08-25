using System.Globalization;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal readonly record struct FormulaReferenceTarget(
    string? WorksheetName,
    CellRange Range);

internal static class IndirectFormulaEvaluation
{
    public const int MaximumReferenceTextLength = 8192;

    public static bool TryResolve(
        FunctionNode function,
        Func<FormulaNode, CellValue> evaluate,
        IFormulaEvaluationContext context,
        out FormulaReferenceTarget target,
        out CellValue error)
    {
        ArgumentNullException.ThrowIfNull(function);
        ArgumentNullException.ThrowIfNull(evaluate);
        ArgumentNullException.ThrowIfNull(context);

        target = default;
        if (function.Arguments.Count is < 1 or > 2)
        {
            error = CellValue.FromError("#VALUE!");
            return false;
        }

        var referenceValue = evaluate(function.Arguments[0]);
        if (referenceValue.Kind == CellValueKind.Error)
        {
            error = referenceValue;
            return false;
        }
        var referenceText = FormulaValueCoercion.ToText(referenceValue);
        if (referenceText.Length == 0)
        {
            error = CellValue.FromError("#REF!");
            return false;
        }
        if (referenceText.Length > MaximumReferenceTextLength)
        {
            error = CellValue.FromError("#NUM!");
            return false;
        }

        var useA1Style = true;
        if (function.Arguments.Count == 2 &&
            function.Arguments[1] is not MissingArgumentNode)
        {
            var styleValue = evaluate(function.Arguments[1]);
            if (styleValue.Kind == CellValueKind.Error)
            {
                error = styleValue;
                return false;
            }
            if (!FormulaValueCoercion.TryBoolean(
                    styleValue,
                    out useA1Style,
                    allowText: true))
            {
                error = CellValue.FromError("#VALUE!");
                return false;
            }
        }

        if (!TryParseReference(
                referenceText,
                useA1Style,
                context,
                out target))
        {
            error = CellValue.FromError("#REF!");
            return false;
        }

        error = default;
        return true;
    }

    private static bool TryParseReference(
        string text,
        bool useA1Style,
        IFormulaEvaluationContext context,
        out FormulaReferenceTarget target)
    {
        target = default;
        var trimmed = text.Trim();
        if (trimmed.Length == 0 ||
            trimmed.Contains('[') ||
            trimmed.Contains(']') ||
            !TrySplitWorksheet(
                trimmed,
                out var worksheetName,
                out var referenceText))
        {
            return false;
        }

        var separator = referenceText.IndexOf(':');
        if (separator >= 0 &&
            referenceText.IndexOf(':', separator + 1) >= 0)
        {
            return false;
        }

        var firstText = separator < 0
            ? referenceText
            : referenceText[..separator];
        var secondText = separator < 0
            ? firstText
            : referenceText[(separator + 1)..];
        if (firstText.Length == 0 || secondText.Length == 0)
        {
            return false;
        }

        CellAddress first;
        CellAddress second;
        if (useA1Style)
        {
            if (!CellAddress.TryParseA1(firstText, out first) ||
                !CellAddress.TryParseA1(secondText, out second))
            {
                return false;
            }
        }
        else
        {
            var current = context is IFormulaReferenceIntrospectionContext
                introspection
                ? introspection.CurrentCellAddress
                : (CellAddress?)null;
            if (!TryParseR1C1(firstText, current, out first) ||
                !TryParseR1C1(secondText, current, out second))
            {
                return false;
            }
        }

        target = new FormulaReferenceTarget(
            worksheetName,
            new CellRange(first, second));
        return true;
    }

    private static bool TrySplitWorksheet(
        string text,
        out string? worksheetName,
        out string referenceText)
    {
        var separator = -1;
        var quoted = false;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\'')
            {
                if (quoted &&
                    index + 1 < text.Length &&
                    text[index + 1] == '\'')
                {
                    index++;
                    continue;
                }
                quoted = !quoted;
                continue;
            }
            if (!quoted && text[index] == '!')
            {
                if (separator >= 0)
                {
                    worksheetName = null;
                    referenceText = string.Empty;
                    return false;
                }
                separator = index;
            }
        }
        if (quoted)
        {
            worksheetName = null;
            referenceText = string.Empty;
            return false;
        }

        if (separator < 0)
        {
            worksheetName = null;
            referenceText = text.Trim();
            return referenceText.Length > 0;
        }

        var worksheetText = text[..separator].Trim();
        referenceText = text[(separator + 1)..].Trim();
        if (worksheetText.Length == 0 || referenceText.Length == 0)
        {
            worksheetName = null;
            return false;
        }
        if (worksheetText[0] == '\'' ||
            worksheetText[^1] == '\'')
        {
            if (worksheetText.Length < 2 ||
                worksheetText[0] != '\'' ||
                worksheetText[^1] != '\'')
            {
                worksheetName = null;
                return false;
            }
            worksheetName = worksheetText[1..^1].Replace(
                "''",
                "'",
                StringComparison.Ordinal);
        }
        else
        {
            if (worksheetText.Contains('\''))
            {
                worksheetName = null;
                return false;
            }
            worksheetName = worksheetText;
        }

        return worksheetName.Length > 0;
    }

    private static bool TryParseR1C1(
        string text,
        CellAddress? current,
        out CellAddress address)
    {
        address = default;
        var value = text.Trim();
        if (value.Length < 2 ||
            char.ToUpperInvariant(value[0]) != 'R')
        {
            return false;
        }

        var index = 1;
        if (!TryParseComponent(
                value,
                ref index,
                'C',
                current?.RowIndex,
                SpreadsheetLimits.MaxRows,
                out var rowIndex) ||
            index >= value.Length ||
            char.ToUpperInvariant(value[index]) != 'C')
        {
            return false;
        }
        index++;
        if (!TryParseComponent(
                value,
                ref index,
                null,
                current?.ColumnIndex,
                SpreadsheetLimits.MaxColumns,
                out var columnIndex) ||
            index != value.Length)
        {
            return false;
        }

        address = new CellAddress(rowIndex, columnIndex);
        return true;
    }

    private static bool TryParseComponent(
        string text,
        ref int index,
        char? terminator,
        int? currentIndex,
        int maximum,
        out int result)
    {
        if (index >= text.Length ||
            terminator is not null &&
            char.ToUpperInvariant(text[index]) == terminator)
        {
            if (currentIndex is null)
            {
                result = default;
                return false;
            }
            result = currentIndex.Value;
            return true;
        }

        if (text[index] == '[')
        {
            if (currentIndex is null)
            {
                result = default;
                return false;
            }
            var close = text.IndexOf(']', index + 1);
            if (close < 0 ||
                !int.TryParse(
                    text.AsSpan(index + 1, close - index - 1),
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out var offset))
            {
                result = default;
                return false;
            }
            var computed = (long)currentIndex.Value + offset;
            if (computed < 0 || computed >= maximum)
            {
                result = default;
                return false;
            }
            result = (int)computed;
            index = close + 1;
            return true;
        }

        var start = index;
        long number = 0;
        while (index < text.Length && char.IsAsciiDigit(text[index]))
        {
            number = (number * 10) + (text[index] - '0');
            if (number > maximum)
            {
                result = default;
                return false;
            }
            index++;
        }
        if (index == start || number < 1 || number > maximum)
        {
            result = default;
            return false;
        }
        result = checked((int)number - 1);
        return true;
    }
}

public sealed partial class NeraFormulaEngine
{
    private CellValue EvaluateIndirect(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (!IndirectFormulaEvaluation.TryResolve(
                function,
                node => EvaluateNode(node, context, dependencies),
                context,
                out var target,
                out var error))
        {
            return error;
        }

        dependencies.Add(new FormulaDependency(
            target.WorksheetName,
            target.Range));
        return context.GetCellValue(
            target.WorksheetName,
            target.Range.TopLeft);
    }

    private bool TryEvaluateIndirectInvocationArgument(
        FormulaNode node,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        out FormulaFunctionArgument argument)
    {
        if (node is not FunctionNode function ||
            !string.Equals(
                function.Name,
                "INDIRECT",
                StringComparison.OrdinalIgnoreCase))
        {
            argument = null!;
            return false;
        }

        if (!IndirectFormulaEvaluation.TryResolve(
                function,
                candidate => EvaluateNode(
                    candidate,
                    context,
                    dependencies),
                context,
                out var target,
                out var error))
        {
            argument = FormulaFunctionArgument.Scalar(error);
            return true;
        }

        var dependency = new FormulaDependency(
            target.WorksheetName,
            target.Range);
        dependencies.Add(dependency);
        var values = new List<CellValue>(
            checked(target.Range.RowCount * target.Range.ColumnCount));
        for (var row = target.Range.Top;
             row <= target.Range.Bottom;
             row++)
        {
            for (var column = target.Range.Left;
                 column <= target.Range.Right;
                 column++)
            {
                values.Add(context.GetCellValue(
                    target.WorksheetName,
                    new CellAddress(row, column)));
            }
        }
        argument = FormulaFunctionArgument.Range(dependency, values);
        return true;
    }

    private CellValue EvaluateGetPivotData(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count is < 2 or > 254 ||
            (function.Arguments.Count - 2) % 2 != 0)
        {
            return CellValue.FromError("#VALUE!");
        }

        var dataFieldValue = EvaluateNode(
            function.Arguments[0],
            context,
            dependencies);
        if (dataFieldValue.Kind == CellValueKind.Error)
        {
            return dataFieldValue;
        }
        var dataField = FormulaValueCoercion.ToText(dataFieldValue);
        if (dataField.Length == 0)
        {
            return CellValue.FromError("#VALUE!");
        }

        string? worksheetName;
        CellRange pivotRange;
        CellValue referenceError;
        if (function.Arguments[1] is FunctionNode indirect &&
            string.Equals(
                indirect.Name,
                "INDIRECT",
                StringComparison.OrdinalIgnoreCase))
        {
            if (!IndirectFormulaEvaluation.TryResolve(
                    indirect,
                    node => EvaluateNode(
                        node,
                        context,
                        dependencies),
                    context,
                    out var target,
                    out referenceError))
            {
                return referenceError;
            }
            worksheetName = target.WorksheetName;
            pivotRange = target.Range;
        }
        else if (!TryResolveReference(
                     function.Arguments[1],
                     context,
                     dependencies,
                     out worksheetName,
                     out pivotRange,
                     out referenceError))
        {
            return referenceError;
        }

        var fieldItems = new List<FormulaPivotFieldItem>(
            (function.Arguments.Count - 2) / 2);
        for (var index = 2;
             index < function.Arguments.Count;
             index += 2)
        {
            var fieldValue = EvaluateNode(
                function.Arguments[index],
                context,
                dependencies);
            if (fieldValue.Kind == CellValueKind.Error)
            {
                return fieldValue;
            }
            var fieldName = FormulaValueCoercion.ToText(fieldValue);
            if (fieldName.Length == 0)
            {
                return CellValue.FromError("#VALUE!");
            }

            var item = EvaluateNode(
                function.Arguments[index + 1],
                context,
                dependencies);
            if (item.Kind == CellValueKind.Error)
            {
                return item;
            }
            fieldItems.Add(new FormulaPivotFieldItem(fieldName, item));
        }

        if (context is not IFormulaPivotDataEvaluationContext pivotContext ||
            !pivotContext.TryGetPivotData(
                worksheetName,
                pivotRange,
                dataField,
                fieldItems,
                out var value,
                out var providerDependencies))
        {
            return CellValue.FromError("#REF!");
        }

        dependencies.AddRange(
            providerDependencies ?? Array.Empty<FormulaDependency>());
        return value;
    }
}
