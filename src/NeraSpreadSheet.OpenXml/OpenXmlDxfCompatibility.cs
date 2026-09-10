using System.Globalization;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Validation;
using S = DocumentFormat.OpenXml.Spreadsheet;

namespace NeraSpreadSheet.OpenXml;

/// <summary>
/// Detects properties the existing dxf decoder cannot faithfully model. A detected
/// feature is validated before becoming opaque; malformed data never becomes an
/// empty patch through a catch-all InvalidDataException fallback.
/// </summary>
internal static class OpenXmlDxfCompatibility
{
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Mc = "http://schemas.openxmlformats.org/markup-compatibility/2006";

    internal static string? UnmodeledFeature(XElement dxf)
    {
        if (!dxf.HasElements) return "dxf/empty";
        if (OtherAttributes(dxf) || dxf.Elements().Any(child => child.Name.Namespace != Ns ||
            child.Name.LocalName is not ("font" or "numFmt" or "fill" or "alignment" or "border"))) return "dxf/extension-or-property";
        var border = dxf.Element(Ns + "border");
        if (border is not null)
        {
            foreach (var side in border.Elements())
            {
                if (side.Name == Ns + "vertical") return "dxf/border/vertical";
                if (side.Name == Ns + "horizontal") return "dxf/border/horizontal";
                if (side.Name.Namespace != Ns || side.Name.LocalName is not ("left" or "right" or "top" or "bottom" or "diagonal"))
                    return "dxf/border/side";
                if (side.Name.LocalName == "diagonal" && (side.HasElements || side.HasAttributes)) return "dxf/border/diagonal";
                if ((string?)side.Attribute("style") is not (null or "thin" or "medium" or "thick" or "dashed" or "dotted" or "double"))
                    return "dxf/border/line-style";
                if (OtherAttributes(side, "style") || side.Elements().Any(child => child.Name != Ns + "color")) return "dxf/border/property";
                if (side.Element(Ns + "color") is { } color && !ModeledColor(color)) return "dxf/border/color";
            }
            if (OtherAttributes(border)) return "dxf/border/flags";
        }
        var font = dxf.Element(Ns + "font");
        if (font is not null)
        {
            if (OtherAttributes(font) || font.Elements().Any(child => child.Name.Namespace != Ns ||
                child.Name.LocalName is not ("name" or "sz" or "b" or "i" or "u" or "color"))) return "dxf/font/property";
            if ((string?)font.Element(Ns + "u")?.Attribute("val") is not (null or "single" or "none")) return "dxf/font/underline";
            if (font.Element(Ns + "color") is { } color && !ModeledColor(color)) return "dxf/font/color";
        }
        var fill = dxf.Element(Ns + "fill");
        if (fill is not null)
        {
            var pattern = fill.Element(Ns + "patternFill");
            if (OtherAttributes(fill) || pattern is null || fill.Elements().Any(child => child.Name != Ns + "patternFill")) return "dxf/fill/property";
            if ((string?)pattern.Attribute("patternType") is not (null or "" or "none" or "solid")) return "dxf/fill/pattern";
            var color = pattern.Element(Ns + "fgColor") ?? pattern.Element(Ns + "bgColor");
            if ((string?)pattern.Attribute("patternType") != "none" && color is not null && !ModeledColor(color)) return "dxf/fill/color";
        }
        var alignment = dxf.Element(Ns + "alignment");
        if (alignment is not null)
        {
            if (OtherAttributes(alignment, "horizontal", "vertical", "wrapText", "textRotation") || alignment.HasElements) return "dxf/alignment/property";
            if ((string?)alignment.Attribute("horizontal") is not (null or "general" or "left" or "center" or "right") ||
                (string?)alignment.Attribute("vertical") is not (null or "top" or "center" or "bottom")) return "dxf/alignment/mode";
            if ((string?)alignment.Attribute("textRotation") == "255") return "dxf/alignment/stacked-text";
        }
        if (dxf.Elements().All(child => !child.HasElements && !child.HasAttributes)) return "dxf/empty-properties";
        return null;
    }

    internal static void ValidateOpaque(XElement source, bool differentialStyle, string? sheet = null)
    {
        var fragment = new XElement(source);
        foreach (var ancestor in source.AncestorsAndSelf())
            foreach (var attribute in ancestor.Attributes().Where(attribute => attribute.IsNamespaceDeclaration || attribute.Name.Namespace == Mc))
                if (fragment.Attribute(attribute.Name) is null) fragment.Add(new XAttribute(attribute));
        OpenXmlElement typed = differentialStyle
            ? new S.DifferentialFormat(fragment.ToString(SaveOptions.DisableFormatting))
            : new S.ConditionalFormatting(fragment.ToString(SaveOptions.DisableFormatting));
        var validator = new OpenXmlValidator(FileFormatVersions.Office2019) { MaxNumberOfErrors = 0 };
        var error = validator.Validate(typed).FirstOrDefault(item => item.ErrorType == ValidationErrorType.Schema);
        if (error is not null)
            throw OpenXmlImportDiagnostics.Invalid("Invalid formatting markup (" + error.Id + "). Compatibility cannot ignore this error.", differentialStyle ? "dxf" : "conditionalFormatting", sheet);
        foreach (var color in fragment.Descendants().Where(child => child.Name.Namespace == Ns && child.Name.LocalName is "color" or "fgColor" or "bgColor"))
            if (color.Attribute("theme") is { } theme && (!uint.TryParse(theme.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var index) || index > 11))
                throw OpenXmlImportDiagnostics.Invalid("Differential theme color index is outside the twelve-slot theme.", "dxf/color", sheet);
    }

    internal static bool UnmodeledRule(XElement container, XElement rule) =>
        OtherAttributes(container, "sqref") || OtherAttributes(rule, "type", "dxfId", "priority", "stopIfTrue", "operator") ||
        rule.Elements().Any(child => child.Name != Ns + "formula");

    private static bool ModeledColor(XElement color) =>
        (color.Attribute("rgb") is not null || color.Attribute("theme") is not null) && !OtherAttributes(color, "rgb", "theme", "tint");
    private static bool OtherAttributes(XElement element, params string[] allowed) => element.Attributes().Any(attribute =>
        !attribute.IsNamespaceDeclaration && (attribute.Name.Namespace != XNamespace.None || !allowed.Contains(attribute.Name.LocalName, StringComparer.Ordinal)));
}
