using System.Globalization;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Scalar reference-text helpers that do not require AST-level reference
/// identity or dynamic-array spill ownership.
/// </summary>
internal static class ReferenceSelectionFormulaFunctions
{
    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return new FormulaFunctionDefinition(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity(
                    "NERA.BUILTIN",
                    "ADDRESS"),
                new FormulaFunctionVersion(1, 0, 0),
                FormulaFunctionApiVersion.Current,
                2,
                5,
                FormulaFunctionCapabilities.ScalarArguments |
                FormulaFunctionCapabilities.ReturnsScalar,
                argumentCountPolicy:
                    FormulaFunctionArgumentCountPolicy.LogicalArguments),
            EvaluateAddress);
    }

    private static FormulaEvaluationResult EvaluateAddress(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetRequiredIndex(
                invocation.Arguments[0],
                SpreadsheetLimits.MaxRows,
                out var rowNumber,
                out var error) ||
            !TryGetRequiredIndex(
                invocation.Arguments[1],
                SpreadsheetLimits.MaxColumns,
                out var columnNumber,
                out error))
        {
            return error;
        }

        var absoluteMode = 1;
        if (invocation.Arguments.Count >= 3 &&
            !IsBlank(invocation.Arguments[2]) &&
            !TryGetAbsoluteMode(
                invocation.Arguments[2],
                out absoluteMode,
                out error))
        {
            return error;
        }

        var useA1Style = true;
        if (invocation.Arguments.Count >= 4 &&
            !IsBlank(invocation.Arguments[3]) &&
            !TryGetScalarBoolean(
                invocation.Arguments[3],
                out useA1Style,
                out error))
        {
            return error;
        }

        string? sheetText = null;
        if (invocation.Arguments.Count == 5 &&
            !IsBlank(invocation.Arguments[4]))
        {
            if (invocation.Arguments[4].Kind !=
                FormulaFunctionArgumentKind.Scalar)
            {
                return InvalidValue();
            }
            sheetText = FormulaValueCoercion.ToText(
                invocation.Arguments[4].ScalarValue);
        }

        var address = useA1Style
            ? FormatA1(rowNumber, columnNumber, absoluteMode)
            : FormatR1C1(rowNumber, columnNumber, absoluteMode);
        if (!string.IsNullOrEmpty(sheetText))
        {
            address = FormatSheetPrefix(sheetText) + address;
        }

        return FormulaEvaluationResult.Success(
            CellValue.FromText(address));
    }

    private static bool TryGetRequiredIndex(
        FormulaFunctionArgument argument,
        int maximum,
        out int value,
        out FormulaEvaluationResult error)
    {
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar ||
            !FormulaValueCoercion.TryNumber(
                argument.ScalarValue,
                out var number,
                allowText: true) ||
            !double.IsFinite(number))
        {
            value = default;
            error = InvalidValue();
            return false;
        }

        var truncated = Math.Truncate(number);
        if (truncated < 1d || truncated > maximum)
        {
            value = default;
            error = InvalidValue();
            return false;
        }

        value = checked((int)truncated);
        error = default!;
        return true;
    }

    private static bool TryGetAbsoluteMode(
        FormulaFunctionArgument argument,
        out int value,
        out FormulaEvaluationResult error)
    {
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar ||
            !FormulaValueCoercion.TryNumber(
                argument.ScalarValue,
                out var number,
                allowText: true) ||
            !double.IsFinite(number))
        {
            value = default;
            error = InvalidValue();
            return false;
        }

        var truncated = Math.Truncate(number);
        if (truncated is < 1d or > 4d)
        {
            value = default;
            error = InvalidValue();
            return false;
        }

        value = checked((int)truncated);
        error = default!;
        return true;
    }

    private static bool TryGetScalarBoolean(
        FormulaFunctionArgument argument,
        out bool value,
        out FormulaEvaluationResult error)
    {
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar ||
            !FormulaValueCoercion.TryBoolean(
                argument.ScalarValue,
                out value,
                allowText: true))
        {
            value = default;
            error = InvalidValue();
            return false;
        }

        error = default!;
        return true;
    }

    private static bool IsBlank(FormulaFunctionArgument argument) =>
        argument.Kind == FormulaFunctionArgumentKind.Scalar &&
        argument.ScalarValue.Kind == CellValueKind.Blank;

    private static string FormatA1(
        int rowNumber,
        int columnNumber,
        int absoluteMode)
    {
        var absoluteRow = absoluteMode is 1 or 2;
        var absoluteColumn = absoluteMode is 1 or 3;
        return string.Concat(
            absoluteColumn ? "$" : string.Empty,
            FormatColumnName(columnNumber),
            absoluteRow ? "$" : string.Empty,
            rowNumber.ToString(CultureInfo.InvariantCulture));
    }

    private static string FormatR1C1(
        int rowNumber,
        int columnNumber,
        int absoluteMode)
    {
        var absoluteRow = absoluteMode is 1 or 2;
        var absoluteColumn = absoluteMode is 1 or 3;
        var rowText = rowNumber.ToString(CultureInfo.InvariantCulture);
        var columnText = columnNumber.ToString(CultureInfo.InvariantCulture);
        return string.Concat(
            absoluteRow ? "R" + rowText : "R[" + rowText + "]",
            absoluteColumn ? "C" + columnText : "C[" + columnText + "]");
    }

    private static string FormatColumnName(int columnNumber)
    {
        Span<char> buffer = stackalloc char[8];
        var position = buffer.Length;
        var remaining = columnNumber;
        while (remaining > 0)
        {
            remaining--;
            buffer[--position] = (char)('A' + (remaining % 26));
            remaining /= 26;
        }
        return new string(buffer[position..]);
    }

    private static string FormatSheetPrefix(string sheetText)
    {
        if (!RequiresSheetQuoting(sheetText))
        {
            return sheetText + "!";
        }

        return "'" +
               sheetText.Replace(
                   "'",
                   "''",
                   StringComparison.Ordinal) +
               "'!";
    }

    private static bool RequiresSheetQuoting(string sheetText)
    {
        if (CellAddress.TryParseA1(sheetText, out _))
        {
            return true;
        }
        if (sheetText.Length == 0 ||
            (!char.IsAsciiLetter(sheetText[0]) &&
             sheetText[0] != '_'))
        {
            return true;
        }

        foreach (var character in sheetText)
        {
            if (!char.IsAsciiLetterOrDigit(character) &&
                character != '_' &&
                character != '.')
            {
                return true;
            }
        }
        return false;
    }

    private static FormulaEvaluationResult InvalidValue() =>
        FormulaEvaluationResult.Failure(FormulaErrorCode.InvalidValue);
}
