namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Exposes deterministic workbook-sheet identity for worksheet functions that
/// must resolve the current sheet or a named sheet without depending on a UI
/// host or package serializer.
/// </summary>
public interface IFormulaWorkbookMetadataEvaluationContext :
    IFormulaEvaluationContext
{
    int WorksheetCount { get; }

    bool TryGetWorksheetIndex(
        string? worksheetName,
        out int oneBasedIndex);
}
