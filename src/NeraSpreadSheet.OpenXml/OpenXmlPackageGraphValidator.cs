using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;

namespace NeraSpreadSheet.OpenXml;

internal static class OpenXmlPackageGraphValidator
{
    private const string RelationshipNamespace =
        "http://schemas.openxmlformats.org/package/2006/relationships";
    private const int MaxPartCount = 100_000;
    private const int MaxRelationshipsPerContainer = 100_000;
    private const int MaxPartUriCharacters = 32 * 1024;
    private const int MaxRelationshipIdCharacters = 1024;
    private const int MaxRelationshipTypeCharacters = 64 * 1024;
    private const int MaxReferenceTargetCharacters = 64 * 1024;

    public static void Validate(byte[] packageBytes)
    {
        ArgumentNullException.ThrowIfNull(packageBytes);
        using var stream = new MemoryStream(
            packageBytes,
            writable: false);
        try
        {
            ValidatePackageArchive(stream);
            stream.Position = 0;
            using var document = SpreadsheetDocument.Open(stream, false);
            Validate(document);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (OpenXmlPackageException exception)
        {
            throw new InvalidDataException(
                "The XLSX package contains an invalid relationship graph.",
                exception);
        }
        catch (UriFormatException exception)
        {
            throw new InvalidDataException(
                "The XLSX package contains an invalid relationship graph.",
                exception);
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException(
                "The XLSX package contains an invalid relationship graph.",
                exception);
        }
    }

    private static void ValidatePackageArchive(Stream stream)
    {
        using var archive = new ZipArchive(
            stream,
            ZipArchiveMode.Read,
            leaveOpen: true);
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith('/'))
            {
                continue;
            }

            if (!string.Equals(
                    entry.FullName,
                    "[Content_Types].xml",
                    StringComparison.OrdinalIgnoreCase))
            {
                ValidatePartUri(
                    new Uri(
                        "/" + entry.FullName,
                        UriKind.Relative));
            }

            if (entry.FullName.EndsWith(
                    ".rels",
                    StringComparison.OrdinalIgnoreCase))
            {
                ValidateRelationshipEntry(entry);
            }
        }
    }

    private static void ValidateRelationshipEntry(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        var document = XDocument.Load(
            stream,
            LoadOptions.None);
        var relationships = document.Root;
        if (relationships is null ||
            relationships.Name != XName.Get(
                "Relationships",
                RelationshipNamespace))
        {
            throw new InvalidDataException(
                "The XLSX package contains an invalid relationship graph.");
        }

        var relationshipIds = new HashSet<string>(StringComparer.Ordinal);
        var relationshipCount = 0;
        foreach (var relationship in relationships.Elements(
                     XName.Get(
                         "Relationship",
                         RelationshipNamespace)))
        {
            var relationshipId = (string?)relationship.Attribute("Id");
            var relationshipType = (string?)relationship.Attribute("Type");
            var target = (string?)relationship.Attribute("Target");
            if (relationshipId is null ||
                relationshipType is null ||
                target is null)
            {
                throw new InvalidDataException(
                    "The XLSX package contains an invalid relationship graph.");
            }

            RegisterRelationship(
                relationshipId,
                relationshipType,
                relationshipIds,
                ref relationshipCount);
            ValidateReferenceTarget(
                new Uri(
                    target,
                    UriKind.RelativeOrAbsolute));
        }
    }

    public static void Validate(OpenXmlPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        var packagePartsByUri = new Dictionary<string, object>(
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
                    pair.OpenXmlPart.RelationshipType,
                    relationshipIds,
                    ref relationshipCount);

                var part = pair.OpenXmlPart;
                if (RegisterPackagePart(
                        part.Uri,
                        part,
                        packagePartsByUri))
                {
                    pending.Push(part);
                }
            }

            foreach (var relationship in container.ExternalRelationships)
            {
                RegisterRelationship(
                    relationship.Id,
                    relationship.RelationshipType,
                    relationshipIds,
                    ref relationshipCount);
                ValidateReferenceTarget(relationship.Uri);
            }

            foreach (var relationship in container.HyperlinkRelationships)
            {
                RegisterRelationship(
                    relationship.Id,
                    relationship.RelationshipType,
                    relationshipIds,
                    ref relationshipCount);
                ValidateReferenceTarget(relationship.Uri);
            }

            foreach (var relationship in container.DataPartReferenceRelationships)
            {
                RegisterRelationship(
                    relationship.Id,
                    relationship.RelationshipType,
                    relationshipIds,
                    ref relationshipCount);
                RegisterPackagePart(
                    relationship.DataPart.Uri,
                    relationship.DataPart,
                    packagePartsByUri);
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

            var decoded = DecodeUriText(
                segment,
                "The XLSX package contains an invalid escaped part URI.");
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
        HashSet<string> existingIds)
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

    internal static void ValidateRelationshipType(string relationshipType)
    {
        if (string.IsNullOrWhiteSpace(relationshipType) ||
            relationshipType.Length > MaxRelationshipTypeCharacters ||
            ContainsControlCharacter(relationshipType))
        {
            throw new InvalidDataException(
                "The XLSX package contains an invalid relationship type URI.");
        }

        var decoded = DecodeUriText(
            relationshipType,
            "The XLSX package contains an invalid escaped relationship type URI.");
        if (ContainsControlCharacter(decoded) ||
            !Uri.TryCreate(
                decoded,
                UriKind.Absolute,
                out var relationshipTypeUri) ||
            string.IsNullOrWhiteSpace(relationshipTypeUri.Scheme))
        {
            throw new InvalidDataException(
                "The XLSX package contains an invalid relationship type URI.");
        }
    }

    internal static void ValidateExternalTarget(Uri targetUri) =>
        ValidateReferenceTarget(targetUri);

    internal static void ValidateReferenceTarget(Uri targetUri)
    {
        ArgumentNullException.ThrowIfNull(targetUri);
        var value = targetUri.OriginalString;
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > MaxReferenceTargetCharacters ||
            ContainsControlCharacter(value))
        {
            throw new InvalidDataException(
                "The XLSX package contains an invalid reference relationship target.");
        }

        var decoded = DecodeUriText(
            value,
            "The XLSX package contains an invalid escaped reference relationship target.");
        if (ContainsControlCharacter(decoded))
        {
            throw new InvalidDataException(
                "The XLSX package contains an invalid reference relationship target.");
        }
    }

    private static bool RegisterPackagePart(
        Uri partUri,
        object part,
        Dictionary<string, object> packagePartsByUri)
    {
        var validatedUri = ValidatePartUri(partUri);
        if (packagePartsByUri.TryGetValue(
                validatedUri,
                out var existingPart))
        {
            if (!ReferenceEquals(existingPart, part))
            {
                throw new InvalidDataException(
                    "The XLSX package contains multiple parts with the same package URI.");
            }
            return false;
        }

        packagePartsByUri.Add(validatedUri, part);
        if (packagePartsByUri.Count > MaxPartCount)
        {
            throw new InvalidDataException(
                "The XLSX package relationship graph exceeds the supported part-count limit.");
        }
        return true;
    }

    private static void RegisterRelationship(
        string relationshipId,
        string relationshipType,
        HashSet<string> relationshipIds,
        ref int relationshipCount)
    {
        relationshipCount++;
        if (relationshipCount > MaxRelationshipsPerContainer)
        {
            throw new InvalidDataException(
                "An XLSX relationship container exceeds the supported relationship-count limit.");
        }
        ValidateRelationshipId(relationshipId, relationshipIds);
        ValidateRelationshipType(relationshipType);
    }

    private static string DecodeUriText(
        string value,
        string errorMessage)
    {
        try
        {
            ValidatePercentEscapes(
                value,
                errorMessage);
            return Uri.UnescapeDataString(value);
        }
        catch (UriFormatException exception)
        {
            throw new InvalidDataException(
                errorMessage,
                exception);
        }
    }

    private static void ValidatePercentEscapes(
        string value,
        string errorMessage)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '%')
            {
                continue;
            }

            if (index + 2 >= value.Length ||
                !IsHexDigit(value[index + 1]) ||
                !IsHexDigit(value[index + 2]))
            {
                throw new InvalidDataException(errorMessage);
            }
        }
    }

    private static bool IsHexDigit(char character) =>
        character is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f';

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
