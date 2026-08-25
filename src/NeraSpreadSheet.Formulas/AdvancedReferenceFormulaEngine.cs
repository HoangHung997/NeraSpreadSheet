using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static class AdvancedReferenceFormulaEvaluation
{
    public static bool IsReferenceCandidate(FormulaNode node) =>
        node is CellNode or RangeNode ||
        node is FunctionNode function &&
        (string.Equals(
             function.Name,
             "CHOOSE",
             StringComparison.OrdinalIgnoreCase) ||
         IsReferenceFunction(function.Name));

    public static bool IsReferenceFunction(string name) =>
        string.Equals(
            name,
            "INDIRECT",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            name,
            "OFFSET",
            StringComparison.OrdinalIgnoreCase);

    public static bool TryResolve(
        FormulaNode node,
        Func<FormulaNode, CellValue> evaluateScalar,
        IFormulaEvaluationContext context,
        out FormulaReferenceTarget target,
        out CellValue error)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(evaluateScalar);
        ArgumentNullException.ThrowIfNull(context);

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
            case FunctionNode choose
                when string.Equals(
                    choose.Name,
                    "CHOOSE",
                    StringComparison.OrdinalIgnoreCase):
                if (!ReferenceSelectionFormulaEvaluation.TrySelectChooseNode(
                        choose,
                        evaluateScalar,
                        out var selected,
                        out error))
                {
                    target = default;
                    return false;
                }

                return TryResolve(
                    selected,
                    evaluateScalar,
                    context,
                    out target,
                    out error);
            case FunctionNode indirect
                when string.Equals(
                    indirect.Name,
                    "INDIRECT",
                    StringComparison.OrdinalIgnoreCase):
                return IndirectFormulaEvaluation.TryResolve(
                    indirect,
                    evaluateScalar,
                    context,
                    out target,
                    out error);
            case FunctionNode offset
                when string.Equals(
                    offset.Name,
                    "OFFSET",
                    StringComparison.OrdinalIgnoreCase):
                return TryResolveOffset(
                    offset,
                    evaluateScalar,
                    context,
                    out target,
                    out error);
            default:
                target = default;
                error = CellValue.FromError("#VALUE!");
                return false;
        }
    }

    public static bool TryResolveOffset(
        FunctionNode function,
        Func<FormulaNode, CellValue> evaluateScalar,
        IFormulaEvaluationContext context,
        out FormulaReferenceTarget target,
        out CellValue error)
    {
        if (function.Arguments.Count is < 3 or > 5)
        {
            target = default;
            error = CellValue.FromError("#VALUE!");
            return false;
        }
        if (!TryResolve(
                function.Arguments[0],
                evaluateScalar,
                context,
                out var source,
                out error) ||
            !TryReadTruncatedInteger(
                function.Arguments[1],
                evaluateScalar,
                out var rowOffset,
                out error) ||
            !TryReadTruncatedInteger(
                function.Arguments[2],
                evaluateScalar,
                out var columnOffset,
                out error))
        {
            target = default;
            return false;
        }

        var height = source.Range.RowCount;
        if (function.Arguments.Count >= 4 &&
            function.Arguments[3] is not MissingArgumentNode &&
            (!TryReadTruncatedInteger(
                    function.Arguments[3],
                    evaluateScalar,
                    out height,
                    out error) ||
             height <= 0))
        {
            target = default;
            error = error.Kind == CellValueKind.Error
                ? error
                : CellValue.FromError("#VALUE!");
            return false;
        }

        var width = source.Range.ColumnCount;
        if (function.Arguments.Count == 5 &&
            function.Arguments[4] is not MissingArgumentNode &&
            (!TryReadTruncatedInteger(
                    function.Arguments[4],
                    evaluateScalar,
                    out width,
                    out error) ||
             width <= 0))
        {
            target = default;
            error = error.Kind == CellValueKind.Error
                ? error
                : CellValue.FromError("#VALUE!");
            return false;
        }

        var top = (long)source.Range.Top + rowOffset;
        var left = (long)source.Range.Left + columnOffset;
        var bottom = top + height - 1L;
        var right = left + width - 1L;
        if (top < 0 ||
            left < 0 ||
            bottom >= SpreadsheetLimits.MaxRows ||
            right >= SpreadsheetLimits.MaxColumns)
        {
            target = default;
            error = CellValue.FromError("#REF!");
            return false;
        }

        target = new FormulaReferenceTarget(
            source.WorksheetName,
            new CellRange(
                new CellAddress((int)top, (int)left),
                new CellAddress((int)bottom, (int)right)));
        error = default;
        return true;
    }

    private static bool TryReadTruncatedInteger(
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
        if (!FormulaValueCoercion.TryNumber(
                scalar,
                out var number,
                allowText: true) ||
            !double.IsFinite(number))
        {
            value = default;
            error = CellValue.FromError("#VALUE!");
            return false;
        }

        var truncated = Math.Truncate(number);
        if (truncated < int.MinValue || truncated > int.MaxValue)
        {
            value = default;
            error = CellValue.FromError("#VALUE!");
            return false;
        }

        value = checked((int)truncated);
        error = default;
        return true;
    }
}

