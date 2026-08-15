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

    internal SpreadsheetClipboardPackage(string sourceWorksheetName, CellRange sourceRange, IEnumerable<SpreadsheetClipboardCell> cells)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceWorksheetName);
        ArgumentNullException.ThrowIfNull(cells);
        SourceWorksheetName = sourceWorksheetName;
        SourceRange = sourceRange;
        _cells = cells.ToDictionary(cell => (cell.RowOffset, cell.ColumnOffset));
    }

    public string SourceWorksheetName { get; }
    public CellRange SourceRange { get; }
    public int RowCount => SourceRange.RowCount;
    public int ColumnCount => SourceRange.ColumnCount;
    public int UsedCellCount => _cells.Count;
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

    internal bool TryGetStoredCell(int rowOffset, int columnOffset, out SpreadsheetClipboardCell cell) =>
        _cells.TryGetValue((rowOffset, columnOffset), out cell!);
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
        EnsureMaterializationLimit(range);
        var cells = _session.ActiveWorksheet.EnumerateUsedCells()
            .Where(pair => range.Contains(pair.Key))
            .Select(pair => new SpreadsheetClipboardCell(
                pair.Key.RowIndex - range.Top,
                pair.Key.ColumnIndex - range.Left,
                pair.Key,
                pair.Value))
            .ToArray();
        Clipboard = new SpreadsheetClipboardPackage(_session.ActiveWorksheet.Name, range, cells);
        return Clipboard;
    }

    public bool CutPrimarySelection()
    {
        CopyPrimarySelection();
        return _session.ClearSelection();
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

        var updates = new List<KeyValuePair<CellAddress, CellData>>(checked(Clipboard.RowCount * Clipboard.ColumnCount));
        for (var rowOffset = 0; rowOffset < Clipboard.RowCount; rowOffset++)
        {
            for (var columnOffset = 0; columnOffset < Clipboard.ColumnCount; columnOffset++)
            {
                var targetAddress = new CellAddress(destination.RowIndex + rowOffset, destination.ColumnIndex + columnOffset);
                CellData data;
                if (Clipboard.TryGetStoredCell(rowOffset, columnOffset, out var stored))
                {
                    var formula = stored.Data.Formula is null
                        ? null
                        : FormulaReferenceTranslator.Translate(stored.Data.Formula, stored.SourceAddress, targetAddress);
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
        var pastedRange = new CellRange(
            destination,
            new CellAddress(
                destination.RowIndex + Clipboard.RowCount - 1,
                destination.ColumnIndex + Clipboard.ColumnCount - 1));
        _session.Selection.Select(pastedRange);
        return true;
    }

    private void EnsureMaterializationLimit(CellRange range)
    {
        var cellCount = checked((long)range.RowCount * range.ColumnCount);
        if (cellCount > _maximumMaterializedCells)
        {
            throw new InvalidOperationException($"Clipboard range contains {cellCount.ToString(System.Globalization.CultureInfo.InvariantCulture)} cells, exceeding the configured limit of {_maximumMaterializedCells.ToString(System.Globalization.CultureInfo.InvariantCulture)}.");
        }
    }

    private static void EnsureTargetFits(SpreadsheetClipboardPackage clipboard, CellAddress destination)
    {
        if ((long)destination.RowIndex + clipboard.RowCount > SpreadsheetLimits.MaxRows ||
            (long)destination.ColumnIndex + clipboard.ColumnCount > SpreadsheetLimits.MaxColumns)
        {
            throw new InvalidOperationException("The clipboard range does not fit at the target address.");
        }
    }
}
