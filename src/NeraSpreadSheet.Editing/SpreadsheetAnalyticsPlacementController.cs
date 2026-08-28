using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Editing;

public enum SpreadsheetAnalyticsPlacementChangeKind
{
    Added,
    Removed,
    BoundsChanged,
    ZOrderChanged,
}

public sealed class SpreadsheetAnalyticsPlacementChangedEventArgs : EventArgs
{
    public SpreadsheetAnalyticsPlacementChangedEventArgs(
        Worksheet worksheet,
        SpreadsheetAnalyticsPlacementChangeKind changeKind,
        SpreadsheetAnalyticsItemKey item,
        SpreadsheetAnalyticsPlacement? placement)
    {
        Worksheet = worksheet ?? throw new ArgumentNullException(nameof(worksheet));
        ChangeKind = changeKind;
        Item = item;
        Placement = placement;
    }

    public Worksheet Worksheet { get; }

    public SpreadsheetAnalyticsPlacementChangeKind ChangeKind { get; }

    public SpreadsheetAnalyticsItemKey Item { get; }

    public SpreadsheetAnalyticsPlacement? Placement { get; }
}

/// <summary>
/// Owns floating chart/pivot placement in document coordinates. Item lifetime follows the
/// analytics controller, while user transforms participate in the shared SpreadsheetSession
/// Undo/Redo history. Reads may occur from a GPU render thread while host input commits
/// placement changes on the UI thread, so all placement-map access is synchronized.
/// </summary>
public sealed class SpreadsheetAnalyticsPlacementController
{
    public const double DefaultWidth = 360d;
    public const double DefaultHeight = 240d;
    public const double DefaultInset = 16d;
    public const double CascadeStep = 24d;
    public const int CascadeSlots = 12;

    private readonly SpreadsheetSession _session;
    private readonly SpreadsheetAnalyticsController _analytics;
    private readonly object _placementGate = new();
    private readonly Dictionary<Worksheet, Dictionary<SpreadsheetAnalyticsItemKey, SpreadsheetAnalyticsPlacement>>
        _placements = [];
    private readonly Dictionary<Worksheet, Dictionary<SpreadsheetAnalyticsItemKey, SpreadsheetAnalyticsPlacement>>
        _detachedPlacements = [];

