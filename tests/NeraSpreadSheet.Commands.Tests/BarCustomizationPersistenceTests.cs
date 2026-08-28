using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Bars.Core;

namespace NeraSpreadSheet.Commands.Tests;

[TestClass]
public sealed class BarCustomizationPersistenceTests
{
    [TestMethod]
    public void SerializeShouldProduceCanonicalNestedRoundTripJson()
    {
        var first = new BarCustomization(
            "main",
            [
                new BarItemCustomization("split", isVisible: false),
                new BarItemCustomization(
                    "export",
                    order: -2,
                    children:
                    [
                        new BarItemCustomization("file.pdf", isVisible: false),
                        new BarItemCustomization("file.csv", order: 3),
                    ]),
            ]);
        var equivalent = new BarCustomization(
            "main",
            [
                first.Items[1],
                first.Items[0],
            ]);

        var json = BarCustomizationJsonSerializer.Serialize(first);
        var roundTrip = BarCustomizationJsonSerializer.Deserialize(json);

        Assert.AreEqual(
            json,
            BarCustomizationJsonSerializer.Serialize(equivalent));
        Assert.AreEqual("main", roundTrip.BarId);
        Assert.AreEqual("export", roundTrip.Items[0].ItemId);
        Assert.AreEqual(-2, roundTrip.Items[0].Order);
        Assert.AreEqual("file.csv", roundTrip.Items[0].Children[0].ItemId);
        Assert.AreEqual(3, roundTrip.Items[0].Children[0].Order);
        Assert.IsFalse(roundTrip.Items[0].Children[1].IsVisible);
        Assert.AreEqual("split", roundTrip.Items[1].ItemId);
        Assert.IsFalse(roundTrip.Items[1].IsVisible);
    }

    [TestMethod]
    public void MigrateToCurrentShouldUpgradeHeaderlessLegacyDocument()
    {
        const string LegacyJson =
            """
            {
              "barId": "main",
              "items": [
                {
                  "itemId": "export",
                  "children": [ { "itemId": "file.pdf", "isVisible": false } ]
                }
              ]
            }
            """;

        var migrated = BarCustomizationJsonSerializer.MigrateToCurrent(LegacyJson);
        using var document = JsonDocument.Parse(migrated);
        var customization = BarCustomizationJsonSerializer.Deserialize(migrated);

        Assert.AreEqual(
            BarCustomizationJsonSerializer.SchemaName,
            document.RootElement.GetProperty("schema").GetString());
        Assert.AreEqual(
            BarCustomizationJsonSerializer.CurrentSchemaVersion,
            document.RootElement.GetProperty("version").GetInt32());
        Assert.IsTrue(customization.Items[0].IsVisible);
        Assert.IsFalse(customization.Items[0].Children[0].IsVisible);
    }

    [TestMethod]
    [DataRow("{\"schema\":\"wrong\",\"version\":1,\"barId\":\"main\",\"items\":[]}")]
    [DataRow("{\"schema\":\"neraspreadsheet.bar-customization\",\"version\":2,\"barId\":\"main\",\"items\":[]}")]
    [DataRow("{\"version\":1,\"barId\":\"main\",\"items\":[]}")]
    public void DeserializeShouldRejectUnsupportedSchemaHeaders(string json)
    {
        Assert.ThrowsExactly<InvalidDataException>(() =>
            BarCustomizationJsonSerializer.Deserialize(json));
    }

    [TestMethod]
    public void DeserializeShouldRejectMalformedAndAmbiguousDocuments()
    {
        var malformed = Assert.ThrowsExactly<InvalidDataException>(() =>
            BarCustomizationJsonSerializer.Deserialize("{"));
        Assert.IsInstanceOfType<JsonException>(malformed.InnerException);

        Assert.ThrowsExactly<InvalidDataException>(() =>
            BarCustomizationJsonSerializer.Deserialize(
                "{\"barId\":\"main\",\"items\":[],\"items\":[]}"));

        var duplicateTarget = Assert.ThrowsExactly<InvalidDataException>(() =>
            BarCustomizationJsonSerializer.Deserialize(
                "{\"barId\":\"main\",\"items\":[{\"itemId\":\"open\"},{\"itemId\":\"OPEN\"}]}"));
        Assert.IsInstanceOfType<InvalidOperationException>(duplicateTarget.InnerException);
    }

    [TestMethod]
    public void DeserializeShouldIgnoreUnknownFieldsAndUseDocumentedDefaults()
    {
        const string Json =
            """
            {
              "schema": "neraspreadsheet.bar-customization",
              "version": 1,
              "barId": "main",
              "future": { "enabled": true },
              "items": [ { "itemId": "file.open", "futureItemValue": 42 } ]
            }
            """;

        var customization = BarCustomizationJsonSerializer.Deserialize(Json);

        Assert.AreEqual("main", customization.BarId);
        Assert.AreEqual(1, customization.Items.Count);
        Assert.IsTrue(customization.Items[0].IsVisible);
        Assert.IsNull(customization.Items[0].Order);
        Assert.AreEqual(0, customization.Items[0].Children.Count);
    }

    [TestMethod]
    public void PersistenceShouldEnforcePayloadEntryAndNestingLimits()
    {
        var oversizedJson =
            "{\"barId\":\"main\",\"items\":[],\"padding\":\"" +
            new string('x', 1024 * 1024) +
            "\"}";
        Assert.ThrowsExactly<InvalidDataException>(() =>
            BarCustomizationJsonSerializer.Deserialize(oversizedJson));

        var tooManyEntries = new BarCustomization(
            "main",
            Enumerable.Range(0, 10_001)
                .Select(index => new BarItemCustomization($"item-{index}")));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            BarCustomizationJsonSerializer.Serialize(tooManyEntries));

        var nested = new BarItemCustomization("level-32");
        for (var depth = 31; depth >= 1; depth--)
        {
            nested = new BarItemCustomization($"level-{depth}", children: [nested]);
        }
        var tooDeep = new BarCustomization("main", [nested]);
        Assert.ThrowsExactly<InvalidDataException>(() =>
            BarCustomizationJsonSerializer.Serialize(tooDeep));
    }
}
