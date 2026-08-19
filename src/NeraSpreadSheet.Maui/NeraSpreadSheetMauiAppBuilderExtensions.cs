using Microsoft.Maui.Hosting;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace NeraSpreadSheet.Maui;

public static class NeraSpreadSheetMauiAppBuilderExtensions
{
    /// <summary>
    /// Registers SkiaSharp's cross-platform GPU handler graph. The derived
    /// <see cref="NeraSpreadsheetView"/> resolves through the registered
    /// <c>SKGLView</c> base type, allowing SkiaSharp to select the native GPU
    /// surface for each platform, including Metal on Apple targets.
    /// </summary>
    public static MauiAppBuilder UseNeraSpreadSheet(this MauiAppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UseSkiaSharp();
    }
}
