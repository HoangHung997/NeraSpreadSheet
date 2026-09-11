using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using NeraSpreadSheet.Core;
using NeraWorkbook = NeraSpreadSheet.Core.Workbook;

namespace NeraSpreadSheet.OpenXml;

internal static class OpenXmlProtection009Codec
{
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public static void Read(SpreadsheetDocument document, NeraWorkbook workbook, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(workbook);
        var workbookPart = document.WorkbookPart ?? throw new InvalidDataException("The XLSX package does not contain a workbook part.");
        ReadWorkbookProtection(workbookPart, workbook);
        var sheets = workbookPart.Workbook?.GetFirstChild<Sheets>()?.Elements<Sheet>().ToArray() ?? [];
        var count = Math.Min(sheets.Length, workbook.Worksheets.Count);
        for (var index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relationshipId = sheets[index].Id?.Value;
            if (string.IsNullOrWhiteSpace(relationshipId) || workbookPart.GetPartById(relationshipId) is not WorksheetPart part) continue;
            ReadWorksheetProtection(part, workbook.Worksheets[index]);
        }
    }

    public static byte[] Patch(byte[] packageBytes, NeraWorkbook workbook, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packageBytes);
        ArgumentNullException.ThrowIfNull(workbook);
        using var buffer = new MemoryStream();
        buffer.Write(packageBytes);
        buffer.Position = 0;
        using (var document = SpreadsheetDocument.Open(buffer, true))
        {
            var workbookPart = document.WorkbookPart ?? throw new InvalidDataException("The XLSX package does not contain a workbook part.");
            PatchWorkbookProtection(workbookPart, workbook.GetProtectionSettings());
            var sheets = workbookPart.Workbook?.GetFirstChild<Sheets>()?.Elements<Sheet>().ToArray() ?? [];
            if (sheets.Length != workbook.Worksheets.Count) throw new InvalidOperationException("Worksheet topology differs while writing protection settings.");
            for (var index = 0; index < sheets.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relationshipId = sheets[index].Id?.Value;
                if (string.IsNullOrWhiteSpace(relationshipId) || workbookPart.GetPartById(relationshipId) is not WorksheetPart part)
                    throw new InvalidDataException("The XLSX worksheet relationship is invalid.");
                PatchWorksheetProtection(part, workbook.Worksheets[index].GetProtectionSettings());
            }
        }
        return buffer.ToArray();
    }

    private static void ReadWorkbookProtection(WorkbookPart part, NeraWorkbook workbook)
    {
        var xml = Load(part);
        var element = xml.Root?.Element(Ns + "workbookProtection");
        if (element is null)
        {
            workbook.SetProtectionSettings(new WorkbookProtectionSettings());
            return;
        }
        var structure = ReadBool(element.Attribute("lockStructure"));
        var windows = ReadBool(element.Attribute("lockWindows"));
        workbook.SetProtectionSettings(new WorkbookProtectionSettings
        {
            Enabled = structure || windows,
            LockStructure = structure,
            LockWindows = windows,
            PasswordHash = NormalizeHash((string?)element.Attribute("workbookPassword")),
        });
    }

    private static void ReadWorksheetProtection(WorksheetPart part, Worksheet worksheet)
    {
        var xml = Load(part);
        var element = xml.Root?.Element(Ns + "sheetProtection");
        if (element is null || !ReadBool(element.Attribute("sheet")))
        {
            worksheet.SetProtectionSettings(new WorksheetProtectionSettings());
            return;
        }
        worksheet.SetProtectionSettings(new WorksheetProtectionSettings
        {
            Enabled = true,
            PasswordHash = NormalizeHash((string?)element.Attribute("password")),
            SelectLockedCells = !ReadBool(element.Attribute("selectLockedCells")),
            SelectUnlockedCells = !ReadBool(element.Attribute("selectUnlockedCells")),
            AllowFormatCells = !ReadBool(element.Attribute("formatCells")),
            AllowFormatColumns = !ReadBool(element.Attribute("formatColumns")),
            AllowFormatRows = !ReadBool(element.Attribute("formatRows")),
            AllowInsertColumns = !ReadBool(element.Attribute("insertColumns")),
            AllowInsertRows = !ReadBool(element.Attribute("insertRows")),
            AllowInsertHyperlinks = !ReadBool(element.Attribute("insertHyperlinks")),
            AllowDeleteColumns = !ReadBool(element.Attribute("deleteColumns")),
            AllowDeleteRows = !ReadBool(element.Attribute("deleteRows")),
            AllowSort = !ReadBool(element.Attribute("sort")),
            AllowAutoFilter = !ReadBool(element.Attribute("autoFilter")),
            AllowPivotTables = !ReadBool(element.Attribute("pivotTables")),
            AllowEditObjects = !ReadBool(element.Attribute("objects")),
            AllowEditScenarios = !ReadBool(element.Attribute("scenarios")),
        });
    }

    private static void PatchWorkbookProtection(WorkbookPart part, WorkbookProtectionSettings settings)
    {
        var xml = Load(part);
        var root = xml.Root ?? throw new InvalidDataException("The XLSX workbook XML has no root element.");
        root.Elements(Ns + "workbookProtection").Remove();
        if (settings.Enabled)
        {
            var element = new XElement(Ns + "workbookProtection");
            SetBool(element, "lockStructure", settings.LockStructure);
            SetBool(element, "lockWindows", settings.LockWindows);
            element.SetAttributeValue("workbookPassword", settings.PasswordHash);
            var before = root.Element(Ns + "bookViews") ?? root.Element(Ns + "sheets");
            if (before is null) root.Add(element); else before.AddBeforeSelf(element);
        }
        Save(part, xml);
    }

    private static void PatchWorksheetProtection(WorksheetPart part, WorksheetProtectionSettings settings)
    {
        var xml = Load(part);
        var root = xml.Root ?? throw new InvalidDataException("The XLSX worksheet XML has no root element.");
        root.Elements(Ns + "sheetProtection").Remove();
        if (settings.Enabled)
        {
            var element = new XElement(Ns + "sheetProtection", new XAttribute("sheet", "1"));
            element.SetAttributeValue("password", settings.PasswordHash);
            SetProhibition(element, "selectLockedCells", settings.SelectLockedCells);
            SetProhibition(element, "selectUnlockedCells", settings.SelectUnlockedCells);
            SetProhibition(element, "formatCells", settings.AllowFormatCells);
            SetProhibition(element, "formatColumns", settings.AllowFormatColumns);
            SetProhibition(element, "formatRows", settings.AllowFormatRows);
            SetProhibition(element, "insertColumns", settings.AllowInsertColumns);
            SetProhibition(element, "insertRows", settings.AllowInsertRows);
            SetProhibition(element, "insertHyperlinks", settings.AllowInsertHyperlinks);
            SetProhibition(element, "deleteColumns", settings.AllowDeleteColumns);
            SetProhibition(element, "deleteRows", settings.AllowDeleteRows);
            SetProhibition(element, "sort", settings.AllowSort);
            SetProhibition(element, "autoFilter", settings.AllowAutoFilter);
            SetProhibition(element, "pivotTables", settings.AllowPivotTables);
            SetProhibition(element, "objects", settings.AllowEditObjects);
            SetProhibition(element, "scenarios", settings.AllowEditScenarios);
            var before = root.Elements().FirstOrDefault(candidate => candidate.Name.LocalName is "protectedRanges" or "scenarios" or "autoFilter" or "sortState" or "dataConsolidate" or "mergeCells" or "conditionalFormatting" or "dataValidations" or "hyperlinks" or "printOptions" or "pageMargins" or "pageSetup" or "headerFooter" or "rowBreaks" or "colBreaks" or "extLst");
            var sheetData = root.Element(Ns + "sheetData");
            if (before is not null) before.AddBeforeSelf(element);
            else if (sheetData is not null) sheetData.AddAfterSelf(element);
            else root.Add(element);
        }
        Save(part, xml);
    }

    private static void SetProhibition(XElement element, string attribute, bool allow) =>
        element.SetAttributeValue(attribute, allow ? null : "1");

    private static void SetBool(XElement element, string attribute, bool value) =>
        element.SetAttributeValue(attribute, value ? "1" : null);

    private static bool ReadBool(XAttribute? attribute) => attribute?.Value is "1" or "true" or "TRUE";

    private static string? NormalizeHash(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return ushort.TryParse(value, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed.ToString("X4", System.Globalization.CultureInfo.InvariantCulture)
            : null;
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
