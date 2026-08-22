using System.Text;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

public static class DelimitedTextAtomicExporter
{
    public const long DefaultMaximumOutputBytes =
        512L * 1024L * 1024L;

    public static async Task SaveAsync(
        Worksheet worksheet,
        Stream destination,
        DelimitedTextExportOptions? options = null,
        Encoding? encoding = null,
        long maximumOutputBytes = DefaultMaximumOutputBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite)
        {
            throw new ArgumentException(
                "Destination stream must be writable.",
                nameof(destination));
        }
        if (maximumOutputBytes <= 0L)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumOutputBytes));
        }

        cancellationToken.ThrowIfCancellationRequested();
        await using var buffer = new MemoryStream();
        await using (var bounded = new MaximumLengthWriteStream(
                         buffer,
                         maximumOutputBytes))
        {
            await DelimitedTextWorkbookSerializer.SaveAsync(
                worksheet,
                bounded,
                options,
                encoding,
                cancellationToken).ConfigureAwait(false);
        }
        cancellationToken.ThrowIfCancellationRequested();

        buffer.Position = 0L;
        if (destination.CanSeek)
        {
            destination.Position = 0L;
            destination.SetLength(0L);
        }

        // Cancellation is observed before this commit boundary. Once commit
        // starts, finish the staged copy so caller cancellation cannot leave a
        // seekable destination half-written.
        await buffer.CopyToAsync(
            destination,
            81920,
            CancellationToken.None).ConfigureAwait(false);
        await destination.FlushAsync(CancellationToken.None)
            .ConfigureAwait(false);
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
            if (maximumLength <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumLength));
            }
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

        public override void Write(
            ReadOnlySpan<byte> buffer)
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

        public override ValueTask DisposeAsync()
        {
            _disposed = true;
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }

        private void EnsureCapacity(int additionalBytes)
        {
            if (additionalBytes < 0 ||
                _inner.Length > _maximumLength - additionalBytes)
            {
                throw new InvalidDataException(
                    $"Delimited-text output exceeds the staging limit of " +
                    $"{_maximumLength:N0} bytes.");
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }
    }
}
