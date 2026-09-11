using System.Globalization;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using NeraSpreadSheet.Core;
using NeraWorkbook = NeraSpreadSheet.Core.Workbook;

namespace NeraSpreadSheet.OpenXml;

/// <summary>
/// Adds the Page Setup fields intentionally introduced by PAGE-SETUP-008 while the
/// established print-settings codec continues to own print area/titles/base settings.
/// The patch is deliberately narrow so unrelated worksheet markup remains opaque.
/// </summary>
internal static class OpenXmlPageSetupFidelity008Codec
{
    private static readonly XNamespace Ns =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public static void Read(
        SpreadsheetDocument document,
        NeraWorkbook workbook,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(workbook);
        var workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException("The XLSX package does not contain a workbook part.");
        var sheets = workbookPart.Workbook?.GetFirstChild<Sheets>()?.Elements<Sheet>().ToArray() ?? [];
        var count = Math.Min(sheets.Length, workbook.Worksheets.Count);
        for (var index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relationshipId = sheets[index].Id?.Value;
            if (string.IsNullOrWhiteSpace(relationshipId) ||
                workbookPart.GetPartById(relationshipId) is not WorksheetPart worksheetPart)
                continue;

            var xml = Load(worksheetPart);
            var root = xml.Root ?? throw new InvalidDataException("The XLSX worksheet XML has no root element.");
            var page = root.Element(Ns + "pageSetup");
            var hf = root.Element(Ns + "headerFooter");
            var current = workbook.Worksheets[index].GetPrintSettings();
            var setup = current.PageSetup with
            {
                PageOrder = string.Equals((string?)page?.Attribute("pageOrder"), "overThenDown", StringComparison.OrdinalIgnoreCase)
                    ? SpreadsheetPageOrder.OverThenDown
                    : SpreadsheetPageOrder.DownThenOver,
                PrintQualityDpi = ReadPositiveInt(page?.Attribute("horizontalDpi")) ?? ReadPositiveInt(page?.Attribute("verticalDpi")),
                FirstPageNumber = ReadBoolean(page?.Attribute("useFirstPageNumber"), false)
                    ? ReadPositiveInt(page?.Attribute("firstPageNumber"))
                    : null,
                BlackAndWhite = ReadBoolean(page?.Attribute("blackAndWhite"), false),
                DraftQuality = ReadBoolean(page?.Attribute("draft"), false),
                PrintComments = ReadComments((string?)page?.Attribute("cellComments")),
                PrintErrors = ReadErrors((string?)page?.Attribute("errors")),
                EvenHeader = hf?.Element(Ns + "evenHeader")?.Value,
                EvenFooter = hf?.Element(Ns + "evenFooter")?.Value,
                FirstHeader = hf?.Element(Ns + "firstHeader")?.Value,
                FirstFooter = hf?.Element(Ns + "firstFooter")?.Value,
                DifferentOddEvenPages = ReadBoolean(hf?.Attribute("differentOddEven"), false),
                DifferentFirstPage = ReadBoolean(hf?.Attribute("differentFirst"), false),
                ScaleHeaderFooterWithDocument = ReadBoolean(hf?.Attribute("scaleWithDoc"), true),
                AlignHeaderFooterWithMargins = ReadBoolean(hf?.Attribute("alignWithMargins"), true),
            };
            workbook.Worksheets[index].SetPrintSettings(current with { PageSetup = setup });
        }
    }

