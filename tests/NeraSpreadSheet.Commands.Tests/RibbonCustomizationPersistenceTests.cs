using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Commands.Tests;

[TestClass]
public sealed class RibbonCustomizationPersistenceTests
{
    [TestMethod]
    public void SerializeShouldProduceCanonicalRoundTripJson()
    {
        var first = new RibbonCustomization(
        [
            new RibbonTabCustomization("view", isVisible: false),
            new RibbonTabCustomization(
                "home",
                order: 4,
                groups:
                [
                    new RibbonGroupCustomization(
                        "clipboard",
                        items:
                        [
                            new RibbonItemCustomization(
                                "edit.paste",
                                Order: -2,
                                IsLarge: true),
                        ]),
                ]),
        ]);
        var equivalent = new RibbonCustomization(
        [
            first.Tabs[1],
            first.Tabs[0],
        ]);

        var json = RibbonCustomizationJsonSerializer.Serialize(first);
        var roundTrip = RibbonCustomizationJsonSerializer.Deserialize(json);

        Assert.AreEqual(
            json,
            RibbonCustomizationJsonSerializer.Serialize(equivalent));
        Assert.AreEqual(2, roundTrip.Tabs.Count);
        Assert.AreEqual("home", roundTrip.Tabs[0].TabId);
        Assert.AreEqual(4, roundTrip.Tabs[0].Order);
        Assert.AreEqual("edit.paste", roundTrip.Tabs[0].Groups[0].Items[0].CommandId.Value);
        Assert.AreEqual(-2, roundTrip.Tabs[0].Groups[0].Items[0].Order);
        Assert.IsTrue(roundTrip.Tabs[0].Groups[0].Items[0].IsLarge);
        Assert.AreEqual("view", roundTrip.Tabs[1].TabId);
        Assert.IsFalse(roundTrip.Tabs[1].IsVisible);
    }

    [TestMethod]
    public void MigrateToCurrentShouldUpgradeHeaderlessLegacyDocument()
    {
        const string LegacyJson =
            """
            {
              "tabs": [
                {
                  "tabId": "home",
                  "groups": [
                    {
                      "groupId": "clipboard",
                      "items": [
                        { "commandId": "edit.copy", "isVisible": false, "isLarge": true }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        var migrated = RibbonCustomizationJsonSerializer.MigrateToCurrent(LegacyJson);
        using var document = JsonDocument.Parse(migrated);
        var customization = RibbonCustomizationJsonSerializer.Deserialize(migrated);

        Assert.AreEqual(
            RibbonCustomizationJsonSerializer.SchemaName,
            document.RootElement.GetProperty("schema").GetString());
        Assert.AreEqual(
            RibbonCustomizationJsonSerializer.CurrentSchemaVersion,
            document.RootElement.GetProperty("version").GetInt32());
        Assert.IsTrue(customization.Tabs[0].IsVisible);
        Assert.IsFalse(customization.Tabs[0].Groups[0].Items[0].IsVisible);
        Assert.IsTrue(customization.Tabs[0].Groups[0].Items[0].IsLarge);
    }

    [TestMethod]
    [DataRow("{\"schema\":\"wrong\",\"version\":1,\"tabs\":[]}")]
    [DataRow("{\"schema\":\"neraspreadsheet.ribbon-customization\",\"version\":2,\"tabs\":[]}")]
    [DataRow("{\"schema\":\"neraspreadsheet.ribbon-customization\",\"tabs\":[]}")]
    public void DeserializeShouldRejectUnsupportedSchemaHeaders(string json)
    {
        Assert.ThrowsExactly<InvalidDataException>(() =>
            RibbonCustomizationJsonSerializer.Deserialize(json));
    }

    [TestMethod]
    public void DeserializeShouldRejectMalformedAndAmbiguousDocuments()
    {
        var malformed = Assert.ThrowsExactly<InvalidDataException>(() =>
            RibbonCustomizationJsonSerializer.Deserialize("{"));
        Assert.IsInstanceOfType<JsonException>(malformed.InnerException);

        Assert.ThrowsExactly<InvalidDataException>(() =>
            RibbonCustomizationJsonSerializer.Deserialize(
                "{\"tabs\":[],\"tabs\":[]}"));

        var duplicateTarget = Assert.ThrowsExactly<InvalidDataException>(() =>
            RibbonCustomizationJsonSerializer.Deserialize(
                "{\"tabs\":[{\"tabId\":\"home\"},{\"tabId\":\"HOME\"}]}"));
        Assert.IsInstanceOfType<InvalidOperationException>(duplicateTarget.InnerException);
    }

    [TestMethod]
    public void DeserializeShouldIgnoreUnknownFieldsAndUseDocumentedDefaults()
    {
        const string Json =
            """
            {
              "schema": "neraspreadsheet.ribbon-customization",
              "version": 1,
              "future": { "enabled": true },
              "tabs": [ { "tabId": "home", "futureTabValue": 42 } ]
            }
            """;

        var customization = RibbonCustomizationJsonSerializer.Deserialize(Json);

        Assert.AreEqual(1, customization.Tabs.Count);
        Assert.IsTrue(customization.Tabs[0].IsVisible);
        Assert.IsNull(customization.Tabs[0].Order);
        Assert.AreEqual(0, customization.Tabs[0].Groups.Count);
    }

    [TestMethod]
    public void PersistenceShouldEnforcePayloadAndEntryLimits()
    {
        var oversizedJson =
            "{\"tabs\":[],\"padding\":\"" +
            new string('x', 1024 * 1024) +
            "\"}";
        Assert.ThrowsExactly<InvalidDataException>(() =>
            RibbonCustomizationJsonSerializer.Deserialize(oversizedJson));

        var tooManyEntries = new RibbonCustomization(
            Enumerable.Range(0, 10_001)
                .Select(index => new RibbonTabCustomization($"tab-{index}")));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            RibbonCustomizationJsonSerializer.Serialize(tooManyEntries));
    }
}
