using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Wpf;

/// <summary>
/// A snapshot of the existing native editor, including its UTF-16 text selection.
/// Reading a snapshot does not commit, validate or calculate workbook content.
/// </summary>
/// <param name="Address">The canonical cell being edited.</param>
/// <param name="Text">The current uncommitted native text.</param>
/// <param name="SelectionStart">The first selected UTF-16 position.</param>
/// <param name="SelectionLength">The selected UTF-16 length, or zero for a caret.</param>
/// <param name="CaretIndex">The native editor's current UTF-16 caret position.</param>
public sealed record SpreadsheetEditorDraft(
    CellAddress Address,
    string Text,
    int SelectionStart,
    int SelectionLength,
    int CaretIndex);
