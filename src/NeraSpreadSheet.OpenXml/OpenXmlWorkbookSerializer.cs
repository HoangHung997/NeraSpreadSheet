using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.OpenXml;

/// <summary>Strict retains existing defaults; Compatibility requests existing opaque preservation.</summary>
public enum OpenXmlImportMode { Strict, Compatibility }

public sealed record OpenXmlImportOptions
{
    /// <summary>Creates an explicit import policy without changing constructor defaults.
    /// Compatibility requires PreserveUnknownParts on export; inspect OpenXmlImportDiagnostics.Get.</summary>
    public static OpenXmlImportOptions ForMode(OpenXmlImportMode mode) => mode switch
    {
        OpenXmlImportMode.Strict => new(),
        OpenXmlImportMode.Compatibility => new() { PreserveUnknownParts = true },
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };
    public bool PreserveUnknownParts { get; init; }
    public bool LoadCachedFormulaValues { get; init; } = true;
}

public sealed record OpenXmlExportOptions
{
    public bool PreserveUnknownParts { get; init; }
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

public sealed record OpenXmlSerializerCapabilities(
    bool ReadsBasicCells,
    bool WritesBasicCells,
    bool ReadsFormulas,
    bool WritesFormulas,
    bool ReadsBasicDimensions,
    bool WritesBasicDimensions,
    bool PreservesUnknownParts,
    bool ReadsMergedCells = false,
    bool WritesMergedCells = false);
