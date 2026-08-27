using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Interaction;

public enum SpreadsheetAnalyticsResizeHandle
{
    None,
    Move,
    NorthWest,
    North,
    NorthEast,
    East,
    SouthEast,
    South,
    SouthWest,
    West,
}

public readonly record struct SpreadsheetAnalyticsInteractionTarget
{
    public SpreadsheetAnalyticsInteractionTarget(
        SpreadsheetAnalyticsItemKey item,
        RectD documentBounds,
        RectD viewportBounds,
        RectD clipBounds,
        int zIndex)
    {
        if (item.Id == Guid.Empty)
        {
            throw new ArgumentException(
                "Interaction targets require a non-empty analytics item ID.",
                nameof(item));
        }
        if (documentBounds.IsEmpty)
        {
            throw new ArgumentException(
                "Interaction targets require non-empty document bounds.",
                nameof(documentBounds));
        }
        if (viewportBounds.IsEmpty)
        {
            throw new ArgumentException(
                "Interaction targets require non-empty viewport bounds.",
                nameof(viewportBounds));
        }
        if (clipBounds.IsEmpty)
        {
            throw new ArgumentException(
                "Interaction targets require non-empty clip bounds.",
                nameof(clipBounds));
        }
        ArgumentOutOfRangeException.ThrowIfNegative(zIndex);

        Item = item;
        DocumentBounds = documentBounds;
        ViewportBounds = viewportBounds;
        ClipBounds = clipBounds;
        ZIndex = zIndex;
    }

    public SpreadsheetAnalyticsItemKey Item { get; }

    public RectD DocumentBounds { get; }

    public RectD ViewportBounds { get; }

    public RectD ClipBounds { get; }

    public int ZIndex { get; }
}

public readonly record struct SpreadsheetAnalyticsHitTestResult(
    SpreadsheetAnalyticsItemKey Item,
    SpreadsheetAnalyticsResizeHandle Handle,
    RectD DocumentBounds,
    RectD ViewportBounds,
    int ZIndex);

public readonly record struct SpreadsheetAnalyticsTransformCommit(
    SpreadsheetAnalyticsItemKey Item,
    RectD BeforeBounds,
    RectD AfterBounds)
{
    public bool HasChanges => BeforeBounds != AfterBounds;
}

public sealed record SpreadsheetAnalyticsInteractionSnapshot(
    SpreadsheetAnalyticsItemKey? SelectedItem,
    bool IsTransforming,
    SpreadsheetAnalyticsResizeHandle ActiveHandle,
    RectD? PreviewDocumentBounds,
    long Version);

public static class SpreadsheetAnalyticsHitTester
{
    public const double DefaultHandleHitSize = 12d;

    public static SpreadsheetAnalyticsHitTestResult? HitTest(
        IReadOnlyList<SpreadsheetAnalyticsInteractionTarget> targets,
        PointD viewportPoint,
        SpreadsheetAnalyticsItemKey? selectedItem = null,
        double handleHitSize = DefaultHandleHitSize)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (!double.IsFinite(handleHitSize) || handleHitSize <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(handleHitSize));
        }

        foreach (var target in targets
                     .OrderByDescending(static target => target.ZIndex)
                     .ThenByDescending(static target => target.Item.Kind)
                     .ThenByDescending(static target => target.Item.Id))
        {
            if (!ContainsHalfOpen(target.ClipBounds, viewportPoint) ||
                !target.ViewportBounds.Contains(viewportPoint))
            {
                continue;
            }

            if (selectedItem.HasValue && selectedItem.Value == target.Item)
            {
                var handle = HitResizeHandle(
                    target.ViewportBounds,
                    viewportPoint,
                    handleHitSize);
                if (handle != SpreadsheetAnalyticsResizeHandle.None)
                {
                    return new SpreadsheetAnalyticsHitTestResult(
                        target.Item,
                        handle,
                        target.DocumentBounds,
                        target.ViewportBounds,
                        target.ZIndex);
                }
            }

            return new SpreadsheetAnalyticsHitTestResult(
                target.Item,
                SpreadsheetAnalyticsResizeHandle.Move,
                target.DocumentBounds,
                target.ViewportBounds,
                target.ZIndex);
        }

        return null;
    }

    private static SpreadsheetAnalyticsResizeHandle HitResizeHandle(
        RectD bounds,
        PointD point,
        double handleHitSize)
    {
        var half = handleHitSize / 2d;
        var left = bounds.Left;
        var centerX = bounds.Left + (bounds.Width / 2d);
        var right = bounds.Right;
        var top = bounds.Top;
        var centerY = bounds.Top + (bounds.Height / 2d);
        var bottom = bounds.Bottom;

        var handles = new[]
        {
            (SpreadsheetAnalyticsResizeHandle.NorthWest, new PointD(left, top)),
            (SpreadsheetAnalyticsResizeHandle.North, new PointD(centerX, top)),
            (SpreadsheetAnalyticsResizeHandle.NorthEast, new PointD(right, top)),
            (SpreadsheetAnalyticsResizeHandle.East, new PointD(right, centerY)),
            (SpreadsheetAnalyticsResizeHandle.SouthEast, new PointD(right, bottom)),
            (SpreadsheetAnalyticsResizeHandle.South, new PointD(centerX, bottom)),
            (SpreadsheetAnalyticsResizeHandle.SouthWest, new PointD(left, bottom)),
            (SpreadsheetAnalyticsResizeHandle.West, new PointD(left, centerY)),
        };
        foreach (var (handle, center) in handles)
        {
            var hitBounds = new RectD(
                center.X - half,
                center.Y - half,
                handleHitSize,
                handleHitSize);
            if (hitBounds.Contains(point))
            {
                return handle;
            }
        }

        return SpreadsheetAnalyticsResizeHandle.None;
    }

    private static bool ContainsHalfOpen(RectD bounds, PointD point) =>
        point.X >= bounds.Left &&
        point.Y >= bounds.Top &&
        point.X < bounds.Right &&
        point.Y < bounds.Bottom;
}

