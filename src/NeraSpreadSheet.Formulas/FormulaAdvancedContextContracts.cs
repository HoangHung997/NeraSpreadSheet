using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

/// <summary>
/// One field/item constraint supplied to GETPIVOTDATA.
/// </summary>
public readonly record struct FormulaPivotFieldItem(
    string FieldName,
    CellValue Item);

/// <summary>
/// Optional deterministic provider used by GETPIVOTDATA. Pivot ownership and
/// cache invalidation remain outside the scalar formula evaluator.
/// </summary>
public interface IFormulaPivotDataEvaluationContext
    : IFormulaEvaluationContext
{
    bool TryGetPivotData(
        string? worksheetName,
        CellRange pivotTableReference,
        string dataField,
        IReadOnlyList<FormulaPivotFieldItem> fieldItems,
        out CellValue value,
        out IReadOnlyList<FormulaDependency> dependencies);
}

/// <summary>
/// Hyperlink metadata emitted by HYPERLINK while the cell value remains the
/// display value used by existing renderers and serializers.
/// </summary>
public readonly record struct FormulaHyperlink(
    string LinkLocation,
    CellValue DisplayValue);

/// <summary>
/// Optional host-owned sink for formula hyperlink metadata.
/// </summary>
public interface IFormulaHyperlinkEvaluationContext
    : IFormulaEvaluationContext
{
    void SetCurrentFormulaHyperlink(FormulaHyperlink hyperlink);
}
