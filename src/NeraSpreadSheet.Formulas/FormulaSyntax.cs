using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal abstract record FormulaNode;

internal sealed record ConstantNode(CellValue Value) : FormulaNode;

internal sealed record CellNode(string? WorksheetName, CellAddress Address) : FormulaNode;

internal sealed record RangeNode(string? WorksheetName, CellRange Range) : FormulaNode;

internal sealed record UnaryNode(FormulaTokenKind Operator, FormulaNode Operand) : FormulaNode;

internal sealed record BinaryNode(FormulaTokenKind Operator, FormulaNode Left, FormulaNode Right) : FormulaNode;

internal sealed record FunctionNode(string Name, IReadOnlyList<FormulaNode> Arguments) : FormulaNode;
