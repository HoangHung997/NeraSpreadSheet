using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal sealed class FormulaParser
{
    private readonly FormulaTokenizer _tokenizer;
    private FormulaToken _current;

    public FormulaParser(string formula)
    {
        _tokenizer = new FormulaTokenizer(formula);
        _current = _tokenizer.Next();
    }

    public FormulaNode Parse()
    {
        var expression = ParseComparison();
        Expect(FormulaTokenKind.End);
        return expression;
    }

    private FormulaNode ParseComparison()
    {
        var left = ParseConcat();
        while (_current.Kind is FormulaTokenKind.Equal or
            FormulaTokenKind.NotEqual or
            FormulaTokenKind.Less or
            FormulaTokenKind.LessOrEqual or
            FormulaTokenKind.Greater or
            FormulaTokenKind.GreaterOrEqual)
        {
            var operation = _current.Kind;
            MoveNext();
            left = new BinaryNode(operation, left, ParseConcat());
        }
        return left;
    }

    private FormulaNode ParseConcat()
    {
        var left = ParseAdditive();
        while (_current.Kind == FormulaTokenKind.Concat)
        {
            var operation = _current.Kind;
            MoveNext();
            left = new BinaryNode(operation, left, ParseAdditive());
        }
        return left;
    }

    private FormulaNode ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (_current.Kind is FormulaTokenKind.Plus or FormulaTokenKind.Minus)
        {
            var operation = _current.Kind;
            MoveNext();
            left = new BinaryNode(operation, left, ParseMultiplicative());
        }
        return left;
    }

    private FormulaNode ParseMultiplicative()
    {
        var left = ParsePower();
        while (_current.Kind is FormulaTokenKind.Multiply or FormulaTokenKind.Divide)
        {
            var operation = _current.Kind;
            MoveNext();
            left = new BinaryNode(operation, left, ParsePower());
        }
        return left;
    }

    private FormulaNode ParsePower()
    {
        var left = ParseUnary();
        if (_current.Kind == FormulaTokenKind.Power)
        {
            var operation = _current.Kind;
            MoveNext();
            return new BinaryNode(operation, left, ParsePower());
        }
        return left;
    }

    private FormulaNode ParseUnary()
    {
        if (_current.Kind is FormulaTokenKind.Plus or FormulaTokenKind.Minus)
        {
            var operation = _current.Kind;
            MoveNext();
            return new UnaryNode(operation, ParseUnary());
        }
        return ParsePrimary();
    }

    private FormulaNode ParsePrimary()
    {
        if (_current.Kind == FormulaTokenKind.Number)
        {
            var value = CellValue.FromNumber(_current.Number);
            MoveNext();
            return new ConstantNode(value);
        }
        if (_current.Kind == FormulaTokenKind.String)
        {
            var value = CellValue.FromText(_current.Text);
            MoveNext();
            return new ConstantNode(value);
        }
        if (_current.Kind == FormulaTokenKind.Error)
        {
            var value = CellValue.FromError(_current.Text);
            MoveNext();
            return new ConstantNode(value);
        }
        if (_current.Kind == FormulaTokenKind.LeftParenthesis)
        {
            return ParseParenthesizedExpression();
        }
        if (_current.Kind != FormulaTokenKind.Identifier)
        {
            throw new FormatException(
                $"Expected a value but found '{_current.Text}'.");
        }

        var identifier = _current.Text;
        MoveNext();
        if (_current.Kind == FormulaTokenKind.LeftParenthesis)
        {
            return ParseFunction(identifier);
        }
        if (string.Equals(
                identifier,
                "TRUE",
                StringComparison.OrdinalIgnoreCase))
        {
            return new ConstantNode(CellValue.FromBoolean(true));
        }
        if (string.Equals(
                identifier,
                "FALSE",
                StringComparison.OrdinalIgnoreCase))
        {
            return new ConstantNode(CellValue.FromBoolean(false));
        }

        string? worksheetName = null;
        var addressText = identifier;
        if (_current.Kind == FormulaTokenKind.Exclamation)
        {
            worksheetName = identifier;
            MoveNext();
            if (_current.Kind != FormulaTokenKind.Identifier)
            {
                throw new FormatException(
                    "Expected a cell address after '!'.");
            }
            addressText = _current.Text;
            MoveNext();
        }
        if (!CellAddress.TryParseA1(addressText, out var firstAddress))
        {
            if (worksheetName is not null)
            {
                throw new FormatException(
                    $"Unknown reference '{worksheetName}!{addressText}'.");
            }
            return new NameNode(identifier);
        }
        if (_current.Kind != FormulaTokenKind.Colon)
        {
            return new CellNode(worksheetName, firstAddress);
        }

        MoveNext();
        if (_current.Kind != FormulaTokenKind.Identifier ||
            !CellAddress.TryParseA1(
                _current.Text,
                out var secondAddress))
        {
            throw new FormatException(
                "Expected a valid cell address after ':'.");
        }
        MoveNext();
        return new RangeNode(
            worksheetName,
            new CellRange(firstAddress, secondAddress));
    }

    private FormulaNode ParseParenthesizedExpression()
    {
        MoveNext();
        if (_current.Kind == FormulaTokenKind.RightParenthesis)
        {
            throw new FormatException(
                "A parenthesized formula expression cannot be empty.");
        }

        var areas = new List<FormulaNode>
        {
            ParseComparison(),
        };
        while (_current.Kind == FormulaTokenKind.Comma)
        {
            MoveNext();
            if (_current.Kind == FormulaTokenKind.RightParenthesis)
            {
                throw new FormatException(
                    "A reference union cannot contain a missing area.");
            }
            areas.Add(ParseComparison());
        }

        Expect(FormulaTokenKind.RightParenthesis);
        MoveNext();
        return areas.Count == 1
            ? areas[0]
            : new ReferenceUnionNode(areas);
    }

    private FunctionNode ParseFunction(string name)
    {
        MoveNext();
        var arguments = new List<FormulaNode>();
        if (_current.Kind == FormulaTokenKind.RightParenthesis)
        {
            MoveNext();
            return new FunctionNode(name, arguments);
        }

        while (true)
        {
            if (_current.Kind is FormulaTokenKind.Comma or
                FormulaTokenKind.RightParenthesis)
            {
                arguments.Add(new MissingArgumentNode());
            }
            else
            {
                arguments.Add(ParseComparison());
            }

            if (_current.Kind == FormulaTokenKind.RightParenthesis)
            {
                MoveNext();
                return new FunctionNode(name, arguments);
            }
            Expect(FormulaTokenKind.Comma);
            MoveNext();
        }
    }

    private void Expect(FormulaTokenKind kind)
    {
        if (_current.Kind != kind)
        {
            throw new FormatException(
                $"Expected {kind} but found '{_current.Text}'.");
        }
    }

    private void MoveNext() => _current = _tokenizer.Next();
}
