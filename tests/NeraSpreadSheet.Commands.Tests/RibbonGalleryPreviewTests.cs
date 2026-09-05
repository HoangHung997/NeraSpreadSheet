using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Commands.Tests;

[TestClass]
public sealed class RibbonGalleryPreviewTests
{
    [TestMethod]
    public void PreviewShouldCopyBoundedRowMajorVisualCells()
    {
        var source = new List<RibbonGalleryPreviewCell>
        {
            new(0xff156082, 0xffffffff), new(0xff156082, 0xffffffff),
            new(0xffdae9f1, 0xff15212b), new(0xffdae9f1, 0xff15212b),
        };
        var preview = new RibbonGalleryPreview(2, 2, source);
        source.Clear();

        Assert.AreEqual(2, preview.Rows);
        Assert.AreEqual(2, preview.Columns);
        Assert.AreEqual(4, preview.Cells.Count);
        Assert.AreEqual(0xff156082u, preview.Cells[0].BackgroundArgb);
        Assert.AreEqual(0xff15212bu, preview.Cells[3].ForegroundArgb);
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((IList<RibbonGalleryPreviewCell>)preview.Cells).Clear());
    }

    [TestMethod]
    public void PreviewShouldRejectInvalidDimensionsAndBoundEnumeration()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RibbonGalleryPreview(0, 1, []));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RibbonGalleryPreview(1, 17, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => new RibbonGalleryPreview(1, 1, null!));
        Assert.ThrowsExactly<ArgumentException>(() => new RibbonGalleryPreview(2, 2, [default]));
        var enumerated = 0;
        Assert.ThrowsExactly<ArgumentException>(() => new RibbonGalleryPreview(2, 2, InfiniteCells()));
        Assert.AreEqual(5, enumerated);

        IEnumerable<RibbonGalleryPreviewCell> InfiniteCells()
        {
            while (true)
            {
                enumerated++;
                yield return default;
            }
        }
    }

    [TestMethod]
    public void CustomizationShouldRetainGalleryPreviewWithoutInvokingItDuringLayout()
    {
        var invocations = 0;
        RibbonGalleryPreview? Preview(CommandItem item)
        {
            invocations++;
            return item.Value == "blue" ? new RibbonGalleryPreview(1, 1, [new(0xff156082, 0xffffffff)]) : null;
        }
        var definition = new RibbonDefinition([
            new RibbonTabDefinition("home", "Trang đầu", [
                new RibbonGroupDefinition("styles", "Kiểu", [
                    new RibbonItemDefinition("table.style", RibbonItemKind.Gallery) { GalleryPreview = Preview },
                ]),
            ]),
        ]);
        var customized = new RibbonCustomization([
            new RibbonTabCustomization("home", groups: [
                new RibbonGroupCustomization("styles", items: [new RibbonItemCustomization("table.style", IsLarge: true)]),
            ]),
        ]).ApplyTo(definition);
        var item = customized.Tabs[0].Groups[0].Items[0];
        var snapshot = new RibbonPresentationProjector(new CommandRegistry()).Project(customized);
        _ = new RibbonResponsiveLayoutEngine().Layout(snapshot, new RibbonLayoutRequest(820d));

        Assert.AreEqual(0, invocations);
        Assert.IsNotNull(item.GalleryPreview);
        Assert.IsNotNull(item.GalleryPreview(new CommandItem("blue", "Xanh")));
        Assert.IsNull(item.GalleryPreview(new CommandItem("unknown", "Không xác định")));
        Assert.AreEqual(2, invocations);
    }
}
