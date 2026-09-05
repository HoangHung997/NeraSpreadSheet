using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Usage",
    "CA2219",
    Justification =
        "The finally block only raises after renderSucceeded is true, so no active render exception can be masked. A rejected completion means the native GPU context changed during an otherwise successful frame and must fail before PaintSurface observers run.",
    Scope = "member",
    Target =
        "~M:NeraSpreadSheet.Maui.NeraSpreadsheetView.OnPaintSurface(SkiaSharp.Views.Maui.SKPaintGLSurfaceEventArgs)")]
