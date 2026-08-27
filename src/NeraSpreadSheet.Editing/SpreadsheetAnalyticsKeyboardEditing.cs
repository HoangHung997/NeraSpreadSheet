using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;

namespace NeraSpreadSheet.Editing;

/// <summary>
/// Applies host-neutral analytics keyboard intents to the session's undoable placement model.
/// Hosts keep focus/key plumbing only; bounds/history semantics stay identical everywhere.
/// </summary>
public static class SpreadsheetAnalyticsKeyboardEditing
{
    public static bool Execute(
        SpreadsheetAnalyticsKeyboardIntent intent,
        SpreadsheetAnalyticsInteractionController interaction,
        SpreadsheetAnalyticsPlacementController placements,
        SpreadsheetAnalyticsController analytics,
        double minimumWidth = SpreadsheetAnalyticsTransformMath.DefaultMinimumWidth,
        double minimumHeight = SpreadsheetAnalyticsTransformMath.DefaultMinimumHeight)
    {
        ArgumentNullException.ThrowIfNull(interaction);
        ArgumentNullException.ThrowIfNull(placements);
        ArgumentNullException.ThrowIfNull(analytics);
        if (!double.IsFinite(minimumWidth) || minimumWidth <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumWidth));
        }
        if (!double.IsFinite(minimumHeight) || minimumHeight <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumHeight));
        }
        if (!intent.IsHandled)
        {
            return false;
        }

        if (intent.Action == SpreadsheetAnalyticsKeyboardAction.CancelOrClearSelection)
        {
            return interaction.IsTransforming
                ? interaction.CancelTransform()
                : interaction.ClearSelection();
        }

        var selected = interaction.SelectedItem;
        if (!selected.HasValue)
        {
            return false;
        }

        if (intent.Action == SpreadsheetAnalyticsKeyboardAction.Delete)
        {
            var removed = SpreadsheetAnalyticsInteractionEditing.RemoveItem(
                analytics,
                selected.Value);
            if (removed)
            {
                interaction.ClearSelection();
            }
            return removed;
        }

        if (!placements.TryGetPlacement(selected.Value, out var placement))
        {
            interaction.ClearSelection();
            return false;
        }

        return intent.Action switch
        {
            SpreadsheetAnalyticsKeyboardAction.Move => placements.MoveBy(
                selected.Value,
                intent.DeltaX,
                intent.DeltaY),
            SpreadsheetAnalyticsKeyboardAction.Resize => Resize(
                selected.Value,
                placement.DocumentBounds,
                intent.DeltaX,
                intent.DeltaY,
                placements,
                minimumWidth,
                minimumHeight),
            _ => false,
        };
    }

    private static bool Resize(
        SpreadsheetAnalyticsItemKey item,
        RectD bounds,
        double deltaX,
        double deltaY,
        SpreadsheetAnalyticsPlacementController placements,
        double minimumWidth,
        double minimumHeight)
    {
        var handle = GetResizeHandle(deltaX, deltaY);
        if (handle == SpreadsheetAnalyticsResizeHandle.None)
        {
            return false;
        }
        var next = SpreadsheetAnalyticsTransformMath.Apply(
            bounds,
            handle,
            deltaX,
            deltaY,
            minimumWidth,
            minimumHeight);
        return next != bounds && placements.SetBounds(item, next);
    }

    private static SpreadsheetAnalyticsResizeHandle GetResizeHandle(
        double deltaX,
        double deltaY)
    {
        if (deltaX < 0d && deltaY == 0d)
        {
            return SpreadsheetAnalyticsResizeHandle.West;
        }
        if (deltaX > 0d && deltaY == 0d)
        {
            return SpreadsheetAnalyticsResizeHandle.East;
        }
        if (deltaY < 0d && deltaX == 0d)
        {
            return SpreadsheetAnalyticsResizeHandle.North;
        }
        if (deltaY > 0d && deltaX == 0d)
        {
            return SpreadsheetAnalyticsResizeHandle.South;
        }
        return SpreadsheetAnalyticsResizeHandle.None;
    }
}
