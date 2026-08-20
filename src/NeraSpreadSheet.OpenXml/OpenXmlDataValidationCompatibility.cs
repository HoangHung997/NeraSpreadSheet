using System.Diagnostics.CodeAnalysis;
using System.Xml;
using System.Xml.Linq;

[assembly: SuppressMessage(
    "Performance",
    "CA1859:Use concrete types when possible for improved performance",
    Justification = "The schema-order helper intentionally exposes a read-only contract used by insertion helpers.",
    Scope = "type",
    Target = "~T:NeraSpreadSheet.OpenXml.OpenXmlDataValidationCodec")]
[assembly: SuppressMessage(
    "Performance",
    "CA1859:Use concrete types when possible for improved performance",
    Justification = "The schema-order helper intentionally exposes a read-only contract used by insertion helpers.",
    Scope = "type",
    Target = "~T:NeraSpreadSheet.OpenXml.OpenXmlDataValidationPackagePatcher")]

namespace NeraSpreadSheet.OpenXml;

internal static class OpenXmlDataValidationXDocumentExtensions
{
    public static void Save(
        this XDocument document,
        XmlWriter writer,
        SaveOptions options)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(writer);
        _ = options;
        document.Save(writer);
    }
}
