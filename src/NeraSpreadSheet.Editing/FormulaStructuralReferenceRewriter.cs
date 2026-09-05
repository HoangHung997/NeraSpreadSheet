using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

/// <summary>
/// Compatibility facade for editing callers. Structural reference rewriting
/// lives in Core so worksheet rules and editing operations use one engine.
/// </summary>
public static class FormulaStructuralReferenceRewriter
{
    public static string Rewrite(
        string formula,
        string formulaWorksheetName,
        string changedWorksheetName,
        WorksheetStructuralChange change) =>
        NeraSpreadSheet.Core.FormulaStructuralReferenceRewriter.Rewrite(
            formula,
            formulaWorksheetName,
            changedWorksheetName,
            change);

    public static string Rewrite(
        string formula,
        string formulaWorksheetName,
        string changedWorksheetName,
        WorksheetAxisMove move) =>
        NeraSpreadSheet.Core.FormulaStructuralReferenceRewriter.Rewrite(
            formula,
            formulaWorksheetName,
            changedWorksheetName,
            move);
}
