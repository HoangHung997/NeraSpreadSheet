using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

public sealed partial class NeraFormulaEngine
{
    private static CellValue EvaluateF019Formula(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count != 1 ||
            context is not IFormulaReferenceIntrospectionContext introspection)
        {
            return CellValue.FromError("#VALUE!");
        }

        string? worksheetName;
        CellAddress address;
        switch (function.Arguments[0])
        {
            case CellNode cell:
                worksheetName = cell.WorksheetName;
                address = cell.Address;
                break;
            case RangeNode range when range.Range.RowCount == 1 && range.Range.ColumnCount == 1:
                worksheetName = range.WorksheetName;
                address = range.Range.TopLeft;
                break;
            default:
                return CellValue.FromError("#VALUE!");
        }

        dependencies.Add(new FormulaDependency(
            worksheetName,
            new CellRange(address, address)));
        return introspection.TryGetCellFormula(worksheetName, address, out var formula) &&
               !string.IsNullOrEmpty(formula)
            ? CellValue.FromText(formula)
            : CellValue.FromError("#N/A");
    }
}
