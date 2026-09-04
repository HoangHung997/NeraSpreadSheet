namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Extracts syntactic cell and range references without evaluating a formula.
/// This is intended for editing affordances such as colored precedent outlines.
/// </summary>
public static class FormulaReferenceAnalyzer
{
    /// <summary>
    /// Attempts to parse <paramref name="formula"/> and returns its explicit
    /// cell and range references. Dynamic references produced by functions such
    /// as INDIRECT are not included because they require evaluation.
    /// </summary>
    public static bool TryGetReferences(
        string formula,
        out IReadOnlyList<FormulaDependency> references)
    {
        ArgumentNullException.ThrowIfNull(formula);
        try
        {
            var result = new List<FormulaDependency>();
            Collect(new FormulaParser(formula).Parse(), result);
            references = result.Distinct().ToArray();
            return true;
        }
        catch (FormatException)
        {
            references = Array.Empty<FormulaDependency>();
            return false;
        }
    }

    private static void Collect(
        FormulaNode node,
        List<FormulaDependency> references)
    {
        switch (node)
        {
            case CellNode cell:
                references.Add(new FormulaDependency(
                    cell.WorksheetName,
                    new Core.CellRange(cell.Address, cell.Address)));
                break;
            case RangeNode range:
                references.Add(new FormulaDependency(
                    range.WorksheetName,
                    range.Range));
                break;
            case ReferenceUnionNode union:
                foreach (var area in union.Areas)
                {
                    Collect(area, references);
                }
                break;
            case UnaryNode unary:
                Collect(unary.Operand, references);
                break;
            case BinaryNode binary:
                Collect(binary.Left, references);
                Collect(binary.Right, references);
                break;
            case FunctionNode function:
                foreach (var argument in function.Arguments)
                {
                    Collect(argument, references);
                }
                break;
        }
    }
}
