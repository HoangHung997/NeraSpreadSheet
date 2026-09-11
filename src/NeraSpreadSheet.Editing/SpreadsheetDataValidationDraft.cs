using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

/// <summary>Selection-bound draft that replaces overlapping validation rules in one history entry.</summary>
public sealed class SpreadsheetDataValidationDraft : IDisposable
{
    private readonly SpreadsheetSession _session;
    private readonly Worksheet _worksheet;
    private readonly CellRange[] _ranges;
    private readonly DataValidationRule[] _before;
    private readonly long _version;
    private bool _invalidated;
    private bool _disposed;

    public SpreadsheetDataValidationDraft(SpreadsheetSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        if (session.Editor.IsEditing) throw new InvalidOperationException("Commit or cancel the cell editor before editing data validation.");
        _worksheet = session.ActiveWorksheet;
        _ranges = session.Selection.Ranges.ToArray();
        if (_ranges.Length == 0) throw new InvalidOperationException("Select at least one range before editing data validation.");
        _before = _worksheet.DataValidationRules.Select(static rule => rule.Copy()).ToArray();
        _version = _worksheet.Version;
        session.Selection.Changed += Invalidate;
        session.ActiveWorksheetChanged += Invalidate;
        _worksheet.CellsChanged += Invalidate;
    }

    public IReadOnlyList<CellRange> TargetRanges => _ranges;

    public DataValidationRule? ExistingRule
    {
        get
        {
            var active = _session.Selection.ActiveCell;
            return _before.FirstOrDefault(rule => rule.AppliesTo(active));
        }
    }

    public bool IsCurrent => !_disposed && !_invalidated && !_session.Editor.IsEditing &&
        ReferenceEquals(_session.ActiveWorksheet, _worksheet) && _worksheet.Version == _version;

    public bool Apply(DataValidationRule? rule)
    {
        if (!IsCurrent) throw new InvalidOperationException("The validation target changed. Reopen the dialog.");
        var retained = _before.Where(existing => !existing.Ranges.Any(existingRange => _ranges.Any(target => target.Intersects(existingRange))))
            .Select(static existing => existing.Copy()).ToList();
        if (rule is not null)
        {
            var replacement = new DataValidationRule(
                rule.Id,
                _ranges,
                rule.Type,
                rule.Operator,
                rule.Formula1,
                rule.Formula2,
                rule.AllowBlank,
                rule.ShowInputMessage,
                rule.PromptTitle,
                rule.Prompt,
                rule.ShowErrorMessage,
                rule.ErrorStyle,
                rule.ErrorTitle,
                rule.Error,
                rule.ShowDropDown);
            retained.Add(replacement);
        }
        var after = retained.ToArray();
        if (Equivalent(_before, after)) { Dispose(); return false; }
        _session.Execute(new ReplaceValidationRulesOperation(_worksheet, _before, after, SignalRange(_ranges)));
        Dispose();
        return true;
    }

    private static bool Equivalent(DataValidationRule[] left, DataValidationRule[] right) =>
        left.Length == right.Length && left.Zip(right).All(pair => pair.First == pair.Second);

    private static CellRange SignalRange(IReadOnlyList<CellRange> ranges) => new(
        new CellAddress(ranges.Min(static range => range.Top), ranges.Min(static range => range.Left)),
        new CellAddress(ranges.Max(static range => range.Bottom), ranges.Max(static range => range.Right)));

    private void Invalidate(object? sender, EventArgs e) => _invalidated = true;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _session.Selection.Changed -= Invalidate;
        _session.ActiveWorksheetChanged -= Invalidate;
        _worksheet.CellsChanged -= Invalidate;
    }

    private sealed class ReplaceValidationRulesOperation(
        Worksheet worksheet,
        DataValidationRule[] before,
        DataValidationRule[] after,
        CellRange signal) : ISpreadsheetEditOperation
    {
        public Worksheet Worksheet => worksheet;
        public CellRange AffectedRange => signal;
        public bool AffectsCalculation => false;
        public string Description => "Data validation";
        public void Execute() => worksheet.RestoreDataValidations(after, signal);
        public void Undo() => worksheet.RestoreDataValidations(before, signal);
    }
}
