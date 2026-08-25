from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def replace_once(relative: str, old: str, new: str) -> None:
    path = ROOT / relative
    text = path.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise RuntimeError(
            f"Expected exactly one replacement in {relative}, found {count}."
        )
    path.write_text(text.replace(old, new), encoding="utf-8", newline="\n")


def write(relative: str, content: str) -> None:
    path = ROOT / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8", newline="\n")


replace_once(
    "src/NeraSpreadSheet.Formulas/FormulaParser.cs",
    '''        if (!CellAddress.TryParseA1(addressText, out var firstAddress))
        {
            throw new FormatException($"Unknown name '{identifier}'.");
        }
''',
    '''        if (!CellAddress.TryParseA1(addressText, out var firstAddress))
        {
            if (worksheetName is not null)
            {
                throw new FormatException(
                    $"Unknown reference '{worksheetName}!{addressText}'.");
            }
            return new NameNode(identifier);
        }
''',
)

replace_once(
    "src/NeraSpreadSheet.Formulas/NeraFormulaEngine.cs",
    '''            MissingArgumentNode => CellValue.Blank,
            CellNode cell => EvaluateCell(cell, context, dependencies),
''',
    '''            MissingArgumentNode => CellValue.Blank,
            NameNode => CellValue.FromError("#NAME?"),
            CellNode cell => EvaluateCell(cell, context, dependencies),
''',
)

replace_once(
    "src/NeraSpreadSheet.Formulas/NeraFormulaEngine.cs",
    '''    {
        if (string.Equals(
                function.Name,
                "AREAS",
''',
    '''    {
        if (string.Equals(
                function.Name,
                "GETPIVOTDATA",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateGetPivotData(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "INDIRECT",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateIndirect(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "AREAS",
''',
)

replace_once(
    "src/NeraSpreadSheet.Formulas/NeraFormulaEngine.cs",
    '''        foreach (var argumentNode in function.Arguments)
        {
            if (TryEvaluateChooseInvocationArgument(
''',
    '''        foreach (var argumentNode in function.Arguments)
        {
            if (TryEvaluateIndirectInvocationArgument(
                    argumentNode,
                    context,
                    dependencies,
                    out var indirectArgument))
            {
                invocationArguments.Add(indirectArgument);
                continue;
            }
            if (TryEvaluateChooseInvocationArgument(
''',
)

replace_once(
    "src/NeraSpreadSheet.Formulas/DynamicArrayFormulaEngine.cs",
    '''        if (string.Equals(
                function.Name,
                "EXPAND",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateExpand(function, context, dependencies);
        }
        return Failure("#NAME?", FormulaErrorCode.InvalidName, dependencies);
''',
    '''        if (string.Equals(
                function.Name,
                "EXPAND",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateExpand(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "GROUPBY",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateGroupBy(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "HSTACK",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateHStack(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "INDIRECT",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateIndirectArray(function, context, dependencies);
        }
        return Failure("#NAME?", FormulaErrorCode.InvalidName, dependencies);
''',
)

replace_once(
    "src/NeraSpreadSheet.Formulas/DynamicArrayFormulaEngine.cs",
    '''        string.Equals(
            name,
            "EXPAND",
            StringComparison.OrdinalIgnoreCase);
''',
    '''        string.Equals(
            name,
            "EXPAND",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            name,
            "GROUPBY",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            name,
            "HSTACK",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            name,
            "INDIRECT",
            StringComparison.OrdinalIgnoreCase);
''',
)

replace_once(
    "src/NeraSpreadSheet.Formulas/DynamicArrayFormulaEngine.cs",
    '''                case MissingArgumentNode:
                    break;
                case CellNode cell:
''',
    '''                case MissingArgumentNode:
                    break;
                case NameNode name:
                    builder.Append(name.Name);
                    break;
                case CellNode cell:
''',
)

replace_once(
    "src/NeraSpreadSheet.Formulas/StandardFormulaFunctions.cs",
    '''        foreach (var function in ReferenceSelectionFormulaFunctions.Create())
        {
            yield return function;
        }
        foreach (var function in StatisticalFormulaFunctions.Create())
''',
    '''        foreach (var function in ReferenceSelectionFormulaFunctions.Create())
        {
            yield return function;
        }
        foreach (var function in HyperlinkFormulaFunctions.Create())
        {
            yield return function;
        }
        foreach (var function in StatisticalFormulaFunctions.Create())
''',
)

replace_once(
    "tests/NeraSpreadSheet.Formulas.Tests/BuiltInFormulaTestCounts.cs",
    "    public const int EagerVersioned = 239;\n",
    "    public const int EagerVersioned = 240;\n",
)

