using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using NeraSpreadSheet.Iconography;

namespace NeraSpreadSheet.Avalonia;

/// <summary>Owns native images decoded from the shared embedded Nera icon catalog.
/// Dispose only after the controls using these images have been detached.</summary>
public sealed class NeraAvaloniaIconProvider : IDisposable
{
    private readonly Dictionary<NeraIconRequest, Bitmap> _images = [];
    private bool _disposed;

    /// <summary>Returns a cached icon, or null for a key not present in the catalog.
    /// Called on the owning UI thread. Images remain owned by this provider.</summary>
    public IImage? Resolve(NeraIconRequest request)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!NeraIconCatalog.TryGetDescriptor(request.IconKey, out _)) return null;
        if (_images.TryGetValue(request, out var existing)) return existing;
        using var stream = NeraIconCatalog.OpenPng(request);
        var bitmap = new Bitmap(stream);
        _images.Add(request, bitmap);
        return bitmap;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var bitmap in _images.Values) bitmap.Dispose();
        _images.Clear();
    }
}
