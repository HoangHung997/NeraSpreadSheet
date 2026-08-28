#if MACCATALYST
using CoreGraphics;
using ObjCRuntime;
using UIKit;

namespace NeraSpreadSheet.Maui;

/// <summary>
/// Native Mac Catalyst host for the GPU spreadsheet. The view declares
/// UIAccessibilityContainer conformance directly so UIKit can dispatch
/// accessibilityElements to the native view without re-wrapping its handle
/// through ObjCRuntime and accidentally resolving the MAUI handler as owner.
/// </summary>
internal sealed class NeraMacCatalystAccessibilityContainerView :
    UIView,
    IUIAccessibilityContainer
{
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
}
#endif