write(
    "src/NeraSpreadSheet.Formulas/FormulaNameNode.cs",
    '''namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Represents an unqualified formula name. F010 uses this syntax for the
/// eta-reduced aggregate name supplied to GROUPBY; unresolved names continue
/// to evaluate as #NAME?.
/// </summary>
internal sealed record NameNode(string Name) : FormulaNode;
''',
)

write(
    "src/NeraSpreadSheet.Formulas/FormulaAdvancedContextContracts.cs",
    '''using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

/// <summary>
/// One field/item constraint supplied to GETPIVOTDATA.
/// </summary>
public readonly record struct FormulaPivotFieldItem(
    string FieldName,
    CellValue Item);

/// <summary>
/// Optional deterministic provider used by GETPIVOTDATA. Pivot ownership and
/// cache invalidation remain outside the scalar formula evaluator.
/// </summary>
public interface IFormulaPivotDataEvaluationContext
    : IFormulaEvaluationContext
{
    bool TryGetPivotData(
        string? worksheetName,
        CellRange pivotTableReference,
        string dataField,
        IReadOnlyList<FormulaPivotFieldItem> fieldItems,
        out CellValue value,
        out IReadOnlyList<FormulaDependency> dependencies);
}

/// <summary>
/// Hyperlink metadata emitted by HYPERLINK while the cell value remains the
/// display value used by existing renderers and serializers.
/// </summary>
public readonly record struct FormulaHyperlink(
    string LinkLocation,
    CellValue DisplayValue);

/// <summary>
/// Optional host-owned sink for formula hyperlink metadata.
/// </summary>
public interface IFormulaHyperlinkEvaluationContext
    : IFormulaEvaluationContext
{
    void SetCurrentFormulaHyperlink(FormulaHyperlink hyperlink);
}
''',
)

write(
    "src/NeraSpreadSheet.Formulas/HyperlinkFormulaFunctions.cs",
    '''using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static class HyperlinkFormulaFunctions
{
    public const int MaximumLinkLocationLength = 32_767;

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return new FormulaFunctionDefinition(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity(
                    "NERA.BUILTIN",
                    "HYPERLINK"),
                new FormulaFunctionVersion(1, 0, 0),
                FormulaFunctionApiVersion.Current,
                1,
                2,
                FormulaFunctionCapabilities.ScalarArguments |
                FormulaFunctionCapabilities.ReturnsScalar,
                securityClassification:
                    FormulaFunctionSecurityClassification.ContextReadOnly,
                argumentCountPolicy:
                    FormulaFunctionArgumentCountPolicy.LogicalArguments),
            EvaluateHyperlink);
    }

    private static FormulaEvaluationResult EvaluateHyperlink(
        FormulaFunctionInvocation invocation)
    {
        if (invocation.Arguments[0].Kind !=
            FormulaFunctionArgumentKind.Scalar)
        {
            return FormulaEvaluationResult.Failure(
                FormulaErrorCode.InvalidValue);
        }

        var linkLocation = FormulaValueCoercion.ToText(
            invocation.Arguments[0].ScalarValue);
        if (linkLocation.Length > MaximumLinkLocationLength)
        {
            return FormulaEvaluationResult.Failure(
                FormulaErrorCode.InvalidValue);
        }

        var displayValue = invocation.Arguments.Count == 2
            ? invocation.Arguments[1].ScalarValue
            : CellValue.FromText(linkLocation);
        if (invocation.Context is IFormulaHyperlinkEvaluationContext
            hyperlinkContext)
        {
            hyperlinkContext.SetCurrentFormulaHyperlink(
                new FormulaHyperlink(linkLocation, displayValue));
        }

        return FormulaEvaluationResult.Success(displayValue);
    }
}
''',
)

write(
    "src/NeraSpreadSheet.Formulas/IndirectAndPivotFormulaEngine.cs",
    r'''using System.Globalization;
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
''',
)

