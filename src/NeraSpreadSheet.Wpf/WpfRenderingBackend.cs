namespace NeraSpreadSheet.Wpf;

public enum WpfRenderingBackend
{
    DrawingContext,
    Direct2DD3DImage,
}

public readonly record struct WpfGpuRendererDiagnostics(
    int TextureWidth,
    int TextureHeight,
    int CachedTextLayouts,
    long TextLayoutCacheHits,
    long TextLayoutCacheMisses,
    long TextLayoutCacheEvictions);
