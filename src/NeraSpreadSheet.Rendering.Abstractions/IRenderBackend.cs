using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Rendering;

public readonly record struct RenderFrameContext(
    SizeD ViewportSize,
    double DpiScaleX,
    double DpiScaleY,
    TimeSpan PresentationTime);

public interface IRenderBackend : IDisposable
{
    string Id { get; }

    bool IsHardwareAccelerated { get; }

    void Render(DisplayList displayList, RenderFrameContext context);
}

public interface IRenderBackendFactory
{
    string Id { get; }

    bool IsSupported { get; }

    IRenderBackend Create();
}
