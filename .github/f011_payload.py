from pathlib import Path
import re

repo = Path('.')
formulas = repo / 'src/NeraSpreadSheet.Formulas'
tests = repo / 'tests/NeraSpreadSheet.Formulas.Tests'


def replace_once(path: Path, old: str, new: str) -> None:
    text = path.read_text(encoding='utf-8')
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f'{path}: expected one occurrence, found {count}')
    path.write_text(text.replace(old, new), encoding='utf-8')


if not (formulas / 'FormulaWorkbookMetadataContext.cs').exists():
    (formulas / 'FormulaWorkbookMetadataContext.cs').write_text('''using NeraSpreadSheet.Core;\n\nnamespace NeraSpreadSheet.Formulas;\n\n/// <summary>\n/// Exposes deterministic workbook worksheet metadata to reference functions.\n/// Sheet indexes are one-based and follow workbook order.\n/// </summary>\npublic interface IFormulaWorkbookMetadataContext\n    : IFormulaEvaluationContext\n{\n    int CurrentWorksheetIndex { get; }\n\n    int WorksheetCount { get; }\n\n    bool TryGetWorksheetIndex(\n        string? worksheetName,\n        out int oneBasedIndex);\n}\n''', encoding='utf-8')

if not (formulas / 'AdvancedReferenceFormulaEngine.cs').exists():
    (formulas / 'AdvancedReferenceFormulaEngine.cs').write_text(r'''using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static class AdvancedReferenceFormulaEvaluation
{
    public const int MaximumReferenceDepth = 64;

    public static bool TryResolve(
        FormulaNode node,
        Func<FormulaNode, CellValue> evaluateScalar,
        IFormulaEvaluationContext context,
        out FormulaReferenceTarget target,
        out CellValue error,
        int depth = 0)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(evaluateScalar);
        ArgumentNullException.ThrowIfNull(context);
        if (depth >= MaximumReferenceDepth)
        {
            target = default;
            error = CellValue.FromError("#REF!");
            return false;
        }
        switch (node)
        {
            case CellNode cell:
                target = new FormulaReferenceTarget(
                    cell.WorksheetName,
                    new CellRange(cell.Address, cell.Address));
                error = default;
                return true;
            case RangeNode range:
                target = new FormulaReferenceTarget(
                    range.WorksheetName,
                    range.Range);
                error = default;
                return true;
            case FunctionNode function when string.Equals(
                function.Name, "CHOOSE", StringComparison.OrdinalIgnoreCase):
                if (!ReferenceSelectionFormulaEvaluation.TrySelectChooseNode(
                    function, evaluateScalar, out var selected, out error))
                {
                    target = default;
                    return false;
                }
                return TryResolve(selected, evaluateScalar, context,
                    out target, out error, depth + 1);
            case FunctionNode function when string.Equals(
                function.Name, "INDIRECT", StringComparison.OrdinalIgnoreCase):
                return IndirectFormulaEvaluation.TryResolve(
                    function, evaluateScalar, context, out target, out error);
            case FunctionNode function when string.Equals(
                function.Name, "OFFSET", StringComparison.OrdinalIgnoreCase):
                return TryResolveOffset(function, evaluateScalar, context,
                    out target, out error, depth + 1);
            default:
                target = default;
                error = CellValue.FromError("#VALUE!");
                return false;
        }
    }

    private static bool TryResolveOffset(
        FunctionNode function,
        Func<FormulaNode, CellValue> evaluateScalar,
        IFormulaEvaluationContext context,
        out FormulaReferenceTarget target,
        out CellValue error,
        int depth)
    {
        target = default;
        if (function.Arguments.Count is < 3 or > 5)
        {
            error = CellValue.FromError("#VALUE!");
            return false;
        }
        if (!TryResolve(function.Arguments[0], evaluateScalar, context,
                out var source, out error, depth))
        {
            return false;
        }
        if (!TryReadInteger(function.Arguments[1], evaluateScalar,
                out var rowOffset, out error) ||
            !TryReadInteger(function.Arguments[2], evaluateScalar,
                out var columnOffset, out error))
        {
            return false;
        }
        var height = source.Range.RowCount;
        if (function.Arguments.Count >= 4 &&
            function.Arguments[3] is not MissingArgumentNode)
        {
            var value = evaluateScalar(function.Arguments[3]);
            if (value.Kind != CellValueKind.Blank &&
                !TryReadPositiveDimension(value, out height, out error))
            {
                return false;
            }
        }
        var width = source.Range.ColumnCount;
        if (function.Arguments.Count == 5 &&
            function.Arguments[4] is not MissingArgumentNode)
        {
            var value = evaluateScalar(function.Arguments[4]);
            if (value.Kind != CellValueKind.Blank &&
                !TryReadPositiveDimension(value, out width, out error))
            {
                return false;
            }
        }
        var top = (long)source.Range.Top + rowOffset;
        var left = (long)source.Range.Left + columnOffset;
        var bottom = top + height - 1L;
        var right = left + width - 1L;
        if (top < 0 || left < 0 ||
            bottom >= SpreadsheetLimits.MaxRows ||
            right >= SpreadsheetLimits.MaxColumns)
        {
            error = CellValue.FromError("#REF!");
            return false;
        }
        target = new FormulaReferenceTarget(
            source.WorksheetName,
            new CellRange(new CellAddress((int)top, (int)left),
                new CellAddress((int)bottom, (int)right)));
        error = default;
        return true;
    }

    private static bool TryReadInteger(
        FormulaNode node,
        Func<FormulaNode, CellValue> evaluateScalar,
        out int value,
        out CellValue error)
    {
        var scalar = evaluateScalar(node);
        if (scalar.Kind == CellValueKind.Error)
        {
            value = default;
            error = scalar;
            return false;
        }
        if (!FormulaValueCoercion.TryNumber(scalar, out var number,
                allowText: true) || !double.IsFinite(number))
        {
            value = default;
            error = CellValue.FromError("#VALUE!");
            return false;
        }
        var truncated = Math.Truncate(number);
        if (truncated < int.MinValue || truncated > int.MaxValue)
        {
            value = default;
            error = CellValue.FromError("#REF!");
            return false;
        }
        value = checked((int)truncated);
        error = default;
        return true;
    }

    private static bool TryReadPositiveDimension(
        CellValue scalar,
        out int value,
        out CellValue error)
    {
        if (scalar.Kind == CellValueKind.Error)
        {
            value = default;
            error = scalar;
            return false;
        }
        if (!FormulaValueCoercion.TryNumber(scalar, out var number,
                allowText: true) || !double.IsFinite(number))
        {
            value = default;
            error = CellValue.FromError("#VALUE!");
            return false;
        }
        var truncated = Math.Truncate(number);
        if (truncated < 1d || truncated > int.MaxValue)
        {
            value = default;
            error = CellValue.FromError("#REF!");
            return false;
        }
        value = checked((int)truncated);
        error = default;
        return true;
    }
}

public sealed partial class NeraFormulaEngine
{
    private CellValue EvaluateLookup(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count is < 2 or > 3)
        {
            return CellValue.FromError("#VALUE!");
        }
        var lookupValue = EvaluateNode(function.Arguments[0], context, dependencies);
        if (lookupValue.Kind == CellValueKind.Error)
        {
            return lookupValue;
        }
        if (!TryReadLookupVector(function.Arguments[1], context, dependencies,
                out var lookupVector, out var error))
        {
            return error;
        }
        var resultVector = lookupVector;
        if (function.Arguments.Count == 3 &&
            !TryReadLookupVector(function.Arguments[2], context, dependencies,
                out resultVector, out error))
        {
            return error;
        }
        if (resultVector.Length != lookupVector.Length)
        {
            return CellValue.FromError("#VALUE!");
        }
        var best = -1;
        for (var index = 0; index < lookupVector.Length; index++)
        {
            var candidate = lookupVector[index];
            if (candidate.Kind == CellValueKind.Error ||
                !TryCompareLookupValues(candidate, lookupValue,
                    out var comparison) || comparison > 0)
            {
                continue;
            }
            best = index;
        }
        return best < 0 ? CellValue.FromError("#N/A") : resultVector[best];
    }

    private CellValue EvaluateOffset(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (!AdvancedReferenceFormulaEvaluation.TryResolve(
                function, node => EvaluateNode(node, context, dependencies),
                context, out var target, out var error))
        {
            return error;
        }
        dependencies.Add(new FormulaDependency(target.WorksheetName, target.Range));
        return context.GetCellValue(target.WorksheetName, target.Range.TopLeft);
    }

    private CellValue EvaluateRow(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count == 0)
        {
            return context is IFormulaReferenceIntrospectionContext current
                ? CellValue.FromNumber(current.CurrentCellAddress.RowIndex + 1d)
                : CellValue.FromError("#VALUE!");
        }
        if (function.Arguments.Count != 1)
        {
            return CellValue.FromError("#VALUE!");
        }
        if (!TryResolveAdvancedReference(function.Arguments[0], context,
                dependencies, out var target, out var error))
        {
            return error.Kind == CellValueKind.Error
                ? error : CellValue.FromError("#VALUE!");
        }
        return CellValue.FromNumber(target.Range.Top + 1d);
    }

    private CellValue EvaluateRows(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count != 1)
        {
            return CellValue.FromError("#VALUE!");
        }
        if (TryResolveAdvancedReference(function.Arguments[0], context,
                dependencies, out var target, out _))
        {
            return CellValue.FromNumber(target.Range.RowCount);
        }
        if (function.Arguments[0] is ReferenceUnionNode)
        {
            return CellValue.FromError("#VALUE!");
        }
        var scalar = EvaluateNode(function.Arguments[0], context, dependencies);
        return scalar.Kind == CellValueKind.Error
            ? scalar : CellValue.FromNumber(1d);
    }

    private CellValue EvaluateSheet(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (context is not IFormulaWorkbookMetadataContext metadata ||
            function.Arguments.Count > 1)
        {
            return CellValue.FromError("#N/A");
        }
        if (function.Arguments.Count == 0)
        {
            return CellValue.FromNumber(metadata.CurrentWorksheetIndex);
        }
        if (TryResolveAdvancedReference(function.Arguments[0], context,
                dependencies, out var target, out _))
        {
            return metadata.TryGetWorksheetIndex(target.WorksheetName,
                    out var referenceIndex)
                ? CellValue.FromNumber(referenceIndex)
                : CellValue.FromError("#N/A");
        }
        var value = EvaluateNode(function.Arguments[0], context, dependencies);
        if (value.Kind == CellValueKind.Error)
        {
            return value;
        }
        return metadata.TryGetWorksheetIndex(FormulaValueCoercion.ToText(value),
                out var sheetIndex)
            ? CellValue.FromNumber(sheetIndex)
            : CellValue.FromError("#N/A");
    }

    private CellValue EvaluateSheets(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (context is not IFormulaWorkbookMetadataContext metadata ||
            function.Arguments.Count > 1)
        {
            return CellValue.FromError("#N/A");
        }
        if (function.Arguments.Count == 0)
        {
            return CellValue.FromNumber(metadata.WorksheetCount);
        }
        var indexes = new HashSet<int>();
        return TryCollectSheetIndexes(function.Arguments[0], context,
                dependencies, metadata, indexes, out var error)
            ? CellValue.FromNumber(indexes.Count) : error;
    }

    private bool TryEvaluateOffsetInvocationArgument(
        FormulaNode node,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        out FormulaFunctionArgument argument)
    {
        if (node is not FunctionNode function || !string.Equals(
                function.Name, "OFFSET", StringComparison.OrdinalIgnoreCase))
        {
            argument = null!;
            return false;
        }
        if (!AdvancedReferenceFormulaEvaluation.TryResolve(function,
                candidate => EvaluateNode(candidate, context, dependencies),
                context, out var target, out var error))
        {
            argument = FormulaFunctionArgument.Scalar(error);
            return true;
        }
        var dependency = new FormulaDependency(target.WorksheetName, target.Range);
        dependencies.Add(dependency);
        var values = new List<CellValue>(
            checked(target.Range.RowCount * target.Range.ColumnCount));
        for (var row = target.Range.Top; row <= target.Range.Bottom; row++)
        {
            for (var column = target.Range.Left;
                 column <= target.Range.Right; column++)
            {
                values.Add(context.GetCellValue(target.WorksheetName,
                    new CellAddress(row, column)));
            }
        }
        argument = FormulaFunctionArgument.Range(dependency, values);
        return true;
    }

    private bool TryReadLookupVector(
        FormulaNode node,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        out CellValue[] values,
        out CellValue error)
    {
        if (TryResolveAdvancedReference(node, context, dependencies,
                out var target, out _))
        {
            if (target.Range.RowCount != 1 && target.Range.ColumnCount != 1)
            {
                values = [];
                error = CellValue.FromError("#VALUE!");
                return false;
            }
            dependencies.Add(new FormulaDependency(target.WorksheetName, target.Range));
            values = new CellValue[
                checked(target.Range.RowCount * target.Range.ColumnCount)];
            var index = 0;
            for (var row = target.Range.Top; row <= target.Range.Bottom; row++)
            {
                for (var column = target.Range.Left;
                     column <= target.Range.Right; column++)
                {
                    values[index++] = context.GetCellValue(target.WorksheetName,
                        new CellAddress(row, column));
                }
            }
            error = default;
            return true;
        }
        if (node is ReferenceUnionNode)
        {
            values = [];
            error = CellValue.FromError("#VALUE!");
            return false;
        }
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

    private bool TryResolveAdvancedReference(
        FormulaNode node,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        out FormulaReferenceTarget target,
        out CellValue error) =>
        AdvancedReferenceFormulaEvaluation.TryResolve(node,
            candidate => EvaluateNode(candidate, context, dependencies),
            context, out target, out error);

    private bool TryCollectSheetIndexes(
        FormulaNode node,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        IFormulaWorkbookMetadataContext metadata,
        ISet<int> indexes,
        out CellValue error)
    {
        if (node is ReferenceUnionNode union)
        {
            foreach (var area in union.Areas)
            {
                if (!TryCollectSheetIndexes(area, context, dependencies,
                        metadata, indexes, out error))
                {
                    return false;
                }
            }
            error = default;
            return true;
        }
        if (TryResolveAdvancedReference(node, context, dependencies,
                out var target, out _))
        {
            if (!metadata.TryGetWorksheetIndex(target.WorksheetName, out var index))
            {
                error = CellValue.FromError("#N/A");
                return false;
            }
            indexes.Add(index);
            error = default;
            return true;
        }
        var value = EvaluateNode(node, context, dependencies);
        if (value.Kind == CellValueKind.Error)
        {
            error = value;
            return false;
        }
        if (!metadata.TryGetWorksheetIndex(FormulaValueCoercion.ToText(value),
                out var textIndex))
        {
            error = CellValue.FromError("#N/A");
            return false;
        }
        indexes.Add(textIndex);
        error = default;
        return true;
    }

    private static bool TryCompareLookupValues(
        CellValue left, CellValue right, out int comparison)
    {
        if (left.Kind == CellValueKind.Text && right.Kind == CellValueKind.Text)
        {
            comparison = string.Compare((string)left.RawValue!,
                (string)right.RawValue!, StringComparison.OrdinalIgnoreCase);
            return true;
        }
        if (left.Kind == CellValueKind.Boolean &&
            right.Kind == CellValueKind.Boolean)
        {
            comparison = ((bool)left.RawValue!).CompareTo((bool)right.RawValue!);
            return true;
        }
        if (left.Kind != CellValueKind.Text && right.Kind != CellValueKind.Text &&
            FormulaValueCoercion.TryNumber(left, out var leftNumber) &&
            FormulaValueCoercion.TryNumber(right, out var rightNumber))
        {
            comparison = leftNumber.CompareTo(rightNumber);
            return true;
        }
        comparison = default;
        return false;
    }
}
''', encoding='utf-8')

