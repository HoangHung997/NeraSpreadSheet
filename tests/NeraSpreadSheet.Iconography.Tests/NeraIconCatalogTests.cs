using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeraSpreadSheet.Iconography.Tests;

[TestClass]
public sealed class NeraIconCatalogTests
{
    [TestMethod]
    public void CatalogShouldExposeUniqueCanonicalSemanticKeys()
    {
        Assert.IsGreaterThanOrEqualTo(200, NeraIconCatalog.Icons.Count);
        Assert.AreEqual(
            NeraIconCatalog.Icons.Count,
            NeraIconCatalog.Icons
                .Select(icon => icon.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());
        Assert.IsTrue(NeraIconCatalog.Icons.All(icon =>
            string.Equals(
                icon.Key,
                icon.Key.ToLowerInvariant(),
                StringComparison.Ordinal)));
    }

    [TestMethod]
    public void CatalogShouldResolveKnownKeyCaseInsensitively()
    {
        Assert.IsTrue(NeraIconCatalog.TryGetDescriptor("EDIT.COPY", out var icon));
        Assert.IsNotNull(icon);
        Assert.AreEqual("edit.copy", icon.Key);
    }

    [TestMethod]
    public void EveryIconShouldExposeSvgAndAllPngVariants()
    {
        var signature = new byte[8];
        foreach (var icon in NeraIconCatalog.Icons)
        {
            using var svg = NeraIconCatalog.OpenSvg(icon.Key);
            using var reader = new StreamReader(svg, Encoding.UTF8);
            StringAssert.Contains(reader.ReadToEnd(), "<svg");

            foreach (var theme in Enum.GetValues<NeraIconTheme>())
            {
                foreach (var size in NeraIconCatalog.SupportedSizes)
                {
                    using var png = NeraIconCatalog.OpenPng(
                        new NeraIconRequest(icon.Key, size, theme));
                    Assert.AreEqual(8, png.Read(signature, 0, signature.Length));
                    CollectionAssert.AreEqual(
                        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 },
                        signature,
                        $"Invalid PNG for {icon.Key}, {theme}, {size}.");
                }
            }
        }
    }

    [TestMethod]
    public void PngLookupShouldUseNearestSupportedSize()
    {
        using var png = NeraIconCatalog.OpenPng(
            new NeraIconRequest("edit.copy", 31, NeraIconTheme.Light));

        Assert.IsGreaterThan(8, png.Length);
    }
}
