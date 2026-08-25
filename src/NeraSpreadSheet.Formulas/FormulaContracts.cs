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
    Spill,
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
            CellValue.FromError(errorCode switch
            {
                FormulaErrorCode.DivisionByZero => "#DIV/0!",
                FormulaErrorCode.InvalidReference => "#REF!",
                FormulaErrorCode.InvalidName => "#NAME?",
                FormulaErrorCode.InvalidValue => "#VALUE!",
                FormulaErrorCode.CircularReference => "#CIRC!",
                FormulaErrorCode.NotAvailable => "#N/A",
                FormulaErrorCode.Spill => "#SPILL!",
                _ => "#VALUE!",
            }),
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

/// <summary>
/// Exposes the current formula-cell identity and formula text without forcing
/// value evaluation. Reference-introspection functions use this optional
/// contract to preserve laziness and exact metadata dependencies.
/// </summary>
public interface IFormulaReferenceIntrospectionContext
    : IFormulaEvaluationContext
{
    string CurrentWorksheetName { get; }

    CellAddress CurrentCellAddress { get; }

    bool TryGetCellFormula(
        string? worksheetName,
        CellAddress address,
        out string? formula);
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