if not (formulas / 'AdvancedArrayProjectionFormulaFunctions.cs').exists():
    (formulas / 'AdvancedArrayProjectionFormulaFunctions.cs').write_text(r'''using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

public sealed partial class NeraDynamicArrayFormulaEngine
{
    private FormulaArrayEvaluationResult EvaluateOffsetArray(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (!AdvancedReferenceFormulaEvaluation.TryResolve(function,
                node => EvaluateScalarNode(node, context, dependencies),
                context, out var target, out var error))
        {
            return FormulaArrayEvaluationResult.Failure(error,
                ToErrorCode(error), DistinctDependencies(dependencies));
        }
        dependencies.Add(new FormulaDependency(target.WorksheetName, target.Range));
        return FormulaArrayEvaluationResult.Success(
            FormulaArrayValue.Create(target.Range.RowCount,
                target.Range.ColumnCount,
                (row, column) => context.GetCellValue(target.WorksheetName,
                    new CellAddress(target.Range.Top + row,
                        target.Range.Left + column))),
            DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateRowArray(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count == 0)
        {
            if (context is not IFormulaReferenceIntrospectionContext current)
            {
                return Failure("#VALUE!", FormulaErrorCode.InvalidValue,
                    dependencies);
            }
            return FormulaArrayEvaluationResult.Success(
                new FormulaArrayValue(1, 1,
                    [CellValue.FromNumber(current.CurrentCellAddress.RowIndex + 1d)]),
                DistinctDependencies(dependencies));
        }
        if (function.Arguments.Count != 1)
        {
            return Failure("#VALUE!", FormulaErrorCode.InvalidValue, dependencies);
        }
        if (!AdvancedReferenceFormulaEvaluation.TryResolve(
                function.Arguments[0],
                node => EvaluateScalarNode(node, context, dependencies),
                context, out var target, out var error))
        {
            return FormulaArrayEvaluationResult.Failure(
                error.Kind == CellValueKind.Error
                    ? error : CellValue.FromError("#VALUE!"),
                FormulaErrorCode.InvalidValue,
                DistinctDependencies(dependencies));
        }
        return FormulaArrayEvaluationResult.Success(
            FormulaArrayValue.Create(target.Range.RowCount, 1,
                (row, _) => CellValue.FromNumber(target.Range.Top + row + 1d)),
            DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateRowsArray(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count != 1)
        {
            return Failure("#VALUE!", FormulaErrorCode.InvalidValue, dependencies);
        }
        if (AdvancedReferenceFormulaEvaluation.TryResolve(
                function.Arguments[0],
                node => EvaluateScalarNode(node, context, dependencies),
                context, out var target, out _))
        {
            return FormulaArrayEvaluationResult.Success(
                new FormulaArrayValue(1, 1,
                    [CellValue.FromNumber(target.Range.RowCount)]),
                DistinctDependencies(dependencies));
        }
        if (function.Arguments[0] is ReferenceUnionNode)
        {
            return Failure("#VALUE!", FormulaErrorCode.InvalidValue, dependencies);
        }
        var source = EvaluateNodeAsArray(function.Arguments[0], context, dependencies);
        return source.IsSuccess
            ? FormulaArrayEvaluationResult.Success(
                new FormulaArrayValue(1, 1,
                    [CellValue.FromNumber(source.Value!.RowCount)]),
                DistinctDependencies(dependencies))
            : source;
    }

    private FormulaArrayEvaluationResult EvaluateSortBy(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count is < 2 or > 253)
        {
            return Failure("#VALUE!", FormulaErrorCode.InvalidValue, dependencies);
        }
        var sourceResult = EvaluateNodeAsArray(
            function.Arguments[0], context, dependencies);
        if (!sourceResult.IsSuccess)
        {
            return sourceResult;
        }
        var source = sourceResult.Value!;
        var keys = new List<SortByKey>();
        bool? sortRows = null;
        var argumentIndex = 1;
        while (argumentIndex < function.Arguments.Count)
        {
            var keyResult = EvaluateNodeAsArray(
                function.Arguments[argumentIndex++], context, dependencies);
            if (!keyResult.IsSuccess)
            {
                return keyResult;
            }
            var keyArray = keyResult.Value!;
            var keySortRows = keyArray.Count == source.RowCount;
            var keySortColumns = keyArray.Count == source.ColumnCount;
            if (!keySortRows && !keySortColumns)
            {
                return Failure("#VALUE!", FormulaErrorCode.InvalidValue,
                    dependencies);
            }
            var effectiveSortRows = sortRows ?? keySortRows;
            if (sortRows is null && keySortRows && keySortColumns)
            {
                effectiveSortRows = true;
            }
            if (effectiveSortRows && !keySortRows ||
                !effectiveSortRows && !keySortColumns)
            {
                return Failure("#VALUE!", FormulaErrorCode.InvalidValue,
                    dependencies);
            }
            sortRows = effectiveSortRows;
            var order = 1;
            if (argumentIndex < function.Arguments.Count)
            {
                var orderNode = function.Arguments[argumentIndex];
                var lastKeyWithoutOrder =
                    argumentIndex == function.Arguments.Count - 1 &&
                    orderNode is RangeNode or FunctionNode;
                if (!lastKeyWithoutOrder)
                {
                    argumentIndex++;
                    if (orderNode is not MissingArgumentNode)
                    {
                        var orderValue = EvaluateScalarNode(
                            orderNode, context, dependencies);
                        if (!FormulaValueCoercion.TryNumber(orderValue,
                                out var orderNumber, allowText: true) ||
                            !double.IsFinite(orderNumber) ||
                            Math.Truncate(orderNumber) != 1d &&
                            Math.Truncate(orderNumber) != -1d)
                        {
                            return Failure("#VALUE!",
                                FormulaErrorCode.InvalidValue, dependencies);
                        }
                        order = checked((int)Math.Truncate(orderNumber));
                    }
                }
            }
            keys.Add(new SortByKey(keyArray.ToArray(), order));
        }
        var dimension = sortRows == true ? source.RowCount : source.ColumnCount;
        var indexes = Enumerable.Range(0, dimension).ToArray();
        Array.Sort(indexes, new SortByIndexComparer(keys));
        return FormulaArrayEvaluationResult.Success(
            FormulaArrayValue.Create(source.RowCount, source.ColumnCount,
                (row, column) => sortRows == true
                    ? source[indexes[row], column]
                    : source[row, indexes[column]]),
            DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateTake(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count is < 2 or > 3)
        {
            return Failure("#VALUE!", FormulaErrorCode.InvalidValue, dependencies);
        }
        var sourceResult = EvaluateNodeAsArray(
            function.Arguments[0], context, dependencies);
        if (!sourceResult.IsSuccess)
        {
            return sourceResult;
        }
        var source = sourceResult.Value!;
        var rows = source.RowCount;
        FormulaArrayEvaluationResult? error = null;
        if (function.Arguments[1] is not MissingArgumentNode &&
            !TryEvaluateShapeInteger(function.Arguments[1], context,
                dependencies, out rows, out error))
        {
            return error!;
        }
        var columns = source.ColumnCount;
        if (function.Arguments.Count == 3 &&
            function.Arguments[2] is not MissingArgumentNode &&
            !TryEvaluateShapeInteger(function.Arguments[2], context,
                dependencies, out columns, out error))
        {
            return error!;
        }
        if (rows == 0 || columns == 0)
        {
            return Failure("#CALC!", FormulaErrorCode.InvalidValue, dependencies);
        }
        var outputRows = checked((int)Math.Min(
            Math.Abs((long)rows), source.RowCount));
        var outputColumns = checked((int)Math.Min(
            Math.Abs((long)columns), source.ColumnCount));
        var rowOffset = rows < 0 ? source.RowCount - outputRows : 0;
        var columnOffset = columns < 0
            ? source.ColumnCount - outputColumns : 0;
        return FormulaArrayEvaluationResult.Success(
            FormulaArrayValue.Create(outputRows, outputColumns,
                (row, column) => source[rowOffset + row, columnOffset + column]),
            DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateToColumn(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies) =>
        EvaluateToVector(function, context, dependencies, asColumn: true);

    private FormulaArrayEvaluationResult EvaluateToRow(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies) =>
        EvaluateToVector(function, context, dependencies, asColumn: false);

    private FormulaArrayEvaluationResult EvaluateToVector(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        bool asColumn)
    {
        if (function.Arguments.Count is < 1 or > 3)
        {
            return Failure("#VALUE!", FormulaErrorCode.InvalidValue, dependencies);
        }
        var sourceResult = EvaluateNodeAsArray(
            function.Arguments[0], context, dependencies);
        if (!sourceResult.IsSuccess)
        {
            return sourceResult;
        }
        var source = sourceResult.Value!;
        var ignore = 0;
        if (function.Arguments.Count >= 2 &&
            function.Arguments[1] is not MissingArgumentNode &&
            !TryEvaluateShapeInteger(function.Arguments[1], context,
                dependencies, out ignore, out var error))
        {
            return error!;
        }
        if (ignore is < 0 or > 3)
        {
            return Failure("#VALUE!", FormulaErrorCode.InvalidValue, dependencies);
        }
        var scanByColumn = false;
        if (function.Arguments.Count == 3 &&
            function.Arguments[2] is not MissingArgumentNode)
        {
            var scan = EvaluateScalarNode(
                function.Arguments[2], context, dependencies);
            if (!FormulaValueCoercion.TryBoolean(scan,
                    out scanByColumn, allowText: true))
            {
                return Failure("#VALUE!", FormulaErrorCode.InvalidValue,
                    dependencies);
            }
        }
        var values = new List<CellValue>(source.Count);
        if (scanByColumn)
        {
            for (var column = 0; column < source.ColumnCount; column++)
            {
                for (var row = 0; row < source.RowCount; row++)
                {
                    AppendVectorValue(values, source[row, column], ignore);
                }
            }
        }
        else
        {
            for (var row = 0; row < source.RowCount; row++)
            {
                for (var column = 0; column < source.ColumnCount; column++)
                {
                    AppendVectorValue(values, source[row, column], ignore);
                }
            }
        }
        if (values.Count == 0)
        {
            return Failure("#CALC!", FormulaErrorCode.InvalidValue, dependencies);
        }
        return FormulaArrayEvaluationResult.Success(
            new FormulaArrayValue(asColumn ? values.Count : 1,
                asColumn ? 1 : values.Count, values),
            DistinctDependencies(dependencies));
    }

    private static void AppendVectorValue(
        ICollection<CellValue> values, CellValue value, int ignore)
    {
        if ((ignore & 1) != 0 && value.IsBlank ||
            (ignore & 2) != 0 && value.Kind == CellValueKind.Error)
        {
            return;
        }
        values.Add(value);
    }

    private sealed record SortByKey(CellValue[] Values, int Order);

    private sealed class SortByIndexComparer(
        IReadOnlyList<SortByKey> keys) : IComparer<int>
    {
        public int Compare(int left, int right)
        {
            foreach (var key in keys)
            {
                var comparison = CompareCells(
                    key.Values[left], key.Values[right]);
                if (comparison != 0)
                {
                    return key.Order * comparison;
                }
            }
            return left.CompareTo(right);
        }

        private static int CompareCells(CellValue left, CellValue right)
        {
            if (left.IsBlank || right.IsBlank)
            {
                return left.IsBlank == right.IsBlank
                    ? 0 : left.IsBlank ? 1 : -1;
            }
            if (left.Kind == CellValueKind.Error ||
                right.Kind == CellValueKind.Error)
            {
                if (left.Kind != CellValueKind.Error) return -1;
                if (right.Kind != CellValueKind.Error) return 1;
                return string.Compare(left.ToString(), right.ToString(),
                    StringComparison.OrdinalIgnoreCase);
            }
            if (left.Kind != CellValueKind.Text &&
                right.Kind != CellValueKind.Text &&
                FormulaValueCoercion.TryNumber(left, out var leftNumber) &&
                FormulaValueCoercion.TryNumber(right, out var rightNumber))
            {
                return leftNumber.CompareTo(rightNumber);
            }
            return string.Compare(FormulaValueCoercion.ToText(left),
                FormulaValueCoercion.ToText(right),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
''', encoding='utf-8')

