using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Viewport;
using SkiaSharp.Views.Maui;

namespace NeraSpreadSheet.Maui;

/// <summary>
/// Gives a single pointer exclusive ownership of a floating analytics transform before the
/// normal spreadsheet pan/pinch/tap state machine sees the event. Touches that do not hit an
/// analytics item fall through unchanged to <see cref="NeraSpreadsheetInputController"/>.
/// </summary>
internal sealed class NeraSpreadsheetAnalyticsTouchRouter
{
    private readonly Func<SpreadsheetAnalyticsViewportInteractionController?> _getController;
    private readonly Func<ViewportLayout?> _getLayout;
    private readonly Action _cancelSpreadsheetGestures;
    private readonly Action _invalidate;
    private long? _activeTouchId;

    public NeraSpreadsheetAnalyticsTouchRouter(
        Func<SpreadsheetAnalyticsViewportInteractionController?> getController,
        Func<ViewportLayout?> getLayout,
        Action cancelSpreadsheetGestures,
        Action invalidate)
    {
        _getController = getController
            ?? throw new ArgumentNullException(nameof(getController));
        _getLayout = getLayout
            ?? throw new ArgumentNullException(nameof(getLayout));
        _cancelSpreadsheetGestures = cancelSpreadsheetGestures
            ?? throw new ArgumentNullException(nameof(cancelSpreadsheetGestures));
        _invalidate = invalidate
            ?? throw new ArgumentNullException(nameof(invalidate));
    }

    public bool HasActiveTouch => _activeTouchId.HasValue;

    public long? ActiveTouchId => _activeTouchId;

    public bool Process(
        SKTouchEventArgs input,
        PointD bodyPoint,
        bool isBodyRegion)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (_activeTouchId.HasValue)
        {
            return ProcessOwnedTouch(input, bodyPoint);
        }

        if (input.ActionType != SKTouchAction.Pressed || !isBodyRegion)
        {
            return false;
        }

        var controller = _getController();
        var layout = _getLayout();
        if (controller is null || layout is null ||
            !controller.PointerPressed(bodyPoint, layout))
        {
            return false;
        }

        _cancelSpreadsheetGestures();
        _activeTouchId = input.Id;
        _invalidate();
        return true;
    }

    public bool CancelAll()
    {
        if (!_activeTouchId.HasValue)
        {
            return false;
        }

        _activeTouchId = null;
        _getController()?.Cancel();
        _invalidate();
        return true;
    }

    private bool ProcessOwnedTouch(
        SKTouchEventArgs input,
        PointD bodyPoint)
    {
        if (input.Id != _activeTouchId.Value)
        {
            return true;
        }

        var controller = _getController();
        switch (input.ActionType)
        {
            case SKTouchAction.Moved:
                controller?.PointerMoved(bodyPoint);
                _invalidate();
                return true;
            case SKTouchAction.Released:
                try
                {
                    controller?.PointerReleased(bodyPoint);
                }
                finally
                {
                    _activeTouchId = null;
                    _invalidate();
                }
                return true;
            case SKTouchAction.Cancelled:
            case SKTouchAction.Exited:
                try
                {
                    controller?.Cancel();
                }
                finally
                {
                    _activeTouchId = null;
                    _invalidate();
                }
                return true;
            default:
                return true;
        }
    }
}