write(
    "src/NeraSpreadSheet.Formulas/DynamicArrayCombinationFormulaFunctions.cs",
    r'''using System.Globalization;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

public sealed partial class NeraDynamicArrayFormulaEngine
{
    private FormulaArrayEvaluationResult EvaluateHStack(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count is < 1 or > 254)
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var arrays = new List<FormulaArrayValue>(function.Arguments.Count);
        var rowCount = 0;
        long columnCount = 0;
        foreach (var node in function.Arguments)
        {
            var source = EvaluateNodeAsArray(node, context, dependencies);
            if (!source.IsSuccess)
            {
                return source;
            }
            arrays.Add(source.Value!);
            rowCount = Math.Max(rowCount, source.Value!.RowCount);
            columnCount = checked(columnCount + source.Value.ColumnCount);
        }

        var cellCount = checked((long)rowCount * columnCount);
        if (columnCount > int.MaxValue ||
            cellCount > FormulaArrayValue.MaximumCellCount)
        {
            return Failure(
                "#NUM!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var offsets = new int[arrays.Count];
        var runningOffset = 0;
        for (var index = 0; index < arrays.Count; index++)
        {
            offsets[index] = runningOffset;
            runningOffset = checked(
                runningOffset + arrays[index].ColumnCount);
        }

        var padding = CellValue.FromError("#N/A");
        return FormulaArrayEvaluationResult.Success(
            FormulaArrayValue.Create(
                rowCount,
                checked((int)columnCount),
                (row, column) =>
                {
                    for (var index = arrays.Count - 1;
                         index >= 0;
                         index--)
                    {
                        if (column < offsets[index])
                        {
                            continue;
                        }
                        var localColumn = column - offsets[index];
                        if (localColumn >= arrays[index].ColumnCount)
                        {
                            continue;
                        }
                        return row < arrays[index].RowCount
                            ? arrays[index][row, localColumn]
                            : padding;
                    }
                    return padding;
                }),
            DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateIndirectArray(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (!IndirectFormulaEvaluation.TryResolve(
                function,
                node => EvaluateScalarNode(
                    node,
                    context,
                    dependencies),
                context,
                out var target,
                out var error))
        {
            return FormulaArrayEvaluationResult.Failure(
                error,
                ToErrorCode(error),
                DistinctDependencies(dependencies));
        }

        dependencies.Add(new FormulaDependency(
            target.WorksheetName,
            target.Range));
        return FormulaArrayEvaluationResult.Success(
            FormulaArrayValue.Create(
                target.Range.RowCount,
                target.Range.ColumnCount,
                (row, column) => context.GetCellValue(
                    target.WorksheetName,
                    new CellAddress(
                        target.Range.Top + row,
                        target.Range.Left + column))),
            DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateGroupBy(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count is < 3 or > 8)
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var rowFieldsResult = EvaluateNodeAsArray(
            function.Arguments[0],
            context,
            dependencies);
        if (!rowFieldsResult.IsSuccess)
        {
            return rowFieldsResult;
        }
        var valuesResult = EvaluateNodeAsArray(
            function.Arguments[1],
            context,
            dependencies);
        if (!valuesResult.IsSuccess)
        {
            return valuesResult;
        }

        var rowFields = rowFieldsResult.Value!;
        var values = valuesResult.Value!;
        if (rowFields.RowCount != values.RowCount)
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }
        if (!TryGetGroupByAggregate(
                function.Arguments[2],
                out var aggregateKind))
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        if (!TryReadOptionalInteger(
                function,
                3,
                -1,
                context,
                dependencies,
                out var fieldHeaders,
                out var argumentError) ||
            fieldHeaders is < -1 or > 3)
        {
            return argumentError ?? Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }
        if (!TryReadOptionalInteger(
                function,
                4,
                1,
                context,
                dependencies,
                out var totalDepth,
                out argumentError) ||
            totalDepth is < -1 or > 1)
        {
            return argumentError ?? Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }
        if (!TryReadOptionalInteger(
                function,
                5,
                0,
                context,
                dependencies,
                out var sortOrder,
                out argumentError))
        {
            return argumentError!;
        }
        if (!TryReadOptionalInteger(
                function,
                7,
                0,
                context,
                dependencies,
                out var fieldRelationship,
                out argumentError) ||
            fieldRelationship is < 0 or > 1)
        {
            return argumentError ?? Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var hasInputHeaders = fieldHeaders switch
        {
            1 or 3 => true,
            0 or 2 => false,
            _ => DetectGroupByHeaders(values),
        };
        var showHeaders = fieldHeaders switch
        {
            2 or 3 => true,
            0 or 1 => false,
            _ => rowFields.ColumnCount > 1 || values.ColumnCount > 1,
        };
        var dataStart = hasInputHeaders ? 1 : 0;
        if (dataStart >= rowFields.RowCount)
        {
            return Failure(
                "#CALC!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        bool[]? filter = null;
        if (function.Arguments.Count >= 7 &&
            function.Arguments[6] is not MissingArgumentNode)
        {
            if (!TryReadGroupByFilter(
                    function.Arguments[6],
                    rowFields.RowCount,
                    context,
                    dependencies,
                    out filter,
                    out argumentError))
            {
                return argumentError!;
            }
        }

        var groups = new List<GroupByGroup>();
        var lookup = new Dictionary<CellValue[], GroupByGroup>(
            GroupByKeyComparer.Instance);
        var includedRows = new List<int>();
        for (var row = dataStart; row < rowFields.RowCount; row++)
        {
            if (filter is not null && !filter[row])
            {
                continue;
            }
            includedRows.Add(row);
            var key = rowFields.EnumerateRow(row).ToArray();
            if (!lookup.TryGetValue(key, out var group))
            {
                group = new GroupByGroup(key);
                lookup.Add(key, group);
                groups.Add(group);
            }
            group.Rows.Add(row);
        }
        if (includedRows.Count == 0)
        {
            return Failure(
                "#CALC!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var results = groups
            .Select((group, index) => new GroupByResult(
                index,
                group.Key,
                AggregateColumns(values, group.Rows, aggregateKind)))
            .ToList();
        var outputColumns = checked(
            rowFields.ColumnCount + values.ColumnCount);
        if (sortOrder != 0)
        {
            var absoluteSort = Math.Abs((long)sortOrder);
            if (absoluteSort < 1 || absoluteSort > outputColumns)
            {
                return Failure(
                    "#VALUE!",
                    FormulaErrorCode.InvalidValue,
                    dependencies);
            }
            var sortIndex = checked((int)absoluteSort - 1);
            var descending = sortOrder < 0;
            results = results
                .OrderBy(
                    result => GetGroupByOutputValue(
                        result,
                        sortIndex,
                        rowFields.ColumnCount),
                    descending
                        ? GroupByCellComparer.Descending
                        : GroupByCellComparer.Ascending)
                .ThenBy(static result => result.OriginalIndex)
                .ToList();
        }

        var outputRows = checked(
            results.Count +
            (showHeaders ? 1 : 0) +
            (totalDepth == 0 ? 0 : 1));
        var outputCellCount = checked((long)outputRows * outputColumns);
        if (outputCellCount > FormulaArrayValue.MaximumCellCount)
        {
            return Failure(
                "#NUM!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var output = new List<CellValue[]>(outputRows);
        if (showHeaders)
        {
            output.Add(CreateGroupByHeaders(
                rowFields,
                values,
                hasInputHeaders));
        }
        var total = new GroupByResult(
            -1,
            CreateTotalKey(rowFields.ColumnCount),
            AggregateColumns(values, includedRows, aggregateKind));
        if (totalDepth < 0)
        {
            output.Add(FlattenGroupByResult(total));
        }
        output.AddRange(results.Select(FlattenGroupByResult));
        if (totalDepth > 0)
        {
            output.Add(FlattenGroupByResult(total));
        }

        return FormulaArrayEvaluationResult.Success(
            new FormulaArrayValue(
                output.Count,
                outputColumns,
                output.SelectMany(static row => row)),
            DistinctDependencies(dependencies));
    }

    private bool TryReadOptionalInteger(
        FunctionNode function,
        int argumentIndex,
        int defaultValue,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        out int value,
        out FormulaArrayEvaluationResult? error)
    {
        if (function.Arguments.Count <= argumentIndex ||
            function.Arguments[argumentIndex] is MissingArgumentNode)
        {
            value = defaultValue;
            error = null;
            return true;
        }

        var scalar = EvaluateScalarNode(
            function.Arguments[argumentIndex],
            context,
            dependencies);
        if (scalar.Kind == CellValueKind.Error)
        {
            value = default;
            error = FormulaArrayEvaluationResult.Failure(
                scalar,
                ToErrorCode(scalar),
                DistinctDependencies(dependencies));
            return false;
        }
        if (scalar.Kind == CellValueKind.Blank)
        {
            value = defaultValue;
            error = null;
            return true;
        }
        if (!FormulaValueCoercion.TryNumber(
                scalar,
                out var number,
                allowText: true) ||
            !double.IsFinite(number))
        {
            value = default;
            error = Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
            return false;
        }

        var truncated = Math.Truncate(number);
        if (truncated < int.MinValue || truncated > int.MaxValue)
        {
            value = default;
            error = Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
            return false;
        }
        value = checked((int)truncated);
        error = null;
        return true;
    }

    private bool TryReadGroupByFilter(
        FormulaNode node,
        int rowCount,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        out bool[] filter,
        out FormulaArrayEvaluationResult? error)
    {
        var filterResult = EvaluateNodeAsArray(
            node,
            context,
            dependencies);
        if (!filterResult.IsSuccess)
        {
            filter = [];
            error = filterResult;
            return false;
        }
        var value = filterResult.Value!;
        var isColumn = value.RowCount == rowCount &&
                       value.ColumnCount == 1;
        var isRow = value.RowCount == 1 &&
                    value.ColumnCount == rowCount;
        if (!isColumn && !isRow)
        {
            filter = [];
            error = Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
            return false;
        }

        filter = new bool[rowCount];
        for (var index = 0; index < rowCount; index++)
        {
            var cell = isColumn ? value[index, 0] : value[0, index];
            if (cell.Kind == CellValueKind.Error)
            {
                error = FormulaArrayEvaluationResult.Failure(
                    cell,
                    ToErrorCode(cell),
                    DistinctDependencies(dependencies));
                return false;
            }
            if (!FormulaValueCoercion.TryBoolean(cell, out filter[index]))
            {
                error = Failure(
                    "#VALUE!",
                    FormulaErrorCode.InvalidValue,
                    dependencies);
                return false;
            }
        }
        error = null;
        return true;
    }

    private static bool TryGetGroupByAggregate(
        FormulaNode node,
        out GroupByAggregateKind kind)
    {
        string? name = node switch
        {
            NameNode formulaName => formulaName.Name,
            ConstantNode constant
                when constant.Value.Kind == CellValueKind.Text =>
                (string)constant.Value.RawValue!,
            FunctionNode function when function.Arguments.Count == 0 =>
                function.Name,
            _ => null,
        };
        kind = name?.Trim().ToUpperInvariant() switch
        {
            "SUM" => GroupByAggregateKind.Sum,
            "AVERAGE" => GroupByAggregateKind.Average,
            "COUNT" => GroupByAggregateKind.Count,
            "COUNTA" => GroupByAggregateKind.CountA,
            "MAX" => GroupByAggregateKind.Maximum,
            "MIN" => GroupByAggregateKind.Minimum,
            _ => default,
        };
        return name is not null &&
               name.Trim().ToUpperInvariant() is
                   "SUM" or "AVERAGE" or "COUNT" or
                   "COUNTA" or "MAX" or "MIN";
    }

    private static bool DetectGroupByHeaders(FormulaArrayValue values)
    {
        if (values.RowCount < 2)
        {
            return false;
        }
        var firstHasText = values.EnumerateRow(0)
            .Any(static value => value.Kind == CellValueKind.Text);
        var secondHasNumber = values.EnumerateRow(1)
            .Any(static value => value.Kind == CellValueKind.Number);
        return firstHasText && secondHasNumber;
    }

    private static CellValue[] AggregateColumns(
        FormulaArrayValue values,
        IReadOnlyList<int> rows,
        GroupByAggregateKind kind)
    {
        var result = new CellValue[values.ColumnCount];
        for (var column = 0;
             column < values.ColumnCount;
             column++)
        {
            result[column] = AggregateGroupByColumn(
                values,
                rows,
                column,
                kind);
        }
        return result;
    }

    private static CellValue AggregateGroupByColumn(
        FormulaArrayValue values,
        IReadOnlyList<int> rows,
        int column,
        GroupByAggregateKind kind)
    {
        CellValue? firstError = null;
        var numbers = new List<double>();
        var nonBlankCount = 0;
        foreach (var row in rows)
        {
            var value = values[row, column];
            if (value.Kind == CellValueKind.Error)
            {
                firstError ??= value;
                continue;
            }
            if (!value.IsBlank)
            {
                nonBlankCount++;
            }
            if (value.Kind == CellValueKind.Number)
            {
                numbers.Add((double)value.RawValue!);
            }
        }
        if (firstError is not null)
        {
            return firstError.Value;
        }

        return kind switch
        {
            GroupByAggregateKind.Sum =>
                FormulaValueCoercion.SafeNumber(numbers.Sum()),
            GroupByAggregateKind.Average => numbers.Count == 0
                ? CellValue.FromError("#DIV/0!")
                : FormulaValueCoercion.SafeNumber(numbers.Average()),
            GroupByAggregateKind.Count =>
                CellValue.FromNumber(numbers.Count),
            GroupByAggregateKind.CountA =>
                CellValue.FromNumber(nonBlankCount),
            GroupByAggregateKind.Maximum => numbers.Count == 0
                ? CellValue.FromNumber(0d)
                : FormulaValueCoercion.SafeNumber(numbers.Max()),
            GroupByAggregateKind.Minimum => numbers.Count == 0
                ? CellValue.FromNumber(0d)
                : FormulaValueCoercion.SafeNumber(numbers.Min()),
            _ => CellValue.FromError("#VALUE!"),
        };
    }

    private static CellValue[] CreateGroupByHeaders(
        FormulaArrayValue rowFields,
        FormulaArrayValue values,
        bool hasInputHeaders)
    {
        var headers = new CellValue[
            checked(rowFields.ColumnCount + values.ColumnCount)];
        for (var column = 0;
             column < rowFields.ColumnCount;
             column++)
        {
            headers[column] = hasInputHeaders
                ? rowFields[0, column]
                : CellValue.FromText(
                    $"Row {column + 1}");
        }
        for (var column = 0;
             column < values.ColumnCount;
             column++)
        {
            headers[rowFields.ColumnCount + column] = hasInputHeaders
                ? values[0, column]
                : CellValue.FromText(
                    $"Value {column + 1}");
        }
        return headers;
    }

    private static CellValue[] CreateTotalKey(int columnCount)
    {
        var key = new CellValue[columnCount];
        key[0] = CellValue.FromText("Grand Total");
        return key;
    }

    private static CellValue[] FlattenGroupByResult(
        GroupByResult result) =>
        [.. result.Key, .. result.Aggregates];

    private static CellValue GetGroupByOutputValue(
        GroupByResult result,
        int outputIndex,
        int keyColumnCount) =>
        outputIndex < keyColumnCount
            ? result.Key[outputIndex]
            : result.Aggregates[outputIndex - keyColumnCount];

    private enum GroupByAggregateKind
    {
        Sum = 0,
        Average,
        Count,
        CountA,
        Maximum,
        Minimum,
    }

    private sealed class GroupByGroup
    {
        public GroupByGroup(CellValue[] key)
        {
            Key = key;
        }

        public CellValue[] Key { get; }

        public List<int> Rows { get; } = [];
    }

    private sealed record GroupByResult(
        int OriginalIndex,
        CellValue[] Key,
        CellValue[] Aggregates);

    private sealed class GroupByKeyComparer :
        IEqualityComparer<CellValue[]>
    {
        public static GroupByKeyComparer Instance { get; } = new();

        public bool Equals(CellValue[]? left, CellValue[]? right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }
            if (left is null || right is null ||
                left.Length != right.Length)
            {
                return false;
            }
            for (var index = 0; index < left.Length; index++)
            {
                if (!CellEquals(left[index], right[index]))
                {
                    return false;
                }
            }
            return true;
        }

        public int GetHashCode(CellValue[] values)
        {
            var hash = new HashCode();
            foreach (var value in values)
            {
                if (value.Kind == CellValueKind.Text)
                {
                    hash.Add(
                        (string)value.RawValue!,
                        StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    hash.Add(value);
                }
            }
            return hash.ToHashCode();
        }

        private static bool CellEquals(CellValue left, CellValue right) =>
            left.Kind == CellValueKind.Text &&
            right.Kind == CellValueKind.Text
                ? string.Equals(
                    (string)left.RawValue!,
                    (string)right.RawValue!,
                    StringComparison.OrdinalIgnoreCase)
                : left.Equals(right);
    }

    private sealed class GroupByCellComparer : IComparer<CellValue>
    {
        public static GroupByCellComparer Ascending { get; } =
            new(descending: false);

        public static GroupByCellComparer Descending { get; } =
            new(descending: true);

        private readonly bool _descending;

        private GroupByCellComparer(bool descending)
        {
            _descending = descending;
        }

        public int Compare(CellValue left, CellValue right)
        {
            var comparison = CompareAscending(left, right);
            return _descending ? -comparison : comparison;
        }

        private static int CompareAscending(
            CellValue left,
            CellValue right)
        {
            var rank = GetRank(left).CompareTo(GetRank(right));
            if (rank != 0)
            {
                return rank;
            }
            return left.Kind switch
            {
                CellValueKind.Blank => 0,
                CellValueKind.Number =>
                    ((double)left.RawValue!).CompareTo(
                        (double)right.RawValue!),
                CellValueKind.DateTime =>
                    ((DateTime)left.RawValue!).CompareTo(
                        (DateTime)right.RawValue!),
                CellValueKind.Text => string.Compare(
                    (string)left.RawValue!,
                    (string)right.RawValue!,
                    StringComparison.OrdinalIgnoreCase),
                CellValueKind.Boolean =>
                    ((bool)left.RawValue!).CompareTo(
                        (bool)right.RawValue!),
                CellValueKind.Error => string.Compare(
                    Convert.ToString(
                        left.RawValue,
                        CultureInfo.InvariantCulture),
                    Convert.ToString(
                        right.RawValue,
                        CultureInfo.InvariantCulture),
                    StringComparison.OrdinalIgnoreCase),
                _ => 0,
            };
        }

        private static int GetRank(CellValue value) =>
            value.Kind switch
            {
                CellValueKind.Blank => 0,
                CellValueKind.Number => 1,
                CellValueKind.DateTime => 2,
                CellValueKind.Text => 3,
                CellValueKind.Boolean => 4,
                CellValueKind.Error => 5,
                _ => 6,
            };
    }
}
''',
)

