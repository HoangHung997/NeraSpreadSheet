using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

/// <summary>
/// Compatibility facade for editing callers. The actual translator lives in
/// Core so copy/paste, conditional formatting and document adapters share one
/// A1 reference implementation.
/// </summary>
public static class FormulaReferenceTranslator
{
    public static string Translate(
        string formula,
        CellAddress sourceCell,
        CellAddress targetCell) =>
        A1FormulaReferenceTranslator.Translate(
            formula,
            sourceCell,
            targetCell);
}
