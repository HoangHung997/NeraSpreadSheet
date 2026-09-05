using System.Globalization;
using System.Text;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

public sealed record SpreadsheetClipboardCell(
    int RowOffset,
    int ColumnOffset,
    CellAddress SourceAddress,
    CellData Data);

public sealed class SpreadsheetClipboardPackage
{
    private readonly Dictionary<(int Row, int Column), SpreadsheetClipboardCell> _cells;

    internal SpreadsheetClipboardPackage(
        string sourceWorksheetName,
        CellRange sourceRange,
        IEnumerable<SpreadsheetClipboardCell> cells,
        bool translateFormulasOnPaste = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceWorksheetName);
        ArgumentNullException.ThrowIfNull(cells);
        SourceWorksheetName = sourceWorksheetName;
        SourceRange = sourceRange;
        TranslateFormulasOnPaste = translateFormulasOnPaste;
        _cells = cells.ToDictionary(cell => (cell.RowOffset, cell.ColumnOffset));
    }

    public string SourceWorksheetName { get; }
    public CellRange SourceRange { get; }
    public int RowCount => SourceRange.RowCount;
    public int ColumnCount => SourceRange.ColumnCount;
    public int UsedCellCount => _cells.Count;
    public bool TranslateFormulasOnPaste { get; }
    public IReadOnlyCollection<SpreadsheetClipboardCell> Cells => _cells.Values;

    public CellData GetCell(int rowOffset, int columnOffset)
    {
        if (rowOffset < 0 || rowOffset >= RowCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rowOffset));
        }
        if (columnOffset < 0 || columnOffset >= ColumnCount)
        {
            throw new ArgumentOutOfRangeException(nameof(columnOffset));
        }
        return _cells.TryGetValue((rowOffset, columnOffset), out var cell) ? cell.Data : CellData.Empty;
    }

    public string ToTabSeparatedText()
    {
        var builder = new StringBuilder();
        for (var row = 0; row < RowCount; row++)
        {
            if (row > 0)
            {
                builder.Append("\r\n");
            }
            for (var column = 0; column < ColumnCount; column++)
            {
                if (column > 0)
                {
                    builder.Append('\t');
                }
                var cell = GetCell(row, column);
                AppendEscapedField(builder, cell.Formula ?? cell.Value.ToString());
            }
        }
        return builder.ToString();
    }

    internal bool TryGetStoredCell(int rowOffset, int columnOffset, out SpreadsheetClipboardCell cell) =>
        _cells.TryGetValue((rowOffset, columnOffset), out cell!);

    private static void AppendEscapedField(StringBuilder builder, string text)
    {
        if (text.AsSpan().IndexOfAny("\t\r\n\"") < 0)
        {
            builder.Append(text);
            return;
        }

        builder.Append('"');
        foreach (var character in text)
        {
            if (character == '"')
            {
                builder.Append("\"\"");
            }
            else
            {
                builder.Append(character);
            }
        }
        builder.Append('"');
    }
}

public sealed class SpreadsheetClipboardController
{
    public const long DefaultMaximumMaterializedCells = 1_000_000;
    private readonly SpreadsheetSession _session;
    private readonly long _maximumMaterializedCells;

