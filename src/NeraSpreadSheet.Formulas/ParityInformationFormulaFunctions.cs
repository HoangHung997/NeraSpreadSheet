using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static class ParityInformationFormulaFunctions
{
    private const double MaximumExactInteger =
        9_007_199_254_740_991d;

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return FormulaFunctionFactory.Create(
            "ISEVEN",
            1,
            1,
            static (arguments, _) => IsEvenOrOdd(
                arguments[0],
                even: true));
        yield return FormulaFunctionFactory.Create(
            "ISODD",
            1,
            1,
            static (arguments, _) => IsEvenOrOdd(
                arguments[0],
                even: false));
        yield return FormulaFunctionFactory.Create(
            "ISNONTEXT",
            1,
            1,
            static (arguments, _) => CellValue.FromBoolean(
                arguments[0].Kind != CellValueKind.Text),
            propagateErrors: false);
    }

    private static CellValue IsEvenOrOdd(
        CellValue value,
        bool even)
    {
        if (!FormulaValueCoercion.TryNumber(
                value,
                out var number,
                allowText: true))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }

        var truncated = Math.Truncate(number);
        if (!double.IsFinite(truncated) ||
            Math.Abs(truncated) > MaximumExactInteger)
        {
            return FormulaValueCoercion.Error("#NUM!");
        }

        var isEven = truncated % 2d == 0d;
        return CellValue.FromBoolean(
            even ? isEven : !isEven);
    }
}
