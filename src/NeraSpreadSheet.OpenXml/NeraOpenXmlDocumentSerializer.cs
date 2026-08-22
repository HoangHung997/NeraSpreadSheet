using DocumentFormat.OpenXml.Packaging;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.OpenXml;

/// <summary>
/// Full-document serializer that layers worksheet print settings over the
/// established workbook serializer without introducing OpenXml types into Core.
/// </summary>
public sealed class NeraOpenXmlDocumentSerializer :
    IOpenXmlWorkbookSerializer
{
    private const int MaximumPackageBytes = 512 * 1024 * 1024;
    private readonly NeraOpenXmlWorkbookSerializer _inner = new();

    public OpenXmlSerializerCapabilities Capabilities =>
        _inner.Capabilities;

    public async Task<Workbook> LoadAsync(
        Stream source,
        OpenXmlImportOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        if (!source.CanRead)
        {
            throw new ArgumentException(
                "Source stream must be readable.",
                nameof(source));
        }

        var packageBytes = await ReadPackageAsync(
            source,
            cancellationToken).ConfigureAwait(false);
        OpenXmlPackageGraphValidator.Validate(packageBytes);
        await using var innerSource = new MemoryStream(
            packageBytes,
            writable: false);
        var workbook = await _inner.LoadAsync(
            innerSource,
            options,
            cancellationToken).ConfigureAwait(false);
        using var printSource = new MemoryStream(
            packageBytes,
            writable: false);
        using var document = SpreadsheetDocument.Open(
            printSource,
            false);
        OpenXmlWorksheetPrintSettingsCodec.Read(
            document,
            workbook,
            cancellationToken);
        return workbook;
    }

    public async Task SaveAsync(
        Workbook workbook,
        Stream destination,
        OpenXmlExportOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(options);
        if (!destination.CanWrite)
        {
            throw new ArgumentException(
                "Destination stream must be writable.",
                nameof(destination));
        }

        cancellationToken.ThrowIfCancellationRequested();
        await using var generated = new MemoryStream();
        await _inner.SaveAsync(
            workbook,
            generated,
            options,
            cancellationToken).ConfigureAwait(false);
        var patched = OpenXmlWorksheetPrintSettingsCodec.Patch(
            generated.ToArray(),
            workbook,
            cancellationToken);
        if (patched.Length > MaximumPackageBytes)
        {
            throw new InvalidDataException(
                $"The XLSX package exceeds the document serializer limit of " +
                $"{MaximumPackageBytes:N0} bytes.");
        }
        OpenXmlPackageGraphValidator.Validate(patched);
        var outputEnvelope = options.PreserveUnknownParts
            ? OpenXmlPackageEnvelope.Capture(patched, workbook)
            : null;
        cancellationToken.ThrowIfCancellationRequested();

        if (destination.CanSeek)
        {
            destination.Position = 0L;
            destination.SetLength(0L);
        }
        await destination.WriteAsync(
            patched.AsMemory(),
            CancellationToken.None).ConfigureAwait(false);
        await destination.FlushAsync(CancellationToken.None)
            .ConfigureAwait(false);
        if (outputEnvelope is not null)
        {
            OpenXmlPackageEnvelopeStore.Attach(
                workbook,
                outputEnvelope);
        }
    }

    private static async Task<byte[]> ReadPackageAsync(
        Stream source,
        CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (true)
        {
            var read = await source.ReadAsync(
                chunk.AsMemory(),
                cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }
            if (buffer.Length + read > MaximumPackageBytes)
            {
                throw new InvalidDataException(
                    $"The XLSX package exceeds the document serializer limit of " +
                    $"{MaximumPackageBytes:N0} bytes.");
            }
            await buffer.WriteAsync(
                chunk.AsMemory(0, read),
                cancellationToken).ConfigureAwait(false);
        }
        return buffer.ToArray();
    }
}
