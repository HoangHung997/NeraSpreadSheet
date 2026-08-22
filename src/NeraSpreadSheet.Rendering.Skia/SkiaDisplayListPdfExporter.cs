using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering;
using SkiaSharp;

namespace NeraSpreadSheet.Rendering.Skia;

public sealed record SkiaPdfExportOptions
{
    public float RasterDpi { get; init; } = 144f;

    public int MaximumPages { get; init; } = 100_000;

    public long MaximumOutputBytes { get; init; } =
        512L * 1024L * 1024L;

    public double MaximumPageDimensionDips { get; init; } = 200_000d;
}

public sealed record SkiaPdfPage(
    SizeD PageSizeDips,
    DisplayList DisplayList);

/// <summary>
/// Writes platform-neutral Nera display lists as a staged PDF document.
/// PDF pages use point units while Nera display lists use 96-DPI-independent
/// units, so one page-level scale converts 96 DIPs/inch to 72 points/inch.
/// </summary>
public static class SkiaDisplayListPdfExporter
{
    public const double PdfPointsPerDip = 72d / 96d;

    public static async Task SaveAsync(
        IEnumerable<SkiaPdfPage> pages,
        Stream destination,
        SkiaPdfExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite)
        {
            throw new ArgumentException(
                "Destination stream must be writable.",
                nameof(destination));
        }

        options ??= new SkiaPdfExportOptions();
        ValidateOptions(options);
        cancellationToken.ThrowIfCancellationRequested();

        await using var stagingBuffer = new MemoryStream();
        await using (var boundedOutput = new MaximumLengthWriteStream(
                         stagingBuffer,
                         options.MaximumOutputBytes))
        using (var renderer = new SkiaDisplayListRenderer())
        using (var document = SKDocument.CreatePdf(
                   boundedOutput,
                   options.RasterDpi) ??
               throw new InvalidOperationException(
                   "Skia could not create the PDF document."))
        {
            var pageCount = 0;
            var completed = false;
            try
            {
                foreach (var page in pages)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ArgumentNullException.ThrowIfNull(page);
                    ValidatePage(page, options);
                    pageCount = checked(pageCount + 1);
                    if (pageCount > options.MaximumPages)
                    {
                        throw new InvalidDataException(
                            $"The PDF exceeds the page limit of " +
                            $"{options.MaximumPages:N0}.");
                    }

                    var widthPoints = checked((float)(
                        page.PageSizeDips.Width * PdfPointsPerDip));
                    var heightPoints = checked((float)(
                        page.PageSizeDips.Height * PdfPointsPerDip));
                    var canvas = document.BeginPage(
                        widthPoints,
                        heightPoints) ??
                        throw new InvalidOperationException(
                            "Skia could not begin a PDF page.");
                    canvas.Scale(
                        (float)PdfPointsPerDip,
                        (float)PdfPointsPerDip);
                    renderer.Render(canvas, page.DisplayList);
                    document.EndPage();
                }

                if (pageCount == 0)
                {
                    throw new InvalidDataException(
                        "A PDF document must contain at least one page.");
                }

                document.Close();
                completed = true;
            }
            finally
            {
                if (!completed)
                {
                    try
                    {
                        document.Abort();
                    }
                    catch
                    {
                        // Preserve the original generation failure.
                    }
                }
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        stagingBuffer.Position = 0L;
        if (destination.CanSeek)
        {
            destination.Position = 0L;
            destination.SetLength(0L);
        }

        // Cancellation is observed before this commit boundary. Once copying
        // begins, finish the staged document so caller cancellation cannot
        // intentionally leave a seekable destination half-written.
        await stagingBuffer.CopyToAsync(
            destination,
            81920,
            CancellationToken.None).ConfigureAwait(false);
        await destination.FlushAsync(CancellationToken.None)
            .ConfigureAwait(false);
    }

    private static void ValidateOptions(SkiaPdfExportOptions options)
    {
        if (!float.IsFinite(options.RasterDpi) ||
            options.RasterDpi < 36f ||
            options.RasterDpi > 2400f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "RasterDpi must be finite and between 36 and 2400.");
        }
        if (options.MaximumPages <= 0 ||
            options.MaximumPages > 1_000_000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "MaximumPages must be between 1 and 1,000,000.");
        }
        if (options.MaximumOutputBytes <= 0L)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "MaximumOutputBytes must be positive.");
        }
        if (!double.IsFinite(options.MaximumPageDimensionDips) ||
            options.MaximumPageDimensionDips <= 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "MaximumPageDimensionDips must be finite and positive.");
        }
    }

    private static void ValidatePage(
        SkiaPdfPage page,
        SkiaPdfExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(page.DisplayList);
        if (!double.IsFinite(page.PageSizeDips.Width) ||
            page.PageSizeDips.Width <= 0d ||
            page.PageSizeDips.Width > options.MaximumPageDimensionDips ||
            !double.IsFinite(page.PageSizeDips.Height) ||
            page.PageSizeDips.Height <= 0d ||
            page.PageSizeDips.Height > options.MaximumPageDimensionDips)
        {
            throw new InvalidDataException(
                "A PDF page has invalid or excessive physical dimensions.");
        }
    }

    private sealed class MaximumLengthWriteStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _maximumLength;
        private bool _disposed;

        public MaximumLengthWriteStream(
            Stream inner,
            long maximumLength)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            if (!inner.CanWrite)
            {
                throw new ArgumentException(
                    "Inner stream must be writable.",
                    nameof(inner));
            }
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
                maximumLength,
                0L);
            _maximumLength = maximumLength;
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => !_disposed;

        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            ThrowIfDisposed();
            _inner.Flush();
        }

        public override Task FlushAsync(
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            return _inner.FlushAsync(cancellationToken);
        }

        public override void Write(
            byte[] buffer,
            int offset,
            int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            ThrowIfDisposed();
            EnsureCapacity(count);
            _inner.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            ThrowIfDisposed();
            EnsureCapacity(buffer.Length);
            _inner.Write(buffer);
        }

        public override Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            ThrowIfDisposed();
            EnsureCapacity(count);
            return _inner.WriteAsync(
                buffer,
                offset,
                count,
                cancellationToken);
        }

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            EnsureCapacity(buffer.Length);
            return _inner.WriteAsync(buffer, cancellationToken);
        }

        public override int Read(
            byte[] buffer,
            int offset,
            int count) =>
            throw new NotSupportedException();

        public override long Seek(
            long offset,
            SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            _disposed = true;
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            _disposed = true;
            await base.DisposeAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        private void EnsureCapacity(int additionalBytes)
        {
            if (additionalBytes < 0 ||
                _inner.Length > _maximumLength - additionalBytes)
            {
                throw new InvalidDataException(
                    $"PDF output exceeds the staging limit of " +
                    $"{_maximumLength:N0} bytes.");
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }
    }
}
