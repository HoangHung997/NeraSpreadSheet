using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.OpenXml;

internal static class OpenXmlTableStyleCodec
{
    private const int MaximumCustomStyles = 4096;
    private const int MaximumDifferentialStyles = 65536;
    private const long MaxXmlCharacters = 256L * 1024L * 1024L;
    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public static void Read(WorkbookPart workbookPart, Workbook workbook, bool preserveUnsupportedMarkup)
    {
        ArgumentNullException.ThrowIfNull(workbookPart);
        ArgumentNullException.ThrowIfNull(workbook);
        var part = workbookPart.WorkbookStylesPart;
        if (part is null)
        {
            return;
        }

        var root = Load(part).Root
            ?? throw new InvalidDataException("The XLSX style part is empty.");
        var differentialStyles = root
            .Elements(SpreadsheetNamespace + "dxfs")
            .SingleOrDefault()?
            .Elements(SpreadsheetNamespace + "dxf")
            .ToArray() ?? [];
        var containers = root
            .Elements(SpreadsheetNamespace + "tableStyles")
            .ToArray();
        if (containers.Length > 1)
        {
            throw new InvalidDataException(
                "The XLSX style table contains duplicate tableStyles collections.");
        }
        var styleElements = containers.SingleOrDefault()?
            .Elements(SpreadsheetNamespace + "tableStyle")
            .ToArray() ?? [];
        if (styleElements.Length > MaximumCustomStyles)
        {
            throw new InvalidDataException(
                $"The XLSX style table exceeds the custom Table style limit of {MaximumCustomStyles}.");
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var styleElement in styleElements)
        {
            if (!names.Add(RequiredAttribute(styleElement, "name")))
            {
                throw new InvalidDataException("Custom Table style names must be unique.");
            }
            if (!ReadBoolean(styleElement, "table", defaultValue: true))
            {
                continue;
            }
            try
            {
                var name = RequiredAttribute(styleElement, "name");
                var elements = styleElement
                    .Elements(SpreadsheetNamespace + "tableStyleElement")
                    .Select(element => ReadElement(element, differentialStyles))
                    .ToArray();
                workbook.TableStyles.AddOrReplaceCustom(new TableStyleDefinition(
                    $"custom:{name}",
                    name,
                    elements));
            }
            catch (UnsupportedTableStyleException exception)
            {
                if (!preserveUnsupportedMarkup)
                {
                    throw new InvalidDataException("Unsupported custom Table styles require package preservation.", exception);
                }
                // The package-preservation path retains producer-owned markup.
            }
        }
    }

    public static void Write(WorkbookPart workbookPart, Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbookPart);
        ArgumentNullException.ThrowIfNull(workbook);
        var part = workbookPart.WorkbookStylesPart
            ?? throw new InvalidDataException(
                "The generated XLSX package does not contain a style table.");
        var document = Load(part);
        var root = document.Root
            ?? throw new InvalidDataException("The XLSX style part is empty.");
        var customStyles = workbook.TableStyles.CustomStyles;
        var dxfs = root.Element(SpreadsheetNamespace + "dxfs");
        var differentialStyleCount = dxfs?
            .Elements(SpreadsheetNamespace + "dxf")
            .Count() ?? 0;
        var requiredCount = customStyles.Sum(static style =>
            style.Elements.Count(element =>
                element.Type != TableStyleElementType.FilterButton));
        if (differentialStyleCount + requiredCount > MaximumDifferentialStyles)
        {
            throw new InvalidOperationException(
                $"The workbook exceeds the differential-style limit of {MaximumDifferentialStyles}.");
        }
        if (requiredCount > 0 && dxfs is null)
        {
            dxfs = new XElement(
                SpreadsheetNamespace + "dxfs",
                new XAttribute("count", 0));
            InsertBefore(root, dxfs, "tableStyles", "colors", "extLst");
        }