# Existing engine/context modifications are applied only once.
wb = formulas / 'WorkbookCalculationEngine.cs'
text = wb.read_text(encoding='utf-8')
if 'IFormulaWorkbookMetadataContext' not in text:
    replace_once(wb,
        '          IFormulaReferenceIntrospectionContext\n',
        '          IFormulaReferenceIntrospectionContext,\n          IFormulaWorkbookMetadataContext\n')
    replace_once(wb,
        '        public CellAddress CurrentCellAddress => _currentAddress;\n\n',
        '''        public CellAddress CurrentCellAddress => _currentAddress;\n\n        public int CurrentWorksheetIndex\n        {\n            get\n            {\n                for (var index = 0; index < _workbook.Worksheets.Count; index++)\n                {\n                    if (ReferenceEquals(_workbook.Worksheets[index], _currentWorksheet))\n                    {\n                        return index + 1;\n                    }\n                }\n                throw new InvalidOperationException(\n                    "The current worksheet is not in its workbook.");\n            }\n        }\n\n        public int WorksheetCount => _workbook.Worksheets.Count;\n\n        public bool TryGetWorksheetIndex(\n            string? worksheetName, out int oneBasedIndex)\n        {\n            var name = worksheetName ?? _currentWorksheet.Name;\n            for (var index = 0; index < _workbook.Worksheets.Count; index++)\n            {\n                if (string.Equals(_workbook.Worksheets[index].Name, name,\n                        StringComparison.OrdinalIgnoreCase))\n                {\n                    oneBasedIndex = index + 1;\n                    return true;\n                }\n            }\n            oneBasedIndex = default;\n            return false;\n        }\n\n''')