    public SpreadsheetClipboardController(SpreadsheetSession session, long maximumMaterializedCells = DefaultMaximumMaterializedCells)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumMaterializedCells);
        _maximumMaterializedCells = maximumMaterializedCells;
    }

    public SpreadsheetClipboardPackage? Clipboard { get; private set; }
    public bool CanPaste => Clipboard is not null;

    public SpreadsheetClipboardPackage CopyPrimarySelection()
    {
        var range = _session.Selection.Ranges[0];
        EnsureSourceSpillsFullySelected(range);
        EnsureMaterializationLimit(range);
        var worksheet = _session.ActiveWorksheet;
        var cells = new List<SpreadsheetClipboardCell>();
        foreach (var pair in worksheet.EnumerateUsedCells()
                     .Where(pair => range.Contains(pair.Key)))
        {
            var data = pair.Value;
            if (worksheet.TryGetFormulaSpillOwner(pair.Key, out var owner) &&
                owner != pair.Key)
            {
                // Spill children are derived output. Copy only direct child
                // formatting so the pasted owner can regenerate its values.
                if (data.StyleId == CellStyleCatalog.DefaultStyleId)
                {
                    continue;
                }
                data = new CellData(
                    CellValue.Blank,
                    styleId: data.StyleId);
            }

            cells.Add(new SpreadsheetClipboardCell(
                pair.Key.RowIndex - range.Top,
                pair.Key.ColumnIndex - range.Left,
                pair.Key,
                data));
        }
        Clipboard = new SpreadsheetClipboardPackage(
            worksheet.Name,
            range,
            cells);
        return Clipboard;
    }

    public bool CutPrimarySelection()
    {
        CopyPrimarySelection();
        return _session.ClearSelection();
    }

    public SpreadsheetClipboardPackage ImportTabSeparatedText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var rows = ParseTabSeparatedText(text);
        var rowCount = Math.Max(1, rows.Count);
        var columnCount = Math.Max(1, rows.Max(row => row.Count));
        var logicalRange = new CellRange(default, new CellAddress(rowCount - 1, columnCount - 1));
        EnsureMaterializationLimit(logicalRange);

        var cells = new List<SpreadsheetClipboardCell>();
        for (var row = 0; row < rows.Count; row++)
        {
            for (var column = 0; column < rows[row].Count; column++)
            {
                var data = ParseExternalCell(rows[row][column]);
                if (!data.IsEmpty)
                {
                    var sourceAddress = new CellAddress(row, column);
                    cells.Add(new SpreadsheetClipboardCell(row, column, sourceAddress, data));
                }
            }
        }

        Clipboard = new SpreadsheetClipboardPackage(
            "ExternalText",
            logicalRange,
            cells,
            translateFormulasOnPaste: false);
        return Clipboard;
    }

    public bool PasteAtActiveCell() => Paste(_session.Selection.ActiveCell);

    public bool Paste(CellAddress destination)
    {
        if (Clipboard is null)
        {
            return false;
        }
        EnsureTargetFits(Clipboard, destination);
        EnsureMaterializationLimit(Clipboard.SourceRange);
        var pastedRange = CreateTargetRange(Clipboard, destination);
        EnsureTargetDoesNotIntersectSpill(pastedRange);

        var updates = new List<KeyValuePair<CellAddress, CellData>>(checked(Clipboard.RowCount * Clipboard.ColumnCount));
        for (var rowOffset = 0; rowOffset < Clipboard.RowCount; rowOffset++)
        {
            for (var columnOffset = 0; columnOffset < Clipboard.ColumnCount; columnOffset++)
            {
                var targetAddress = new CellAddress(destination.RowIndex + rowOffset, destination.ColumnIndex + columnOffset);
                CellData data;
                if (Clipboard.TryGetStoredCell(rowOffset, columnOffset, out var stored))
                {
                    var formula = stored.Data.Formula;
                    if (formula is not null && Clipboard.TranslateFormulasOnPaste)
                    {
                        formula = FormulaReferenceTranslator.Translate(formula, stored.SourceAddress, targetAddress);
                    }
                    data = new CellData(stored.Data.Value, formula, stored.Data.StyleId);
                }
                else
                {
                    data = CellData.Empty;
                }
                updates.Add(new KeyValuePair<CellAddress, CellData>(targetAddress, data));
            }
        }

        _session.Execute(new SetCellsOperation(_session.ActiveWorksheet, updates, "Paste cells"));
        _session.Selection.Select(pastedRange);
        return true;
    }

    private static CellData ParseExternalCell(string text)
    {
        if (text.Length == 0)
        {
            return CellData.Empty;
        }
        if (text.StartsWith('='))
        {
            return new CellData(CellValue.Blank, text);
        }
        if (bool.TryParse(text, out var boolean))
        {
            return new CellData(CellValue.FromBoolean(boolean));
        }
        if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var localNumber) && double.IsFinite(localNumber))
        {
            return new CellData(CellValue.FromNumber(localNumber));
        }
        if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var invariantNumber) && double.IsFinite(invariantNumber))
        {
            return new CellData(CellValue.FromNumber(invariantNumber));
        }
        return new CellData(CellValue.FromText(text));
    }

    private static List<List<string>> ParseTabSeparatedText(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (quoted)
            {
                if (character == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(character);
                }
                continue;
            }

            if (character == '"' && field.Length == 0)
            {
                quoted = true;
            }
            else if (character == '\t')
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (character is '\r' or '\n')
            {
                if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }
                row.Add(field.ToString());
                field.Clear();
                rows.Add(row);
                row = [];
            }
            else
            {
                field.Append(character);
            }
        }

        row.Add(field.ToString());
        rows.Add(row);
        return rows;
    }

    private void EnsureSourceSpillsFullySelected(CellRange sourceRange)
    {
        foreach (var spill in _session.ActiveWorksheet.GetFormulaSpills())
        {
            if (!spill.Range.Intersects(sourceRange))
            {
                continue;
            }
            if (!Contains(sourceRange, spill.Range))
            {
                throw new InvalidOperationException(
                    $"Cannot copy or cut part of the dynamic-array spill " +
                    $"owned by {spill.Owner.ToA1()}. Select its complete " +
                    $"spill range {spill.Range.TopLeft.ToA1()}:" +
                    $"{spill.Range.BottomRight.ToA1()}.");
            }
        }
    }

    private void EnsureTargetDoesNotIntersectSpill(CellRange targetRange)
    {
        foreach (var spill in _session.ActiveWorksheet.GetFormulaSpills())
        {
            if (!spill.Range.Intersects(targetRange))
            {
                continue;
            }
            throw new InvalidOperationException(
                $"Cannot paste into the dynamic-array spill owned by " +
                $"{spill.Owner.ToA1()}. Clear or replace the owner formula first.");
        }
    }

    private void EnsureMaterializationLimit(CellRange range)
    {
        var cellCount = checked((long)range.RowCount * range.ColumnCount);
        if (cellCount > _maximumMaterializedCells)
        {
            throw new InvalidOperationException($"Clipboard range contains {cellCount.ToString(CultureInfo.InvariantCulture)} cells, exceeding the configured limit of {_maximumMaterializedCells.ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    private static CellRange CreateTargetRange(
        SpreadsheetClipboardPackage clipboard,
        CellAddress destination) =>
        new(
            destination,
            new CellAddress(
                destination.RowIndex + clipboard.RowCount - 1,
                destination.ColumnIndex + clipboard.ColumnCount - 1));

    private static bool Contains(CellRange outer, CellRange inner) =>
        inner.Top >= outer.Top &&
        inner.Left >= outer.Left &&
        inner.Bottom <= outer.Bottom &&
        inner.Right <= outer.Right;

    private static void EnsureTargetFits(SpreadsheetClipboardPackage clipboard, CellAddress destination)
    {
        if ((long)destination.RowIndex + clipboard.RowCount > SpreadsheetLimits.MaxRows ||
            (long)destination.ColumnIndex + clipboard.ColumnCount > SpreadsheetLimits.MaxColumns)
        {
            throw new InvalidOperationException("The clipboard range does not fit at the target address.");
        }
    }
}
