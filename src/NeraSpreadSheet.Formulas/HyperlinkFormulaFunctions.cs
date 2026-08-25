using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static class HyperlinkFormulaFunctions
{
    public const int MaximumLinkLocationLength = 32_767;

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return new FormulaFunctionDefinition(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity(
                    "NERA.BUILTIN",
                    "HYPERLINK"),
                new FormulaFunctionVersion(1, 0, 0),
                FormulaFunctionApiVersion.Current,
                1,
                2,
                FormulaFunctionCapabilities.ScalarArguments |
                FormulaFunctionCapabilities.ReturnsScalar,
                securityClassification:
                    FormulaFunctionSecurityClassification.ContextReadOnly,
                argumentCountPolicy:
                    FormulaFunctionArgumentCountPolicy.LogicalArguments),
            EvaluateHyperlink);
    }

    private static FormulaEvaluationResult EvaluateHyperlink(
        FormulaFunctionInvocation invocation)
    {
        if (invocation.Arguments[0].Kind !=
            FormulaFunctionArgumentKind.Scalar)
        {
            return FormulaEvaluationResult.Failure(
                FormulaErrorCode.InvalidValue);
        }

        var linkLocation = FormulaValueCoercion.ToText(
            invocation.Arguments[0].ScalarValue);
        if (linkLocation.Length > MaximumLinkLocationLength)
        {
            return FormulaEvaluationResult.Failure(
                FormulaErrorCode.InvalidValue);
        }

        var displayValue = invocation.Arguments.Count == 2
            ? invocation.Arguments[1].ScalarValue
            : CellValue.FromText(linkLocation);
        if (invocation.Context is IFormulaHyperlinkEvaluationContext
            hyperlinkContext)
        {
            hyperlinkContext.SetCurrentFormulaHyperlink(
                new FormulaHyperlink(linkLocation, displayValue));
        }

        return FormulaEvaluationResult.Success(displayValue);
    }
}
