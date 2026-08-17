using System.Globalization;

namespace NeraSpreadSheet.Formulas;

internal sealed class FormulaTokenizer
{
    private readonly string _text;
    private int _position;

    public FormulaTokenizer(string formula)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formula);
        _text = formula[0] == '=' ? formula[1..] : formula;
    }

    public FormulaToken Next()
    {
        SkipWhitespace();
        if (_position >= _text.Length)
        {
            return new FormulaToken(FormulaTokenKind.End, string.Empty);
        }

        var current = _text[_position];
        if (char.IsAsciiDigit(current) || current == '.' && PeekIsDigit())
        {
            return ReadNumber();
        }

        if (current == '"')
        {
            return ReadString();
        }

        if (current == '#')
        {
            return ReadError();
        }

        if (char.IsAsciiLetter(current) || current is '_' or '$')
        {
            return ReadIdentifier();
        }

        _position++;
        return current switch
        {
            '(' => new FormulaToken(FormulaTokenKind.LeftParenthesis, "("),
            ')' => new FormulaToken(FormulaTokenKind.RightParenthesis, ")"),
            ',' or ';' => new FormulaToken(FormulaTokenKind.Comma, current.ToString()),
            ':' => new FormulaToken(FormulaTokenKind.Colon, ":"),
            '!' => new FormulaToken(FormulaTokenKind.Exclamation, "!"),
            '+' => new FormulaToken(FormulaTokenKind.Plus, "+"),
            '-' => new FormulaToken(FormulaTokenKind.Minus, "-"),
            '*' => new FormulaToken(FormulaTokenKind.Multiply, "*"),
            '/' => new FormulaToken(FormulaTokenKind.Divide, "/"),
            '^' => new FormulaToken(FormulaTokenKind.Power, "^"),
            '&' => new FormulaToken(FormulaTokenKind.Concat, "&"),
            '=' => new FormulaToken(FormulaTokenKind.Equal, "="),
            '<' => ReadLessOperator(),
            '>' => ReadGreaterOperator(),
            _ => throw new FormatException($"Unexpected character '{current}' at position {_position}.")
        };
    }

    private FormulaToken ReadNumber()
    {
        var start = _position;
        var seenExponent = false;
        while (_position < _text.Length)
        {
            var character = _text[_position];
            if (char.IsAsciiDigit(character) || character == '.')
            {
                _position++;
                continue;
            }

            if (!seenExponent && character is 'e' or 'E')
            {
                seenExponent = true;
                _position++;
                if (_position < _text.Length && _text[_position] is '+' or '-')
                {
                    _position++;
                }
                continue;
            }

            break;
        }

        var text = _text[start.._position];
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
        {
            throw new FormatException($"'{text}' is not a valid finite number.");
        }

        return new FormulaToken(FormulaTokenKind.Number, text, value);
    }

    private FormulaToken ReadString()
    {
        _position++;
        var builder = new System.Text.StringBuilder();
        while (_position < _text.Length)
        {
            var character = _text[_position++];
            if (character != '"')
            {
                builder.Append(character);
                continue;
            }

            if (_position < _text.Length && _text[_position] == '"')
            {
                builder.Append('"');
                _position++;
                continue;
            }

            return new FormulaToken(FormulaTokenKind.String, builder.ToString());
        }

        throw new FormatException("Formula string literal is not terminated.");
    }

    private FormulaToken ReadError()
    {
        var start = _position++;
        while (_position < _text.Length && IsErrorCharacter(_text[_position]))
        {
            _position++;
        }

        var text = _text[start.._position];
        if (text.Length < 2 || !text.Any(char.IsAsciiLetter))
        {
            throw new FormatException($"'{text}' is not a valid formula error literal.");
        }

        return new FormulaToken(FormulaTokenKind.Error, text.ToUpperInvariant());
    }

    private FormulaToken ReadIdentifier()
    {
        var start = _position;
        while (_position < _text.Length)
        {
            var character = _text[_position];
            if (char.IsAsciiLetterOrDigit(character) || character is '_' or '.' or '$')
            {
                _position++;
                continue;
            }

            break;
        }

        return new FormulaToken(FormulaTokenKind.Identifier, _text[start.._position]);
    }

    private FormulaToken ReadLessOperator()
    {
        if (_position < _text.Length && _text[_position] == '=')
        {
            _position++;
            return new FormulaToken(FormulaTokenKind.LessOrEqual, "<=");
        }

        if (_position < _text.Length && _text[_position] == '>')
        {
            _position++;
            return new FormulaToken(FormulaTokenKind.NotEqual, "<>");
        }

        return new FormulaToken(FormulaTokenKind.Less, "<");
    }

    private FormulaToken ReadGreaterOperator()
    {
        if (_position < _text.Length && _text[_position] == '=')
        {
            _position++;
            return new FormulaToken(FormulaTokenKind.GreaterOrEqual, ">=");
        }

        return new FormulaToken(FormulaTokenKind.Greater, ">");
    }

    private bool PeekIsDigit() => _position + 1 < _text.Length && char.IsAsciiDigit(_text[_position + 1]);

    private static bool IsErrorCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is '/' or '!' or '?' or '_' or '.';

    private void SkipWhitespace()
    {
        while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
        {
            _position++;
        }
    }
}
