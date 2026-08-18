namespace NeraSpreadSheet.Core;

public static class WorksheetSnapshotExtensions
{
    public static WorksheetSnapshot CaptureSnapshot(this Worksheet worksheet)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        var axisStyles = worksheet.CaptureAxisStyleState();
        return new WorksheetSnapshot(
            worksheet.EnumerateUsedCells().ToDictionary(),
            worksheet.Dimensions.CaptureSnapshot(),
            worksheet.MergedCells.CaptureSnapshot(),
            worksheet.Version,
            axisStyles.RowSpans,
            axisStyles.ColumnSpans);
    }
}