public sealed partial class NeraFormulaEngine
{
    private CellValue EvaluateOffset(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (!TryResolveAdvancedReference(
                function,
                context,
                dependencies,
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

    private CellValue EvaluateRow(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count == 0)
        {
            return context is IFormulaReferenceIntrospectionContext
                introspection
                ? CellValue.FromNumber(
                    introspection.CurrentCellAddress.RowIndex + 1d)
                : CellValue.FromError("#VALUE!");
        }
        if (function.Arguments.Count != 1)
        {
            return CellValue.FromError("#VALUE!");
        }
        if (!TryResolveAdvancedReference(
                function.Arguments[0],
                context,
                dependencies,
                out var target,
                out var error))
        {
            return error.Kind == CellValueKind.Error
                ? error
                : CellValue.FromError("#VALUE!");
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

        if (AdvancedReferenceFormulaEvaluation.TryResolve(
                function.Arguments[0],
                node => EvaluateNode(node, context, dependencies),
                context,
                out var target,
                out _))
        {
            return CellValue.FromNumber(target.Range.RowCount);
        }
        if (function.Arguments[0] is ReferenceUnionNode)
        {
            return CellValue.FromError("#VALUE!");
        }

        var scalar = EvaluateNode(
            function.Arguments[0],
            context,
            dependencies);
        return scalar.Kind == CellValueKind.Error
            ? scalar
            : CellValue.FromNumber(1d);
    }

    private CellValue EvaluateSheet(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count > 1 ||
            context is not IFormulaWorkbookMetadataEvaluationContext metadata)
        {
            return CellValue.FromError("#N/A");
        }

        string? worksheetName;
        if (function.Arguments.Count == 0 ||
            function.Arguments[0] is MissingArgumentNode)
        {
            worksheetName = null;
        }
        else if (AdvancedReferenceFormulaEvaluation.TryResolve(
                     function.Arguments[0],
                     node => EvaluateNode(node, context, dependencies),
                     context,
                     out var target,
                     out _))
        {
            worksheetName = target.WorksheetName;
        }
        else
        {
            var value = EvaluateNode(
                function.Arguments[0],
                context,
                dependencies);
            if (value.Kind == CellValueKind.Error)
            {
                return value;
            }
            worksheetName = FormulaValueCoercion.ToText(value);
            if (worksheetName.Length == 0)
            {
                return CellValue.FromError("#N/A");
            }
        }

        return metadata.TryGetWorksheetIndex(
                worksheetName,
                out var index)
            ? CellValue.FromNumber(index)
            : CellValue.FromError("#N/A");
    }

    private CellValue EvaluateSheets(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count > 1 ||
            context is not IFormulaWorkbookMetadataEvaluationContext metadata)
        {
            return CellValue.FromError("#VALUE!");
        }
        if (function.Arguments.Count == 0 ||
            function.Arguments[0] is MissingArgumentNode)
        {
            return CellValue.FromNumber(metadata.WorksheetCount);
        }

        var sheetIndexes = new HashSet<int>();
        if (TryCollectSheetIndexes(
                function.Arguments[0],
                context,
                dependencies,
                metadata,
                sheetIndexes,
                out var referenceError))
        {
            return CellValue.FromNumber(sheetIndexes.Count);
        }
        if (function.Arguments[0] is ReferenceUnionNode ||
            AdvancedReferenceFormulaEvaluation.IsReferenceCandidate(
                function.Arguments[0]))
        {
            return referenceError.Kind == CellValueKind.Error
                ? referenceError
                : CellValue.FromError("#REF!");
        }

        var value = EvaluateNode(
            function.Arguments[0],
            context,
            dependencies);
        if (value.Kind == CellValueKind.Error)
        {
            return value;
        }
        var worksheetName = FormulaValueCoercion.ToText(value);
        return worksheetName.Length > 0 &&
               metadata.TryGetWorksheetIndex(worksheetName, out _)
            ? CellValue.FromNumber(1d)
            : CellValue.FromError("#REF!");
    }

    private bool TryCollectSheetIndexes(
        FormulaNode node,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        IFormulaWorkbookMetadataEvaluationContext metadata,
        ISet<int> sheetIndexes,
        out CellValue error)
    {
        if (node is ReferenceUnionNode union)
        {
            foreach (var area in union.Areas)
            {
                if (!TryCollectSheetIndexes(
                        area,
                        context,
                        dependencies,
                        metadata,
                        sheetIndexes,
                        out error))
                {
                    return false;
                }
            }
            error = default;
            return true;
        }
        if (node is FunctionNode choose &&
            string.Equals(
                choose.Name,
                "CHOOSE",
                StringComparison.OrdinalIgnoreCase))
        {
            if (!TrySelectChooseNode(
                    choose,
                    context,
                    dependencies,
                    out var selected,
                    out error))
            {
                return false;
            }
            return TryCollectSheetIndexes(
                selected,
                context,
                dependencies,
                metadata,
                sheetIndexes,
                out error);
        }
        if (!TryResolveAdvancedReference(
                node,
                context,
                dependencies,
                out var target,
                out error) ||
            !metadata.TryGetWorksheetIndex(
                target.WorksheetName,
                out var oneBasedIndex))
        {
            if (error.Kind != CellValueKind.Error)
            {
                error = CellValue.FromError("#REF!");
            }
            return false;
        }

        sheetIndexes.Add(oneBasedIndex);
        error = default;
        return true;
    }

    private bool TryEvaluateAdvancedReferenceInvocationArgument(
        FormulaNode node,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        out FormulaFunctionArgument argument)
    {
        if (node is not FunctionNode function ||
            !string.Equals(
                function.Name,
                "OFFSET",
                StringComparison.OrdinalIgnoreCase))
        {
            argument = null!;
            return false;
        }

        if (!TryResolveAdvancedReference(
                function,
                context,
                dependencies,
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

    private bool TryResolveAdvancedReference(
        FormulaNode node,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        out FormulaReferenceTarget target,
        out CellValue error) =>
        AdvancedReferenceFormulaEvaluation.TryResolve(
            node,
            candidate => EvaluateNode(
                candidate,
                context,
                dependencies),
            context,
            out target,
            out error);
}
