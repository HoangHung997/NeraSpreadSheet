using System.Text;
using System.Text.Json;
using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Ribbon.Core;

/// <summary>
/// Persists <see cref="RibbonCustomization"/> values using the versioned Nera JSON schema.
/// </summary>
public static class RibbonCustomizationJsonSerializer
{
    /// <summary>Gets the stable schema identifier written to persisted documents.</summary>
    public const string SchemaName = "neraspreadsheet.ribbon-customization";

    /// <summary>Gets the schema version written by this serializer.</summary>
    public const int CurrentSchemaVersion = 1;

    private const int MaxPayloadBytes = 1024 * 1024;
    private const int MaxEntries = 10_000;

    /// <summary>
    /// Serializes a customization to canonical schema-v1 JSON.
    /// </summary>
    public static string Serialize(RibbonCustomization customization)
    {
        ArgumentNullException.ThrowIfNull(customization);
        ValidateEntryCount(customization);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("schema", SchemaName);
            writer.WriteNumber("version", CurrentSchemaVersion);
            writer.WritePropertyName("tabs");
            WriteTabs(writer, customization.Tabs);
            writer.WriteEndObject();
        }

        if (stream.Length > MaxPayloadBytes)
        {
            throw new InvalidDataException(
                "The ribbon customization exceeds the supported size limit.");
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Deserializes schema-v1 JSON or migrates a headerless legacy-v0 document in memory.
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// The payload is malformed, exceeds safety limits or uses an unsupported schema version.
    /// </exception>
    public static RibbonCustomization Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        ValidatePayloadSize(json);

        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64,
            });
            var root = RequireObject(document.RootElement, "ribbon customization");
            EnsureUniqueProperties(root, "ribbon customization");
            ValidateSchemaHeader(root);

            var remainingEntries = MaxEntries;
            return new RibbonCustomization(ReadTabs(root, ref remainingEntries));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The ribbon customization is not valid JSON.",
                exception);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            throw new InvalidDataException(
                "The ribbon customization contains invalid values.",
                exception);
        }
    }

    /// <summary>
    /// Validates and rewrites schema-v1 or legacy-v0 JSON as canonical schema-v1 JSON.
    /// </summary>
    public static string MigrateToCurrent(string json) =>
        Serialize(Deserialize(json));

    private static void WriteTabs(
        Utf8JsonWriter writer,
        IReadOnlyList<RibbonTabCustomization> tabs)
    {
        writer.WriteStartArray();
        foreach (var tab in tabs.OrderBy(
                     static value => value.TabId,
                     StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("tabId", tab.TabId);
            writer.WriteBoolean("isVisible", tab.IsVisible);
            WriteNullableNumber(writer, "order", tab.Order);
            writer.WritePropertyName("groups");
            WriteGroups(writer, tab.Groups);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteGroups(
        Utf8JsonWriter writer,
        IReadOnlyList<RibbonGroupCustomization> groups)
    {
        writer.WriteStartArray();
        foreach (var group in groups.OrderBy(
                     static value => value.GroupId,
                     StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("groupId", group.GroupId);
            writer.WriteBoolean("isVisible", group.IsVisible);
            WriteNullableNumber(writer, "order", group.Order);
            writer.WritePropertyName("items");
            WriteItems(writer, group.Items);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteItems(
        Utf8JsonWriter writer,
        IReadOnlyList<RibbonItemCustomization> items)
    {
        writer.WriteStartArray();
        foreach (var item in items.OrderBy(
                     static value => value.CommandId.Value,
                     StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("commandId", item.CommandId.Value);
            writer.WriteBoolean("isVisible", item.IsVisible);
            WriteNullableNumber(writer, "order", item.Order);
            if (item.IsLarge is bool isLarge)
            {
                writer.WriteBoolean("isLarge", isLarge);
            }
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static RibbonTabCustomization[] ReadTabs(
        JsonElement root,
        ref int remainingEntries)
    {
        var array = RequireArrayProperty(root, "tabs", "ribbon customization");
        var result = new RibbonTabCustomization[array.GetArrayLength()];
        var index = 0;
        foreach (var element in array.EnumerateArray())
        {
            CountEntry(ref remainingEntries);
            var tab = RequireObject(element, "ribbon tab customization");
            EnsureUniqueProperties(tab, "ribbon tab customization");
            result[index++] = new RibbonTabCustomization(
                RequireString(tab, "tabId", "ribbon tab customization"),
                ReadBoolean(tab, "isVisible", defaultValue: true),
                ReadNullableInt32(tab, "order"),
                ReadGroups(tab, ref remainingEntries));
        }
        return result;
    }

    private static RibbonGroupCustomization[] ReadGroups(
        JsonElement tab,
        ref int remainingEntries)
    {
        if (!tab.TryGetProperty("groups", out var element))
        {
            return [];
        }

        var array = RequireArray(element, "ribbon group customization collection");
        var result = new RibbonGroupCustomization[array.GetArrayLength()];
        var index = 0;
        foreach (var child in array.EnumerateArray())
        {
            CountEntry(ref remainingEntries);
            var group = RequireObject(child, "ribbon group customization");
            EnsureUniqueProperties(group, "ribbon group customization");
            result[index++] = new RibbonGroupCustomization(
                RequireString(group, "groupId", "ribbon group customization"),
                ReadBoolean(group, "isVisible", defaultValue: true),
                ReadNullableInt32(group, "order"),
                ReadItems(group, ref remainingEntries));
        }
        return result;
    }

    private static RibbonItemCustomization[] ReadItems(
        JsonElement group,
        ref int remainingEntries)
    {
        if (!group.TryGetProperty("items", out var element))
        {
            return [];
        }

        var array = RequireArray(element, "ribbon item customization collection");
        var result = new RibbonItemCustomization[array.GetArrayLength()];
        var index = 0;
        foreach (var child in array.EnumerateArray())
        {
            CountEntry(ref remainingEntries);
            var item = RequireObject(child, "ribbon item customization");
            EnsureUniqueProperties(item, "ribbon item customization");
            result[index++] = new RibbonItemCustomization(
                new CommandId(RequireString(
                    item,
                    "commandId",
                    "ribbon item customization")),
                ReadBoolean(item, "isVisible", defaultValue: true),
                ReadNullableInt32(item, "order"),
                ReadNullableBoolean(item, "isLarge"));
        }
        return result;
    }

    private static void ValidateSchemaHeader(JsonElement root)
    {
        var hasSchema = root.TryGetProperty("schema", out var schema);
        var hasVersion = root.TryGetProperty("version", out var version);
        if (!hasSchema && !hasVersion)
        {
            return;
        }
        if (!hasSchema || !hasVersion ||
            schema.ValueKind != JsonValueKind.String ||
            !string.Equals(schema.GetString(), SchemaName, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The ribbon customization schema header is invalid.");
        }
        if (version.ValueKind != JsonValueKind.Number ||
            !version.TryGetInt32(out var parsedVersion) ||
            parsedVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"The ribbon customization schema version is unsupported; expected {CurrentSchemaVersion}.");
        }
    }

    private static void ValidatePayloadSize(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaxPayloadBytes)
        {
            throw new InvalidDataException(
                "The ribbon customization exceeds the supported size limit.");
        }
    }

    private static void ValidateEntryCount(RibbonCustomization customization)
    {
        var remainingEntries = MaxEntries;
        foreach (var tab in customization.Tabs)
        {
            CountEntry(ref remainingEntries);
            foreach (var group in tab.Groups)
            {
                CountEntry(ref remainingEntries);
                foreach (var _ in group.Items)
                {
                    CountEntry(ref remainingEntries);
                }
            }
        }
    }

    private static JsonElement RequireObject(JsonElement value, string scope)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"The {scope} must be a JSON object.");
        }
        return value;
    }

    private static JsonElement RequireArrayProperty(
        JsonElement owner,
        string propertyName,
        string scope)
    {
        if (!owner.TryGetProperty(propertyName, out var value))
        {
            throw new InvalidDataException(
                $"The {scope} is missing '{propertyName}'.");
        }
        return RequireArray(value, $"{scope} '{propertyName}'");
    }

    private static JsonElement RequireArray(JsonElement value, string scope)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"The {scope} must be a JSON array.");
        }
        return value;
    }

    private static string RequireString(
        JsonElement owner,
        string propertyName,
        string scope)
    {
        if (!owner.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException(
                $"The {scope} is missing string '{propertyName}'.");
        }
        return value.GetString()!;
    }

    private static bool ReadBoolean(
        JsonElement owner,
        string propertyName,
        bool defaultValue)
    {
        if (!owner.TryGetProperty(propertyName, out var value))
        {
            return defaultValue;
        }
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidDataException($"'{propertyName}' must be a boolean.");
        }
        return value.GetBoolean();
    }

    private static bool? ReadNullableBoolean(
        JsonElement owner,
        string propertyName)
    {
        if (!owner.TryGetProperty(propertyName, out var value) ||
            value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidDataException($"'{propertyName}' must be a boolean or null.");
        }
        return value.GetBoolean();
    }

    private static int? ReadNullableInt32(
        JsonElement owner,
        string propertyName)
    {
        if (!owner.TryGetProperty(propertyName, out var value) ||
            value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        if (value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var result))
        {
            throw new InvalidDataException($"'{propertyName}' must be a 32-bit integer or null.");
        }
        return result;
    }

    private static void EnsureUniqueProperties(JsonElement value, string scope)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                throw new InvalidDataException(
                    $"The {scope} contains duplicate property '{property.Name}'.");
            }
        }
    }

    private static void CountEntry(ref int remainingEntries)
    {
        remainingEntries--;
        if (remainingEntries < 0)
        {
            throw new InvalidDataException(
                "The ribbon customization contains too many entries.");
        }
    }

    private static void WriteNullableNumber(
        Utf8JsonWriter writer,
        string propertyName,
        int? value)
    {
        if (value is int number)
        {
            writer.WriteNumber(propertyName, number);
        }
    }
}
