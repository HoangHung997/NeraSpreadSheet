using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

public enum FormulaErrorCode
{
    None = 0,
    DivisionByZero,
    InvalidReference,
    InvalidName,
    InvalidValue,
    CircularReference,
    NotAvailable,
}

public readonly record struct FormulaDependency(
    string? WorksheetName,
    CellRange Range);

public sealed record FormulaEvaluationResult(
    CellValue Value,
    FormulaErrorCode ErrorCode,
    IReadOnlyList<FormulaDependency> Dependencies)
{
    public bool IsSuccess => ErrorCode == FormulaErrorCode.None;

    public static FormulaEvaluationResult Success(
        CellValue value,
        IReadOnlyList<FormulaDependency>? dependencies = null) =>
        new(
            value,
            FormulaErrorCode.None,
            dependencies ?? Array.Empty<FormulaDependency>());

    public static FormulaEvaluationResult Failure(
        FormulaErrorCode errorCode) =>
        new(
            CellValue.FromError($"#{errorCode}"),
            errorCode,
            Array.Empty<FormulaDependency>());
}

public interface IFormulaEvaluationContext
{
    CellValue GetCellValue(
        string? worksheetName,
        CellAddress address);
}

public interface IStructuredReferenceEvaluationContext
    : IFormulaEvaluationContext
{
    string ExpandStructuredReferences(string formula);
}

public interface IFilterAwareFormulaEvaluationContext
    : IFormulaEvaluationContext
{
    bool IsRowVisible(
        string? worksheetName,
        int rowIndex);

    IReadOnlyList<FormulaDependency> GetRowVisibilityDependencies(
        string? worksheetName,
        CellRange referencedRange);
}

public interface IFormulaEngine
{
    FormulaEvaluationResult Evaluate(
        string formula,
        IFormulaEvaluationContext context);
}

public interface IFormulaFunction
{
    string Name { get; }

    FormulaEvaluationResult Invoke(
        IReadOnlyList<CellValue> arguments,
        IFormulaEvaluationContext context);
}

public interface IFormulaFunctionRegistry
{
    bool TryResolve(
        string name,
        out IFormulaFunction formulaFunction);
}
