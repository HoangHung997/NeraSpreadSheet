namespace NeraSpreadSheet.Formulas;

internal enum FormulaTokenKind
{
    End,
    Number,
    String,
    Identifier,
    LeftParenthesis,
    RightParenthesis,
    Comma,
    Colon,
    Exclamation,
    Plus,
    Minus,
    Multiply,
    Divide,
    Power,
    Concat,
    Equal,
    NotEqual,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual,
}

internal readonly record struct FormulaToken(FormulaTokenKind Kind, string Text, double Number = 0d);
