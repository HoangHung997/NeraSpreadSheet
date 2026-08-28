#if MACCATALYST
using CoreGraphics;
using Foundation;
using ObjCRuntime;
using UIKit;

namespace NeraSpreadSheet.Maui;

/// <summary>
/// Native Mac Catalyst host for the GPU spreadsheet. The view declares
/// UIAccessibilityContainer conformance directly and implements the optional
/// accessibilityElements selectors itself so UIKit never has to resolve a
/// synthetic protocol wrapper around the MAUI handler-owned native view.
/// </summary>
internal sealed class NeraMacCatalystAccessibilityContainerView :
    UIView,
    IUIAccessibilityContainer
{
    private NSObject? _accessibilityElements;

    internal NeraMacCatalystAccessibilityContainerView()
        : base(CGRect.Empty)
    {
        BackgroundColor = UIColor.Clear;
        Opaque = false;
        ClipsToBounds = true;
    }

    public NeraMacCatalystAccessibilityContainerView(NativeHandle handle)
        : base(handle)
    {
    }

    [Export("accessibilityElements")]
    public NSObject GetAccessibilityElements() => _accessibilityElements!;

    [Export("setAccessibilityElements:")]
    public void SetAccessibilityElements(NSObject? elements)
    {
        if (ReferenceEquals(elements, _accessibilityElements))
        {
            return;
        }

        var previous = _accessibilityElements;
        _accessibilityElements = elements;
        previous?.Dispose();
    }

    internal void ReplaceAccessibilityElements(IReadOnlyList<NSObject> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        SetAccessibilityElements(
            elements.Count == 0
                ? null
                : NSArray.FromNSObjects(elements.ToArray()));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            var previous = _accessibilityElements;
            _accessibilityElements = null;
            previous?.Dispose();
        }

        base.Dispose(disposing);
    }
}
#endif
