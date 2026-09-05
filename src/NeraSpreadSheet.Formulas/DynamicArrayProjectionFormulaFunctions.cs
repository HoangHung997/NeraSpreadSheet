using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

public sealed partial class NeraDynamicArrayFormulaEngine
{
    private FormulaArrayEvaluationResult EvaluateChooseArray(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (!ReferenceSelectionFormulaEvaluation.TrySelectChooseNode(
                function,
                node => EvaluateScalarNode(node, context, dependencies),
                out var selected,
                out var error))
        {
            return Failure(
                error,
                ToErrorCode(error),
                DistinctDependencies(dependencies));
        }

        return EvaluateNodeAsArray(selected, context, dependencies);
    }

    private FormulaArrayEvaluationResult EvaluateChooseColumns(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies) =>
        EvaluateProjection(
            function,
            context,
            dependencies,
            selectColumns: true);

    private FormulaArrayEvaluationResult EvaluateChooseRows(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies) =>
        EvaluateProjection(
            function,
            context,
            dependencies,
            selectColumns: false);

    private FormulaArrayEvaluationResult EvaluateProjection(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        bool selectColumns)
    {
        if (function.Arguments.Count is < 2 or > 255)
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
        var dimension = selectColumns
            ? sourceValue.ColumnCount
            : sourceValue.RowCount;
        if (!TryReadProjectionIndexes(
                function.Arguments.Skip(1),
                dimension,
                context,
                dependencies,
                out var indexes,
                out var error))
        {
            return error!;
        }

        var outputRows = selectColumns
            ? sourceValue.RowCount
            : indexes.Length;
        var outputColumns = selectColumns
            ? indexes.Length
            : sourceValue.ColumnCount;
        var outputCellCount = checked((long)outputRows * outputColumns);
        if (outputCellCount > FormulaArrayValue.MaximumCellCount)
        {
            return Failure(
                "#NUM!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        return FormulaArrayEvaluationResult.Success(
            FormulaArrayValue.Create(
                outputRows,
                outputColumns,
                (row, column) => selectColumns
                    ? sourceValue[row, indexes[column]]
                    : sourceValue[indexes[row], column]),
            DistinctDependencies(dependencies));
    }

    private bool TryReadProjectionIndexes(
        IEnumerable<FormulaNode> nodes,
        int dimension,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        out int[] indexes,
        out FormulaArrayEvaluationResult? error)
    {
        var selected = new List<int>();
        foreach (var node in nodes)
        {
            var indexValues = EvaluateNodeAsArray(
                node,
                context,
                dependencies);
            if (!indexValues.IsSuccess)
            {
                indexes = [];
                error = indexValues;
                return false;
            }

            foreach (var value in indexValues.Value!.ToArray())
            {
                if (value.Kind == CellValueKind.Error)
                {
                    indexes = [];
                    error = FormulaArrayEvaluationResult.Failure(
                        value,
                        ToErrorCode(value),
                        DistinctDependencies(dependencies));
                    return false;
                }
                if (!FormulaValueCoercion.TryNumber(
                        value,
                        out var number,
                        allowText: true) ||
                    !double.IsFinite(number))
                {
                    indexes = [];
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
                    indexes = [];
                    error = Failure(
                        "#VALUE!",
                        FormulaErrorCode.InvalidValue,
                        dependencies);
                    return false;
                }

                var index = checked((int)truncated);
                if (index == 0 ||
                    index > dimension ||
                    index < -dimension)
                {
                    indexes = [];
                    error = Failure(
                        "#VALUE!",
                        FormulaErrorCode.InvalidValue,
                        dependencies);
                    return false;
                }

                selected.Add(index > 0
                    ? index - 1
                    : dimension + index);
                if (selected.Count > FormulaArrayValue.MaximumCellCount)
                {
                    indexes = [];
                    error = Failure(
                        "#NUM!",
                        FormulaErrorCode.InvalidValue,
                        dependencies);
                    return false;
                }
            }
        }

        if (selected.Count == 0)
        {
            indexes = [];
            error = Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
            return false;
        }

        indexes = selected.ToArray();
        error = null;
        return true;
    }
}
