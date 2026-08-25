using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

public sealed partial class NeraDynamicArrayFormulaEngine
{
    private FormulaArrayEvaluationResult EvaluateColumnArray(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count == 0)
        {
            if (context is not IFormulaReferenceIntrospectionContext
                introspection)
            {
                return Failure(
                    "#VALUE!",
                    FormulaErrorCode.InvalidValue,
                    dependencies);
            }

            return FormulaArrayEvaluationResult.Success(
                new FormulaArrayValue(
                    1,
                    1,
                    [CellValue.FromNumber(
                        introspection.CurrentCellAddress.ColumnIndex + 1d)]),
                DistinctDependencies(dependencies));
        }
        if (function.Arguments.Count != 1)
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }
        if (!ReferenceIntrospectionFormulaEvaluation.TryResolveReferenceNode(
                function.Arguments[0],
                node => EvaluateScalarNode(
                    node,
                    context,
                    dependencies),
                out var reference,
                out var error) ||
            !ReferenceIntrospectionFormulaEvaluation.TryGetRange(
                reference,
                out _,
                out var range))
        {
            return ReferenceError(error, dependencies);
        }

        return FormulaArrayEvaluationResult.Success(
            FormulaArrayValue.Create(
                1,
                range.ColumnCount,
                (_, column) => CellValue.FromNumber(
                    range.Left + column + 1d)),
            DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateColumnsArray(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count != 1)
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var node = function.Arguments[0];
        if (node is FunctionNode choose &&
            string.Equals(
                choose.Name,
                "CHOOSE",
                StringComparison.OrdinalIgnoreCase))
        {
            if (!ReferenceSelectionFormulaEvaluation.TrySelectChooseNode(
                    choose,
                    candidate => EvaluateScalarNode(
                        candidate,
                        context,
                        dependencies),
                    out node,
                    out var chooseError))
            {
                return ReferenceError(chooseError, dependencies);
            }
        }

        if (ReferenceIntrospectionFormulaEvaluation.IsReferenceCandidate(node))
        {
            if (!ReferenceIntrospectionFormulaEvaluation
                    .TryResolveReferenceNode(
                        node,
                        candidate => EvaluateScalarNode(
                            candidate,
                            context,
                            dependencies),
                        out var reference,
                        out var referenceError) ||
                !ReferenceIntrospectionFormulaEvaluation.TryGetRange(
                    reference,
                    out _,
                    out var range))
            {
                return ReferenceError(referenceError, dependencies);
            }

            return FormulaArrayEvaluationResult.Success(
                new FormulaArrayValue(
                    1,
                    1,
                    [CellValue.FromNumber(range.ColumnCount)]),
                DistinctDependencies(dependencies));
        }
        if (node is ReferenceUnionNode)
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var source = EvaluateNodeAsArray(node, context, dependencies);
        if (!source.IsSuccess)
        {
            return source;
        }

        return FormulaArrayEvaluationResult.Success(
            new FormulaArrayValue(
                1,
                1,
                [CellValue.FromNumber(source.Value!.ColumnCount)]),
            DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateDrop(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count is < 2 or > 3)
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var source = EvaluateNodeAsArray(
            function.Arguments[0],
            context,
            dependencies);
        if (!source.IsSuccess)
        {
            return source;
        }

        FormulaArrayEvaluationResult? error;
        var rowsProvided =
            function.Arguments[1] is not MissingArgumentNode;
        var rows = 0;
        if (rowsProvided &&
            !TryEvaluateShapeInteger(
                function.Arguments[1],
                context,
                dependencies,
                out rows,
                out error))
        {
            return error!;
        }

        var columnsProvided =
            function.Arguments.Count == 3 &&
            function.Arguments[2] is not MissingArgumentNode;
        var columns = 0;
        if (columnsProvided &&
            !TryEvaluateShapeInteger(
                function.Arguments[2],
                context,
                dependencies,
                out columns,
                out error))
        {
            return error!;
        }

        if ((!rowsProvided && !columnsProvided) ||
            (rowsProvided && rows == 0) ||
            (columnsProvided && columns == 0))
        {
            return Failure(
                "#CALC!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var sourceValue = source.Value!;
        var removedRows = Math.Abs((long)rows);
        var removedColumns = Math.Abs((long)columns);
        if (removedRows >= sourceValue.RowCount ||
            removedColumns >= sourceValue.ColumnCount)
        {
            return Failure(
                "#CALC!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var outputRows = checked(
            sourceValue.RowCount - (int)removedRows);
        var outputColumns = checked(
            sourceValue.ColumnCount - (int)removedColumns);
        var rowOffset = rows > 0 ? rows : 0;
        var columnOffset = columns > 0 ? columns : 0;

        return FormulaArrayEvaluationResult.Success(
            FormulaArrayValue.Create(
                outputRows,
                outputColumns,
                (row, column) => sourceValue[
                    row + rowOffset,
                    column + columnOffset]),
            DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateExpand(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count is < 2 or > 4)
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var source = EvaluateNodeAsArray(
            function.Arguments[0],
            context,
            dependencies);
        if (!source.IsSuccess)
        {
            return source;
        }

        var sourceValue = source.Value!;
        if (!TryEvaluateExpandDimension(
                function.Arguments[1],
                sourceValue.RowCount,
                context,
                dependencies,
                out var rows,
                out var error))
        {
            return error!;
        }

        var columns = sourceValue.ColumnCount;
        if (function.Arguments.Count >= 3 &&
            !TryEvaluateExpandDimension(
                function.Arguments[2],
                sourceValue.ColumnCount,
                context,
                dependencies,
                out columns,
                out error))
        {
            return error!;
        }

        if (rows < sourceValue.RowCount ||
            columns < sourceValue.ColumnCount)
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var outputCellCount = checked((long)rows * columns);
        if (outputCellCount > FormulaArrayValue.MaximumCellCount)
        {
            return Failure(
                "#NUM!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var padding = CellValue.FromError("#N/A");
        if (function.Arguments.Count == 4 &&
            function.Arguments[3] is not MissingArgumentNode)
        {
            padding = EvaluateScalarNode(
                function.Arguments[3],
                context,
                dependencies);
        }

        return FormulaArrayEvaluationResult.Success(
            FormulaArrayValue.Create(
                rows,
                columns,
                (row, column) =>
                    row < sourceValue.RowCount &&
                    column < sourceValue.ColumnCount
                        ? sourceValue[row, column]
                        : padding),
            DistinctDependencies(dependencies));
    }

    private bool TryEvaluateShapeInteger(
        FormulaNode node,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        out int value,
        out FormulaArrayEvaluationResult? error)
    {
        var scalar = EvaluateScalarNode(node, context, dependencies);
        if (scalar.Kind == CellValueKind.Error)
        {
            value = default;
            error = FormulaArrayEvaluationResult.Failure(
                scalar,
                ToErrorCode(scalar),
                DistinctDependencies(dependencies));
            return false;
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
        if (truncated < int.MinValue ||
            truncated > int.MaxValue)
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

    private bool TryEvaluateExpandDimension(
        FormulaNode node,
        int sourceDimension,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        out int value,
        out FormulaArrayEvaluationResult? error)
    {
        if (node is MissingArgumentNode)
        {
            value = sourceDimension;
            error = null;
            return true;
        }

        var scalar = EvaluateScalarNode(node, context, dependencies);
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
            value = sourceDimension;
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
        if (truncated < 1d ||
            truncated > int.MaxValue)
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

    private static FormulaArrayEvaluationResult ReferenceError(
        CellValue error,
        IReadOnlyList<FormulaDependency> dependencies)
    {
        var effectiveError = error.Kind == CellValueKind.Error
            ? error
            : CellValue.FromError("#VALUE!");
        return FormulaArrayEvaluationResult.Failure(
            effectiveError,
            effectiveError.Kind == CellValueKind.Error
                ? ToErrorCode(effectiveError)
                : FormulaErrorCode.InvalidValue,
            DistinctDependencies(dependencies));
    }
}