write(
    "tests/NeraSpreadSheet.Formulas.Tests/AdvancedReferenceAndAggregationFormulaFunctionTests.cs",
    r'''using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class AdvancedReferenceAndAggregationFormulaFunctionTests
{
    [TestMethod]
    public void GetPivotDataUsesProviderAndProviderDependencies()
    {
        var context = new F010TestContext();
        var engine = new NeraFormulaEngine();

        var result = engine.Evaluate(
            "=GETPIVOTDATA(\"Sales\",A1,\"Region\",\"East\")",
            context);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(42d, GetNumber(result.Value), 1e-12d);
        Assert.AreEqual("Sales", context.LastPivotDataField);
        Assert.AreEqual(1, context.LastPivotItems.Count);
        Assert.AreEqual("Region", context.LastPivotItems[0].FieldName);
        Assert.AreEqual("East", context.LastPivotItems[0].Item.RawValue);
        CollectionAssert.Contains(
            result.Dependencies.ToArray(),
            new FormulaDependency(
                null,
                new CellRange(
                    new CellAddress(0, 3),
                    new CellAddress(3, 3))));
    }

    [TestMethod]
    public void GroupByAggregatesFiltersSortsAndAddsGrandTotal()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromText("Category"),
            [new CellAddress(0, 1)] = CellValue.FromText("Sales"),
            [new CellAddress(0, 2)] = CellValue.FromBoolean(true),
            [new CellAddress(1, 0)] = CellValue.FromText("B"),
            [new CellAddress(1, 1)] = CellValue.FromNumber(2d),
            [new CellAddress(1, 2)] = CellValue.FromBoolean(true),
            [new CellAddress(2, 0)] = CellValue.FromText("A"),
            [new CellAddress(2, 1)] = CellValue.FromNumber(3d),
            [new CellAddress(2, 2)] = CellValue.FromBoolean(true),
            [new CellAddress(3, 0)] = CellValue.FromText("B"),
            [new CellAddress(3, 1)] = CellValue.FromNumber(5d),
            [new CellAddress(3, 2)] = CellValue.FromBoolean(false),
        };
        var context = new F010TestContext(values);
        var engine = new NeraDynamicArrayFormulaEngine();

        Assert.IsTrue(engine.TryEvaluate(
            "=GROUPBY(A1:A4,B1:B4,SUM,3,1,2,C1:C4,1)",
            context,
            out var result));
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(4, result.Value!.RowCount);
        Assert.AreEqual(2, result.Value.ColumnCount);
        Assert.AreEqual("Category", result.Value[0, 0].RawValue);
        Assert.AreEqual("Sales", result.Value[0, 1].RawValue);
        Assert.AreEqual("B", result.Value[1, 0].RawValue);
        Assert.AreEqual(2d, GetNumber(result.Value[1, 1]), 1e-12d);
        Assert.AreEqual("A", result.Value[2, 0].RawValue);
        Assert.AreEqual(3d, GetNumber(result.Value[2, 1]), 1e-12d);
        Assert.AreEqual("Grand Total", result.Value[3, 0].RawValue);
        Assert.AreEqual(5d, GetNumber(result.Value[3, 1]), 1e-12d);
    }

    [TestMethod]
    public void HStackPadsShortArraysWithNotAvailable()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(1d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(2d),
            [new CellAddress(0, 1)] = CellValue.FromNumber(10d),
            [new CellAddress(0, 2)] = CellValue.FromNumber(11d),
            [new CellAddress(1, 1)] = CellValue.FromNumber(12d),
            [new CellAddress(1, 2)] = CellValue.FromNumber(13d),
            [new CellAddress(2, 1)] = CellValue.FromNumber(14d),
            [new CellAddress(2, 2)] = CellValue.FromNumber(15d),
        };
        var context = new F010TestContext(values);
        var engine = new NeraDynamicArrayFormulaEngine();

        Assert.IsTrue(engine.TryEvaluate(
            "=HSTACK(A1:A2,B1:C3)",
            context,
            out var result));
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(3, result.Value!.RowCount);
        Assert.AreEqual(3, result.Value.ColumnCount);
        Assert.AreEqual("#N/A", result.Value[2, 0].RawValue);
        Assert.AreEqual(14d, GetNumber(result.Value[2, 1]), 1e-12d);
        Assert.AreEqual(15d, GetNumber(result.Value[2, 2]), 1e-12d);
    }

    [TestMethod]
    public void HyperlinkReturnsFriendlyValueAndPublishesMetadata()
    {
        var context = new F010TestContext();
        var engine = new NeraFormulaEngine();

        var result = engine.Evaluate(
            "=HYPERLINK(\"https://example.com\",\"Open\")",
            context);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(CellValueKind.Text, result.Value.Kind);
        Assert.AreEqual("Open", result.Value.RawValue);
        Assert.IsNotNull(context.LastHyperlink);
        Assert.AreEqual(
            "https://example.com",
            context.LastHyperlink.Value.LinkLocation);
        Assert.AreEqual(
            "Open",
            context.LastHyperlink.Value.DisplayValue.RawValue);

        var registry = new BuiltInFormulaFunctionRegistry();
        Assert.AreEqual(BuiltInFormulaTestCounts.EagerVersioned, registry.Count);
        Assert.IsTrue(registry.TryGetDescriptor(
            "HYPERLINK",
            out var descriptor));
        Assert.AreEqual(
            FormulaFunctionSecurityClassification.ContextReadOnly,
            descriptor.SecurityClassification);
    }

    [TestMethod]
    public void IndirectSupportsRangeIdentityDynamicSpillAndRelativeR1C1()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromText("B1:B2"),
            [new CellAddress(0, 1)] = CellValue.FromNumber(4d),
            [new CellAddress(1, 1)] = CellValue.FromNumber(6d),
            [new CellAddress(2, 1)] = CellValue.FromNumber(9d),
        };
        var context = new F010TestContext(
            values,
            new CellAddress(3, 3));
        var scalarEngine = new NeraFormulaEngine();

        var sum = scalarEngine.Evaluate("=SUM(INDIRECT(A1))", context);
        Assert.IsTrue(sum.IsSuccess);
        Assert.AreEqual(10d, GetNumber(sum.Value), 1e-12d);
        CollectionAssert.Contains(
            sum.Dependencies.ToArray(),
            new FormulaDependency(
                null,
                new CellRange(
                    new CellAddress(0, 1),
                    new CellAddress(1, 1))));

        var relative = scalarEngine.Evaluate(
            "=INDIRECT(\"R[-1]C[-2]\",FALSE)",
            context);
        Assert.IsTrue(relative.IsSuccess);
        Assert.AreEqual(9d, GetNumber(relative.Value), 1e-12d);

        var arrayEngine = new NeraDynamicArrayFormulaEngine(scalarEngine);
        Assert.IsTrue(arrayEngine.TryEvaluate(
            "=INDIRECT(\"B1:B2\")",
            context,
            out var array));
        Assert.IsTrue(array.IsSuccess);
        Assert.AreEqual(2, array.Value!.RowCount);
        Assert.AreEqual(1, array.Value.ColumnCount);
        Assert.AreEqual(4d, GetNumber(array.Value[0, 0]), 1e-12d);
        Assert.AreEqual(6d, GetNumber(array.Value[1, 0]), 1e-12d);
    }

    private static double GetNumber(CellValue value)
    {
        Assert.AreEqual(CellValueKind.Number, value.Kind);
        return (double)value.RawValue!;
    }

    private sealed class F010TestContext :
        IFormulaReferenceIntrospectionContext,
        IFormulaPivotDataEvaluationContext,
        IFormulaHyperlinkEvaluationContext
    {
        private readonly IReadOnlyDictionary<CellAddress, CellValue> _values;

        public F010TestContext(
            IReadOnlyDictionary<CellAddress, CellValue>? values = null,
            CellAddress? currentAddress = null)
        {
            _values = values ??
                new Dictionary<CellAddress, CellValue>();
            CurrentCellAddress = currentAddress ?? new CellAddress(0, 0);
        }

        public string CurrentWorksheetName => "Sheet1";

        public CellAddress CurrentCellAddress { get; }

        public FormulaHyperlink? LastHyperlink { get; private set; }

        public string? LastPivotDataField { get; private set; }

        public IReadOnlyList<FormulaPivotFieldItem> LastPivotItems
        {
            get;
            private set;
        } = Array.Empty<FormulaPivotFieldItem>();

        public CellValue GetCellValue(
            string? worksheetName,
            CellAddress address) =>
            _values.GetValueOrDefault(address, CellValue.Blank);

        public bool TryGetCellFormula(
            string? worksheetName,
            CellAddress address,
            out string? formula)
        {
            formula = null;
            return false;
        }

        public bool TryGetPivotData(
            string? worksheetName,
            CellRange pivotTableReference,
            string dataField,
            IReadOnlyList<FormulaPivotFieldItem> fieldItems,
            out CellValue value,
            out IReadOnlyList<FormulaDependency> dependencies)
        {
            LastPivotDataField = dataField;
            LastPivotItems = fieldItems.ToArray();
            value = CellValue.FromNumber(42d);
            dependencies =
            [
                new FormulaDependency(
                    worksheetName,
                    new CellRange(
                        new CellAddress(0, 3),
                        new CellAddress(3, 3))),
            ];
            return pivotTableReference.Contains(new CellAddress(0, 0));
        }

        public void SetCurrentFormulaHyperlink(FormulaHyperlink hyperlink)
        {
            LastHyperlink = hyperlink;
        }
    }
}
''',
)

print("F010 source and tests generated.")