        var tableStyles = new XElement(
            SpreadsheetNamespace + "tableStyles",
            new XAttribute("count", customStyles.Count),
            new XAttribute("defaultTableStyle", "TableStyleMedium2"),
            new XAttribute("defaultPivotStyle", "PivotStyleLight16"));
        foreach (var style in customStyles)
        {
            var tableStyle = new XElement(
                SpreadsheetNamespace + "tableStyle",
                new XAttribute("name", style.Name),
                new XAttribute("pivot", 0),
                new XAttribute("table", 1));
            foreach (var element in style.Elements)
            {
                if (element.Type == TableStyleElementType.FilterButton)
                {
                    continue;
                }
                var dxfId = differentialStyleCount++;
                dxfs!.Add(WriteDifferentialStyle(element.Format));
                var tableStyleElement = new XElement(
                    SpreadsheetNamespace + "tableStyleElement",
                    new XAttribute("type", ToOpenXmlType(element.Type)),
                    new XAttribute("dxfId", dxfId));
                if (IsStripe(element.Type))
                {
                    tableStyleElement.SetAttributeValue("size", element.StripeSize);
                }
                tableStyle.Add(tableStyleElement);
            }
            tableStyle.SetAttributeValue(
                "count",
                tableStyle.Elements(SpreadsheetNamespace + "tableStyleElement").Count());
            tableStyles.Add(tableStyle);
        }
        if (dxfs is not null)
        {
            dxfs.SetAttributeValue("count", differentialStyleCount);
        }
        root.Elements(SpreadsheetNamespace + "tableStyles").Remove();
        InsertBefore(root, tableStyles, "colors", "extLst");
        Save(part, document);
    }

    private static TableStyleElement ReadElement(
        XElement element,
        XElement[] differentialStyles)
    {
        var type = FromOpenXmlType(RequiredAttribute(element, "type"));
        var dxfId = ReadInt(element, "dxfId", -1);
        if (dxfId < 0 || dxfId >= differentialStyles.Length)
        {
            throw new InvalidDataException(
                "A custom Table style references an unavailable differential style.");
        }
        var stripeSize = IsStripe(type)
            ? ReadInt(element, "size", 1)
            : 1;
        return new TableStyleElement(
            type,
            ReadDifferentialStyle(differentialStyles[dxfId]),
            stripeSize);
    }

    private static TableStyleFormat ReadDifferentialStyle(XElement dxf)
    {
        if (dxf.Elements().Any(child =>
                child.Name.Namespace != SpreadsheetNamespace ||
                child.Name.LocalName is not ("font" or "fill" or "border" or "alignment")))
        {
            throw new UnsupportedTableStyleException();
        }
        var font = GetSingleChild(dxf, "font");
        var fill = GetSingleChild(dxf, "fill");
        var border = GetSingleChild(dxf, "border");
        var alignment = GetSingleChild(dxf, "alignment");
        var fillData = fill is null ? default : ReadFill(fill);
        return new TableStyleFormat
        {
            FontFamily = ReadOptionalValue(font, "name"),
            FontSize = ReadOptionalDoubleValue(font, "sz"),
            FontWeight = ReadOptionalBooleanValue(font, "b") is { } bold
                ? bold ? 700 : 400
                : null,
            FontItalic = ReadOptionalBooleanValue(font, "i"),
            FontUnderline = ReadOptionalBooleanValue(font, "u"),
            FontStrikeThrough = ReadOptionalBooleanValue(font, "strike"),
            FontColor = ReadOptionalColor(font?.Element(SpreadsheetNamespace + "color")),
            FillColor = fillData.Foreground,
            FillBackgroundColor = fillData.Background,
            FillPattern = fillData.Pattern,
            Border = border is null ? null : ReadBorder(border),
            HorizontalAlignment = ReadHorizontalAlignment(alignment),
            VerticalAlignment = ReadVerticalAlignment(alignment),
        };
    }

    private static XElement WriteDifferentialStyle(TableStyleFormat format)
    {
        var dxf = new XElement(SpreadsheetNamespace + "dxf");
        var font = WriteFont(format);
        if (font is not null)
        {
            dxf.Add(font);
        }
        if (format.FillColor is not null)
        {
            dxf.Add(new XElement(
                SpreadsheetNamespace + "fill",
                new XElement(
                    SpreadsheetNamespace + "patternFill",
                    new XAttribute("patternType", ToPattern(format.FillPattern)),
                    WriteColor("fgColor", format.FillColor.Value),
                    WriteColor(
                        "bgColor",
                        format.FillBackgroundColor ??
                        TableStyleColor.FromRgb(ColorRgba.Transparent)))));
        }
        if (format.Border is not null)
        {
            dxf.Add(WriteBorder(format.Border));
        }
        if (format.HorizontalAlignment is not null ||
            format.VerticalAlignment is not null)
        {
            var alignment = new XElement(SpreadsheetNamespace + "alignment");
            if (format.HorizontalAlignment is { } horizontal)
            {
                alignment.SetAttributeValue("horizontal", ToHorizontal(horizontal));
            }
            if (format.VerticalAlignment is { } vertical)
            {
                alignment.SetAttributeValue("vertical", ToVertical(vertical));
            }
            dxf.Add(alignment);
        }
        return dxf;
    }

    private static XElement? WriteFont(TableStyleFormat format)
    {
        if (format.FontFamily is null && format.FontSize is null &&
            format.FontWeight is null && format.FontItalic is null &&
            format.FontUnderline is null && format.FontStrikeThrough is null &&
            format.FontColor is null)
        {
            return null;
        }
        var font = new XElement(SpreadsheetNamespace + "font");
        AddBoolean(font, "b", format.FontWeight is { } weight ? weight >= 600 : null);
        AddBoolean(font, "i", format.FontItalic);
        AddBoolean(font, "u", format.FontUnderline);
        AddBoolean(font, "strike", format.FontStrikeThrough);
        if (format.FontSize is { } size)
        {
            font.Add(ValueElement("sz", size.ToString("R", CultureInfo.InvariantCulture)));
        }
        if (format.FontColor is { } color)
        {
            font.Add(WriteColor("color", color));
        }
        if (format.FontFamily is { } family)
        {
            font.Add(ValueElement("name", family));
        }
        return font;
    }

    private static XElement WriteBorder(TableStyleBorder border) =>
        new(
            SpreadsheetNamespace + "border",
            WriteBorderSide("left", border.Left),
            WriteBorderSide("right", border.Right),
            WriteBorderSide("top", border.Top),
            WriteBorderSide("bottom", border.Bottom),
            new XElement(SpreadsheetNamespace + "diagonal"));

    private static XElement WriteBorderSide(
        string name,
        TableStyleBorderSide? side)
    {
        var element = new XElement(SpreadsheetNamespace + name);
        if (side is not null && side.Style != CellBorderLineStyle.None)
        {
            element.SetAttributeValue("style", ToBorderStyle(side.Style));
            element.Add(WriteColor("color", side.Color));
        }
        return element;
    }

    private static TableStyleBorder ReadBorder(XElement element) =>
        new()
        {
            Left = ReadBorderSide(element.Element(SpreadsheetNamespace + "left")),
            Right = ReadBorderSide(element.Element(SpreadsheetNamespace + "right")),
            Top = ReadBorderSide(element.Element(SpreadsheetNamespace + "top")),
            Bottom = ReadBorderSide(element.Element(SpreadsheetNamespace + "bottom")),
        };

    private static TableStyleBorderSide? ReadBorderSide(XElement? element)
    {
        var style = (string?)element?.Attribute("style");
        if (style is null)
        {
            return null;
        }
        var color = ReadOptionalColor(element!.Element(SpreadsheetNamespace + "color"))
            ?? TableStyleColor.FromRgb(ColorRgba.Black);
        return new TableStyleBorderSide
        {
            Style = FromBorderStyle(style),
            Color = color,
            Width = style.StartsWith("medium", StringComparison.Ordinal) || style == "double"
                ? 2d
                : style == "thick" ? 3d : 1d,
        };
    }

    private static (TableStyleColor? Foreground, TableStyleColor? Background, CellFillPattern Pattern)
        ReadFill(XElement fill)
    {
        var pattern = GetSingleChild(fill, "patternFill")
            ?? throw new UnsupportedTableStyleException();
        return (
            ReadOptionalColor(pattern.Element(SpreadsheetNamespace + "fgColor")),
            ReadOptionalColor(pattern.Element(SpreadsheetNamespace + "bgColor")),
            FromPattern((string?)pattern.Attribute("patternType")));
    }

    private static XElement WriteColor(string name, TableStyleColor color)
    {
        var element = new XElement(SpreadsheetNamespace + name);
        if (color.ThemeColor is { } themeColor)
        {
            element.SetAttributeValue("theme", ToThemeIndex(themeColor));
            if (color.Tint != 0d)
            {
                element.SetAttributeValue(
                    "tint",
                    color.Tint.ToString("R", CultureInfo.InvariantCulture));
            }
        }
        else
        {
            var rgb = color.Rgb!.Value;
            element.SetAttributeValue(
                "rgb",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{rgb.Alpha:X2}{rgb.Red:X2}{rgb.Green:X2}{rgb.Blue:X2}"));
        }
        return element;
    }

    private static TableStyleColor? ReadOptionalColor(XElement? element)
    {
        if (element is null)
        {
            return null;
        }
        if (uint.TryParse(
                (string?)element.Attribute("theme"),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var themeIndex) && TryFromThemeIndex(themeIndex, out var themeColor))
        {
            return TableStyleColor.FromTheme(
                themeColor,
                ReadDouble(element, "tint", 0d));
        }
        var rgbText = (string?)element.Attribute("rgb");
        if (rgbText is { Length: 6 or 8 } && uint.TryParse(
                rgbText,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out var argb))
        {
            if (rgbText.Length == 6)
            {
                argb |= 0xFF000000U;
            }
            return TableStyleColor.FromRgb(new ColorRgba(
                (byte)(argb >> 16),
                (byte)(argb >> 8),
                (byte)argb,
                (byte)(argb >> 24)));
        }
        throw new UnsupportedTableStyleException();
    }

    private static XElement? GetSingleChild(XElement parent, string name)
    {
        var children = parent.Elements(SpreadsheetNamespace + name).ToArray();
        if (children.Length > 1)
        {
            throw new InvalidDataException(
                $"A differential style contains duplicate {name} elements.");
        }
        return children.SingleOrDefault();
    }

    private static string? ReadOptionalValue(XElement? parent, string name) =>
        parent?.Element(SpreadsheetNamespace + name)?.Attribute("val")?.Value;

    private static double? ReadOptionalDoubleValue(XElement? parent, string name)
    {
        var value = ReadOptionalValue(parent, name);
        if (value is null)
        {
            return null;
        }
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ||
            !double.IsFinite(parsed))
        {
            throw new InvalidDataException($"A differential style {name} value is invalid.");
        }
        return parsed;
    }

    private static bool? ReadOptionalBooleanValue(XElement? parent, string name)
    {
        var element = parent?.Element(SpreadsheetNamespace + name);
        if (element is null)
        {
            return null;
        }
        return ReadBoolean(element, "val", true);
    }

    private static CellHorizontalAlignment? ReadHorizontalAlignment(XElement? alignment) =>
        (string?)alignment?.Attribute("horizontal") switch
        {
            null => null,
            "left" => CellHorizontalAlignment.Left,
            "center" => CellHorizontalAlignment.Center,
            "right" => CellHorizontalAlignment.Right,
            "fill" => CellHorizontalAlignment.Fill,
            "justify" => CellHorizontalAlignment.Justify,
            "centerContinuous" => CellHorizontalAlignment.CenterContinuous,
            "distributed" => CellHorizontalAlignment.Distributed,
            _ => throw new UnsupportedTableStyleException(),
        };

    private static CellVerticalAlignment? ReadVerticalAlignment(XElement? alignment) =>
        (string?)alignment?.Attribute("vertical") switch
        {
            null => null,
            "top" => CellVerticalAlignment.Top,
            "center" => CellVerticalAlignment.Center,
            "bottom" => CellVerticalAlignment.Bottom,
            "justify" => CellVerticalAlignment.Justify,
            "distributed" => CellVerticalAlignment.Distributed,
            _ => throw new UnsupportedTableStyleException(),
        };

    private static string ToHorizontal(CellHorizontalAlignment value) => value switch
    {
        CellHorizontalAlignment.Left => "left",
        CellHorizontalAlignment.Center => "center",
        CellHorizontalAlignment.Right => "right",
        CellHorizontalAlignment.Fill => "fill",
        CellHorizontalAlignment.Justify => "justify",
        CellHorizontalAlignment.CenterContinuous => "centerContinuous",
        CellHorizontalAlignment.Distributed => "distributed",
        _ => "general",
    };

    private static string ToVertical(CellVerticalAlignment value) => value switch
    {
        CellVerticalAlignment.Top => "top",
        CellVerticalAlignment.Center => "center",
        CellVerticalAlignment.Justify => "justify",
        CellVerticalAlignment.Distributed => "distributed",
        _ => "bottom",
    };

    private static void AddBoolean(XElement parent, string name, bool? value)
    {
        if (value is { } present)
        {
            parent.Add(ValueElement(name, present ? "1" : "0"));
        }
    }

    private static XElement ValueElement(string name, string value) =>
        new(
            SpreadsheetNamespace + name,
            new XAttribute("val", value));

    private static bool ReadBoolean(
        XElement element,
        string name,
        bool defaultValue) =>
        (string?)element.Attribute(name) switch
        {
            null => defaultValue,
            "1" or "true" => true,
            "0" or "false" => false,
            _ => throw new InvalidDataException($"The {name} boolean value is invalid."),
        };

    private static int ReadInt(XElement element, string name, int defaultValue)
    {
        var value = (string?)element.Attribute(name);
        if (value is null)
        {
            return defaultValue;
        }
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new InvalidDataException($"The {name} integer value is invalid.");
        }
        return parsed;
    }

    private static double ReadDouble(XElement element, string name, double defaultValue)
    {
        var value = (string?)element.Attribute(name);
        if (value is null)
        {
            return defaultValue;
        }
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ||
            !double.IsFinite(parsed))
        {
            throw new InvalidDataException($"The {name} numeric value is invalid.");
        }
        return parsed;
    }

    private static string RequiredAttribute(XElement element, string name)
    {
        var value = (string?)element.Attribute(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"Required Table style attribute '{name}' is missing.");
        }
        return value;
    }

    private static bool IsStripe(TableStyleElementType type) => type is
        TableStyleElementType.FirstRowStripe or
        TableStyleElementType.SecondRowStripe or
        TableStyleElementType.FirstColumnStripe or
        TableStyleElementType.SecondColumnStripe;

    private static string ToOpenXmlType(TableStyleElementType type) => type switch
    {
        TableStyleElementType.WholeTable => "wholeTable",
        TableStyleElementType.HeaderRow => "headerRow",
        TableStyleElementType.TotalsRow => "totalRow",
        TableStyleElementType.FirstColumn => "firstColumn",
        TableStyleElementType.LastColumn => "lastColumn",
        TableStyleElementType.FirstRowStripe => "firstRowStripe",
        TableStyleElementType.SecondRowStripe => "secondRowStripe",
        TableStyleElementType.FirstColumnStripe => "firstColumnStripe",
        TableStyleElementType.SecondColumnStripe => "secondColumnStripe",
        _ => throw new UnsupportedTableStyleException(),
    };

    private static TableStyleElementType FromOpenXmlType(string type) => type switch
    {
        "wholeTable" => TableStyleElementType.WholeTable,
        "headerRow" => TableStyleElementType.HeaderRow,
        "totalRow" => TableStyleElementType.TotalsRow,
        "firstColumn" => TableStyleElementType.FirstColumn,
        "lastColumn" => TableStyleElementType.LastColumn,
        "firstRowStripe" => TableStyleElementType.FirstRowStripe,
        "secondRowStripe" => TableStyleElementType.SecondRowStripe,
        "firstColumnStripe" => TableStyleElementType.FirstColumnStripe,
        "secondColumnStripe" => TableStyleElementType.SecondColumnStripe,
        _ => throw new UnsupportedTableStyleException(),
    };

    private static uint ToThemeIndex(WorkbookThemeColor color) => color switch
    {
        WorkbookThemeColor.Light1 => 0U,
        WorkbookThemeColor.Dark1 => 1U,
        WorkbookThemeColor.Light2 => 2U,
        WorkbookThemeColor.Dark2 => 3U,
        WorkbookThemeColor.Accent1 => 4U,
        WorkbookThemeColor.Accent2 => 5U,
        WorkbookThemeColor.Accent3 => 6U,
        WorkbookThemeColor.Accent4 => 7U,
        WorkbookThemeColor.Accent5 => 8U,
        WorkbookThemeColor.Accent6 => 9U,
        WorkbookThemeColor.Hyperlink => 10U,
        WorkbookThemeColor.FollowedHyperlink => 11U,
        _ => throw new ArgumentOutOfRangeException(nameof(color)),
    };

    private static bool TryFromThemeIndex(uint index, out WorkbookThemeColor color)
    {
        color = index switch
        {
            0U => WorkbookThemeColor.Light1,
            1U => WorkbookThemeColor.Dark1,
            2U => WorkbookThemeColor.Light2,
            3U => WorkbookThemeColor.Dark2,
            4U => WorkbookThemeColor.Accent1,
            5U => WorkbookThemeColor.Accent2,
            6U => WorkbookThemeColor.Accent3,
            7U => WorkbookThemeColor.Accent4,
            8U => WorkbookThemeColor.Accent5,
            9U => WorkbookThemeColor.Accent6,
            10U => WorkbookThemeColor.Hyperlink,
            11U => WorkbookThemeColor.FollowedHyperlink,
            _ => default,
        };
        return index <= 11U;
    }

    private static CellBorderLineStyle FromBorderStyle(string style) => style switch
    {
        "thin" => CellBorderLineStyle.Thin,
        "medium" => CellBorderLineStyle.Medium,
        "thick" => CellBorderLineStyle.Thick,
        "dashed" => CellBorderLineStyle.Dashed,
        "dotted" => CellBorderLineStyle.Dotted,
        "double" => CellBorderLineStyle.DoubleLine,
        "hair" => CellBorderLineStyle.Hair,
        "mediumDashed" => CellBorderLineStyle.MediumDashed,
        "dashDot" => CellBorderLineStyle.DashDot,
        "mediumDashDot" => CellBorderLineStyle.MediumDashDot,
        "dashDotDot" => CellBorderLineStyle.DashDotDot,
        "mediumDashDotDot" => CellBorderLineStyle.MediumDashDotDot,
        "slantDashDot" => CellBorderLineStyle.SlantDashDot,
        _ => throw new UnsupportedTableStyleException(),
    };

    private static string ToBorderStyle(CellBorderLineStyle style) => style switch
    {
        CellBorderLineStyle.Medium => "medium",
        CellBorderLineStyle.Thick => "thick",
        CellBorderLineStyle.Dashed => "dashed",
        CellBorderLineStyle.Dotted => "dotted",
        CellBorderLineStyle.DoubleLine => "double",
        CellBorderLineStyle.Hair => "hair",
        CellBorderLineStyle.MediumDashed => "mediumDashed",
        CellBorderLineStyle.DashDot => "dashDot",
        CellBorderLineStyle.MediumDashDot => "mediumDashDot",
        CellBorderLineStyle.DashDotDot => "dashDotDot",
        CellBorderLineStyle.MediumDashDotDot => "mediumDashDotDot",
        CellBorderLineStyle.SlantDashDot => "slantDashDot",
        _ => "thin",
    };

    private static CellFillPattern FromPattern(string? pattern) => pattern switch
    {
        null or "none" => CellFillPattern.None,
        "solid" => CellFillPattern.Solid,
        "gray125" => CellFillPattern.Gray125,
        "darkGray" => CellFillPattern.DarkGray,
        "mediumGray" => CellFillPattern.MediumGray,
        "lightGray" => CellFillPattern.LightGray,
        "darkHorizontal" => CellFillPattern.DarkHorizontal,
        "darkVertical" => CellFillPattern.DarkVertical,
        "darkDown" => CellFillPattern.DarkDown,
        "darkUp" => CellFillPattern.DarkUp,
        "darkGrid" => CellFillPattern.DarkGrid,
        "darkTrellis" => CellFillPattern.DarkTrellis,
        "lightHorizontal" => CellFillPattern.LightHorizontal,
        "lightVertical" => CellFillPattern.LightVertical,
        "lightDown" => CellFillPattern.LightDown,
        "lightUp" => CellFillPattern.LightUp,
        "lightGrid" => CellFillPattern.LightGrid,
        "lightTrellis" => CellFillPattern.LightTrellis,
        _ => throw new UnsupportedTableStyleException(),
    };

    private static string ToPattern(CellFillPattern pattern) => pattern switch
    {
        CellFillPattern.Solid or CellFillPattern.None => "solid",
        CellFillPattern.Gray125 => "gray125",
        CellFillPattern.DarkGray => "darkGray",
        CellFillPattern.MediumGray => "mediumGray",
        CellFillPattern.LightGray => "lightGray",
        CellFillPattern.DarkHorizontal => "darkHorizontal",
        CellFillPattern.DarkVertical => "darkVertical",
        CellFillPattern.DarkDown => "darkDown",
        CellFillPattern.DarkUp => "darkUp",
        CellFillPattern.DarkGrid => "darkGrid",
        CellFillPattern.DarkTrellis => "darkTrellis",
        CellFillPattern.LightHorizontal => "lightHorizontal",
        CellFillPattern.LightVertical => "lightVertical",
        CellFillPattern.LightDown => "lightDown",
        CellFillPattern.LightUp => "lightUp",
        CellFillPattern.LightGrid => "lightGrid",
        CellFillPattern.LightTrellis => "lightTrellis",
        _ => "solid",
    };

    private static void InsertBefore(
        XElement root,
        XElement element,
        params string[] followingNames)
    {
        var next = root.Elements().FirstOrDefault(candidate =>
            candidate.Name.Namespace == SpreadsheetNamespace &&
            followingNames.Contains(candidate.Name.LocalName, StringComparer.Ordinal));
        if (next is null)
        {
            root.Add(element);
        }
        else
        {
            next.AddBeforeSelf(element);
        }
    }

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

    private sealed class UnsupportedTableStyleException : Exception;
}
