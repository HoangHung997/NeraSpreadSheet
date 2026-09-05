using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Bars.Core;

namespace NeraSpreadSheet.Commands.Tests;

[TestClass]
public sealed class BarCustomizationTests
{
    [TestMethod]
    public void ApplyToShouldCustomizeNestedItemsWithoutMutatingSource()
    {
        var source = CreateBar();
        var customization = new BarCustomization(
            "main",
            [
                new BarItemCustomization("split", isVisible: false),
                new BarItemCustomization(
                    "export",
                    order: -10,
                    children:
                    [
                        new BarItemCustomization("file.pdf", isVisible: false),
                        new BarItemCustomization("file.csv", order: -5),
                    ]),
            ]);

        var customized = customization.ApplyTo(source);

        Assert.AreEqual(3, source.Items.Count);
        Assert.AreEqual(2, customized.Items.Count);
        Assert.AreEqual("export", customized.Items[0].Id);
        Assert.AreEqual(2, source.Items[2].Children.Count);
        Assert.AreEqual(1, customized.Items[0].Children.Count);
        Assert.AreEqual(
            new CommandId("file.csv"),
            customized.Items[0].Children[0].CommandId);
    }

    [TestMethod]
    public void ApplyToShouldIgnoreUnknownTargetsAndMatchIdsCaseInsensitively()
    {
        var source = CreateBar();
        var customization = new BarCustomization(
            "MAIN",
            [
                new BarItemCustomization("FILE.OPEN", order: 10),
                new BarItemCustomization("missing", isVisible: false),
            ]);

        var customized = customization.ApplyTo(source);

        Assert.AreEqual(3, customized.Items.Count);
        Assert.AreEqual("file.open", customized.Items[^1].Id);
    }

    [TestMethod]
    public void ApplyToShouldRejectARequestForAnotherBar()
    {
        var customization = new BarCustomization("other", []);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            customization.ApplyTo(CreateBar()));
    }

    [TestMethod]
    public void ConstructorsShouldRejectDuplicateStableIds()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new BarCustomization(
                "main",
                [
                    new BarItemCustomization("file.open"),
                    new BarItemCustomization("FILE.OPEN"),
                ]));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new BarDefinition(
                "main",
                BarKind.Toolbar,
                [
                    BarItemDefinition.Command("file.open"),
                    BarItemDefinition.Separator("FILE.OPEN"),
                ]));
    }

    [TestMethod]
    public void ItemFactoriesShouldProvideStableTrimmedIdsAndSortedChildren()
    {
        var command = BarItemDefinition.Command("file.open", " custom.open ");
        var separator = BarItemDefinition.Separator(" split ");
        var submenu = BarItemDefinition.Submenu(
            " Xuất ",
            [
                BarItemDefinition.Command("file.pdf", order: 10),
                BarItemDefinition.Command("file.csv", order: -10),
            ],
            " export ");

        Assert.AreEqual("custom.open", command.Id);
        Assert.AreEqual("split", separator.Id);
        Assert.AreEqual("export", submenu.Id);
        Assert.AreEqual("Xuất", submenu.Caption);
        Assert.AreEqual("file.csv", submenu.Children[0].Id);
    }

    private static BarDefinition CreateBar() =>
        new(
            "main",
            BarKind.Toolbar,
            [
                BarItemDefinition.Command("file.open", order: 0),
                BarItemDefinition.Separator("split", order: 1),
                BarItemDefinition.Submenu(
                    "Xuất",
                    [
                        BarItemDefinition.Command("file.pdf", order: 0),
                        BarItemDefinition.Command("file.csv", order: 1),
                    ],
                    "export",
                    order: 2),
            ],
            "Chính");
}
