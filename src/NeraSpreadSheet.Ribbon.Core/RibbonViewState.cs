using System.Text.Json;

namespace NeraSpreadSheet.Ribbon.Core;

/// <summary>Persisted user view state independent from Ribbon customization.</summary>
public sealed record RibbonViewState(bool IsMinimized = false);

/// <summary>Versioned serializer for the small persisted Ribbon view-state payload.</summary>
public static class RibbonViewStateJsonSerializer
{
    private const string Schema = "neraspreadsheet.ribbon-view-state";
    private const int MaximumDocumentLength = 65_536;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        MaxDepth = 8,
        PropertyNameCaseInsensitive = false,
    };

    public static string Serialize(RibbonViewState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return JsonSerializer.Serialize(new Payload(Schema, 1, state.IsMinimized), SerializerOptions);
    }

    public static RibbonViewState Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException("Ribbon view-state JSON is required.");
        }
        if (json.Length > MaximumDocumentLength)
        {
            throw new InvalidDataException("Ribbon view-state JSON exceeds 64 KiB.");
        }
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            string[] duplicates = document.RootElement.EnumerateObject()
                .GroupBy(static property => property.Name, StringComparer.Ordinal)
                .Where(static group => group.Count() > 1)
                .Select(static group => group.Key)
                .ToArray();
            if (duplicates.Length > 0)
            {
                throw new InvalidDataException(
                    $"Duplicate Ribbon view-state property: {string.Join(", ", duplicates)}.");
            }
            var payload = JsonSerializer.Deserialize<Payload>(json, SerializerOptions) ??
                throw new JsonException("Ribbon view-state payload is empty.");
            if (!string.Equals(payload.Schema, Schema, StringComparison.Ordinal) ||
                payload.Version != 1)
            {
                throw new InvalidDataException("Unsupported Ribbon view-state schema or version.");
            }
            return new RibbonViewState(payload.IsMinimized);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new InvalidDataException("Invalid Ribbon view-state JSON.", exception);
        }
    }

    private sealed record Payload(string Schema, int Version, bool IsMinimized);
}
