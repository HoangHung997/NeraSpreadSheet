using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Scrolling;

namespace NeraSpreadSheet.Viewport;

public sealed record SpreadsheetSplitScrollFrameResult(
    IReadOnlyList<SpreadsheetPaneId> ChangedPanes)
{
    public bool Changed => ChangedPanes.Count > 0;
}

/// <summary>
/// Owns one continuous pixel-scroll controller per split pane.
/// Hidden panes keep their offsets so restoring a split does not discard view state.
/// </summary>
public sealed class SpreadsheetSplitScrollController
{
    private readonly ScrollPhysicsOptions? _physicsOptions;
    private readonly Dictionary<SpreadsheetPaneId, ContinuousScrollController> _controllers = [];

    public SpreadsheetSplitScrollController(ScrollPhysicsOptions? physicsOptions = null)
    {
        _physicsOptions = physicsOptions;
    }

    public SpreadsheetPaneId ActivePane { get; private set; } = SpreadsheetPaneId.TopLeft;

    public bool HasPendingMotion => _controllers.Values.Any(static controller => controller.HasPendingMotion);

    public ScrollSnapshot GetSnapshot(SpreadsheetPaneId paneId) => GetController(paneId).Snapshot;

    public PointD GetOffset(SpreadsheetPaneId paneId)
    {
        var snapshot = GetSnapshot(paneId);
        return new PointD(snapshot.OffsetX, snapshot.OffsetY);
    }

    public bool SetActivePane(SpreadsheetPaneId paneId)
    {
        ValidatePaneId(paneId);
        if (ActivePane == paneId)
        {
            return false;
        }

        ActivePane = paneId;
        return true;
    }

    public void QueueDelta(SpreadsheetPaneId paneId, ScrollDelta delta) =>
        GetController(paneId).QueueDelta(delta);

    public void QueueActivePaneDelta(ScrollDelta delta) => QueueDelta(ActivePane, delta);

    public void ScrollTo(SpreadsheetPaneId paneId, double offsetX, double offsetY, bool animated) =>
        GetController(paneId).ScrollTo(offsetX, offsetY, animated);

    public SpreadsheetSplitScrollFrameResult AdvanceFrame(
        TimeSpan elapsed,
        SpreadsheetSplitLayout layout,
        SizeD contentExtent)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(layout);

        EnsureActivePane(layout);
        var changed = new List<SpreadsheetPaneId>(layout.Panes.Count);
        foreach (var pane in layout.Panes)
        {
            var bounds = new ScrollBounds(
                Math.Max(0d, contentExtent.Width - pane.Bounds.Width),
                Math.Max(0d, contentExtent.Height - pane.Bounds.Height));
            var result = GetController(pane.PaneId).AdvanceFrame(elapsed, bounds);
            if (result.Changed)
            {
                changed.Add(pane.PaneId);
            }
        }

        return new SpreadsheetSplitScrollFrameResult(changed);
    }

    public void EnsureActivePane(SpreadsheetSplitLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (!layout.TryGetPane(ActivePane, out _))
        {
            ActivePane = SpreadsheetPaneId.TopLeft;
        }
    }

    public void ResetPane(SpreadsheetPaneId paneId) => GetController(paneId).Reset();

    public void Reset()
    {
        foreach (var controller in _controllers.Values)
        {
            controller.Reset();
        }
        ActivePane = SpreadsheetPaneId.TopLeft;
    }

    private ContinuousScrollController GetController(SpreadsheetPaneId paneId)
    {
        ValidatePaneId(paneId);
        if (_controllers.TryGetValue(paneId, out var controller))
        {
            return controller;
        }

        controller = new ContinuousScrollController(_physicsOptions);
        _controllers.Add(paneId, controller);
        return controller;
    }

    private static void ValidatePaneId(SpreadsheetPaneId paneId)
    {
        if (!Enum.IsDefined(paneId))
        {
            throw new ArgumentOutOfRangeException(nameof(paneId));
        }
    }
}
