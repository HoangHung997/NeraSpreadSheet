namespace NeraSpreadSheet.Rendering.Skia;

public readonly record struct SkiaRendererDiagnostics(
    int TypefaceCacheCapacity,
    int CachedTypefaceCount,
    long TypefaceCacheHits,
    long TypefaceCacheMisses,
    long TypefaceCacheEvictions,
    long SuccessfulRenderCount,
    long FailedRenderCount,
    long ExecutedCommandCount);
