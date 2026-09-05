using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Viewport;

public static class SpreadsheetSplitViewStateAdapter
{
    private static readonly SpreadsheetPaneId[] PaneIds =
    [
        SpreadsheetPaneId.TopLeft,
        SpreadsheetPaneId.TopRight,
        SpreadsheetPaneId.BottomLeft,
        SpreadsheetPaneId.BottomRight,
    ];

    public static SpreadsheetSplitViewState Capture(
        SpreadsheetSplitViewportEngine engine,
        double? splitX,
        double? splitY)
    {
        ArgumentNullException.ThrowIfNull(engine);
        var mode = ResolveMode(splitX, splitY);
        return new SpreadsheetSplitViewState(
            mode,
            splitX,
            splitY,
            ToViewPane(engine.ActivePane),
            ToOffset(engine.GetPaneScroll(SpreadsheetPaneId.TopLeft)),
            ToOffset(engine.GetPaneScroll(SpreadsheetPaneId.TopRight)),
            ToOffset(engine.GetPaneScroll(SpreadsheetPaneId.BottomLeft)),
            ToOffset(engine.GetPaneScroll(SpreadsheetPaneId.BottomRight)));
    }

    public static void Apply(
        SpreadsheetSplitViewportEngine engine,
        SpreadsheetSplitViewState state)
    {
        ArgumentNullException.ThrowIfNull(engine);
        engine.ResetPaneScrolls();
        foreach (var paneId in PaneIds)
        {
            var offset = state.GetPaneScroll(ToViewPane(paneId));
            engine.ScrollPaneTo(
                paneId,
                offset.OffsetX,
                offset.OffsetY,
                animated: false);
        }
        engine.SetActivePane(ToPaneId(state.ActivePane));
    }

    public static SpreadsheetSplitViewMode ResolveMode(
        double? splitX,
        double? splitY)
    {
        ValidateCoordinate(splitX, nameof(splitX));
        ValidateCoordinate(splitY, nameof(splitY));
        return (splitX, splitY) switch
        {
            (not null, not null) => SpreadsheetSplitViewMode.Both,
            (not null, null) => SpreadsheetSplitViewMode.Vertical,
            (null, not null) => SpreadsheetSplitViewMode.Horizontal,
            _ => SpreadsheetSplitViewMode.None,
        };
    }

    public static SpreadsheetPaneId ToPaneId(SpreadsheetSplitViewPane pane) => pane switch
    {
        SpreadsheetSplitViewPane.TopLeft => SpreadsheetPaneId.TopLeft,
        SpreadsheetSplitViewPane.TopRight => SpreadsheetPaneId.TopRight,
        SpreadsheetSplitViewPane.BottomLeft => SpreadsheetPaneId.BottomLeft,
        SpreadsheetSplitViewPane.BottomRight => SpreadsheetPaneId.BottomRight,
        _ => throw new ArgumentOutOfRangeException(nameof(pane)),
    };

    public static SpreadsheetSplitViewPane ToViewPane(SpreadsheetPaneId paneId) => paneId switch
    {
        SpreadsheetPaneId.TopLeft => SpreadsheetSplitViewPane.TopLeft,
        SpreadsheetPaneId.TopRight => SpreadsheetSplitViewPane.TopRight,
        SpreadsheetPaneId.BottomLeft => SpreadsheetSplitViewPane.BottomLeft,
        SpreadsheetPaneId.BottomRight => SpreadsheetSplitViewPane.BottomRight,
        _ => throw new ArgumentOutOfRangeException(nameof(paneId)),
    };

    private static SpreadsheetPaneScrollOffset ToOffset(PointD point) =>
        new(point.X, point.Y);

    private static void ValidateCoordinate(double? value, string parameterName)
    {
        if (value is { } coordinate && !double.IsFinite(coordinate))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                coordinate,
                "Split coordinates must be finite.");
        }
    }
}
