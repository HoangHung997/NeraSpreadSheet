namespace NeraSpreadSheet.Rendering.Direct2D;

public readonly record struct Direct2DSwapChainRendererDiagnostics(
    int PixelWidth,
    int PixelHeight,
    string AdapterName,
    string DeviceFeatureLevel,
    bool VSync,
    int TextLayoutCacheCapacity,
    int CachedTextLayouts,
    long TextLayoutCacheHits,
    long TextLayoutCacheMisses,
    long TextLayoutCacheEvictions,
    long DeviceRecoveryCount);
