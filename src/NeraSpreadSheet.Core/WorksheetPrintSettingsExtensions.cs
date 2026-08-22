using System.Runtime.CompilerServices;

namespace NeraSpreadSheet.Core;

public static class WorksheetPrintSettingsExtensions
{
    private static readonly ConditionalWeakTable<Worksheet, Holder>
        Settings = new();

    public static WorksheetPrintSettings GetPrintSettings(
        this Worksheet worksheet)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        var holder = Settings.GetValue(
            worksheet,
            static _ => new Holder());
        lock (holder.Gate)
        {
            return holder.Value.Copy();
        }
    }

    public static void SetPrintSettings(
        this Worksheet worksheet,
        WorksheetPrintSettings settings)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentNullException.ThrowIfNull(settings);
        var holder = Settings.GetValue(
            worksheet,
            static _ => new Holder());
        lock (holder.Gate)
        {
            holder.Value = settings.Copy();
        }
    }

    public static void ResetPrintSettings(this Worksheet worksheet)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        Settings.Remove(worksheet);
    }

    private sealed class Holder
    {
        public object Gate { get; } = new();

        public WorksheetPrintSettings Value { get; set; } = new();
    }
}
