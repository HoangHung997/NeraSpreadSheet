using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

/// <summary>A bounded, non-mutating formatting draft over the existing session.
/// It is invalidated by target changes, even when the user later returns to that target.</summary>
public sealed class SpreadsheetFormatCellsDraft : IDisposable
{
    private const long MaximumInspectedCells = 4096;
    private readonly SpreadsheetSession _session;
    private readonly Worksheet _worksheet;
    private readonly long _worksheetVersion;
    private readonly CellStyle[] _styles;
    private bool _invalidated;
    private bool _completed;
    private bool _disposed;

    public SpreadsheetFormatCellsDraft(SpreadsheetSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        if (session.Editor.IsEditing)
            throw new InvalidOperationException("Commit or cancel the cell editor before opening a formatting draft.");
        _worksheet = session.ActiveWorksheet;
        _worksheetVersion = _worksheet.Version;
        ActiveStyle = session.Styles.ActiveCellStyle;
        PreviewValue = _worksheet.GetCell(_worksheet.ResolveMergedAnchor(session.Selection.ActiveCell)).Value;
        var ranges = session.Selection.Ranges.ToArray();
        var total = ranges.Sum(range => (long)range.RowCount * range.ColumnCount);
        IsSelectionInspected = total is > 0 and <= MaximumInspectedCells;
        var styles = new HashSet<CellStyle>();
        if (IsSelectionInspected)
            foreach (var range in ranges)
                for (var row = range.Top; row <= range.Bottom; row++)
                    for (var column = range.Left; column <= range.Right; column++)
                        styles.Add(_worksheet.GetEffectiveStyle(new CellAddress(row, column), session.Workbook.Styles));
        _styles = styles.ToArray();
        session.Selection.Changed += Invalidate;
        session.ActiveWorksheetChanged += Invalidate;
        _worksheet.CellsChanged += Invalidate;
    }

    public CellStyle ActiveStyle { get; }
    public CellValue PreviewValue { get; }
    /// <summary>False means unknown common values, not a claim that every value differs.
    /// Whole-axis selections are never expanded just to populate a dialog.</summary>
    public bool IsSelectionInspected { get; }
    public bool IsCurrent => !_disposed && !_completed && !_invalidated &&
        ReferenceEquals(_session.ActiveWorksheet, _worksheet) &&
        _session.Workbook.Worksheets.Contains(_worksheet) &&
        _worksheet.Version == _worksheetVersion && !_session.Editor.IsEditing;

    public bool TryGetCommon<T>(Func<CellStyle, T> selector, out T value)
    {
        ArgumentNullException.ThrowIfNull(selector);
        if (_styles.Length != 0)
        {
            value = selector(_styles[0]);
            for (var index = 1; index < _styles.Length; index++)
                if (!EqualityComparer<T>.Default.Equals(value, selector(_styles[index])))
                { value = default!; return false; }
            return true;
        }
        value = default!;
        return false;
    }

    /// <summary>Applies only explicitly chosen properties in one canonical Undo transaction.
    /// Returns false for an untouched draft; does not create an empty history entry.</summary>
    public bool Apply(CellStylePatch patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        if (!IsCurrent) throw new InvalidOperationException("The formatting target changed. Close and reopen the dialog.");
        if (patch.NumberFormatCode is { } code) SpreadsheetNumberFormats.Validate(code);
        // Validate against a temporary catalog, never add preview styles to the workbook.
        _ = new CellStyleCatalog().Intern(patch.Apply(CellStyle.Default));
        var changes = !patch.IsEmpty && (_styles.Length == 0 || _styles.Any(style => patch.Apply(style) != style));
        if (changes) _session.Styles.ApplyPatchToSelection(patch, "Format cells");
        _completed = true;
        Dispose();
        return changes;
    }

    private void Invalidate(object? sender, EventArgs args) => _invalidated = true;
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _session.Selection.Changed -= Invalidate;
        _session.ActiveWorksheetChanged -= Invalidate;
        _worksheet.CellsChanged -= Invalidate;
    }
}
