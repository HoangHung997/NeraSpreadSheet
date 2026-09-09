using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

/// <summary>One history entry composing the existing cell operation with merge/validation snapshots.</summary>
internal sealed class SpreadsheetClipboardPasteOperation : ISpreadsheetEditOperation
{
    private readonly SetCellsOperation _cells;
    private readonly CellRange[] _beforeMerges;
    private readonly CellRange[] _outsideMerges;
    private readonly CellRange[] _afterMerges;
    private readonly DataValidationRule[] _beforeRules;
    private readonly DataValidationRule[] _afterRules;
    private readonly bool _changesMetadata;

    public SpreadsheetClipboardPasteOperation(Worksheet worksheet, CellRange target,
        IEnumerable<KeyValuePair<CellAddress, CellData>> updates, SpreadsheetClipboardPasteMode mode,
        CellRange[] sourceMerges, IReadOnlyList<DataValidationRule> sourceRules,
        CellRange sourceRange, CellAddress destination)
    {
        Worksheet = worksheet;
        AffectedRange = target;
        AffectsCalculation = mode != SpreadsheetClipboardPasteMode.Formats;
        Description = $"Paste {mode}";
        _cells = new SetCellsOperation(worksheet, updates, Description);
        _beforeMerges = worksheet.MergedCells.Ranges.ToArray();
        _outsideMerges = _beforeMerges.Where(range => !range.Intersects(target)).ToArray();
        _beforeRules = worksheet.DataValidationRules.Select(rule => rule.Copy()).ToArray();
        _afterMerges = mode == SpreadsheetClipboardPasteMode.All
            ? _outsideMerges.Concat(sourceMerges).ToArray() : _beforeMerges;
        _afterRules = mode == SpreadsheetClipboardPasteMode.All
            ? CreateRules(_beforeRules, sourceRules, sourceRange, target, destination) : _beforeRules;
        if (_afterRules.Length > WorksheetDataValidationCollection.MaxRulesPerWorksheet)
            throw new InvalidOperationException("Paste would exceed the worksheet validation-rule limit.");
        _changesMetadata = mode == SpreadsheetClipboardPasteMode.All &&
            (sourceMerges.Length > 0 || _beforeMerges.Any(range => range.Intersects(target)) ||
             !_beforeRules.SequenceEqual(_afterRules));
    }

    public string Description { get; }
    public Worksheet Worksheet { get; }
    public CellRange AffectedRange { get; }
    public bool AffectsCalculation { get; }

    public void Execute()
    {
        if (!_changesMetadata)
        {
            _cells.Execute();
            return;
        }
        try
        {
            Apply(original: false);
        }
        catch (Exception failure)
        {
            try { Apply(original: true); }
            catch (Exception recovery) { throw new AggregateException("Paste and recovery both failed.", failure, recovery); }
            throw;
        }
    }

    public void Undo()
    {
        if (!_changesMetadata)
        {
            _cells.Undo();
            return;
        }
        try
        {
            Apply(original: true);
        }
        catch (Exception failure)
        {
            try { Apply(original: false); }
            catch (Exception recovery) { throw new AggregateException("Paste undo and recovery both failed.", failure, recovery); }
            throw;
        }
    }

    private void Apply(bool original)
    {
        // Remove only fully covered merges while cell data is restored. Otherwise the
        // model would redirect writes for interior cells to the merged anchor.
        Worksheet.MergedCells.ReplaceAll(_outsideMerges);
        if (original) _cells.Undo(); else _cells.Execute();
        Worksheet.MergedCells.ReplaceAll(original ? _beforeMerges : _afterMerges);
        Worksheet.RestoreDataValidations(original ? _beforeRules : _afterRules, AffectedRange);
    }

    private static DataValidationRule[] CreateRules(DataValidationRule[] existing,
        IReadOnlyList<DataValidationRule> source, CellRange sourceRange, CellRange target, CellAddress destination)
    {
        var result = new List<DataValidationRule>();
        foreach (var rule in existing)
        {
            if (!rule.Ranges.Any(range => range.Intersects(target)))
            {
                result.Add(rule);
                continue;
            }
            var ranges = rule.Ranges.SelectMany(range => Subtract(range, target)).ToArray();
            if (ranges.Length == 0) continue;
            var anchor = ranges.OrderBy(range => range.Top).ThenBy(range => range.Left).First().TopLeft;
            result.Add(CloneRule(rule, rule.Id, ranges, anchor));
        }
        foreach (var rule in source)
        {
            var ranges = rule.Ranges.Where(range => range.Intersects(sourceRange)).Select(range =>
            {
                var clipped = Intersect(range, sourceRange);
                return new CellRange(
                    new CellAddress(destination.RowIndex + clipped.Top - sourceRange.Top,
                        destination.ColumnIndex + clipped.Left - sourceRange.Left),
                    new CellAddress(destination.RowIndex + clipped.Bottom - sourceRange.Top,
                        destination.ColumnIndex + clipped.Right - sourceRange.Left));
            }).ToArray();
            if (ranges.Length == 0) continue;
            var anchor = ranges.OrderBy(range => range.Top).ThenBy(range => range.Left).First().TopLeft;
            result.Add(CloneRule(rule, Guid.NewGuid(), ranges, anchor));
        }
        return result.ToArray();
    }

    private static DataValidationRule CloneRule(DataValidationRule rule, Guid id,
        CellRange[] ranges, CellAddress anchor) => new(
        id, ranges, rule.Type, rule.Operator,
        FormulaReferenceTranslator.Translate(rule.Formula1, rule.Anchor, anchor),
        rule.Formula2 is null ? null : FormulaReferenceTranslator.Translate(rule.Formula2, rule.Anchor, anchor),
        rule.AllowBlank, rule.ShowInputMessage, rule.PromptTitle, rule.Prompt,
        rule.ShowErrorMessage, rule.ErrorStyle, rule.ErrorTitle, rule.Error, rule.ShowDropDown);

    private static CellRange Intersect(CellRange left, CellRange right) => new(
        new CellAddress(Math.Max(left.Top, right.Top), Math.Max(left.Left, right.Left)),
        new CellAddress(Math.Min(left.Bottom, right.Bottom), Math.Min(left.Right, right.Right)));

    private static IEnumerable<CellRange> Subtract(CellRange range, CellRange removed)
    {
        if (!range.Intersects(removed)) { yield return range; yield break; }
        var overlap = Intersect(range, removed);
        if (range.Top < overlap.Top)
            yield return new(range.TopLeft, new CellAddress(overlap.Top - 1, range.Right));
        if (range.Bottom > overlap.Bottom)
            yield return new(new CellAddress(overlap.Bottom + 1, range.Left), range.BottomRight);
        if (range.Left < overlap.Left)
            yield return new(new CellAddress(overlap.Top, range.Left), new CellAddress(overlap.Bottom, overlap.Left - 1));
        if (range.Right > overlap.Right)
            yield return new(new CellAddress(overlap.Top, overlap.Right + 1), new CellAddress(overlap.Bottom, range.Right));
    }
}
