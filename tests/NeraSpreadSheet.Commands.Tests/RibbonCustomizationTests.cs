using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Commands.Tests;

[TestClass]
public sealed class RibbonCustomizationTests
{
    [TestMethod]
    public void ApplyToShouldHideReorderAndResizeWithoutMutatingSource()
    {
        var source = CreateRibbon();
        var customization = new RibbonCustomization(
        [
            new RibbonTabCustomization("view", order: -10),
            new RibbonTabCustomization(
                "home",
                order: 10,
                groups:
                [
                    new RibbonGroupCustomization("font", isVisible: false),
                    new RibbonGroupCustomization(
                        "clipboard",
                        order: 5,
                        items:
                        [
                            new RibbonItemCustomization(
                                "edit.copy",
                                IsVisible: false),
                            new RibbonItemCustomization(
                                "edit.paste",
                                Order: -5,
                                IsLarge: true),
                        ]),
                ]),
        ]);

        var customized = customization.ApplyTo(source);

        Assert.AreEqual(2, customized.Tabs.Count);
        Assert.AreEqual("view", customized.Tabs[0].Id);
        Assert.AreEqual("home", customized.Tabs[1].Id);
        Assert.AreEqual(2, source.Tabs[0].Groups.Count);
        Assert.AreEqual(1, customized.Tabs[1].Groups.Count);
        Assert.AreEqual("clipboard", customized.Tabs[1].Groups[0].Id);
        Assert.AreEqual(2, source.Tabs[0].Groups[0].Items.Count);
        Assert.AreEqual(1, customized.Tabs[1].Groups[0].Items.Count);
        Assert.AreEqual(
            new CommandId("edit.paste"),
            customized.Tabs[1].Groups[0].Items[0].CommandId);
        Assert.IsTrue(customized.Tabs[1].Groups[0].Items[0].IsLarge);
        Assert.AreEqual(12, customized.Tabs[1].Groups[0].CollapsePriority);
    }

    [TestMethod]
    public void ApplyToShouldIgnoreUnknownTargetsCaseInsensitively()
    {
        var source = CreateRibbon();
        var customization = new RibbonCustomization(
        [
            new RibbonTabCustomization(
                "HOME",
                groups:
                [
                    new RibbonGroupCustomization(
                        "CLIPBOARD",
                        items:
                        [
                            new RibbonItemCustomization("EDIT.PASTE", Order: -10),
                            new RibbonItemCustomization("missing.command", IsVisible: false),
                        ]),
                    new RibbonGroupCustomization("missing.group", isVisible: false),
                ]),
            new RibbonTabCustomization("missing.tab", isVisible: false),
        ]);

        var customized = customization.ApplyTo(source);

        Assert.AreEqual(2, customized.Tabs.Count);
        Assert.AreEqual(
            new CommandId("edit.paste"),
            customized.Tabs[0].Groups[0].Items[0].CommandId);
    }

    [TestMethod]
    public void ConstructorShouldRejectDuplicateCustomizationTargets()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new RibbonCustomization(
            [
                new RibbonTabCustomization("home"),
                new RibbonTabCustomization("HOME"),
            ]));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new RibbonGroupCustomization(
                "clipboard",
                items:
                [
                    new RibbonItemCustomization("edit.copy"),
                    new RibbonItemCustomization("EDIT.COPY"),
                ]));
    }

    [TestMethod]
    public void DefinitionsShouldRejectAmbiguousGroupAndCommandIds()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new RibbonTabDefinition(
                "home",
                "Trang đầu",
                [
                    new RibbonGroupDefinition("font", "Phông", []),
                    new RibbonGroupDefinition("FONT", "Phông khác", []),
                ]));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new RibbonGroupDefinition(
                "clipboard",
                "Bảng tạm",
                [
                    new RibbonItemDefinition("edit.copy"),
                    new RibbonItemDefinition("EDIT.COPY"),
                ]));
    }

    private static RibbonDefinition CreateRibbon() =>
        new(
        [
            new RibbonTabDefinition(
                "home",
                "Trang đầu",
                [
                    new RibbonGroupDefinition(
                        "clipboard",
                        "Bảng tạm",
                        [
                            new RibbonItemDefinition("edit.copy", Order: 0),
                            new RibbonItemDefinition("edit.paste", Order: 1),
                        ],
                        order: 0,
                        collapsePriority: 12),
                    new RibbonGroupDefinition(
                        "font",
                        "Phông",
                        [new RibbonItemDefinition("format.bold")],
                        order: 1),
                ],
                order: 0),
            new RibbonTabDefinition(
                "view",
                "Xem",
                [new RibbonGroupDefinition("window", "Cửa sổ", [])],
                order: 1),
        ]);
}