dwb = formulas / 'DynamicArrayWorkbookCalculationEngine.cs'
text = dwb.read_text(encoding='utf-8')
if 'IFormulaWorkbookMetadataContext' not in text:
    replace_once(dwb,
        '        IFormulaReferenceIntrospectionContext\n',
        '        IFormulaReferenceIntrospectionContext,\n        IFormulaWorkbookMetadataContext\n')
    replace_once(dwb,
        '        public CellAddress CurrentCellAddress => _formulaAddress;\n\n',
        '''        public CellAddress CurrentCellAddress => _formulaAddress;\n\n        public int CurrentWorksheetIndex\n        {\n            get\n            {\n                for (var index = 0; index < _workbook.Worksheets.Count; index++)\n                {\n                    if (ReferenceEquals(_workbook.Worksheets[index], _currentWorksheet))\n                    {\n                        return index + 1;\n                    }\n                }\n                throw new InvalidOperationException(\n                    "The current worksheet is not in its workbook.");\n            }\n        }\n\n        public int WorksheetCount => _workbook.Worksheets.Count;\n\n        public bool TryGetWorksheetIndex(\n            string? worksheetName, out int oneBasedIndex)\n        {\n            var name = worksheetName ?? _currentWorksheet.Name;\n            for (var index = 0; index < _workbook.Worksheets.Count; index++)\n            {\n                if (string.Equals(_workbook.Worksheets[index].Name, name,\n                        StringComparison.OrdinalIgnoreCase))\n                {\n                    oneBasedIndex = index + 1;\n                    return true;\n                }\n            }\n            oneBasedIndex = default;\n            return false;\n        }\n\n''')

