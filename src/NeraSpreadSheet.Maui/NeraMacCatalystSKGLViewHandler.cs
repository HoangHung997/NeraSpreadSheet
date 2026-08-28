#if MACCATALYST
using SkiaSharp.Views.iOS;
using SkiaSharp.Views.Maui.Handlers;
using UIKit;

namespace NeraSpreadSheet.Maui;

/// <summary>
/// Mac Catalyst SKGLView handler that uses SkiaSharp's registered SKMetalView
/// directly. SkiaSharp's intermediate MauiSKMetalView subclass currently trips
/// UIKit 26 class initialization on hosted macOS runners before the first frame.
/// Pixel-scaling normalization is applied by <see cref="NeraSpreadsheetView"/>.
/// </summary>
internal sealed class NeraMacCatalystSKGLViewHandler : SKGLViewHandler
{
    protected override SKMetalView CreatePlatformView() =>
        new()
        {
            BackgroundColor = UIColor.Clear,
            Opaque = false,
        };
}
#endif
