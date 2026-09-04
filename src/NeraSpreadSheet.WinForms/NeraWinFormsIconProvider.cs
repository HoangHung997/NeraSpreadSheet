using System.Collections.Concurrent;
using System.Drawing;
using NeraSpreadSheet.Iconography;

namespace NeraSpreadSheet.WinForms;

/// <summary>
/// Resolves shared WinForms images from the embedded Nera icon catalog.
/// </summary>
public static class NeraWinFormsIconProvider
{
    private static readonly ConcurrentDictionary<NeraIconRequest, Image> Cache = [];

    /// <summary>
    /// Resolves an icon request, or returns <see langword="null"/> for an unknown key.
    /// </summary>
    public static Image? Resolve(NeraIconRequest request)
    {
        if (!NeraIconCatalog.TryGetDescriptor(request.IconKey, out _))
        {
            return null;
        }

        return Cache.GetOrAdd(request, static value => CreateImage(value));
    }

    private static Bitmap CreateImage(NeraIconRequest request)
    {
        using var stream = NeraIconCatalog.OpenPng(request);
        using var source = Image.FromStream(stream);
        return new Bitmap(source);
    }
}