engine = formulas / 'NeraFormulaEngine.cs'
text = engine.read_text(encoding='utf-8')
if '"LOOKUP"' not in text:
    replace_once(engine,
        '        if (string.Equals(\n                function.Name,\n                "GETPIVOTDATA",\n',
        '''        if (string.Equals(\n                function.Name,\n                "LOOKUP",\n                StringComparison.OrdinalIgnoreCase))\n        {\n            return EvaluateLookup(function, context, dependencies);\n        }\n        if (string.Equals(\n                function.Name,\n                "OFFSET",\n                StringComparison.OrdinalIgnoreCase))\n        {\n            return EvaluateOffset(function, context, dependencies);\n        }\n        if (string.Equals(\n                function.Name,\n                "ROW",\n                StringComparison.OrdinalIgnoreCase))\n        {\n            return EvaluateRow(function, context, dependencies);\n        }\n        if (string.Equals(\n                function.Name,\n                "ROWS",\n                StringComparison.OrdinalIgnoreCase))\n        {\n            return EvaluateRows(function, context, dependencies);\n        }\n        if (string.Equals(\n                function.Name,\n                "SHEET",\n                StringComparison.OrdinalIgnoreCase))\n        {\n            return EvaluateSheet(function, context, dependencies);\n        }\n        if (string.Equals(\n                function.Name,\n                "SHEETS",\n                StringComparison.OrdinalIgnoreCase))\n        {\n            return EvaluateSheets(function, context, dependencies);\n        }\n        if (string.Equals(\n                function.Name,\n                "GETPIVOTDATA",\n''')
    replace_once(engine,
        '        foreach (var argumentNode in function.Arguments)\n        {\n            if (TryEvaluateIndirectInvocationArgument(\n',
        '''        foreach (var argumentNode in function.Arguments)\n        {\n            if (TryEvaluateOffsetInvocationArgument(\n                    argumentNode, context, dependencies,\n                    out var offsetArgument))\n            {\n                invocationArguments.Add(offsetArgument);\n                continue;\n            }\n            if (TryEvaluateIndirectInvocationArgument(\n''')

