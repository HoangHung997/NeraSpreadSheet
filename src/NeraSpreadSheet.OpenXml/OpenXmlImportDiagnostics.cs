using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.OpenXml;

/// <summary>Classification of a compatibility observation, not a workbook fidelity claim.</summary>
public enum OpenXmlImportProblemKind
{
    UnsupportedFeature,
    InvalidMarkup,
    SafetyLimit,
}

/// <summary>Describes one unmodeled feature. No cell values or formula bodies are included.</summary>
public sealed record OpenXmlImportWarning(
    string Code,
    OpenXmlImportProblemKind Kind,
    string Feature,
    string? WorksheetName,
    string? Reference,
    int? Priority,
    int? DifferentialStyleId,
    string Message);

/// <summary>
/// Observations from differential-style and conditional-formatting import only.
/// Empty warnings do not certify that every other XLSX feature was evaluated.
/// Opaque formatting is not rendered/edited by the SDK. Retention requires
/// PreserveUnknownParts on export and the protected metadata to remain unchanged.
/// </summary>
public sealed class OpenXmlImportReport
{
    internal static OpenXmlImportReport Empty { get; } = new([], 0, false);
    internal OpenXmlImportReport(OpenXmlImportWarning[] warnings, int omitted, bool opaque)
    {
        Warnings = Array.AsReadOnly(warnings);
        OmittedWarningCount = omitted;
        RequiresMetadataPreservation = opaque;
    }
    public IReadOnlyList<OpenXmlImportWarning> Warnings { get; }
    public int OmittedWarningCount { get; }
    /// <summary>
    /// True means ordinary cell value/formula/base-style edits can be preserved,
    /// but this checkpoint rejects preserve-save after metadata/structure edits.
    /// It does not claim to roll back such edits in the in-memory model.
    /// </summary>
    public bool RequiresMetadataPreservation { get; }
}

/// <summary>Accesses immutable import observations without introducing UI types into the engine.</summary>
public static class OpenXmlImportDiagnostics
{
    private const string FailureKey = "Nera.OpenXml.ImportProblem";
    private static readonly ConditionalWeakTable<Workbook, OpenXmlImportReport> Reports = new();
    private static readonly object Sync = new();

    public static OpenXmlImportReport Get(Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        return Reports.TryGetValue(workbook, out var report) ? report : OpenXmlImportReport.Empty;
    }

    /// <summary>Reads a classified CF/dxf failure; false means unclassified, not ignorable.</summary>
    public static bool TryGetFailure(Exception exception, [NotNullWhen(true)] out OpenXmlImportWarning? problem)
    {
        ArgumentNullException.ThrowIfNull(exception);
        problem = exception.Data[FailureKey] as OpenXmlImportWarning;
        return problem is not null;
    }

    internal static void Attach(Workbook workbook, OpenXmlDifferentialImport state)
    {
        lock (Sync)
        {
            Reports.Remove(workbook);
            Reports.Add(workbook, state.CreateReport());
        }
    }

    internal static void Detach(Workbook workbook)
    {
        lock (Sync) Reports.Remove(workbook);
    }

    internal static InvalidDataException Failure(OpenXmlImportWarning problem)
    {
        var exception = new InvalidDataException(problem.Message);
        exception.Data[FailureKey] = problem;
        return exception;
    }

    internal static OpenXmlImportWarning Unsupported(string feature, string? sheet = null,
        string? reference = null, int? priority = null, int? dxf = null) => new(
        "XLSX_OPAQUE_FORMATTING", OpenXmlImportProblemKind.UnsupportedFeature, feature, sheet, reference, priority, dxf,
        "The SDK does not evaluate/edit " + feature + ". Compatibility retains the original formatting, not Excel visual fidelity. " +
        "Use PreserveUnknownParts on save and keep protected metadata unchanged. Opaque rules may affect priority/stopIfTrue.");

    internal static InvalidDataException Invalid(string message, string feature, string? sheet = null,
        bool limit = false) => Failure(new OpenXmlImportWarning(limit ? "XLSX_FORMATTING_LIMIT" : "XLSX_INVALID_FORMATTING",
            limit ? OpenXmlImportProblemKind.SafetyLimit : OpenXmlImportProblemKind.InvalidMarkup,
            feature, sheet, null, null, null, message));
}

/// <summary>Original dxf slots plus a bounded import report; not a second style model.</summary>
internal sealed class OpenXmlDifferentialImport(int count) : IReadOnlyList<CellStylePatch>
{
    private const int MaximumWarnings = 512;
    private readonly CellStylePatch[] _styles = new CellStylePatch[count];
    private readonly HashSet<int> _opaqueIds = [];
    private readonly List<OpenXmlImportWarning> _warnings = [];
    private int _omitted;
    private bool _requiresPreservation;
    public int Count => _styles.Length;
    public CellStylePatch this[int index] => _styles[index];
    internal bool IsOpaque(int index) => _opaqueIds.Contains(index);
    internal void Set(int index, CellStylePatch value, bool opaque = false)
    {
        _styles[index] = value;
        if (opaque) _opaqueIds.Add(index);
    }
    internal void Warn(OpenXmlImportWarning warning)
    {
        _requiresPreservation = true;
        if (_warnings.Count < MaximumWarnings) _warnings.Add(warning);
        else _omitted++;
    }
    internal OpenXmlImportReport CreateReport() => new(_warnings.ToArray(), _omitted, _requiresPreservation);
    public IEnumerator<CellStylePatch> GetEnumerator() => ((IEnumerable<CellStylePatch>)_styles).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
