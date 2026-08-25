using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static class ReferenceIntrospectionFormulaEvaluation
{
    public const int MaximumFormulaTextLength = 8192;

    public static bool IsReferenceCandidate(FormulaNode node) =>
        node is CellNode or RangeNode ||
        node is FunctionNode function &&
        string.Equals(
            function.Name,
            "CHOOSE",
            StringComparison.OrdinalIgnoreCase);

    public static bool TryResolveReferenceNode(
        FormulaNode node,
        Func<FormulaNode, CellValue> evaluateSelector,
        out FormulaNode reference,
        out CellValue error)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(evaluateSelector);

        switch (node)
        {
            case CellNode:
            case RangeNode:
                reference = node;
                error = default;
                return true;
            case FunctionNode function
                when string.Equals(
                    function.Name,
                    "CHOOSE",
                    StringComparison.OrdinalIgnoreCase):
                if (!ReferenceSelectionFormulaEvaluation.TrySelectChooseNode(
                        function,
                        evaluateSelector,
                        out var selected,
                        out error))
                {
                    reference = null!;
                    return false;
                }

                return TryResolveReferenceNode(
                    selected,
                    evaluateSelector,
                    out reference,
                    out error);
            default:
                reference = null!;
                error = CellValue.FromError("#VALUE!");
                return false;
        }
    }

    public static bool TryGetRange(
        FormulaNode reference,
        out string? worksheetName,
        out CellRange range)
    {
        switch (reference)
        {
            case CellNode cell:
                worksheetName = cell.WorksheetName;
                range = new CellRange(cell.Address, cell.Address);
                return true;
            case RangeNode rangeNode:
                worksheetName = rangeNode.WorksheetName;
                range = rangeNode.Range;
                return true;
            default:
                worksheetName = null;
                range = default;
                return false;
        }
    }
}

public sealed partial class NeraFormulaEngine
{
    private CellValue EvaluateColumn(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count == 0)
        {
            return context is IFormulaReferenceIntrospectionContext
                introspection
                ? CellValue.FromNumber(
                    introspection.CurrentCellAddress.ColumnIndex + 1d)
                : CellValue.FromError("#VALUE!");
        }
        if (function.Arguments.Count != 1)
        {
            return CellValue.FromError("#VALUE!");
        }
        if (!TryResolveReference(
                function.Arguments[0],
                context,
                dependencies,
                out _,
                out var range,
                out var error))
        {
            return error;
        }

        return CellValue.FromNumber(range.Left + 1d);
    }

    private CellValue EvaluateColumns(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count != 1)
        {
            return CellValue.FromError("#VALUE!");
        }

        var node = function.Arguments[0];
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
                    out node,
                    out var chooseError))
            {
                return chooseError;
            }
        }

        if (ReferenceIntrospectionFormulaEvaluation.IsReferenceCandidate(node))
        {
            if (!TryResolveReference(
                    node,
                    context,
                    dependencies,
                    out _,
                    out var range,
                    out var error))
            {
                return error;
            }

            return CellValue.FromNumber(range.ColumnCount);
        }
        if (node is ReferenceUnionNode)
        {
            return CellValue.FromError("#VALUE!");
        }

        var scalar = EvaluateNode(node, context, dependencies);
        return scalar.Kind == CellValueKind.Error
            ? scalar
            : CellValue.FromNumber(1d);
    }

    private CellValue EvaluateFormulaText(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count != 1)
        {
            return CellValue.FromError("#VALUE!");
        }
        if (!TryResolveReference(
                function.Arguments[0],
                context,
                dependencies,
                out var worksheetName,
                out var range,
                out var error))
        {
            return error;
        }
        if (context is not IFormulaReferenceIntrospectionContext
            introspection)
        {
            return CellValue.FromError("#N/A");
        }

        var address = range.TopLeft;
        dependencies.Add(new FormulaDependency(
            worksheetName,
            new CellRange(address, address)));
        if (!introspection.TryGetCellFormula(
                worksheetName,
                address,
                out var formula) ||
            formula is null ||
            formula.Length >
            ReferenceIntrospectionFormulaEvaluation.MaximumFormulaTextLength)
        {
            return CellValue.FromError("#N/A");
        }

        return CellValue.FromText(formula);
    }

    private bool TryResolveReference(
        FormulaNode node,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        out string? worksheetName,
        out CellRange range,
        out CellValue error)
    {
        if (!ReferenceIntrospectionFormulaEvaluation.TryResolveReferenceNode(
                node,
                candidate => EvaluateNode(
                    candidate,
                    context,
                    dependencies),
                out var reference,
                out error) ||
            !ReferenceIntrospectionFormulaEvaluation.TryGetRange(
                reference,
                out worksheetName,
                out range))
        {
            worksheetName = null;
            range = default;
            if (error.Kind != CellValueKind.Error)
            {
                error = CellValue.FromError("#VALUE!");
            }
            return false;
        }

        return true;
    }
}
