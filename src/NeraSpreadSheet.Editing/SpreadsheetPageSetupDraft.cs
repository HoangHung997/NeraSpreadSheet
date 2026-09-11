using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

/// <summary>Pending page settings with target/version guards and one shared history entry.
/// Unrelated print areas, repeat titles, headers, and manual breaks remain caller-owned.</summary>
public sealed class SpreadsheetPageSetupDraft : IDisposable
{
    private readonly SpreadsheetSession _session;
    private readonly Worksheet _worksheet;
    private readonly long _version;
    private readonly WorksheetPrintSettings _initial;
    private bool _invalidated;
    private bool _disposed;
    public SpreadsheetPageSetupDraft(SpreadsheetSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        if (session.Editor.IsEditing) throw new InvalidOperationException("Commit the cell editor first.");
        _worksheet = session.ActiveWorksheet; _version = _worksheet.Version;
        _initial = _worksheet.GetPrintSettings();
        session.ActiveWorksheetChanged += Invalidate;
        _worksheet.CellsChanged += Invalidate;
    }
    public WorksheetPrintSettings Initial => _initial.Copy();
    public bool IsCurrent => !_disposed && !_invalidated && !_session.Editor.IsEditing &&
        ReferenceEquals(_worksheet, _session.ActiveWorksheet) && _session.Workbook.Worksheets.Contains(_worksheet) &&
        _worksheet.Version == _version && Equivalent(_initial, _worksheet.GetPrintSettings());
    public bool Apply(WorksheetPrintSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!IsCurrent) throw new InvalidOperationException("The worksheet or its print settings changed. Reopen Page Setup.");
        Validate(settings);
        var changed = !Equivalent(_initial, settings);
        if (changed) _session.Execute(new PageSettingsOperation(_worksheet, _initial.Copy(), settings.Copy()));
        Dispose(); return changed;
    }
    private static void Validate(WorksheetPrintSettings settings)
    {
        var setup = settings.PageSetup;
        ArgumentNullException.ThrowIfNull(setup);
        if (!Enum.IsDefined(setup.Orientation) || !Enum.IsDefined(setup.PageOrder) ||
            !Enum.IsDefined(setup.PrintComments) || !Enum.IsDefined(setup.PrintErrors))
            throw new ArgumentOutOfRangeException(nameof(settings));
        if (!double.IsFinite(setup.ScalePercent) || setup.ScalePercent is < 10 or > 400)
            throw new ArgumentException("Print scaling must be between 10 and 400 percent.", nameof(settings));
        if (setup.FitToPagesWide is <= 0 || setup.FitToPagesTall is <= 0)
            throw new ArgumentException("Fit-to-page counts must be positive or automatic.", nameof(settings));
        if (setup.PrintQualityDpi is <= 0 or > 9600)
            throw new ArgumentException("Print quality must be a positive DPI value up to 9600.", nameof(settings));
        if (setup.FirstPageNumber is <= 0 or > 32767)
            throw new ArgumentException("First page number must be between 1 and 32767 or automatic.", nameof(settings));
        var width = setup.Orientation == SpreadsheetPageOrientation.Landscape ? setup.PaperSize.HeightInches : setup.PaperSize.WidthInches;
        var height = setup.Orientation == SpreadsheetPageOrientation.Landscape ? setup.PaperSize.WidthInches : setup.PaperSize.HeightInches;
        if (width <= 0 || height <= 0 || setup.Margins.LeftInches + setup.Margins.RightInches >= width || setup.Margins.TopInches + setup.Margins.BottomInches >= height)
            throw new ArgumentException("Margins must leave a positive printable area.", nameof(settings));
        ValidateRange(settings.PrintArea, nameof(settings.PrintArea));
        ValidateRange(setup.RepeatTitles.Rows, nameof(setup.RepeatTitles));
        ValidateRange(setup.RepeatTitles.Columns, nameof(setup.RepeatTitles));
    }
    private static void ValidateRange(CellRange? range, string parameterName)
    {
        if (range is not { } value) return;
        if (value.Top < 0 || value.Left < 0 || value.Bottom >= SpreadsheetLimits.MaxRows || value.Right >= SpreadsheetLimits.MaxColumns)
            throw new ArgumentOutOfRangeException(parameterName);
    }
    private static bool Equivalent(WorksheetPrintSettings first, WorksheetPrintSettings second) =>
        first.PrintArea == second.PrintArea &&
        first.PageSetup.ManualRowBreaks.SequenceEqual(second.PageSetup.ManualRowBreaks) &&
        first.PageSetup.ManualColumnBreaks.SequenceEqual(second.PageSetup.ManualColumnBreaks) &&
        first.PageSetup == (second.PageSetup with { ManualRowBreaks = first.PageSetup.ManualRowBreaks, ManualColumnBreaks = first.PageSetup.ManualColumnBreaks });
    private void Invalidate(object? sender, EventArgs args) => _invalidated = true;
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        _session.ActiveWorksheetChanged -= Invalidate; _worksheet.CellsChanged -= Invalidate;
    }
    private sealed class PageSettingsOperation(Worksheet worksheet, WorksheetPrintSettings before, WorksheetPrintSettings after) : ISpreadsheetEditOperation
    {
        public Worksheet Worksheet => worksheet;
        public CellRange AffectedRange => new(default, default);
        public bool AffectsCalculation => false;
        public string Description => "Page setup";
        public void Execute() => worksheet.SetPrintSettings(after);
        public void Undo() => worksheet.SetPrintSettings(before);
    }
}
