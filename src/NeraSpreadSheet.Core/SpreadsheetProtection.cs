using System.Runtime.CompilerServices;

namespace NeraSpreadSheet.Core;

/// <summary>Excel-compatible worksheet protection capabilities. Permission properties are
/// expressed positively even though SpreadsheetML stores several of them as prohibition flags.</summary>
public sealed record WorksheetProtectionSettings
{
    public bool Enabled { get; init; }
    public string? PasswordHash { get; init; }
    public bool SelectLockedCells { get; init; } = true;
    public bool SelectUnlockedCells { get; init; } = true;
    public bool AllowFormatCells { get; init; }
    public bool AllowFormatColumns { get; init; }
    public bool AllowFormatRows { get; init; }
    public bool AllowInsertColumns { get; init; }
    public bool AllowInsertRows { get; init; }
    public bool AllowInsertHyperlinks { get; init; }
    public bool AllowDeleteColumns { get; init; }
    public bool AllowDeleteRows { get; init; }
    public bool AllowSort { get; init; }
    public bool AllowAutoFilter { get; init; }
    public bool AllowPivotTables { get; init; }
    public bool AllowEditObjects { get; init; }
    public bool AllowEditScenarios { get; init; }
}

public sealed record WorkbookProtectionSettings
{
    public bool Enabled { get; init; }
    public bool LockStructure { get; init; } = true;
    public bool LockWindows { get; init; }
    public string? PasswordHash { get; init; }
}

/// <summary>Legacy Excel password hash used by sheetProtection/workbookProtection.
/// This is compatibility metadata, not cryptographic security and plaintext is never retained.</summary>
public static class SpreadsheetProtectionPassword
{
    public static string? HashLegacy(string? password)
    {
        if (string.IsNullOrEmpty(password)) return null;
        if (password.Length > 255) throw new ArgumentOutOfRangeException(nameof(password));
        ushort hash = 0;
        for (var index = password.Length - 1; index >= 0; index--)
        {
            hash = Rotate15(hash);
            hash ^= password[index];
        }
        hash = Rotate15(hash);
        hash ^= (ushort)password.Length;
        hash ^= 0xCE4B;
        return hash.ToString("X4", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static ushort Rotate15(ushort value) =>
        (ushort)(((value >> 14) & 0x0001) | ((value << 1) & 0x7FFF));
}

public static class SpreadsheetProtectionExtensions
{
    private sealed class WorksheetBox { public WorksheetProtectionSettings Value { get; set; } = new(); }
    private sealed class WorkbookBox { public WorkbookProtectionSettings Value { get; set; } = new(); }
    private static readonly ConditionalWeakTable<Worksheet, WorksheetBox> WorksheetState = new();
    private static readonly ConditionalWeakTable<Workbook, WorkbookBox> WorkbookState = new();

    public static WorksheetProtectionSettings GetProtectionSettings(this Worksheet worksheet)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        return WorksheetState.GetOrCreateValue(worksheet).Value;
    }

    public static void SetProtectionSettings(this Worksheet worksheet, WorksheetProtectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentNullException.ThrowIfNull(settings);
        ValidateHash(settings.PasswordHash, nameof(settings));
        WorksheetState.GetOrCreateValue(worksheet).Value = settings;
    }

    public static WorkbookProtectionSettings GetProtectionSettings(this Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        return WorkbookState.GetOrCreateValue(workbook).Value;
    }

    public static void SetProtectionSettings(this Workbook workbook, WorkbookProtectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(settings);
        ValidateHash(settings.PasswordHash, nameof(settings));
        WorkbookState.GetOrCreateValue(workbook).Value = settings;
    }

    public static bool VerifyPassword(this WorksheetProtectionSettings settings, string? password) =>
        !settings.Enabled || string.Equals(settings.PasswordHash, SpreadsheetProtectionPassword.HashLegacy(password), StringComparison.OrdinalIgnoreCase);

    public static bool VerifyPassword(this WorkbookProtectionSettings settings, string? password) =>
        !settings.Enabled || string.Equals(settings.PasswordHash, SpreadsheetProtectionPassword.HashLegacy(password), StringComparison.OrdinalIgnoreCase);

    private static void ValidateHash(string? hash, string parameterName)
    {
        if (hash is null) return;
        if (hash.Length != 4 || !ushort.TryParse(hash, System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture, out _))
            throw new ArgumentException("Legacy Excel protection hashes must be four hexadecimal characters.", parameterName);
    }
}
