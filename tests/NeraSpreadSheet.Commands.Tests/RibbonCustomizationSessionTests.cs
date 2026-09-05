using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Commands.Tests;

[TestClass]
public sealed class RibbonCustomizationSessionTests
{
    [TestMethod]
    public void SessionShouldEditVisibilityOrderAndSizeWithoutMutatingDefinition()
    {
        var definition = CreateDefinition();
        var session = new RibbonCustomizationSession(
            definition,
            commandCaption: commandId => $"Caption {commandId.Value}");

        Assert.IsTrue(session.Move(
            RibbonCustomizationTarget.Tab("view"),
            -1));
        Assert.IsTrue(session.Move(
            RibbonCustomizationTarget.Command("HOME", "CLIPBOARD", "EDIT.PASTE"),
            -1));
        Assert.IsTrue(session.SetVisible(
            RibbonCustomizationTarget.Group("home", "font"),
            false));
        Assert.IsTrue(session.SetLarge(
            RibbonCustomizationTarget.Command("home", "clipboard", "edit.copy"),
            true));

        var result = session.Apply();
        var entries = session.Entries;

        Assert.AreEqual(
            "view,home",
            string.Join(',', result.Tabs.Select(tab => tab.Id)));
        Assert.AreEqual(
            "edit.paste,edit.copy",
            string.Join(
                ',',
                result.Tabs[1].Groups[0].Items.Select(item => item.CommandId.Value)));
        Assert.AreEqual(1, result.Tabs[1].Groups.Count);
        Assert.IsTrue(result.Tabs[1].Groups[0].Items[1].IsLarge);
        Assert.AreEqual("home", definition.Tabs[0].Id);
        Assert.AreEqual(2, definition.Tabs[0].Groups.Count);
        Assert.AreEqual(
            "Caption edit.copy",
            entries.Single(entry => entry.Target.CommandId == "edit.copy").Caption);
    }

    [TestMethod]
    public void SessionShouldPreserveUnknownOverridesUntilReset()
    {
        var customization = new RibbonCustomization(
        [
            new RibbonTabCustomization(
                "home",
                groups:
                [
                    new RibbonGroupCustomization(
                        "clipboard",
                        items:
                        [new RibbonItemCustomization("module.optional", IsVisible: false)]),
                ]),
            new RibbonTabCustomization("module.tab", isVisible: false),
        ]);
        var session = new RibbonCustomizationSession(CreateDefinition(), customization);

        var retained = session.CreateCustomization();
        session.Reset();
        var reset = session.CreateCustomization();

        Assert.IsTrue(retained.Tabs.Any(tab => tab.TabId == "module.tab"));
        Assert.IsTrue(retained.Tabs
            .Single(tab => tab.TabId == "home")
            .Groups.Single(group => group.GroupId == "clipboard")
            .Items.Any(item => item.CommandId == "module.optional"));
        Assert.IsFalse(reset.Tabs.Any(tab => tab.TabId == "module.tab"));
    }

    [TestMethod]
    public void SessionShouldRejectSizeChangeForNonCommandTarget()
    {
        var session = new RibbonCustomizationSession(CreateDefinition());

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.SetLarge(RibbonCustomizationTarget.Tab("home"), true));
    }

    private static RibbonDefinition CreateDefinition() =>
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
                            new RibbonItemDefinition("edit.copy"),
                            new RibbonItemDefinition("edit.paste", Order: 1),
                        ]),
                    new RibbonGroupDefinition(
                        "font",
                        "Phông",
                        [new RibbonItemDefinition("format.bold")],
                        order: 1),
                ]),
            new RibbonTabDefinition(
                "view",
                "Xem",
                [new RibbonGroupDefinition("display", "Hiển thị", [])],
                order: 1),
        ]);
}
