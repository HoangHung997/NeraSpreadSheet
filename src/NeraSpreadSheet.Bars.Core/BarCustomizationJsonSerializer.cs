using System.Text;
using System.Text.Json;

namespace NeraSpreadSheet.Bars.Core;

/// <summary>
/// Persists <see cref="BarCustomization"/> values using the versioned Nera JSON schema.
/// </summary>
public static class BarCustomizationJsonSerializer
{
    /// <summary>Gets the stable schema identifier written to persisted documents.</summary>
    public const string SchemaName = "neraspreadsheet.bar-customization";

    /// <summary>Gets the schema version written by this serializer.</summary>
    public const int CurrentSchemaVersion = 1;

    private const int MaxPayloadBytes = 1024 * 1024;
    private const int MaxEntries = 10_000;
    private const int MaxItemNesting = 31;

    /// <summary>
    /// Serializes a customization to canonical schema-v1 JSON.
    /// </summary>
    public static string Serialize(BarCustomization customization)
    {
        ArgumentNullException.ThrowIfNull(customization);
        var remainingEntries = MaxEntries;
        ValidateItems(customization.Items, ref remainingEntries, depth: 1);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("schema", SchemaName);
            writer.WriteNumber("version", CurrentSchemaVersion);
            writer.WriteString("barId", customization.BarId);
            writer.WritePropertyName("items");
            WriteItems(writer, customization.Items);
            writer.WriteEndObject();
        }

        if (stream.Length > MaxPayloadBytes)
        {
            throw new InvalidDataException(
                "The bar customization exceeds the supported size limit.");
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Deserializes schema-v1 JSON or migrates a headerless legacy-v0 document in memory.
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// The payload is malformed, exceeds safety limits or uses an unsupported schema version.
    /// </exception>
    public static BarCustomization Deserialize(string json)
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
            var root = RequireObject(document.RootElement, "bar customization");
            EnsureUniqueProperties(root, "bar customization");
            ValidateSchemaHeader(root);

            var remainingEntries = MaxEntries;
            return new BarCustomization(
                RequireString(root, "barId", "bar customization"),
                ReadItems(
                    RequireArrayProperty(
                        root,
                        "items",
                        "bar customization"),
                    ref remainingEntries));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The bar customization is not valid JSON.",
                exception);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            throw new InvalidDataException(
                "The bar customization contains invalid values.",
                exception);
        }
    }

    /// <summary>
    /// Validates and rewrites schema-v1 or legacy-v0 JSON as canonical schema-v1 JSON.
    /// </summary>
    public static string MigrateToCurrent(string json) =>
        Serialize(Deserialize(json));

    private static void WriteItems(
        Utf8JsonWriter writer,
        IReadOnlyList<BarItemCustomization> items)
    {
        writer.WriteStartArray();
        foreach (var item in items.OrderBy(
                     static value => value.ItemId,
                     StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("itemId", item.ItemId);
            writer.WriteBoolean("isVisible", item.IsVisible);
            if (item.Order is int order)
            {
                writer.WriteNumber("order", order);
            }
            writer.WritePropertyName("children");
            WriteItems(writer, item.Children);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static BarItemCustomization[] ReadItems(
        JsonElement array,
        ref int remainingEntries)
    {
        var result = new BarItemCustomization[array.GetArrayLength()];
        var index = 0;
        foreach (var child in array.EnumerateArray())
        {
            CountEntry(ref remainingEntries);
            var item = RequireObject(child, "bar item customization");
            EnsureUniqueProperties(item, "bar item customization");
            result[index++] = new BarItemCustomization(
                RequireString(item, "itemId", "bar item customization"),
                ReadBoolean(item, "isVisible", defaultValue: true),
                ReadNullableInt32(item, "order"),
                ReadChildren(item, ref remainingEntries));
        }
        return result;
    }

    private static BarItemCustomization[] ReadChildren(
        JsonElement item,
        ref int remainingEntries)
    {
        if (!item.TryGetProperty("children", out var element))
        {
            return [];
        }
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "The bar child customization collection must be a JSON array.");
        }
        return ReadItems(element, ref remainingEntries);
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
                "The bar customization schema header is invalid.");
        }
        if (version.ValueKind != JsonValueKind.Number ||
            !version.TryGetInt32(out var parsedVersion) ||
            parsedVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"The bar customization schema version is unsupported; expected {CurrentSchemaVersion}.");
        }
    }

    private static void ValidatePayloadSize(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaxPayloadBytes)
        {
            throw new InvalidDataException(
                "The bar customization exceeds the supported size limit.");
        }
    }

    private static void ValidateItems(
        IReadOnlyList<BarItemCustomization> items,
        ref int remainingEntries,
        int depth)
    {
        if (depth > MaxItemNesting)
        {
            throw new InvalidDataException(
                "The bar customization exceeds the supported nesting limit.");
        }
        foreach (var item in items)
        {
            CountEntry(ref remainingEntries);
            if (item.Children.Count > 0)
            {
                ValidateItems(item.Children, ref remainingEntries, depth + 1);
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

    private static JsonElement RequireArray(JsonElement value, string scope)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"The {scope} must be a JSON array.");
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
                "The bar customization contains too many entries.");
        }
    }
}