dyn = formulas / 'DynamicArrayFormulaEngine.cs'
text = dyn.read_text(encoding='utf-8')
if '"SORTBY"' not in text:
    replace_once(dyn,
        '        if (string.Equals(\n                function.Name,\n                "GROUPBY",\n',
        '''        if (string.Equals(\n                function.Name,\n                "OFFSET",\n                StringComparison.OrdinalIgnoreCase))\n        {\n            return EvaluateOffsetArray(function, context, dependencies);\n        }\n        if (string.Equals(\n                function.Name,\n                "ROW",\n                StringComparison.OrdinalIgnoreCase))\n        {\n            return EvaluateRowArray(function, context, dependencies);\n        }\n        if (string.Equals(\n                function.Name,\n                "ROWS",\n                StringComparison.OrdinalIgnoreCase))\n        {\n            return EvaluateRowsArray(function, context, dependencies);\n        }\n        if (string.Equals(\n                function.Name,\n                "SORTBY",\n                StringComparison.OrdinalIgnoreCase))\n        {\n            return EvaluateSortBy(function, context, dependencies);\n        }\n        if (string.Equals(\n                function.Name,\n                "TAKE",\n                StringComparison.OrdinalIgnoreCase))\n        {\n            return EvaluateTake(function, context, dependencies);\n        }\n        if (string.Equals(\n                function.Name,\n                "TOCOL",\n                StringComparison.OrdinalIgnoreCase))\n        {\n            return EvaluateToColumn(function, context, dependencies);\n        }\n        if (string.Equals(\n                function.Name,\n                "TOROW",\n                StringComparison.OrdinalIgnoreCase))\n        {\n            return EvaluateToRow(function, context, dependencies);\n        }\n        if (string.Equals(\n                function.Name,\n                "GROUPBY",\n''')
    replace_once(dyn,
        '        string.Equals(\n            name,\n            "GROUPBY",\n',
        '''        string.Equals(\n            name,\n            "OFFSET",\n            StringComparison.OrdinalIgnoreCase) ||\n        string.Equals(\n            name,\n            "ROW",\n            StringComparison.OrdinalIgnoreCase) ||\n        string.Equals(\n            name,\n            "ROWS",\n            StringComparison.OrdinalIgnoreCase) ||\n        string.Equals(\n            name,\n            "SORTBY",\n            StringComparison.OrdinalIgnoreCase) ||\n        string.Equals(\n            name,\n            "TAKE",\n            StringComparison.OrdinalIgnoreCase) ||\n        string.Equals(\n            name,\n            "TOCOL",\n            StringComparison.OrdinalIgnoreCase) ||\n        string.Equals(\n            name,\n            "TOROW",\n            StringComparison.OrdinalIgnoreCase) ||\n        string.Equals(\n            name,\n            "GROUPBY",\n''')

