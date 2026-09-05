using Microsoft.Maui.Hosting;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace NeraSpreadSheet.Maui;

public static class NeraSpreadSheetMauiAppBuilderExtensions
{
    /// <summary>
    /// Registers SkiaSharp's cross-platform GPU handler graph. Nera normally
    /// resolves through SkiaSharp's SKGLView handler. Mac Catalyst uses a Nera
    /// handler that keeps the same SKMetalView backend while avoiding SkiaSharp's
    /// intermediate MauiSKMetalView subclass, which is incompatible with UIKit 26
    /// class initialization on current hosted runners.
    /// </summary>
    public static MauiAppBuilder UseNeraSpreadSheet(this MauiAppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseSkiaSharp();
#if IOS || MACCATALYST
        builder.ConfigureMauiHandlers(static handlers =>
            handlers.AddHandler<NeraCellEditor, NeraCellEditorHandler>());
#endif
#if MACCATALYST
        builder.ConfigureMauiHandlers(static handlers =>
            handlers.AddHandler<NeraSpreadsheetView, NeraMacCatalystSKGLViewHandler>());
#endif
        return builder;
    }
}
