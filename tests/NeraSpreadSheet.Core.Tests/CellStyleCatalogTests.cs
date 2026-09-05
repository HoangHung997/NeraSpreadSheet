using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class CellStyleCatalogTests
{
    [TestMethod]
    public void InternDeduplicatesEquivalentImmutableStyles()
    {
        var catalog = new CellStyleCatalog();
        var style = CellStyle.Default with
        {
            Font = CellStyle.Default.Font with { Weight = 700, Color = new ColorRgba(10, 20, 30) },
        };
        var first = catalog.Intern(style);
        var second = catalog.Intern(style with { });
        Assert.AreEqual(first, second);
        Assert.AreEqual(2, catalog.Count);
    }

    [TestMethod]
    public void WorkbookOwnsIndependentStyleCatalog()
    {
        var first = new Workbook();
        var second = new Workbook();
        first.Styles.Intern(CellStyle.Default with { Font = CellStyle.Default.Font with { Italic = true } });
        Assert.AreEqual(2, first.Styles.Count);
        Assert.AreEqual(1, second.Styles.Count);
    }
}
