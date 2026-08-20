using System.Xml;
using DocumentFormat.OpenXml.Packaging;

namespace NeraSpreadSheet.OpenXml;

internal static class OpenXmlPackageGraphValidator
{
    private const int MaxPartCount = 100_000;
    private const int MaxRelationshipsPerContainer = 100_000;
    private const int MaxPartUriCharacters = 32 * 1024;
    private const int MaxRelationshipIdCharacters = 1024;
    private const int MaxExternalTargetCharacters = 64 * 1024;

    public static void Validate(OpenXmlPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        var partsByUri = new Dictionary<string, OpenXmlPart>(
            StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<OpenXmlPartContainer>();
        pending.Push(package);

        while (pending.TryPop(out var container))
        {
            var relationshipIds = new HashSet<string>(StringComparer.Ordinal);
            var relationshipCount = 0;

            foreach (var pair in container.Parts)
            {
                RegisterRelationship(
                    pair.RelationshipId,
                    relationshipIds,
                    ref relationshipCount);

                var part = pair.OpenXmlPart;
                var partUri = ValidatePartUri(part.Uri);
                if (partsByUri.TryGetValue(partUri, out var existingPart))
                {
                    if (!ReferenceEquals(existingPart, part))
                    {
                        throw new InvalidDataException(
                            "The XLSX package contains multiple parts with the same package URI.");
                    }
                    continue;
                }

                partsByUri.Add(partUri, part);
                if (partsByUri.Count > MaxPartCount)
                {
                    throw new InvalidDataException(
                        "The XLSX package relationship graph exceeds the supported part-count limit.");
                }
                pending.Push(part);
            }

            foreach (var relationship in container.ExternalRelationships)
            {
                RegisterRelationship(
                    relationship.Id,
                    relationshipIds,
                    ref relationshipCount);
                ValidateExternalTarget(relationship.Uri);
            }
        }
    }

    internal static string ValidatePartUri(Uri partUri)
    {
        ArgumentNullException.ThrowIfNull(partUri);
        var value = partUri.OriginalString;
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > MaxPartUriCharacters ||
            partUri.IsAbsoluteUri ||
            !value.StartsWith('/') ||
            value.EndsWith('/') ||
            value.Contains('\\') ||
            value.Contains('?') ||
            value.Contains('#') ||
            ContainsControlCharacter(value))
        {
            throw new InvalidDataException(
                "The XLSX package contains an unsafe part URI.");
        }

        var segments = value.Split('/');
        for (var index = 1; index < segments.Length; index++)
        {
            var segment = segments[index];
            if (segment.Length == 0)
            {
                throw new InvalidDataException(
                    "The XLSX package contains an unsafe part URI.");
            }

            string decoded;
            try
            {
                decoded = Uri.UnescapeDataString(segment);
            }
            catch (UriFormatException exception)
            {
                throw new InvalidDataException(
                    "The XLSX package contains an invalid escaped part URI.",
                    exception);
            }

            if (decoded is "." or ".." ||
                decoded.Contains('/') ||
                decoded.Contains('\\') ||
                ContainsControlCharacter(decoded))
            {
                throw new InvalidDataException(
                    "The XLSX package contains an unsafe part URI.");
            }
        }

        return value;
    }

    internal static void ValidateRelationshipId(
        string relationshipId,
        ISet<string> existingIds)
    {
        ArgumentNullException.ThrowIfNull(existingIds);
        if (string.IsNullOrWhiteSpace(relationshipId) ||
            relationshipId.Length > MaxRelationshipIdCharacters ||
            ContainsControlCharacter(relationshipId))
        {
            throw new InvalidDataException(
                "The XLSX package contains an invalid relationship identifier.");
        }

        try
        {
            XmlConvert.VerifyNCName(relationshipId);
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException(
                "The XLSX package contains an invalid relationship identifier.",
                exception);
        }

        if (!existingIds.Add(relationshipId))
        {
            throw new InvalidDataException(
                "The XLSX package contains duplicate relationship identifiers in one relationship container.");
        }
    }

    internal static void ValidateExternalTarget(Uri targetUri)
    {
        ArgumentNullException.ThrowIfNull(targetUri);
        var value = targetUri.OriginalString;
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > MaxExternalTargetCharacters ||
            ContainsControlCharacter(value))
        {
            throw new InvalidDataException(
                "The XLSX package contains an invalid external relationship target.");
        }
    }

    private static void RegisterRelationship(
        string relationshipId,
        ISet<string> relationshipIds,
        ref int relationshipCount)
    {
        relationshipCount++;
        if (relationshipCount > MaxRelationshipsPerContainer)
        {
            throw new InvalidDataException(
                "An XLSX relationship container exceeds the supported relationship-count limit.");
        }
        ValidateRelationshipId(relationshipId, relationshipIds);
    }

    private static bool ContainsControlCharacter(string value)
    {
        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                return true;
            }
        }
        return false;
    }
}
