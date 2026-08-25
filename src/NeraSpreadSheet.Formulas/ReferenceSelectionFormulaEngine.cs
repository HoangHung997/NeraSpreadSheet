using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static class ReferenceSelectionFormulaEvaluation
{
    public const int MaximumChooseValues = 254;

    public static bool TrySelectChooseNode(
        FunctionNode function,
        Func<FormulaNode, CellValue> evaluateIndex,
        out FormulaNode selected,
        out CellValue error)
    {
        ArgumentNullException.ThrowIfNull(function);
        ArgumentNullException.ThrowIfNull(evaluateIndex);
        selected = null!;
        if (function.Arguments.Count is < 2 or > 255)
        {
            error = CellValue.FromError("#VALUE!");
            return false;
        }

        var indexValue = evaluateIndex(function.Arguments[0]);
        if (indexValue.Kind == CellValueKind.Error)
        {
            error = indexValue;
            return false;
        }
        if (!FormulaValueCoercion.TryNumber(
                indexValue,
                out var indexNumber,
                allowText: true) ||
            !double.IsFinite(indexNumber))
        {
            error = CellValue.FromError("#VALUE!");
            return false;
        }

        var truncated = Math.Truncate(indexNumber);
        var valueCount = function.Arguments.Count - 1;
        if (valueCount > MaximumChooseValues ||
            truncated < 1d ||
            truncated > valueCount)
        {
            error = CellValue.FromError("#VALUE!");
            return false;
        }

        selected = function.Arguments[checked((int)truncated)];
        error = default;
        return true;
    }
}

public sealed partial class NeraFormulaEngine
{
    private CellValue EvaluateAreas(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count != 1)
        {
            return CellValue.FromError("#VALUE!");
        }
        if (!TryCountReferenceAreas(
                function.Arguments[0],
                context,
                dependencies,
                out var areaCount,
                out var error))
        {
            return error;
        }

        return CellValue.FromNumber(areaCount);
    }

    private CellValue EvaluateChoose(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (!TrySelectChooseNode(
                function,
                context,
                dependencies,
                out var selected,
                out var error))
        {
            return error;
        }

        return selected switch
        {
            RangeNode range => EvaluateRangeTopLeft(
                range,
                context,
                dependencies),
            ReferenceUnionNode => CellValue.FromError("#VALUE!"),
            _ => EvaluateNode(selected, context, dependencies),
        };
    }

    private bool TryEvaluateChooseInvocationArgument(
        FormulaNode node,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        out FormulaFunctionArgument argument)
    {
        if (node is not FunctionNode function ||
            !string.Equals(
                function.Name,
                "CHOOSE",
                StringComparison.OrdinalIgnoreCase))
        {
            argument = null!;
            return false;
        }

        if (!TrySelectChooseNode(
                function,
                context,
                dependencies,
                out var selected,
                out var error))
        {
            argument = FormulaFunctionArgument.Scalar(error);
            return true;
        }

        if (selected is RangeNode range)
        {
            var dependency = new FormulaDependency(
                range.WorksheetName,
                range.Range);
            dependencies.Add(dependency);
            var values = new List<CellValue>(
                checked(range.Range.RowCount * range.Range.ColumnCount));
            AppendRange(values, range, context);
            argument = FormulaFunctionArgument.Range(dependency, values);
            return true;
        }

        argument = FormulaFunctionArgument.Scalar(
            selected is ReferenceUnionNode
                ? CellValue.FromError("#VALUE!")
                : EvaluateNode(selected, context, dependencies));
        return true;
    }

    private bool TryCountReferenceAreas(
        FormulaNode node,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        out int areaCount,
        out CellValue error)
    {
        switch (node)
        {
            case CellNode:
            case RangeNode:
                areaCount = 1;
                error = default;
                return true;
            case ReferenceUnionNode union:
                areaCount = 0;
                foreach (var area in union.Areas)
                {
                    if (!TryCountReferenceAreas(
                            area,
                            context,
                            dependencies,
                            out var nestedCount,
                            out error))
                    {
                        areaCount = default;
                        return false;
                    }
                    areaCount = checked(areaCount + nestedCount);
                }
                error = default;
                return true;
            case FunctionNode function
                when string.Equals(
                    function.Name,
                    "CHOOSE",
                    StringComparison.OrdinalIgnoreCase):
                if (!TrySelectChooseNode(
                        function,
                        context,
                        dependencies,
                        out var selected,
                        out error))
                {
                    areaCount = default;
                    return false;
                }
                return TryCountReferenceAreas(
                    selected,
                    context,
                    dependencies,
                    out areaCount,
                    out error);
            default:
                areaCount = default;
                error = CellValue.FromError("#VALUE!");
                return false;
        }
    }

    private bool TrySelectChooseNode(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        out FormulaNode selected,
        out CellValue error) =>
        ReferenceSelectionFormulaEvaluation.TrySelectChooseNode(
            function,
            node => EvaluateNode(node, context, dependencies),
            out selected,
            out error);

    private static CellValue EvaluateRangeTopLeft(
        RangeNode range,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        dependencies.Add(new FormulaDependency(
            range.WorksheetName,
            range.Range));
        return context.GetCellValue(
            range.WorksheetName,
            range.Range.TopLeft);
    }
}
