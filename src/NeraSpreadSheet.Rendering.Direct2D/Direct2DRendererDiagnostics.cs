namespace NeraSpreadSheet.Rendering.Direct2D;

public readonly record struct Direct2DRendererDiagnostics(
    int PixelWidth,
    int PixelHeight,
    int TextLayoutCacheCapacity,
    int CachedTextLayouts,
    long TextLayoutCacheHits,
    long TextLayoutCacheMisses,
    long TextLayoutCacheEvictions,
    long DeviceRecoveryCount);
