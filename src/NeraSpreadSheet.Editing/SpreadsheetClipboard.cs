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
    private bool _isClipboardWritePending;
    private bool _pendingWriteInvalidated;
    private Action? _cancelPendingWrite;

    public SpreadsheetClipboardController(SpreadsheetSession session, long maximumMaterializedCells = DefaultMaximumMaterializedCells)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumMaterializedCells);
        _maximumMaterializedCells = maximumMaterializedCells;
    }

    public SpreadsheetClipboardPackage? Clipboard { get; private set; }
    public bool CanPaste => Clipboard is not null && !_isClipboardWritePending;

    /// <summary>Gets whether a host clipboard write or a synchronous/acknowledged cut is still running.</summary>
    public bool IsClipboardWritePending => _isClipboardWritePending;

    public SpreadsheetClipboardPackage CopyPrimarySelection()
    {
        EnsureClipboardIdle();
        Clipboard = CreatePrimarySelectionPackage();
        return Clipboard;
    }

    private SpreadsheetClipboardPackage CreatePrimarySelectionPackage()
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
        return new SpreadsheetClipboardPackage(
            worksheet.Name,
            range,
            cells);
    }

    /// <summary>
    /// Copies and clears a single selected range using the session clipboard.
    /// </summary>
    /// <remarks>
    /// This synchronous method does not write to the operating-system clipboard.
    /// Clipboard entry points remain busy until source mutation and its callbacks finish.
    /// If a downstream observer throws, the captured package is retained for recovery;
    /// this method does not guarantee rollback of arbitrary observer/history failures.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// A cell edit is active, the source worksheet is detached, the selection contains
    /// more than one range, or existing source validation fails.
    /// </exception>
    public bool CutPrimarySelection()
    {
        EnsureClipboardIdle();
        // Copy captures only Ranges[0], whereas ClearSelection clears every range.
        // Reject before either call so neither data nor the old clipboard is lost.
        if (_session.Selection.Ranges.Count != 1)
        {
            throw new InvalidOperationException(
                "Cannot cut multiple selection ranges. Select one range before cutting.");
        }

        if (_session.Editor.IsEditing)
        {
            throw new InvalidOperationException("Cannot cut worksheet cells while a cell edit is active.");
        }
        if (!_session.Workbook.Worksheets.Contains(_session.ActiveWorksheet))
        {
            throw new InvalidOperationException("The source worksheet is no longer in the workbook.");
        }

        EnsureSourceMergesFullySelected(_session.Selection.Ranges[0]);
        _isClipboardWritePending = true;
        try
        {
            // Do not re-enter the public Copy method while the clipboard is busy.
            // Publish only after source preflight/package creation succeeds, then keep
            // that recovery package safe from reentrant worksheet/history observers.
            Clipboard = CreatePrimarySelectionPackage();
            return _session.ClearSelection();
        }
        finally
        {
            _isClipboardWritePending = false;
        }
    }

    /// <summary>
    /// Writes one selection through a host transport and publishes it only after
    /// acknowledgement. A cut clears its source only while the captured context is valid.
    /// </summary>
    /// <param name="writeAsync">
    /// The host's real clipboard writer. Successful completion must acknowledge the
    /// write, including any required flush. The writer must not change this controller's
    /// clipboard, clear the selection, or start another clipboard operation.
    /// </param>
    /// <param name="cut">Whether to clear the acknowledged source through session history.</param>
    /// <param name="cancellationToken">Cancels the pending operation without clearing its source.</param>
    /// <returns>
    /// True for an accepted copy or a cut that cleared cells. False for a stale context
    /// or an accepted cut of an empty range. Transport failures and cancellation propagate.
    /// </returns>
    /// <remarks>
    /// Call on the session's owning context; this controller is not thread-safe.
    /// The continuation intentionally retains that context. Until the writer settles,
    /// even after cancellation is requested, another clipboard operation is rejected.
    /// Hosts must cancel on detach/disposal and arbitrate Esc against their editor/IME.
    /// This method does not choose an OS format or bypass host protection policy.
    /// </remarks>
    public async ValueTask<bool> CopyToClipboardAsync(
        Func<SpreadsheetClipboardPackage, CancellationToken, ValueTask> writeAsync,
        bool cut = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writeAsync);
        EnsureClipboardIdle();
        cancellationToken.ThrowIfCancellationRequested();
        if (_session.Editor.IsEditing || _session.Selection.Ranges.Count != 1)
        {
            throw new InvalidOperationException(
                "Clipboard writes require one selected range and no active cell edit.");
        }
        var worksheet = _session.ActiveWorksheet;
        if (!_session.Workbook.Worksheets.Contains(worksheet))
        {
            throw new InvalidOperationException("The source worksheet is no longer in the workbook.");
        }
        var range = _session.Selection.Ranges[0];
        if (cut)
        {
            EnsureSourceMergesFullySelected(range);
        }
        var lease = new ClipboardWriteLease(
            worksheet,
            worksheet.Name,
            worksheet.Version,
            _session.Workbook.Version,
            worksheet.Dimensions.Version,
            _session.Selection.Version,
            _session.Selection.ActiveCell,
            _session.Selection.AnchorCell,
            range,
            _session.View.Version,
            worksheet.MergedCells.Ranges.ToArray());
        var package = CreatePrimarySelectionPackage();
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _isClipboardWritePending = true;
        _pendingWriteInvalidated = false;
        _cancelPendingWrite = linkedCancellation.Cancel;
        _session.ActiveWorksheetChanged += OnPendingWriteContextChanged;
        _session.Editor.StateChanged += OnPendingWriteContextChanged;
        try
        {
            linkedCancellation.Token.ThrowIfCancellationRequested();
            // Do not ConfigureAwait(false): mutation must return to the owning UI context.
            await writeAsync(package, linkedCancellation.Token).ConfigureAwait(true);
            linkedCancellation.Token.ThrowIfCancellationRequested();
            if (!IsWriteLeaseCurrent(lease))
            {
                return false;
            }

            // The transport has acknowledged this package. Do not discard it if a
            // later session/history subscriber throws after worksheet mutation begins.
            _cancelPendingWrite = null;
            Clipboard = package;
            return !cut || _session.ClearSelection();
        }
        finally
        {
            _session.ActiveWorksheetChanged -= OnPendingWriteContextChanged;
            _session.Editor.StateChanged -= OnPendingWriteContextChanged;
            _cancelPendingWrite = null;
            _pendingWriteInvalidated = false;
            _isClipboardWritePending = false;
        }
    }

    /// <summary>
    /// Requests cancellation of the current host write without changing the published
    /// clipboard or clearing the OS clipboard. Returns false when no request is pending.
    /// </summary>
    /// <remarks>
    /// Call on the session's owning context. The busy guard remains until the writer
    /// actually finishes, including writers that ignore the cancellation token.
    /// </remarks>
    public bool CancelPendingClipboardWrite()
    {
        if (_cancelPendingWrite is not { } cancel)
        {
            return false;
        }
        _cancelPendingWrite = null;
        _pendingWriteInvalidated = true;
        cancel();
        return true;
    }

    private void OnPendingWriteContextChanged(object? sender, EventArgs e) =>
        _pendingWriteInvalidated = true;

    private bool IsWriteLeaseCurrent(ClipboardWriteLease lease) =>
        !_pendingWriteInvalidated &&
        !_session.Editor.IsEditing &&
        ReferenceEquals(_session.ActiveWorksheet, lease.Worksheet) &&
        _session.Workbook.Worksheets.Contains(lease.Worksheet) &&
        string.Equals(lease.Worksheet.Name, lease.WorksheetName, StringComparison.Ordinal) &&
        lease.Worksheet.Version == lease.WorksheetVersion &&
        _session.Workbook.Version == lease.WorkbookVersion &&
        lease.Worksheet.Dimensions.Version == lease.DimensionVersion &&
        _session.Selection.Version == lease.SelectionVersion &&
        _session.Selection.ActiveCell == lease.ActiveCell &&
        _session.Selection.AnchorCell == lease.AnchorCell &&
        _session.Selection.Ranges.Count == 1 &&
        _session.Selection.Ranges[0] == lease.Range &&
        _session.View.Version == lease.ViewVersion &&
        lease.Worksheet.MergedCells.Ranges.SequenceEqual(lease.MergedRanges);

    private void EnsureClipboardIdle()
    {
        if (_isClipboardWritePending)
        {
            throw new InvalidOperationException("A clipboard write is already in progress.");
        }
    }

    private void EnsureSourceMergesFullySelected(CellRange sourceRange)
    {
        if (_session.ActiveWorksheet.MergedCells.Ranges.Any(range =>
                range.Intersects(sourceRange) && !Contains(sourceRange, range)))
        {
            throw new InvalidOperationException(
                "Cannot cut part of a merged range. Select the complete merged range.");
        }
    }

    private sealed record ClipboardWriteLease(
        Worksheet Worksheet,
        string WorksheetName,
        long WorksheetVersion,
        long WorkbookVersion,
        long DimensionVersion,
        long SelectionVersion,
        CellAddress ActiveCell,
        CellAddress AnchorCell,
        CellRange Range,
        long ViewVersion,
        CellRange[] MergedRanges);

    public SpreadsheetClipboardPackage ImportTabSeparatedText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        EnsureClipboardIdle();
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
        if (Clipboard is null || _isClipboardWritePending)
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
