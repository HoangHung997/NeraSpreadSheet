using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Bars.Core;

namespace NeraSpreadSheet.Commands.Tests;

[TestClass]
public sealed class BarCustomizationSessionTests
{
    [TestMethod]
    public void SessionShouldEditNestedVisibilityAndSiblingOrder()
    {
        var definition = CreateDefinition();
        var session = new BarCustomizationSession(definition);

        Assert.IsTrue(session.Move(
            new BarCustomizationTarget(["file", "file.close"]),
            -1));
        Assert.IsTrue(session.SetVisible(
            new BarCustomizationTarget(["file", "file.save"]),
            false));

        var result = session.Apply();

        Assert.AreEqual("file.close", result.Items[0].Children[0].Id);
        Assert.AreEqual(1, result.Items[0].Children.Count);
        Assert.AreEqual("file.close", result.Items[0].Children[0].CommandId?.Value);
        Assert.AreEqual(3, session.Entries.Count);
        Assert.AreEqual(1, session.Entries[1].Depth);
        Assert.AreEqual(2, definition.Items[0].Children.Count);
    }

    [TestMethod]
    public void SessionShouldPreserveUnknownOverrideAndResetToDefinition()
    {
        var customization = new BarCustomization(
            "main",
            [new BarItemCustomization("module.optional", isVisible: false)]);
        var session = new BarCustomizationSession(CreateDefinition(), customization);

        var retained = session.CreateCustomization();
        session.Reset();

        Assert.IsTrue(retained.Items.Any(item => item.ItemId == "module.optional"));
        Assert.IsFalse(session.CreateCustomization().Items.Any(
            item => item.ItemId == "module.optional"));
    }

    [TestMethod]
    public void SessionShouldRejectCustomizationForAnotherBar()
    {
        var customization = new BarCustomization("other", []);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new BarCustomizationSession(CreateDefinition(), customization));
    }

    private static BarDefinition CreateDefinition() =>
        new(
            "main",
            BarKind.MainMenu,
            [
                BarItemDefinition.Submenu(
                    "Tệp",
                    [
                        BarItemDefinition.Command("file.save"),
                        BarItemDefinition.Command("file.close", order: 1),
                    ],
                    id: "file"),
            ]);
}
