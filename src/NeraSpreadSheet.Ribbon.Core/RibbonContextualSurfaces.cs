using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Ribbon.Core;

/// <summary>Identifies the worksheet-selection state required by a contextual tab.</summary>
public enum RibbonContextRequirement
{
    Always,
    Selection,
    Table,
}

/// <summary>Host-neutral selection state used to project contextual Ribbon tabs.</summary>
public readonly record struct RibbonSelectionContext(bool HasSelection, bool IsInTable)
{
    public static RibbonSelectionContext None { get; } = new(false, false);
}

/// <summary>Associates an existing tab with contextual visibility and a top-level key tip.</summary>
public sealed record RibbonContextualTabRule
{
    public RibbonContextualTabRule(
        string tabId,
        RibbonContextRequirement requirement,
        string? keyTip = null)
    {
        if (string.IsNullOrWhiteSpace(tabId))
        {
            throw new ArgumentException("A contextual tab id is required.", nameof(tabId));
        }
        if (!Enum.IsDefined(requirement) || requirement == RibbonContextRequirement.Always)
        {
            throw new ArgumentOutOfRangeException(nameof(requirement));
        }
        TabId = tabId.Trim();
        Requirement = requirement;
        KeyTip = RibbonKeyTip.NormalizeOptional(keyTip, nameof(keyTip));
    }

    public string TabId { get; }
    public RibbonContextRequirement Requirement { get; }
    public string? KeyTip { get; }

    internal bool IsVisible(RibbonSelectionContext context) => Requirement switch
    {
        RibbonContextRequirement.Selection => context.HasSelection,
        RibbonContextRequirement.Table => context.HasSelection && context.IsInTable,
        _ => true,
    };
}

/// <summary>One stable command identity exposed through QAT or backstage.</summary>
public sealed record RibbonCommandSurfaceItem
{
    public RibbonCommandSurfaceItem(CommandId commandId, string keyTip)
    {
        CommandId = commandId;
        KeyTip = RibbonKeyTip.NormalizeRequired(keyTip, nameof(keyTip));
    }

    public CommandId CommandId { get; }
    public string KeyTip { get; }
}

internal sealed record RibbonKeyTipEntry(string Id, string KeyTip);

internal static class RibbonKeyTip
{
    internal static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A key tip is required.", parameterName);
        }
        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length > 4 || normalized.Any(static character => !char.IsLetterOrDigit(character)))
        {
            throw new ArgumentException(
                "A key tip must contain one to four letters or digits.",
                parameterName);
        }
        return normalized;
    }

    internal static string? NormalizeOptional(string? value, string parameterName) =>
        string.IsNullOrWhiteSpace(value) ? null : NormalizeRequired(value, parameterName);

    internal static string CreateDefault(string caption, int index)
    {
        var letter = caption.FirstOrDefault(char.IsLetterOrDigit);
        return letter == default ? $"T{index + 1}" : char.ToUpperInvariant(letter).ToString();
    }

    internal static void ValidateScope(
        IEnumerable<RibbonCommandSurfaceItem> items,
        string scope) =>
        ValidateScope(
            items.Select(static item => new RibbonKeyTipEntry(
                item.CommandId.Value,
                item.KeyTip)),
            scope);

    internal static void ValidateScope(IEnumerable<RibbonKeyTipEntry> entries, string scope)
    {
        var materialized = entries.ToArray();
        string[] collisions = materialized
            .Where((entry, index) => materialized.Skip(index + 1).Any(other =>
                entry.KeyTip.StartsWith(other.KeyTip, StringComparison.OrdinalIgnoreCase) ||
                other.KeyTip.StartsWith(entry.KeyTip, StringComparison.OrdinalIgnoreCase)))
            .Select(static entry => entry.KeyTip)
            .ToArray();
        if (collisions.Length > 0)
        {
            throw new InvalidOperationException(
                $"Key-tip collision in {scope}: {string.Join(", ", collisions)}.");
        }
    }
}
