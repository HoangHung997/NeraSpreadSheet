using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace NeraSpreadSheet.Iconography;

public static class NeraIconCatalog
{
    private const string ManifestResourceName =
        "NeraSpreadSheet.Iconography.icons.catalog.json";

    private static readonly Assembly Assembly = typeof(NeraIconCatalog).Assembly;
    private static readonly JsonSerializerOptions SerializerOptions =
        new() { PropertyNameCaseInsensitive = true };
    private static readonly Lazy<CatalogDocument> Document = new(LoadDocument);
    private static readonly Lazy<Dictionary<string, NeraIconDescriptor>> ByKey =
        new(CreateIndex);
    private static readonly ConcurrentDictionary<string, byte[]> ResourceBytes =
        new(StringComparer.Ordinal);

    public static IReadOnlyList<NeraIconDescriptor> Icons => Document.Value.Icons;

    public static IReadOnlyList<int> SupportedSizes => Document.Value.Sizes;

    public static string FluentSourceCommit => Document.Value.FluentSourceCommit;

    public static bool TryGetDescriptor(
        string iconKey,
        out NeraIconDescriptor? descriptor)
    {
        if (string.IsNullOrWhiteSpace(iconKey))
        {
            descriptor = null;
            return false;
        }

        return ByKey.Value.TryGetValue(iconKey.Trim(), out descriptor);
    }

    public static Stream OpenPng(NeraIconRequest request)
    {
        if (!TryGetDescriptor(request.IconKey, out var descriptor) || descriptor is null)
        {
            throw new KeyNotFoundException(
                $"Icon key '{request.IconKey}' is not in the Nera icon catalog.");
        }

        var size = ResolveSize(request.PixelSize);
        var theme = request.Theme switch
        {
            NeraIconTheme.Light => "light",
            NeraIconTheme.Dark => "dark",
            NeraIconTheme.HighContrastLight => "high_contrast_light",
            NeraIconTheme.HighContrastDark => "high_contrast_dark",
            _ => throw new ArgumentOutOfRangeException(nameof(request)),
        };
        var resourceName =
            $"NeraSpreadSheet.Iconography.Assets.Generated.{theme}_{size}_{descriptor.Asset}.png";
        return OpenCachedResource(resourceName);
    }

    public static Stream OpenSvg(string iconKey)
    {
        if (!TryGetDescriptor(iconKey, out var descriptor) || descriptor is null)
        {
            throw new KeyNotFoundException(
                $"Icon key '{iconKey}' is not in the Nera icon catalog.");
        }

        return OpenCachedResource(
            $"NeraSpreadSheet.Iconography.Assets.Svg.{descriptor.Asset}.svg");
    }

    private static int ResolveSize(int requestedSize)
    {
        if (requestedSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedSize),
                "Icon size must be positive.");
        }

        return SupportedSizes
            .OrderBy(size => Math.Abs(size - requestedSize))
            .ThenBy(size => size)
            .First();
    }

    private static MemoryStream OpenCachedResource(string resourceName)
    {
        var bytes = ResourceBytes.GetOrAdd(resourceName, static name =>
        {
            using var resource = Assembly.GetManifestResourceStream(name) ??
                throw new InvalidDataException(
                    $"Embedded icon resource '{name}' was not found.");
            using var buffer = new MemoryStream();
            resource.CopyTo(buffer);
            return buffer.ToArray();
        });
        return new MemoryStream(bytes, writable: false);
    }

    private static CatalogDocument LoadDocument()
    {
        using var stream = Assembly.GetManifestResourceStream(ManifestResourceName) ??
            throw new InvalidDataException("The embedded Nera icon catalog was not found.");
        var document = JsonSerializer.Deserialize<CatalogDocument>(
            stream,
            SerializerOptions) ??
            throw new InvalidDataException("The Nera icon catalog is empty.");
        if (!string.Equals(
                document.Schema,
                "neraspreadsheet.icon-catalog",
                StringComparison.Ordinal) ||
            document.Version != 1)
        {
            throw new InvalidDataException("Unsupported Nera icon catalog schema or version.");
        }
        if (document.Sizes.Count == 0 || document.Icons.Count == 0)
        {
            throw new InvalidDataException("The Nera icon catalog has no sizes or icons.");
        }
        return document;
    }

    private static Dictionary<string, NeraIconDescriptor> CreateIndex()
    {
        var index = new Dictionary<string, NeraIconDescriptor>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var descriptor in Icons)
        {
            if (string.IsNullOrWhiteSpace(descriptor.Key) ||
                !string.Equals(
                    descriptor.Key,
                    descriptor.Key.ToLowerInvariant(),
                    StringComparison.Ordinal) ||
                !index.TryAdd(descriptor.Key, descriptor))
            {
                throw new InvalidDataException(
                    $"Invalid or duplicate Nera icon key '{descriptor.Key}'.");
            }
        }
        return index;
    }

    private sealed class CatalogDocument
    {
        public string Schema { get; init; } = string.Empty;

        public int Version { get; init; }

        public string FluentSourceCommit { get; init; } = string.Empty;

        public List<int> Sizes { get; init; } = [];

        public List<string> Themes { get; init; } = [];

        public List<NeraIconDescriptor> Icons { get; init; } = [];
    }
}
