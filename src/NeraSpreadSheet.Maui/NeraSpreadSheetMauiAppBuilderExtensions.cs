using Microsoft.Maui.Hosting;
using SkiaSharp.Views.Maui.Controls.Hosting;
using SkiaSharp.Views.Maui.Handlers;

namespace NeraSpreadSheet.Maui;

public static class NeraSpreadSheetMauiAppBuilderExtensions
{
    /// <summary>
    /// Registers the SkiaSharp MAUI GPU handlers and the concrete Nera spreadsheet view handler.
    /// </summary>
    public static MauiAppBuilder UseNeraSpreadSheet(this MauiAppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseSkiaSharp();
        builder.ConfigureMauiHandlers(handlers =>
            handlers.AddHandler<NeraSpreadsheetView, SKGLViewHandler>());
        return builder;
    }
}
