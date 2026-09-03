namespace NeraSpreadSheet.OpenXml;

internal static class OpenXmlPackageWriteRecovery
{
    public static async Task WritePackageAsync(
        Stream destination,
        byte[] packageBytes)
    {
        byte[]? originalBytes = null;
        var originalPosition = 0L;
        if (destination.CanSeek &&
            destination.CanRead)
        {
            originalPosition = destination.Position;
            originalBytes = await CaptureDestinationAsync(destination)
                .ConfigureAwait(false);
        }

        try
        {
            if (destination.CanSeek)
            {
                destination.Position = 0L;
                destination.SetLength(0L);
            }
            await destination.WriteAsync(
                packageBytes.AsMemory(),
                CancellationToken.None).ConfigureAwait(false);
            await destination.FlushAsync(CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception) when (originalBytes is not null)
        {
            await RestoreDestinationAsync(
                destination,
                originalBytes,
                originalPosition).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<byte[]> CaptureDestinationAsync(Stream destination)
    {
        destination.Position = 0L;
        await using var buffer = new MemoryStream();
        await destination.CopyToAsync(
            buffer,
            CancellationToken.None).ConfigureAwait(false);
        return buffer.ToArray();
    }

    private static async Task RestoreDestinationAsync(
        Stream destination,
        byte[] originalBytes,
        long originalPosition)
    {
        destination.Position = 0L;
        destination.SetLength(0L);
        await destination.WriteAsync(
            originalBytes.AsMemory(),
            CancellationToken.None).ConfigureAwait(false);
        await destination.FlushAsync(CancellationToken.None)
            .ConfigureAwait(false);
        destination.Position = Math.Min(
            originalPosition,
            destination.Length);
    }
}
