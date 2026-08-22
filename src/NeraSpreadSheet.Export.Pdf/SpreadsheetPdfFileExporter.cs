using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Export.Pdf;

public static class SpreadsheetPdfFileExporter
{
    public static async Task<SpreadsheetPdfExportResult> SaveAsync(
        Worksheet worksheet,
        string destinationPath,
        SpreadsheetPdfExportOptions? options = null,
        CellStyleCatalog? styles = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException(
                "The PDF destination must resolve to a directory.",
                nameof(destinationPath));
        }
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        var committed = false;
        try
        {
            SpreadsheetPdfExportResult result;
            await using (var temporary = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 81920,
                             options: FileOptions.Asynchronous |
                                      FileOptions.SequentialScan))
            {
                result = await SpreadsheetPdfExporter.SaveAsync(
                    worksheet,
                    temporary,
                    options,
                    styles,
                    cancellationToken).ConfigureAwait(false);
                await temporary.FlushAsync(CancellationToken.None)
                    .ConfigureAwait(false);
                temporary.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(
                temporaryPath,
                fullPath,
                overwrite: true);
            committed = true;
            return result with
            {
                OutputLength = new FileInfo(fullPath).Length,
            };
        }
        finally
        {
            if (!committed)
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch
                {
                    // Preserve the original export failure. The uniquely named
                    // temporary file can be removed by later housekeeping.
                }
            }
        }
    }
}