    public static byte[] Patch(
        byte[] packageBytes,
        NeraWorkbook workbook,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packageBytes);
        ArgumentNullException.ThrowIfNull(workbook);
        using var buffer = new MemoryStream();
        buffer.Write(packageBytes);
        buffer.Position = 0;
        using (var document = SpreadsheetDocument.Open(buffer, true))
        {
            var workbookPart = document.WorkbookPart
                ?? throw new InvalidDataException("The XLSX package does not contain a workbook part.");
            var sheets = workbookPart.Workbook?.GetFirstChild<Sheets>()?.Elements<Sheet>().ToArray() ?? [];
            if (sheets.Length != workbook.Worksheets.Count)
                throw new InvalidOperationException("Worksheet topology differs while writing extended Page Setup settings.");

            for (var index = 0; index < sheets.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relationshipId = sheets[index].Id?.Value;
                if (string.IsNullOrWhiteSpace(relationshipId) ||
                    workbookPart.GetPartById(relationshipId) is not WorksheetPart worksheetPart)
                    throw new InvalidDataException("The XLSX worksheet relationship is invalid.");
                PatchWorksheet(worksheetPart, workbook.Worksheets[index].GetPrintSettings().PageSetup);
            }
        }
        return buffer.ToArray();
    }

    private static void PatchWorksheet(WorksheetPart part, SpreadsheetPageSetup setup)
    {
        var xml = Load(part);
        var root = xml.Root ?? throw new InvalidDataException("The XLSX worksheet XML has no root element.");
        var page = GetOrCreate(root, "pageSetup");
        page.SetAttributeValue("pageOrder", setup.PageOrder == SpreadsheetPageOrder.OverThenDown ? "overThenDown" : "downThenOver");
        page.SetAttributeValue("horizontalDpi", setup.PrintQualityDpi);
        page.SetAttributeValue("verticalDpi", setup.PrintQualityDpi);
        page.SetAttributeValue("useFirstPageNumber", setup.FirstPageNumber.HasValue ? "1" : null);
        page.SetAttributeValue("firstPageNumber", setup.FirstPageNumber);
        SetBool(page, "blackAndWhite", setup.BlackAndWhite, defaultValue: false);
        SetBool(page, "draft", setup.DraftQuality, defaultValue: false);
        page.SetAttributeValue("cellComments", setup.PrintComments switch
        {
            SpreadsheetPrintComments.AsDisplayed => "asDisplayed",
            SpreadsheetPrintComments.AtEnd => "atEnd",
            _ => null,
        });
        page.SetAttributeValue("errors", setup.PrintErrors switch
        {
            SpreadsheetPrintErrors.Blank => "blank",
            SpreadsheetPrintErrors.Dash => "dash",
            SpreadsheetPrintErrors.NotAvailable => "NA",
            _ => "displayed",
        });

        var hasHeaderFooter =
            !string.IsNullOrEmpty(setup.OddHeader) || !string.IsNullOrEmpty(setup.OddFooter) ||
            !string.IsNullOrEmpty(setup.EvenHeader) || !string.IsNullOrEmpty(setup.EvenFooter) ||
            !string.IsNullOrEmpty(setup.FirstHeader) || !string.IsNullOrEmpty(setup.FirstFooter) ||
            setup.DifferentOddEvenPages || setup.DifferentFirstPage ||
            !setup.ScaleHeaderFooterWithDocument || !setup.AlignHeaderFooterWithMargins;
        var hf = root.Element(Ns + "headerFooter");
        if (hasHeaderFooter || hf is not null)
        {
            hf ??= GetOrCreate(root, "headerFooter");
            SetBool(hf, "differentOddEven", setup.DifferentOddEvenPages, defaultValue: false);
            SetBool(hf, "differentFirst", setup.DifferentFirstPage, defaultValue: false);
            SetBool(hf, "scaleWithDoc", setup.ScaleHeaderFooterWithDocument, defaultValue: true);
            SetBool(hf, "alignWithMargins", setup.AlignHeaderFooterWithMargins, defaultValue: true);
            SetText(hf, "oddHeader", setup.OddHeader);
            SetText(hf, "oddFooter", setup.OddFooter);
            SetText(hf, "evenHeader", setup.EvenHeader);
            SetText(hf, "evenFooter", setup.EvenFooter);
            SetText(hf, "firstHeader", setup.FirstHeader);
            SetText(hf, "firstFooter", setup.FirstFooter);
            if (!hf.HasAttributes && !hf.HasElements) hf.Remove();
        }
        Save(part, xml);
    }

    private static SpreadsheetPrintComments ReadComments(string? value) => value switch
    {
        "asDisplayed" => SpreadsheetPrintComments.AsDisplayed,
        "atEnd" => SpreadsheetPrintComments.AtEnd,
        _ => SpreadsheetPrintComments.None,
    };

    private static SpreadsheetPrintErrors ReadErrors(string? value) => value switch
    {
        "blank" => SpreadsheetPrintErrors.Blank,
        "dash" => SpreadsheetPrintErrors.Dash,
        "NA" => SpreadsheetPrintErrors.NotAvailable,
        _ => SpreadsheetPrintErrors.Displayed,
    };

    private static int? ReadPositiveInt(XAttribute? attribute)
    {
        if (!int.TryParse(attribute?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value <= 0)
            return null;
        return value;
    }

    private static bool ReadBoolean(XAttribute? attribute, bool defaultValue)
    {
        if (attribute is null) return defaultValue;
        return attribute.Value is "1" or "true" or "TRUE";
    }

    private static void SetBool(XElement element, string name, bool value, bool defaultValue) =>
        element.SetAttributeValue(name, value == defaultValue ? null : value ? "1" : "0");

    private static void SetText(XElement parent, string name, string? value)
    {
        var element = parent.Element(Ns + name);
        if (string.IsNullOrEmpty(value))
        {
            element?.Remove();
            return;
        }
        if (element is null)
        {
            parent.Add(new XElement(Ns + name, value));
        }
        else element.Value = value;
    }

    private static XElement GetOrCreate(XElement root, string name)
    {
        var existing = root.Element(Ns + name);
        if (existing is not null) return existing;
        var element = new XElement(Ns + name);
        var order = new[] { "printOptions", "pageMargins", "pageSetup", "headerFooter", "rowBreaks", "colBreaks", "extLst" };
        var index = Array.IndexOf(order, name);
        var next = root.Elements().FirstOrDefault(candidate => Array.IndexOf(order, candidate.Name.LocalName) > index);
        if (next is null) root.Add(element); else next.AddBeforeSelf(element);
        return element;
    }

    private static XDocument Load(OpenXmlPart part)
    {
        using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }

    private static void Save(OpenXmlPart part, XDocument xml)
    {
        using var stream = part.GetStream(FileMode.Create, FileAccess.Write);
        xml.Save(stream, SaveOptions.DisableFormatting);
    }
}
