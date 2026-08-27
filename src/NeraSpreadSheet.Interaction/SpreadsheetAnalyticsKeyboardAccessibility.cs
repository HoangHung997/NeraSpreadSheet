using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Interaction;

[Flags]
public enum SpreadsheetAnalyticsKeyboardModifiers
{
    None = 0,
    Shift = 1,
    Control = 2,
}

public enum SpreadsheetAnalyticsKeyboardKey
{
    Left,
    Right,
    Up,
    Down,
    Delete,
    Escape,
}

public enum SpreadsheetAnalyticsKeyboardAction
{
    None,
    Move,
    Resize,
    Delete,
    CancelOrClearSelection,
}

public readonly record struct SpreadsheetAnalyticsKeyboardIntent(
    SpreadsheetAnalyticsKeyboardAction Action,
    double DeltaX,
    double DeltaY)
{
    public bool IsHandled => Action != SpreadsheetAnalyticsKeyboardAction.None;
}

/// <summary>
/// Host-neutral keyboard contract for floating analytics. Desktop and MAUI hosts map
/// native key events into this contract instead of duplicating transform semantics.
/// Shift+Arrow resizes the corresponding edge, Control increases the nudge step.
/// </summary>
public static class SpreadsheetAnalyticsKeyboardMapper
{
    public const double DefaultNudgeStep = 1d;
    public const double AcceleratedNudgeStep = 10d;

    public static SpreadsheetAnalyticsKeyboardIntent Map(
        SpreadsheetAnalyticsKeyboardKey key,
        SpreadsheetAnalyticsKeyboardModifiers modifiers = SpreadsheetAnalyticsKeyboardModifiers.None)
    {
        if (!Enum.IsDefined(key))
        {
            throw new ArgumentOutOfRangeException(nameof(key));
        }
        if ((modifiers & ~(SpreadsheetAnalyticsKeyboardModifiers.Shift |
                          SpreadsheetAnalyticsKeyboardModifiers.Control)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(modifiers));
        }

        if (key == SpreadsheetAnalyticsKeyboardKey.Delete)
        {
            return new SpreadsheetAnalyticsKeyboardIntent(
                SpreadsheetAnalyticsKeyboardAction.Delete,
                0d,
                0d);
        }
        if (key == SpreadsheetAnalyticsKeyboardKey.Escape)
        {
            return new SpreadsheetAnalyticsKeyboardIntent(
                SpreadsheetAnalyticsKeyboardAction.CancelOrClearSelection,
                0d,
                0d);
        }

        var step = (modifiers & SpreadsheetAnalyticsKeyboardModifiers.Control) != 0
            ? AcceleratedNudgeStep
            : DefaultNudgeStep;
        var delta = key switch
        {
            SpreadsheetAnalyticsKeyboardKey.Left => new PointD(-step, 0d),
            SpreadsheetAnalyticsKeyboardKey.Right => new PointD(step, 0d),
            SpreadsheetAnalyticsKeyboardKey.Up => new PointD(0d, -step),
            SpreadsheetAnalyticsKeyboardKey.Down => new PointD(0d, step),
            _ => default,
        };
        var action = (modifiers & SpreadsheetAnalyticsKeyboardModifiers.Shift) != 0
            ? SpreadsheetAnalyticsKeyboardAction.Resize
            : SpreadsheetAnalyticsKeyboardAction.Move;
        return new SpreadsheetAnalyticsKeyboardIntent(action, delta.X, delta.Y);
    }
}

public enum SpreadsheetAnalyticsAccessibleRole
{
    Chart,
    PivotTable,
}

public sealed record SpreadsheetAnalyticsAccessibleNode(
    SpreadsheetAnalyticsItemKey Item,
    string AutomationId,
    string Name,
    SpreadsheetAnalyticsAccessibleRole Role,
    RectD ViewportBounds,
    RectD ClipBounds,
    int ZIndex,
    bool IsSelected,
    bool IsPartiallyClipped,
    IReadOnlyList<string> Actions);

/// <summary>
/// Produces deterministic accessibility metadata from the same interaction targets used
/// for hit-testing. Hosts can expose these nodes through UI Automation / platform semantics
/// without creating one native control per chart or pivot.
/// </summary>
public static class SpreadsheetAnalyticsAccessibilityProjector
{
    private static readonly string[] DefaultActions = ["Select", "Move", "Resize", "Delete"];

    public static IReadOnlyList<SpreadsheetAnalyticsAccessibleNode> Project(
        IReadOnlyList<SpreadsheetAnalyticsInteractionTarget> targets,
        SpreadsheetAnalyticsItemKey? selectedItem,
        Func<SpreadsheetAnalyticsItemKey, string?>? nameResolver = null)
    {
        ArgumentNullException.ThrowIfNull(targets);

        return targets
            .OrderBy(static target => target.ZIndex)
            .ThenBy(static target => target.Item.Kind)
            .ThenBy(static target => target.Item.Id)
            .Select(target => CreateNode(target, selectedItem, nameResolver))
            .ToArray();
    }

    private static SpreadsheetAnalyticsAccessibleNode CreateNode(
        SpreadsheetAnalyticsInteractionTarget target,
        SpreadsheetAnalyticsItemKey? selectedItem,
        Func<SpreadsheetAnalyticsItemKey, string?>? nameResolver)
    {
        var resolvedName = nameResolver?.Invoke(target.Item);
        var role = target.Item.Kind switch
        {
            SpreadsheetAnalyticsItemKind.Chart => SpreadsheetAnalyticsAccessibleRole.Chart,
            SpreadsheetAnalyticsItemKind.Pivot => SpreadsheetAnalyticsAccessibleRole.PivotTable,
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };
        var fallbackName = role == SpreadsheetAnalyticsAccessibleRole.Chart
            ? "Chart"
            : "Pivot table";
        var name = string.IsNullOrWhiteSpace(resolvedName)
            ? fallbackName
            : resolvedName.Trim();
        var clipped = target.ViewportBounds.Intersect(target.ClipBounds) != target.ViewportBounds;
        return new SpreadsheetAnalyticsAccessibleNode(
            target.Item,
            $"analytics-{target.Item.Kind.ToString().ToLowerInvariant()}-{target.Item.Id:N}",
            name,
            role,
            target.ViewportBounds,
            target.ClipBounds,
            target.ZIndex,
            selectedItem.HasValue && selectedItem.Value == target.Item,
            clipped,
            DefaultActions);
    }
}
