using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

public sealed class SpreadsheetProtectionController
{
    private readonly SpreadsheetSession _session;

    public SpreadsheetProtectionController(SpreadsheetSession session) =>
        _session = session ?? throw new ArgumentNullException(nameof(session));

    public WorksheetProtectionSettings WorksheetSettings =>
        _session.ActiveWorksheet.GetProtectionSettings();

    public WorkbookProtectionSettings WorkbookSettings =>
        _session.Workbook.GetProtectionSettings();

    public void SetWorksheetProtection(WorksheetProtectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var before = _session.ActiveWorksheet.GetProtectionSettings();
        if (before == settings) return;
        _session.Execute(new WorksheetProtectionOperation(_session.ActiveWorksheet, before, settings));
    }

    public void SetWorkbookProtection(WorkbookProtectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var before = _session.Workbook.GetProtectionSettings();
        if (before == settings) return;
        _session.History.Execute(new WorkbookProtectionOperation(
            _session.Workbook,
            _session.ActiveWorksheet,
            before,
            settings));
    }

    private sealed class WorksheetProtectionOperation(
        Worksheet worksheet,
        WorksheetProtectionSettings before,
        WorksheetProtectionSettings after) : ISpreadsheetEditOperation
    {
        public Worksheet Worksheet => worksheet;
        public CellRange AffectedRange => new(default, default);
        public bool AffectsCalculation => false;
        public string Description => after.Enabled ? "Protect sheet" : "Unprotect sheet";
        public void Execute() => worksheet.SetProtectionSettings(after);
        public void Undo() => worksheet.SetProtectionSettings(before);
    }

    private sealed class WorkbookProtectionOperation(
        Workbook workbook,
        Worksheet worksheet,
        WorkbookProtectionSettings before,
        WorkbookProtectionSettings after) : ISpreadsheetEditOperation
    {
        public Worksheet Worksheet => worksheet;
        public CellRange AffectedRange => new(default, default);
        public bool AffectsCalculation => false;
        public string Description => after.Enabled ? "Protect workbook" : "Unprotect workbook";
        public void Execute() => workbook.SetProtectionSettings(after);
        public void Undo() => workbook.SetProtectionSettings(before);
    }
}
