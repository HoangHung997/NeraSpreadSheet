using System.Collections.Concurrent;
using Microsoft.Maui.Controls;
using NeraSpreadSheet.Iconography;

namespace NeraSpreadSheet.Maui;

/// <summary>
/// Resolves MAUI image sources from the embedded Nera icon catalog.
/// </summary>
public static class NeraMauiIconProvider
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

        return Cache.GetOrAdd(
            request,
            static value => ImageSource.FromStream(() => NeraIconCatalog.OpenPng(value)));
    }
}
