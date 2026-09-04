using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal abstract record FormulaNode;

internal sealed record ConstantNode(CellValue Value) : FormulaNode;

internal sealed record MissingArgumentNode() : FormulaNode;

internal sealed record CellNode(string? WorksheetName, CellAddress Address) : FormulaNode;

internal sealed record RangeNode(
    string? WorksheetName,
    CellRange Range,
    FormulaRangeExtentKind ExtentKind = FormulaRangeExtentKind.Cells) : FormulaNode;

internal enum FormulaRangeExtentKind
{
    Cells = 0,
    WholeColumns,
}

internal sealed record ReferenceUnionNode(
    IReadOnlyList<FormulaNode> Areas) : FormulaNode;

internal sealed record UnaryNode(
    FormulaTokenKind Operator,
    FormulaNode Operand) : FormulaNode;

internal sealed record BinaryNode(
    FormulaTokenKind Operator,
    FormulaNode Left,
    FormulaNode Right) : FormulaNode;

internal sealed record FunctionNode(
    string Name,
    IReadOnlyList<FormulaNode> Arguments) : FormulaNode;