    public SpreadsheetAnalyticsPlacementController(
        SpreadsheetSession session,
        SpreadsheetAnalyticsController analytics)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
        _analytics.Changed += OnAnalyticsChanged;
    }

    public event EventHandler<SpreadsheetAnalyticsPlacementChangedEventArgs>? Changed;

    public IReadOnlyList<SpreadsheetAnalyticsPlacement> Placements =>
        GetPlacements(_session.ActiveWorksheet);

    public IReadOnlyList<SpreadsheetAnalyticsPlacement> GetPlacements(
        Worksheet worksheet)
    {
        EnsureWorksheet(worksheet);
        lock (_placementGate)
        {
            return GetPlacementMapUnsafe(worksheet)
                .Values
                .OrderBy(static placement => placement.ZIndex)
                .ThenBy(static placement => placement.Item.Kind)
                .ThenBy(static placement => placement.Item.Id)
                .ToArray();
        }
    }

    internal void RestorePlacement(
        Worksheet worksheet,
        SpreadsheetAnalyticsPlacement placement)
    {
        EnsureWorksheet(worksheet);
        var exists = placement.Item.Kind switch
        {
            SpreadsheetAnalyticsItemKind.Chart =>
                _analytics.GetCharts(worksheet).Any(chart => chart.Id == placement.Item.Id),
            SpreadsheetAnalyticsItemKind.Pivot =>
                _analytics.GetPivots(worksheet).Any(pivot => pivot.Id == placement.Item.Id),
            _ => false,
        };
        if (!exists)
        {
            throw new InvalidOperationException(
                $"Cannot restore placement for missing analytics item '{placement.Item.Kind}:{placement.Item.Id}'.");
        }

        lock (_placementGate)
        {
            GetPlacementMapUnsafe(worksheet)[placement.Item] = placement;
            GetDetachedPlacementMapUnsafe(worksheet).Remove(placement.Item);
        }
        Publish(
            worksheet,
            SpreadsheetAnalyticsPlacementChangeKind.BoundsChanged,
            placement.Item,
            placement);
    }

    public bool TryGetPlacement(
        SpreadsheetAnalyticsItemKey item,
        out SpreadsheetAnalyticsPlacement placement)
    {
        lock (_placementGate)
        {
            return GetPlacementMapUnsafe(_session.ActiveWorksheet)
                .TryGetValue(item, out placement!);
        }
    }

    public SpreadsheetAnalyticsPlacement GetPlacement(
        SpreadsheetAnalyticsItemKey item)
    {
        if (!TryGetPlacement(item, out var placement))
        {
            throw new KeyNotFoundException(
                $"Analytics placement '{item.Kind}:{item.Id}' was not found.");
        }
        return placement;
    }

    public bool SetBounds(
        SpreadsheetAnalyticsItemKey item,
        RectD documentBounds)
    {
        var worksheet = _session.ActiveWorksheet;
        SpreadsheetAnalyticsPlacement current;
        SpreadsheetAnalyticsPlacement next;
        lock (_placementGate)
        {
            var map = GetPlacementMapUnsafe(worksheet);
            if (!map.TryGetValue(item, out current!))
            {
                return false;
            }

            next = current.WithBounds(documentBounds);
            if (next == current)
            {
                return false;
            }
        }

        ExecutePlacementChange(
            worksheet,
            current,
            next,
            SpreadsheetAnalyticsPlacementChangeKind.BoundsChanged,
            "Move or resize analytics item");
        return true;
    }

    public bool MoveBy(
        SpreadsheetAnalyticsItemKey item,
        double deltaX,
        double deltaY)
    {
        if (!double.IsFinite(deltaX))
        {
            throw new ArgumentOutOfRangeException(nameof(deltaX));
        }
        if (!double.IsFinite(deltaY))
        {
            throw new ArgumentOutOfRangeException(nameof(deltaY));
        }
        if (!TryGetPlacement(item, out var current))
        {
            return false;
        }

        var bounds = current.DocumentBounds;
        return SetBounds(
            item,
            new RectD(
                Math.Max(0d, bounds.X + deltaX),
                Math.Max(0d, bounds.Y + deltaY),
                bounds.Width,
                bounds.Height));
    }

    public bool BringToFront(SpreadsheetAnalyticsItemKey item)
    {
        var worksheet = _session.ActiveWorksheet;
        SpreadsheetAnalyticsPlacement current;
        SpreadsheetAnalyticsPlacement next;
        lock (_placementGate)
        {
            var map = GetPlacementMapUnsafe(worksheet);
            if (!map.TryGetValue(item, out current!))
            {
                return false;
            }

            var maximum = map.Count == 0
                ? 0
                : map.Values.Max(static placement => placement.ZIndex);
            if (current.ZIndex >= maximum)
            {
                return false;
            }

            next = current.WithZIndex(checked(maximum + 1));
        }

        ExecutePlacementChange(
            worksheet,
            current,
            next,
            SpreadsheetAnalyticsPlacementChangeKind.ZOrderChanged,
            "Bring analytics item to front");
        return true;
    }

    private void ExecutePlacementChange(
        Worksheet worksheet,
        SpreadsheetAnalyticsPlacement before,
        SpreadsheetAnalyticsPlacement after,
        SpreadsheetAnalyticsPlacementChangeKind changeKind,
        string description)
    {
        var affectedRange = GetSourceRange(worksheet, before.Item);
        _session.Execute(new PlacementOperation(
            worksheet,
            affectedRange,
            description,
            () => ApplyPlacement(worksheet, after, changeKind),
            () => ApplyPlacement(worksheet, before, changeKind)));
    }

    private void ApplyPlacement(
        Worksheet worksheet,
        SpreadsheetAnalyticsPlacement placement,
        SpreadsheetAnalyticsPlacementChangeKind changeKind)
    {
        lock (_placementGate)
        {
            GetPlacementMapUnsafe(worksheet)[placement.Item] = placement;
        }
        Publish(worksheet, changeKind, placement.Item, placement);
    }

    private void OnAnalyticsChanged(
        object? sender,
        SpreadsheetAnalyticsChangedEventArgs args)
    {
        var item = args.ChangeKind switch
        {
            SpreadsheetAnalyticsChangeKind.ChartAdded or
                SpreadsheetAnalyticsChangeKind.ChartRemoved =>
                SpreadsheetAnalyticsItemKey.ForChart(args.ItemId),
            SpreadsheetAnalyticsChangeKind.PivotAdded or
                SpreadsheetAnalyticsChangeKind.PivotRemoved =>
                SpreadsheetAnalyticsItemKey.ForPivot(args.ItemId),
            _ => throw new ArgumentOutOfRangeException(
                nameof(args),
                args.ChangeKind,
                "Unknown analytics change kind."),
        };

        switch (args.ChangeKind)
        {
            case SpreadsheetAnalyticsChangeKind.ChartAdded:
            case SpreadsheetAnalyticsChangeKind.PivotAdded:
                AttachPlacement(args.Worksheet, item);
                break;
            case SpreadsheetAnalyticsChangeKind.ChartRemoved:
            case SpreadsheetAnalyticsChangeKind.PivotRemoved:
                DetachPlacement(args.Worksheet, item);
                break;
        }
    }

    private void AttachPlacement(
        Worksheet worksheet,
        SpreadsheetAnalyticsItemKey item)
    {
        SpreadsheetAnalyticsPlacement placement;
        lock (_placementGate)
        {
            var map = GetPlacementMapUnsafe(worksheet);
            if (map.ContainsKey(item))
            {
                return;
            }

            var detached = GetDetachedPlacementMapUnsafe(worksheet);
            if (detached.Remove(item, out var preserved))
            {
                placement = preserved;
            }
            else
            {
                var slot = map.Count % CascadeSlots;
                var inset = DefaultInset + (slot * CascadeStep);
                var zIndex = map.Count == 0
                    ? 0
                    : checked(map.Values.Max(static value => value.ZIndex) + 1);
                placement = new SpreadsheetAnalyticsPlacement(
                    item,
                    new RectD(
                        inset,
                        inset,
                        DefaultWidth,
                        DefaultHeight),
                    zIndex);
            }

            map.Add(item, placement);
        }
        Publish(
            worksheet,
            SpreadsheetAnalyticsPlacementChangeKind.Added,
            item,
            placement);
    }

    private void DetachPlacement(
        Worksheet worksheet,
        SpreadsheetAnalyticsItemKey item)
    {
        SpreadsheetAnalyticsPlacement placement;
        lock (_placementGate)
        {
            var map = GetPlacementMapUnsafe(worksheet);
            if (!map.Remove(item, out placement!))
            {
                return;
            }

            GetDetachedPlacementMapUnsafe(worksheet)[item] = placement;
        }
        Publish(
            worksheet,
            SpreadsheetAnalyticsPlacementChangeKind.Removed,
            item,
            null);
    }

    private CellRange GetSourceRange(
        Worksheet worksheet,
        SpreadsheetAnalyticsItemKey item) =>
        item.Kind switch
        {
            SpreadsheetAnalyticsItemKind.Chart =>
                _analytics.GetCharts(worksheet)
                    .First(chart => chart.Id == item.Id)
                    .SourceRange,
            SpreadsheetAnalyticsItemKind.Pivot =>
                _analytics.GetPivots(worksheet)
                    .First(pivot => pivot.Id == item.Id)
                    .SourceRange,
            _ => throw new ArgumentOutOfRangeException(nameof(item)),
        };

    private Dictionary<SpreadsheetAnalyticsItemKey, SpreadsheetAnalyticsPlacement>
        GetPlacementMapUnsafe(Worksheet worksheet)
    {
        if (!_placements.TryGetValue(worksheet, out var map))
        {
            map = [];
            _placements.Add(worksheet, map);
        }
        return map;
    }

    private Dictionary<SpreadsheetAnalyticsItemKey, SpreadsheetAnalyticsPlacement>
        GetDetachedPlacementMapUnsafe(Worksheet worksheet)
    {
        if (!_detachedPlacements.TryGetValue(worksheet, out var map))
        {
            map = [];
            _detachedPlacements.Add(worksheet, map);
        }
        return map;
    }

    private void EnsureWorksheet(Worksheet worksheet)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        if (!_session.Workbook.Worksheets.Contains(worksheet))
        {
            throw new ArgumentException(
                "Worksheet must belong to the session workbook.",
                nameof(worksheet));
        }
    }

    private void Publish(
        Worksheet worksheet,
        SpreadsheetAnalyticsPlacementChangeKind changeKind,
        SpreadsheetAnalyticsItemKey item,
        SpreadsheetAnalyticsPlacement? placement) =>
        Changed?.Invoke(
            this,
            new SpreadsheetAnalyticsPlacementChangedEventArgs(
                worksheet,
                changeKind,
                item,
                placement));

    private sealed class PlacementOperation : ISpreadsheetEditOperation
    {
        private readonly Action _execute;
        private readonly Action _undo;

        public PlacementOperation(
            Worksheet worksheet,
            CellRange affectedRange,
            string description,
            Action execute,
            Action undo)
        {
            Worksheet = worksheet ?? throw new ArgumentNullException(nameof(worksheet));
            AffectedRange = affectedRange;
            ArgumentException.ThrowIfNullOrWhiteSpace(description);
            Description = description.Trim();
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _undo = undo ?? throw new ArgumentNullException(nameof(undo));
        }

        public string Description { get; }

        public Worksheet Worksheet { get; }

        public CellRange AffectedRange { get; }

        public bool AffectsCalculation => false;

        public void Execute() => _execute();

        public void Undo() => _undo();
    }
}
