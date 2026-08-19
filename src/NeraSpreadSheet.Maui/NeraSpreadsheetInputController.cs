using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace NeraSpreadSheet.Maui;

/// <summary>
/// Immutable diagnostics emitted by the production MAUI pointer state machine.
/// </summary>
public readonly record struct NeraSpreadsheetInputDiagnostics(
    long PressedEvents,
    long MovedEvents,
    long ReleasedEvents,
    long CancelledEvents,
    long WheelEvents,
    long PanUpdates,
    long PinchUpdates,
    long TapSelections,
    long IgnoredEvents,
    long GestureResetCount,
    int ActiveTouchCount,
    bool IsPinching,
    bool IsTapEligible,
    bool IsDisposed);

internal readonly record struct NeraSpreadsheetInputState(
    double Zoom,
    double OffsetX,
    double OffsetY);

internal readonly record struct NeraSpreadsheetInputChrome(
    double RowHeaderWidth,
    double ColumnHeaderHeight);

/// <summary>
/// Owns the production touch/wheel gesture state used by
/// <see cref="NeraSpreadsheetView"/>. Platform events and deterministic
/// runtime tests enter through the same <see cref="Process"/> method.
/// </summary>
internal sealed class NeraSpreadsheetInputController : IDisposable
{
    private const double MinimumTouchDistance = 1d;
    private const double TapMovementThresholdSquared = 9d;

    private readonly Func<NeraSpreadsheetInputState> _captureState;
    private readonly Func<double, NeraSpreadsheetInputChrome> _getChrome;
    private readonly Func<double> _getWheelPixelsPerNotch;
    private readonly Action<double, double> _scrollTo;
    private readonly Action<double, double, double> _applyZoom;
    private readonly Action<double> _queueWheel;
    private readonly Action<SKPoint> _selectAt;
    private readonly double _minimumZoom;
    private readonly double _maximumZoom;
    private readonly Dictionary<long, SKPoint> _touches = [];
    private readonly List<long> _touchOrder = [];

    private NeraSpreadsheetInputState _panStartState;
    private SKPoint _panStartPoint;
    private double _pinchStartDistance;
    private double _pinchStartZoom = 1d;
    private double _pinchAnchorDocumentX;
    private double _pinchAnchorDocumentY;
    private long _pressedEvents;
    private long _movedEvents;
    private long _releasedEvents;
    private long _cancelledEvents;
    private long _wheelEvents;
    private long _panUpdates;
    private long _pinchUpdates;
    private long _tapSelections;
    private long _ignoredEvents;
    private long _gestureResetCount;
    private bool _pinching;
    private bool _tapEligible;
    private bool _disposed;

