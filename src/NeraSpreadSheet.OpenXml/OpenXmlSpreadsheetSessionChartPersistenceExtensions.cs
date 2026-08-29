using DocumentFormat.OpenXml.Packaging;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.OpenXml;

/// <summary>
/// Adds standard XLSX chart/drawing emission to a session serializer without
/// changing the serializer's existing native metadata contract.
/// </summary>
public static class OpenXmlSpreadsheetSessionChartPersistenceExtensions
{
    public static async Task SaveSessionWithStandardChartsAsync(
        this IOpenXmlSpreadsheetSessionSerializer serializer,
        SpreadsheetSession session,
        Stream destination,
        OpenXmlExportOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(options);
        if (!destination.CanWrite)
        {
            throw new ArgumentException("Destination stream must be writable.", nameof(destination));
        }

        await using var buffer = new MemoryStream();
        await serializer
            .SaveSessionAsync(session, buffer, options, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        buffer.Position = 0L;
        using (var document = SpreadsheetDocument.Open(buffer, true))
        {
            NeraOpenXmlChartDrawingCodec.Export(document, session, cancellationToken);
        }

        buffer.Position = 0L;
        if (destination.CanSeek)
        {
            destination.Position = 0L;
            destination.SetLength(0L);
        }
        await buffer.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
    }
}