public static class SpreadsheetAnalyticsTransformMath
{
    public const double DefaultMinimumWidth = 96d;
    public const double DefaultMinimumHeight = 64d;

    public static RectD Apply(
        RectD startBounds,
        SpreadsheetAnalyticsResizeHandle handle,
        double deltaX,
        double deltaY,
        double minimumWidth = DefaultMinimumWidth,
        double minimumHeight = DefaultMinimumHeight)
    {
        if (startBounds.IsEmpty)
        {
            throw new ArgumentException(
                "Transform bounds must be non-empty.",
                nameof(startBounds));
        }
        if (!double.IsFinite(deltaX))
        {
            throw new ArgumentOutOfRangeException(nameof(deltaX));
        }
        if (!double.IsFinite(deltaY))
        {
            throw new ArgumentOutOfRangeException(nameof(deltaY));
        }
        if (!double.IsFinite(minimumWidth) || minimumWidth <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumWidth));
        }
        if (!double.IsFinite(minimumHeight) || minimumHeight <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumHeight));
        }

        if (handle == SpreadsheetAnalyticsResizeHandle.None)
        {
            return startBounds;
        }
        if (handle == SpreadsheetAnalyticsResizeHandle.Move)
        {
            return new RectD(
                Math.Max(0d, startBounds.X + deltaX),
                Math.Max(0d, startBounds.Y + deltaY),
                startBounds.Width,
                startBounds.Height);
        }

        var left = startBounds.Left;
        var top = startBounds.Top;
        var right = startBounds.Right;
        var bottom = startBounds.Bottom;

        if (AffectsWest(handle))
        {
            left = Math.Clamp(
                startBounds.Left + deltaX,
                0d,
                Math.Max(0d, right - minimumWidth));
        }
        if (AffectsEast(handle))
        {
            right = Math.Max(left + minimumWidth, startBounds.Right + deltaX);
        }
        if (AffectsNorth(handle))
        {
            top = Math.Clamp(
                startBounds.Top + deltaY,
                0d,
                Math.Max(0d, bottom - minimumHeight));
        }
        if (AffectsSouth(handle))
        {
            bottom = Math.Max(top + minimumHeight, startBounds.Bottom + deltaY);
        }

        return new RectD(
            left,
            top,
            right - left,
            bottom - top);
    }

    private static bool AffectsWest(SpreadsheetAnalyticsResizeHandle handle) =>
        handle is SpreadsheetAnalyticsResizeHandle.West or
            SpreadsheetAnalyticsResizeHandle.NorthWest or
            SpreadsheetAnalyticsResizeHandle.SouthWest;

    private static bool AffectsEast(SpreadsheetAnalyticsResizeHandle handle) =>
        handle is SpreadsheetAnalyticsResizeHandle.East or
            SpreadsheetAnalyticsResizeHandle.NorthEast or
            SpreadsheetAnalyticsResizeHandle.SouthEast;

    private static bool AffectsNorth(SpreadsheetAnalyticsResizeHandle handle) =>
        handle is SpreadsheetAnalyticsResizeHandle.North or
            SpreadsheetAnalyticsResizeHandle.NorthWest or
            SpreadsheetAnalyticsResizeHandle.NorthEast;

    private static bool AffectsSouth(SpreadsheetAnalyticsResizeHandle handle) =>
        handle is SpreadsheetAnalyticsResizeHandle.South or
            SpreadsheetAnalyticsResizeHandle.SouthWest or
            SpreadsheetAnalyticsResizeHandle.SouthEast;
}