    public NeraSpreadsheetInputController(
        Func<NeraSpreadsheetInputState> captureState,
        Func<double, NeraSpreadsheetInputChrome> getChrome,
        Func<double> getWheelPixelsPerNotch,
        Action<double, double> scrollTo,
        Action<double, double, double> applyZoom,
        Action<double> queueWheel,
        Action<SKPoint> selectAt,
        double minimumZoom,
        double maximumZoom)
    {
        _captureState = captureState
            ?? throw new ArgumentNullException(nameof(captureState));
        _getChrome = getChrome
            ?? throw new ArgumentNullException(nameof(getChrome));
        _getWheelPixelsPerNotch = getWheelPixelsPerNotch
            ?? throw new ArgumentNullException(nameof(getWheelPixelsPerNotch));
        _scrollTo = scrollTo
            ?? throw new ArgumentNullException(nameof(scrollTo));
        _applyZoom = applyZoom
            ?? throw new ArgumentNullException(nameof(applyZoom));
        _queueWheel = queueWheel
            ?? throw new ArgumentNullException(nameof(queueWheel));
        _selectAt = selectAt
            ?? throw new ArgumentNullException(nameof(selectAt));

        if (!double.IsFinite(minimumZoom) || minimumZoom <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumZoom));
        }
        if (!double.IsFinite(maximumZoom) || maximumZoom < minimumZoom)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumZoom));
        }

        _minimumZoom = minimumZoom;
        _maximumZoom = maximumZoom;
    }

    public NeraSpreadsheetInputDiagnostics Diagnostics => new(
        _pressedEvents,
        _movedEvents,
        _releasedEvents,
        _cancelledEvents,
        _wheelEvents,
        _panUpdates,
        _pinchUpdates,
        _tapSelections,
        _ignoredEvents,
        _gestureResetCount,
        _touches.Count,
        _pinching,
        _tapEligible,
        _disposed);

    public void Process(SKTouchEventArgs input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ObjectDisposedException.ThrowIf(_disposed, this);

        switch (input.ActionType)
        {
            case SKTouchAction.Pressed:
                HandlePressed(input.Id, input.Location);
                break;
            case SKTouchAction.Moved:
                HandleMoved(input.Id, input.Location);
                break;
            case SKTouchAction.Released:
                HandleReleased(input.Id, input.Location);
                break;
            case SKTouchAction.Cancelled:
            case SKTouchAction.Exited:
                HandleCancelled(input.Id);
                break;
            case SKTouchAction.WheelChanged:
                HandleWheel(input.WheelDelta);
                break;
            case SKTouchAction.Entered:
            default:
                _ignoredEvents++;
                break;
        }
    }

    public void CancelAll()
    {
        if (_disposed || (_touches.Count == 0 && !_pinching && !_tapEligible))
        {
            return;
        }

        _touches.Clear();
        _touchOrder.Clear();
        _pinching = false;
        _tapEligible = false;
        _gestureResetCount++;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _touches.Clear();
        _touchOrder.Clear();
        _pinching = false;
        _tapEligible = false;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void HandlePressed(long id, SKPoint location)
    {
        _pressedEvents++;
        if (_touches.ContainsKey(id))
        {
            _touches[id] = location;
            _ignoredEvents++;
            return;
        }

        _touchOrder.Add(id);
        _touches[id] = location;

        if (_touches.Count == 1)
        {
            BeginPan(location);
            _tapEligible = true;
        }
        else if (_touches.Count == 2)
        {
            _tapEligible = false;
            BeginPinch();
        }
        else
        {
            _tapEligible = false;
        }
    }

    private void HandleMoved(long id, SKPoint location)
    {
        if (!_touches.ContainsKey(id))
        {
            _ignoredEvents++;
            return;
        }

        _movedEvents++;
        _touches[id] = location;
        if (_touches.Count >= 2)
        {
            UpdatePinch();
        }
        else
        {
            UpdatePan(location);
        }
    }

    private void HandleReleased(long id, SKPoint location)
    {
        if (!_touches.ContainsKey(id))
        {
            _ignoredEvents++;
            return;
        }

        _releasedEvents++;
        _touches[id] = location;
        var shouldTap = _touches.Count == 1 &&
            _tapEligible &&
            !_pinching;
        if (shouldTap)
        {
            _selectAt(location);
            _tapSelections++;
        }
        RemoveTouch(id);

        if (_touches.Count >= 2)
        {
            _pinching = false;
            BeginPinch();
            _tapEligible = false;
        }
        else if (_touches.Count == 1)
        {
            _pinching = false;
            BeginPan(GetFirstPoint());
            _tapEligible = false;
        }
        else if (_touches.Count == 0)
        {
            _pinching = false;
            _tapEligible = false;
        }
    }

    private void HandleCancelled(long id)
    {
        if (!_touches.ContainsKey(id))
        {
            _ignoredEvents++;
            return;
        }

        _cancelledEvents++;
        RemoveTouch(id);
        if (_touches.Count >= 2)
        {
            _pinching = false;
            BeginPinch();
        }
        else if (_touches.Count == 1)
        {
            _pinching = false;
            BeginPan(GetFirstPoint());
        }
        else if (_touches.Count == 0)
        {
            _pinching = false;
        }
        _tapEligible = false;
    }

    private void HandleWheel(int wheelDelta)
    {
        if (wheelDelta == 0)
        {
            _ignoredEvents++;
            return;
        }

        var state = _captureState();
        ValidateState(state);
        var wheelPixels = _getWheelPixelsPerNotch();
        if (!double.IsFinite(wheelPixels) || wheelPixels <= 0d)
        {
            throw new InvalidOperationException(
                "WheelPixelsPerNotch must be finite and positive.");
        }

        var delta = -(wheelDelta / 120d) * wheelPixels / state.Zoom;
        _queueWheel(delta);
        _wheelEvents++;
    }

    private void BeginPan(SKPoint point)
    {
        var state = _captureState();
        ValidateState(state);
        _panStartPoint = point;
        _panStartState = state;
    }

    private void UpdatePan(SKPoint point)
    {
        var deltaX = point.X - _panStartPoint.X;
        var deltaY = point.Y - _panStartPoint.Y;
        if ((deltaX * deltaX) + (deltaY * deltaY) >
            TapMovementThresholdSquared)
        {
            _tapEligible = false;
        }

        _scrollTo(
            _panStartState.OffsetX - (deltaX / _panStartState.Zoom),
            _panStartState.OffsetY - (deltaY / _panStartState.Zoom));
        _panUpdates++;
    }

    private void BeginPinch()
    {
        if (!TryGetFirstTwoPoints(out var first, out var second))
        {
            return;
        }

        var state = _captureState();
        ValidateState(state);
        _pinching = true;
        _pinchStartDistance = Math.Max(
            MinimumTouchDistance,
            Distance(first, second));
        _pinchStartZoom = state.Zoom;
        var midpoint = Midpoint(first, second);
        var chrome = _getChrome(state.Zoom);
        ValidateChrome(chrome);
        _pinchAnchorDocumentX = state.OffsetX +
            Math.Max(0d, (midpoint.X / state.Zoom) - chrome.RowHeaderWidth);
        _pinchAnchorDocumentY = state.OffsetY +
            Math.Max(0d, (midpoint.Y / state.Zoom) - chrome.ColumnHeaderHeight);
    }

    private void UpdatePinch()
    {
        if (!TryGetFirstTwoPoints(out var first, out var second))
        {
            return;
        }
        if (!_pinching)
        {
            BeginPinch();
        }

        var distance = Math.Max(
            MinimumTouchDistance,
            Distance(first, second));
        var nextZoom = Math.Clamp(
            _pinchStartZoom * (distance / _pinchStartDistance),
            _minimumZoom,
            _maximumZoom);
        var midpoint = Midpoint(first, second);
        var chrome = _getChrome(nextZoom);
        ValidateChrome(chrome);
        var nextX = _pinchAnchorDocumentX -
            Math.Max(0d, (midpoint.X / nextZoom) - chrome.RowHeaderWidth);
        var nextY = _pinchAnchorDocumentY -
            Math.Max(0d, (midpoint.Y / nextZoom) - chrome.ColumnHeaderHeight);
        _applyZoom(
            nextZoom,
            Math.Max(0d, nextX),
            Math.Max(0d, nextY));
        _tapEligible = false;
        _pinchUpdates++;
    }

    private SKPoint GetFirstPoint()
    {
        foreach (var id in _touchOrder)
        {
            if (_touches.TryGetValue(id, out var point))
            {
                return point;
            }
        }

        throw new InvalidOperationException(
            "The MAUI spreadsheet input state contains no active touch.");
    }

    private bool TryGetFirstTwoPoints(
        out SKPoint first,
        out SKPoint second)
    {
        first = default;
        second = default;
        var found = 0;
        foreach (var id in _touchOrder)
        {
            if (!_touches.TryGetValue(id, out var point))
            {
                continue;
            }

            if (found == 0)
            {
                first = point;
                found = 1;
            }
            else
            {
                second = point;
                return true;
            }
        }
        return false;
    }

    private void RemoveTouch(long id)
    {
        _touches.Remove(id);
        _touchOrder.Remove(id);
    }

    private static void ValidateState(NeraSpreadsheetInputState state)
    {
        if (!double.IsFinite(state.Zoom) || state.Zoom <= 0d ||
            !double.IsFinite(state.OffsetX) ||
            !double.IsFinite(state.OffsetY))
        {
            throw new InvalidOperationException(
                "The MAUI spreadsheet input state must be finite and have a positive zoom.");
        }
    }

    private static void ValidateChrome(NeraSpreadsheetInputChrome chrome)
    {
        if (!double.IsFinite(chrome.RowHeaderWidth) ||
            !double.IsFinite(chrome.ColumnHeaderHeight) ||
            chrome.RowHeaderWidth < 0d ||
            chrome.ColumnHeaderHeight < 0d)
        {
            throw new InvalidOperationException(
                "The MAUI spreadsheet input chrome metrics must be finite and non-negative.");
        }
    }

    private static double Distance(SKPoint first, SKPoint second)
    {
        var deltaX = second.X - first.X;
        var deltaY = second.Y - first.Y;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    private static SKPoint Midpoint(SKPoint first, SKPoint second) =>
        new((first.X + second.X) / 2f, (first.Y + second.Y) / 2f);
}
