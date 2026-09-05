using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.OpenXml;

internal static class OpenXmlWorkbookThemeCodec
{
    private const long MaxXmlCharacters = 16L * 1024L * 1024L;
    private static readonly XNamespace DrawingNamespace =
        "http://schemas.openxmlformats.org/drawingml/2006/main";

    public static void Read(WorkbookPart workbookPart, Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbookPart);
        ArgumentNullException.ThrowIfNull(workbook);
        var part = workbookPart.ThemePart;
        if (part is null)
        {
            return;
        }

        var root = Load(part).Root;
        var scheme = root?
            .Element(DrawingNamespace + "themeElements")?
            .Element(DrawingNamespace + "clrScheme");
        if (scheme is null)
        {
            return;
        }

        var fallback = WorkbookTheme.Office;
        workbook.Theme = new WorkbookTheme
        {
            Light1 = ReadColor(scheme, "lt1", fallback.Light1),
            Dark1 = ReadColor(scheme, "dk1", fallback.Dark1),
            Light2 = ReadColor(scheme, "lt2", fallback.Light2),
            Dark2 = ReadColor(scheme, "dk2", fallback.Dark2),
            Accent1 = ReadColor(scheme, "accent1", fallback.Accent1),
            Accent2 = ReadColor(scheme, "accent2", fallback.Accent2),
            Accent3 = ReadColor(scheme, "accent3", fallback.Accent3),
            Accent4 = ReadColor(scheme, "accent4", fallback.Accent4),
            Accent5 = ReadColor(scheme, "accent5", fallback.Accent5),
            Accent6 = ReadColor(scheme, "accent6", fallback.Accent6),
            Hyperlink = ReadColor(scheme, "hlink", fallback.Hyperlink),
            FollowedHyperlink = ReadColor(
                scheme,
                "folHlink",
                fallback.FollowedHyperlink),
        };
    }

    public static void Write(WorkbookPart workbookPart, WorkbookTheme theme)
    {
        ArgumentNullException.ThrowIfNull(workbookPart);
        ArgumentNullException.ThrowIfNull(theme);
        var part = workbookPart.ThemePart ?? workbookPart.AddNewPart<ThemePart>();
        var root = new XElement(
            DrawingNamespace + "theme",
            new XAttribute("name", "Nera Spreadsheet Theme"),
            new XElement(
                DrawingNamespace + "themeElements",
                BuildColorScheme(theme),
                BuildFontScheme(),
                BuildFormatScheme()));
        Save(part, new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            root));
    }

    private static XElement BuildColorScheme(WorkbookTheme theme) =>
        new(
            DrawingNamespace + "clrScheme",
            new XAttribute("name", "Nera"),
            BuildColor("dk1", theme.Dark1),
            BuildColor("lt1", theme.Light1),
            BuildColor("dk2", theme.Dark2),
            BuildColor("lt2", theme.Light2),
            BuildColor("accent1", theme.Accent1),
            BuildColor("accent2", theme.Accent2),
            BuildColor("accent3", theme.Accent3),
            BuildColor("accent4", theme.Accent4),
            BuildColor("accent5", theme.Accent5),
            BuildColor("accent6", theme.Accent6),
            BuildColor("hlink", theme.Hyperlink),
            BuildColor("folHlink", theme.FollowedHyperlink));

    private static XElement BuildColor(string name, ColorRgba color) =>
        new(
            DrawingNamespace + name,
            new XElement(
                DrawingNamespace + "srgbClr",
                new XAttribute("val", ToRgb(color))));

    private static XElement BuildFontScheme() =>
        new(
            DrawingNamespace + "fontScheme",
            new XAttribute("name", "Nera"),
            BuildFontCollection("majorFont", "Cambria"),
            BuildFontCollection("minorFont", "Segoe UI"));

    private static XElement BuildFontCollection(
        string name,
        string latinTypeface) =>
        new(
            DrawingNamespace + name,
            new XElement(
                DrawingNamespace + "latin",
                new XAttribute("typeface", latinTypeface)),
            new XElement(
                DrawingNamespace + "ea",
                new XAttribute("typeface", string.Empty)),
            new XElement(
                DrawingNamespace + "cs",
                new XAttribute("typeface", string.Empty)));

    private static XElement BuildFormatScheme()
    {
        var fillStyles = new XElement(DrawingNamespace + "fillStyleLst");
        var lineStyles = new XElement(DrawingNamespace + "lnStyleLst");
        var effectStyles = new XElement(DrawingNamespace + "effectStyleLst");
        var backgroundStyles = new XElement(DrawingNamespace + "bgFillStyleLst");
        for (var index = 0; index < 3; index++)
        {
            fillStyles.Add(BuildSolidSchemeFill());
            backgroundStyles.Add(BuildSolidSchemeFill());
            lineStyles.Add(new XElement(
                DrawingNamespace + "ln",
                new XAttribute("w", 6350 + (index * 6350)),
                new XAttribute("cap", "flat"),
                new XAttribute("cmpd", "sng"),
                new XAttribute("algn", "ctr"),
                BuildSolidSchemeFill(),
                new XElement(
                    DrawingNamespace + "prstDash",
                    new XAttribute("val", "solid")),
                new XElement(
                    DrawingNamespace + "miter",
                    new XAttribute("lim", 800000))));
            effectStyles.Add(new XElement(
                DrawingNamespace + "effectStyle",
                new XElement(DrawingNamespace + "effectLst")));
        }
        return new XElement(
            DrawingNamespace + "fmtScheme",
            new XAttribute("name", "Nera"),
            fillStyles,
            lineStyles,
            effectStyles,
            backgroundStyles);
    }

    private static XElement BuildSolidSchemeFill() =>
        new(
            DrawingNamespace + "solidFill",
            new XElement(
                DrawingNamespace + "schemeClr",
                new XAttribute("val", "phClr")));

    private static ColorRgba ReadColor(
        XElement scheme,
        string name,
        ColorRgba fallback)
    {
        var source = scheme.Element(DrawingNamespace + name)?.Elements().SingleOrDefault();
        var value = source?.Name.LocalName switch
        {
            "srgbClr" => (string?)source.Attribute("val"),
            "sysClr" => (string?)source.Attribute("lastClr"),
            _ => null,
        };
        return TryParseRgb(value, out var color) ? color : fallback;
    }

    private static bool TryParseRgb(string? value, out ColorRgba color)
    {
        if (value?.Length == 6 && uint.TryParse(
                value,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out var rgb))
        {
            color = new ColorRgba(
                (byte)(rgb >> 16),
                (byte)(rgb >> 8),
                (byte)rgb);
            return true;
        }
        color = default;
        return false;
    }

    private static string ToRgb(ColorRgba color) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{color.Red:X2}{color.Green:X2}{color.Blue:X2}");

    private static XDocument Load(OpenXmlPart part)
    {
        using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
        using var reader = XmlReader.Create(
            stream,
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                MaxCharactersInDocument = MaxXmlCharacters,
                XmlResolver = null,
            });
        return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
    }

    private static void Save(OpenXmlPart part, XDocument document)
    {
        using var stream = part.GetStream(FileMode.Create, FileAccess.Write);
        using var writer = XmlWriter.Create(
            stream,
            new XmlWriterSettings
            {
                Encoding = new System.Text.UTF8Encoding(false),
                Indent = false,
                OmitXmlDeclaration = false,
            });
        document.Save(writer, SaveOptions.DisableFormatting);
    }
}