public sealed class SpreadsheetAnalyticsInteractionController
{
    private DragState? _drag;
    private SpreadsheetAnalyticsItemKey? _selectedItem;
    private RectD? _previewDocumentBounds;
    private long _version;

    public SpreadsheetAnalyticsItemKey? SelectedItem => _selectedItem;

    public bool IsTransforming => _drag.HasValue;

    public RectD? PreviewDocumentBounds => _previewDocumentBounds;

    public SpreadsheetAnalyticsInteractionSnapshot Snapshot => new(
        _selectedItem,
        _drag.HasValue,
        _drag?.Handle ?? SpreadsheetAnalyticsResizeHandle.None,
        _previewDocumentBounds,
        _version);

    public event EventHandler? Changed;

    public bool Select(SpreadsheetAnalyticsItemKey item)
    {
        if (item.Id == Guid.Empty)
        {
            throw new ArgumentException(
                "Selected analytics items require a non-empty ID.",
                nameof(item));
        }
        if (_selectedItem == item && !_drag.HasValue)
        {
            return false;
        }

        _selectedItem = item;
        _drag = null;
        _previewDocumentBounds = null;
        Publish();
        return true;
    }

    public bool ClearSelection()
    {
        if (!_selectedItem.HasValue && !_drag.HasValue)
        {
            return false;
        }

        _selectedItem = null;
        _drag = null;
        _previewDocumentBounds = null;
        Publish();
        return true;
    }

    public bool TryBeginTransform(
        PointD viewportPoint,
        IReadOnlyList<SpreadsheetAnalyticsInteractionTarget> targets,
        double handleHitSize = SpreadsheetAnalyticsHitTester.DefaultHandleHitSize)
    {
        var hit = SpreadsheetAnalyticsHitTester.HitTest(
            targets,
            viewportPoint,
            _selectedItem,
            handleHitSize);
        if (!hit.HasValue)
        {
            ClearSelection();
            return false;
        }

        var value = hit.Value;
        _selectedItem = value.Item;
        _drag = new DragState(
            value.Item,
            value.Handle,
            viewportPoint,
            value.DocumentBounds);
        _previewDocumentBounds = value.DocumentBounds;
        Publish();
        return true;
    }

    public bool UpdateTransform(
        PointD viewportPoint,
        double minimumWidth = SpreadsheetAnalyticsTransformMath.DefaultMinimumWidth,
        double minimumHeight = SpreadsheetAnalyticsTransformMath.DefaultMinimumHeight)
    {
        if (!_drag.HasValue)
        {
            return false;
        }

        var drag = _drag.Value;
        var next = SpreadsheetAnalyticsTransformMath.Apply(
            drag.StartDocumentBounds,
            drag.Handle,
            viewportPoint.X - drag.StartViewportPoint.X,
            viewportPoint.Y - drag.StartViewportPoint.Y,
            minimumWidth,
            minimumHeight);
        if (_previewDocumentBounds == next)
        {
            return false;
        }

        _previewDocumentBounds = next;
        Publish();
        return true;
    }

    public bool TryCompleteTransform(
        PointD viewportPoint,
        out SpreadsheetAnalyticsTransformCommit commit,
        double minimumWidth = SpreadsheetAnalyticsTransformMath.DefaultMinimumWidth,
        double minimumHeight = SpreadsheetAnalyticsTransformMath.DefaultMinimumHeight)
    {
        if (!_drag.HasValue)
        {
            commit = default;
            return false;
        }

        UpdateTransform(viewportPoint, minimumWidth, minimumHeight);
        var drag = _drag.Value;
        var after = _previewDocumentBounds ?? drag.StartDocumentBounds;
        commit = new SpreadsheetAnalyticsTransformCommit(
            drag.Item,
            drag.StartDocumentBounds,
            after);
        _drag = null;
        _previewDocumentBounds = null;
        Publish();
        return true;
    }

    public bool CancelTransform()
    {
        if (!_drag.HasValue)
        {
            return false;
        }

        _drag = null;
        _previewDocumentBounds = null;
        Publish();
        return true;
    }

    private void Publish()
    {
        _version++;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private readonly record struct DragState(
        SpreadsheetAnalyticsItemKey Item,
        SpreadsheetAnalyticsResizeHandle Handle,
        PointD StartViewportPoint,
        RectD StartDocumentBounds);
}
