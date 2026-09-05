namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Represents an unqualified formula name. F010 uses this syntax for the
/// eta-reduced aggregate name supplied to GROUPBY; unresolved names continue
/// to evaluate as #NAME?.
/// </summary>
internal sealed record NameNode(string Name) : FormulaNode;