if not (tests / 'CorrectiveReferenceAndProjectionFormulaFunctionTests.cs').exists():
    (tests / 'CorrectiveReferenceAndProjectionFormulaFunctionTests.cs').write_text(r'''using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class CorrectiveReferenceAndProjectionFormulaFunctionTests
{
    [TestMethod]
    public void LookupReturnsApproximateResult()
    {
        var values = Grid();
        SetColumn(values, 0, [1d, 3d, 5d, 7d]);
        SetColumn(values, 1, [10d, 30d, 50d, 70d]);
        var result = new NeraFormulaEngine().Evaluate(
            "=LOOKUP(4,A1:A4,B1:B4)", new Context(values));
        Assert.AreEqual(30d, Number(result.Value), 1e-12d);
        Assert.AreEqual(2, result.Dependencies.Count);
    }

    [TestMethod]
    public void OffsetPreservesIdentityAndSpills()
    {
        var values = Grid();
        SetGrid(values, 1, 1, 2, 2, 1d);
        var context = new Context(values);
        var scalar = new NeraFormulaEngine();
        Assert.AreEqual(10d, Number(scalar.Evaluate(
            "=SUM(OFFSET(A1,1,1,2,2))", context).Value), 1e-12d);
        var arrays = new NeraDynamicArrayFormulaEngine(scalar);
        Assert.IsTrue(arrays.TryEvaluate("=OFFSET(A1,1,1,2,2)",
            context, out var result));
        AssertArray(result.Value!, 2, 2, 1d, 2d, 3d, 4d);
    }

    [TestMethod]
    public void RowUsesCurrentCellAndReferenceRows()
    {
        var context = new Context(currentAddress: new CellAddress(5, 4));
        var scalar = new NeraFormulaEngine();
        Assert.AreEqual(6d, Number(scalar.Evaluate("=ROW()", context).Value));
        var arrays = new NeraDynamicArrayFormulaEngine(scalar);
        Assert.IsTrue(arrays.TryEvaluate("=ROW(B3:B5)", context, out var result));
        AssertArray(result.Value!, 3, 1, 3d, 4d, 5d);
    }

    [TestMethod]
    public void RowsReadsReferenceAndArrayShape()
    {
        var context = new Context();
        var scalar = new NeraFormulaEngine();
        Assert.AreEqual(3d, Number(scalar.Evaluate(
            "=ROWS(B3:C5)", context).Value));
        var arrays = new NeraDynamicArrayFormulaEngine(scalar);
        Assert.IsTrue(arrays.TryEvaluate("=ROWS(SEQUENCE(4,2))",
            context, out var result));
        AssertArray(result.Value!, 1, 1, 4d);
    }

    [TestMethod]
    public void SheetReturnsOneBasedIndexes()
    {
        var context = new Context(currentWorksheet: "Summary",
            worksheetNames: ["Sheet1", "Data", "Summary"]);
        var engine = new NeraFormulaEngine();
        Assert.AreEqual(3d, Number(engine.Evaluate("=SHEET()", context).Value));
        Assert.AreEqual(2d, Number(engine.Evaluate(
            "=SHEET(Data!A1)", context).Value));
    }

    [TestMethod]
    public void SheetsCountsWorkbookAndReferencedSheets()
    {
        var context = new Context(
            worksheetNames: ["Sheet1", "Data", "Summary"]);
        var engine = new NeraFormulaEngine();
        Assert.AreEqual(3d, Number(engine.Evaluate("=SHEETS()", context).Value));
        Assert.AreEqual(2d, Number(engine.Evaluate(
            "=SHEETS((Sheet1!A1,Data!A1))", context).Value));
    }

    [TestMethod]
    public void SortByUsesStableMultipleKeys()
    {
        var values = Grid();
        SetColumn(values, 0, [10d, 20d, 30d, 40d]);
        SetColumn(values, 1, [1d, 1d, 2d, 2d]);
        SetColumn(values, 2, [1d, 2d, 2d, 1d]);
        var engine = new NeraDynamicArrayFormulaEngine();
        Assert.IsTrue(engine.TryEvaluate(
            "=SORTBY(A1:A4,B1:B4,1,C1:C4,-1)",
            new Context(values), out var result));
        AssertArray(result.Value!, 4, 1, 20d, 10d, 30d, 40d);
    }

    [TestMethod]
    public void TakeSupportsNegativeAndClampedDimensions()
    {
        var values = Grid();
        SetGrid(values, 0, 0, 4, 3, 1d);
        var engine = new NeraDynamicArrayFormulaEngine();
        Assert.IsTrue(engine.TryEvaluate("=TAKE(A1:C4,-2,2)",
            new Context(values), out var result));
        AssertArray(result.Value!, 2, 2, 7d, 8d, 10d, 11d);
        Assert.IsTrue(engine.TryEvaluate("=TAKE(A1:C4,10,-1)",
            new Context(values), out var clamped));
        AssertArray(clamped.Value!, 4, 1, 3d, 6d, 9d, 12d);
    }

    [TestMethod]
    public void ToColHonorsIgnoreAndScanFlags()
    {
        var values = Grid();
        values[("Sheet1", new CellAddress(0, 0))] = CellValue.FromNumber(1d);
        values[("Sheet1", new CellAddress(2, 0))] = CellValue.FromNumber(3d);
        values[("Sheet1", new CellAddress(0, 1))] = CellValue.FromError("#N/A");
        values[("Sheet1", new CellAddress(1, 1))] = CellValue.FromNumber(5d);
        values[("Sheet1", new CellAddress(2, 1))] = CellValue.FromNumber(6d);
        var engine = new NeraDynamicArrayFormulaEngine();
        Assert.IsTrue(engine.TryEvaluate("=TOCOL(A1:B3,3,TRUE)",
            new Context(values), out var result));
        AssertArray(result.Value!, 4, 1, 1d, 3d, 5d, 6d);
    }

    [TestMethod]
    public void ToRowUsesRowMajorOrder()
    {
        var values = Grid();
        SetGrid(values, 0, 0, 2, 2, 1d);
        var engine = new NeraDynamicArrayFormulaEngine();
        Assert.IsTrue(engine.TryEvaluate("=TOROW(A1:B2)",
            new Context(values), out var result));
        AssertArray(result.Value!, 1, 4, 1d, 2d, 3d, 4d);
    }

    private static Dictionary<(string, CellAddress), CellValue> Grid() => [];

    private static void SetColumn(
        IDictionary<(string, CellAddress), CellValue> values,
        int column, IReadOnlyList<double> numbers)
    {
        for (var row = 0; row < numbers.Count; row++)
        {
            values[("Sheet1", new CellAddress(row, column))] =
                CellValue.FromNumber(numbers[row]);
        }
    }

    private static void SetGrid(
        IDictionary<(string, CellAddress), CellValue> values,
        int top, int left, int rows, int columns, double start)
    {
        var value = start;
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                values[("Sheet1", new CellAddress(
                    top + row, left + column))] = CellValue.FromNumber(value++);
            }
        }
    }

    private static double Number(CellValue value)
    {
        Assert.AreEqual(CellValueKind.Number, value.Kind, value.ToString());
        return (double)value.RawValue!;
    }

    private static void AssertArray(
        FormulaArrayValue value, int rows, int columns,
        params double[] expected)
    {
        Assert.AreEqual(rows, value.RowCount);
        Assert.AreEqual(columns, value.ColumnCount);
        var actual = value.ToArray();
        Assert.AreEqual(expected.Length, actual.Length);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.AreEqual(expected[index], Number(actual[index]), 1e-12d);
        }
    }

    private sealed class Context :
        IFormulaReferenceIntrospectionContext,
        IFormulaWorkbookMetadataContext
    {
        private readonly IReadOnlyDictionary<
            (string Worksheet, CellAddress Address), CellValue> _values;
        private readonly string[] _worksheets;

        public Context(
            IReadOnlyDictionary<(string, CellAddress), CellValue>? values = null,
            CellAddress? currentAddress = null,
            string currentWorksheet = "Sheet1",
            string[]? worksheetNames = null)
        {
            _values = values ?? Grid();
            CurrentCellAddress = currentAddress ?? new CellAddress(0, 0);
            CurrentWorksheetName = currentWorksheet;
            _worksheets = worksheetNames ?? ["Sheet1"];
        }

        public string CurrentWorksheetName { get; }
        public CellAddress CurrentCellAddress { get; }
        public int WorksheetCount => _worksheets.Length;
        public int CurrentWorksheetIndex
        {
            get
            {
                Assert.IsTrue(TryGetWorksheetIndex(CurrentWorksheetName,
                    out var index));
                return index;
            }
        }

        public CellValue GetCellValue(string? worksheetName, CellAddress address) =>
            _values.GetValueOrDefault(
                (worksheetName ?? CurrentWorksheetName, address), CellValue.Blank);

        public bool TryGetCellFormula(
            string? worksheetName, CellAddress address, out string? formula)
        {
            formula = null;
            return false;
        }

        public bool TryGetWorksheetIndex(
            string? worksheetName, out int oneBasedIndex)
        {
            var name = worksheetName ?? CurrentWorksheetName;
            for (var index = 0; index < _worksheets.Length; index++)
            {
                if (string.Equals(_worksheets[index], name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    oneBasedIndex = index + 1;
                    return true;
                }
            }
            oneBasedIndex = default;
            return false;
        }
    }
}
''', encoding='utf-8')

