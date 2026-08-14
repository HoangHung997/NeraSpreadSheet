using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.OpenXml;

public sealed record OpenXmlImportOptions
{
    public bool PreserveUnknownParts { get; init; } = true;

    public bool LoadCachedFormulaValues { get; init; } = true;
}

public sealed record OpenXmlExportOptions
{
    public bool PreserveUnknownParts { get; init; } = true;

    public bool WriteCachedFormulaValues { get; init; } = true;
}

public interface IOpenXmlWorkbookSerializer
{
    Task<Workbook> LoadAsync(
        Stream source,
        OpenXmlImportOptions options,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        Workbook workbook,
        Stream destination,
        OpenXmlExportOptions options,
        CancellationToken cancellationToken = default);
}
