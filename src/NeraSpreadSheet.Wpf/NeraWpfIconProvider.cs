using System.Collections.Concurrent;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NeraSpreadSheet.Iconography;

namespace NeraSpreadSheet.Wpf;

/// <summary>
/// Resolves immutable WPF images from the embedded Nera icon catalog.
/// </summary>
public static class NeraWpfIconProvider
{
    private static readonly ConcurrentDictionary<NeraIconRequest, ImageSource> Cache = [];

    /// <summary>
    /// Resolves an icon request, or returns <see langword="null"/> for an unknown key.
    /// </summary>
    public static ImageSource? Resolve(NeraIconRequest request)
    {
        if (!NeraIconCatalog.TryGetDescriptor(request.IconKey, out _))
        {
            return null;
        }

        return Cache.GetOrAdd(request, static value => CreateImage(value));
    }

    private static BitmapImage CreateImage(NeraIconRequest request)
    {
        using var stream = NeraIconCatalog.OpenPng(request);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