# Compact source-of-truth docs.
(repo / 'README.md').write_text('''# NeraSpreadSheet\n\nNeraSpreadSheet là SDK spreadsheet độc lập cho WPF, WinForms và .NET MAUI.\n\n- Built-ins: **286** = 240 eager/versioned + 31 AST/reference-aware + 15 dynamic-array.\n- Formula tests sau F011: **259/259**.\n- **Tổng số hàm: 286 / tối thiểu 538 hàm mục tiêu hiện đã khóa.**\n- F011: LOOKUP, OFFSET, ROW, ROWS, SHEET, SHEETS, SORTBY, TAKE, TOCOL, TOROW.\n- F012 tiếp theo: TRIMRANGE, VSTACK, WRAPCOLS, WRAPROWS, XMATCH, IFS, IFERROR, IFNA, SWITCH, XOR.\n\nPR #1 giữ Draft; chưa phải production release.\n''', encoding='utf-8')

(repo / 'docs/current-status.md').write_text('''# NeraSpreadSheet current implementation status\n\n| Chỉ số | Giá trị |\n|---|---:|\n| Eager/versioned | 240 |\n| AST/reference-aware | 31 |\n| Dynamic-array unique | 15 |\n| Tổng built-ins | **286** |\n| Formula tests | **259/259** |\n| Batch hoàn thành | F001–F011 |\n\n**Tổng số hàm: 286 / tối thiểu 538 hàm mục tiêu hiện đã khóa.**\n\nF010 thực tế có 5 hàm: GETPIVOTDATA, GROUPBY, HSTACK, HYPERLINK, INDIRECT.\nF011 có đúng 10 hàm: LOOKUP, OFFSET, ROW, ROWS, SHEET, SHEETS, SORTBY, TAKE, TOCOL, TOROW.\n\nF012 tiếp theo: TRIMRANGE, VSTACK, WRAPCOLS, WRAPROWS, XMATCH, IFS, IFERROR, IFNA, SWITCH, XOR.\n''', encoding='utf-8')

(repo / 'docs/formula-completion-master-schedule.md').write_text('''# Master Formula Completion Schedule\n\nTừ F011, mỗi batch công khai gồm đúng 10 hàm mới và cần exact-head CI xanh.\n\n- F009: COLUMN, COLUMNS, DROP, EXPAND, FORMULATEXT — complete.\n- F010: GETPIVOTDATA, GROUPBY, HSTACK, HYPERLINK, INDIRECT — complete, 5 hàm thực tế.\n- F011: LOOKUP, OFFSET, ROW, ROWS, SHEET, SHEETS, SORTBY, TAKE, TOCOL, TOROW — complete.\n- F012: TRIMRANGE, VSTACK, WRAPCOLS, WRAPROWS, XMATCH, IFS, IFERROR, IFNA, SWITCH, XOR — next.\n\n**Tổng số hàm: 286 / tối thiểu 538 hàm mục tiêu hiện đã khóa.** Catalog audit cuối có thể tăng mẫu số.\n''', encoding='utf-8')

(repo / 'docs/worklog/CURRENT.md').write_text('''# Current Work Handoff\n\n- Branch: feature/bootstrap-architecture-v0.1\n- PR #1: Draft, unmerged.\n- F011: LOOKUP, OFFSET, ROW, ROWS, SHEET, SHEETS, SORTBY, TAKE, TOCOL, TOROW.\n- Formula tests: 259/259.\n- Built-ins: 286 / minimum target 538+.\n- Next: F012 — TRIMRANGE, VSTACK, WRAPCOLS, WRAPROWS, XMATCH, IFS, IFERROR, IFNA, SWITCH, XOR.\n''', encoding='utf-8')

(repo / 'docs/worklog/F011_CORRECTIVE_REFERENCE_AND_PROJECTION.md').write_text('''# F011 — Corrective Reference and Projection\n\nExactly ten new public names: LOOKUP, OFFSET, ROW, ROWS, SHEET, SHEETS, SORTBY, TAKE, TOCOL, TOROW.\n\nContracts: reference identity, workbook metadata, stable multi-key sorting, negative/clamped slicing, vector scan/ignore flags, 64-level reference recursion and 1.000.000-cell array cap.\n\nFormula surface: 286 / minimum target 538+.\n''', encoding='utf-8')

for path in [repo / '.chatgpt-probe', repo / '.github/f011-mini.txt']:
    if path.exists():
        path.unlink()
