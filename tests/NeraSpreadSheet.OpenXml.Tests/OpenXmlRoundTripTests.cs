using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class OpenXmlRoundTripTests
{
    [TestMethod]
    public async Task SaveAndLoadRoundTripsBasicCellsFormulasAndDimensions()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(0, 0), "Nera");
        sheet.SetValue(new CellAddress(1, 0), 12.5d);
        sheet.SetValue(new CellAddress(2, 0), true);
        sheet.SetFormula(new CellAddress(3, 0), "=A2*2");
        sheet.SetCell(
            new CellAddress(3, 0),
            new CellData(
                CellValue.FromNumber(25d),
                "=A2*2"));
        sheet.Dimensions.SetColumnWidth(0, 120d);
        sheet.Dimensions.SetRowHeight(1, 36d);
        workbook
            .AddWorksheet("Second")
            .SetValue(default, "Sheet 2");

        var serializer =
            new NeraOpenXmlWorkbookSerializer();
        await using var stream = new MemoryStream();
        await serializer.SaveAsync(
            workbook,
            stream,
            new OpenXmlExportOptions());
        stream.Position = 0;
        var loaded = await serializer.LoadAsync(
            stream,
            new OpenXmlImportOptions());

        Assert.AreEqual(2, loaded.Worksheets.Count);
        var loadedSheet = loaded.Worksheets[0];
        Assert.AreEqual(
            "Nera",
            loadedSheet
                .GetCell(new CellAddress(0, 0))
                .Value.RawValue);
        Assert.AreEqual(
            12.5d,
            loadedSheet
                .GetCell(new CellAddress(1, 0))
                .Value.RawValue);
        Assert.AreEqual(
            true,
            loadedSheet
                .GetCell(new CellAddress(2, 0))
                .Value.RawValue);
        Assert.AreEqual(
            "=A2*2",
            loadedSheet
                .GetCell(new CellAddress(3, 0))
                .Formula);
        Assert.AreEqual(
            25d,
            loadedSheet
                .GetCell(new CellAddress(3, 0))
                .Value.RawValue);
        Assert.AreEqual(
            120d,
            loadedSheet.Dimensions.GetColumnWidth(0),
            1d);
        Assert.AreEqual(
            36d,
            loadedSheet.Dimensions.GetRowHeight(1),
            0.01d);
        Assert.AreEqual(
            "Sheet 2",
            loaded
                .GetWorksheet("Second")
                .GetCell(default)
                .Value.RawValue);
    }

    [TestMethod]
    public async Task LoadCanIgnoreCachedFormulaValue()
    {
        var workbook = new Workbook();
        workbook.Worksheets[0].SetCell(
            default,
            new CellData(
                CellValue.FromNumber(99d),
                "=1+1"));
        var serializer =
            new NeraOpenXmlWorkbookSerializer();
        await using var stream = new MemoryStream();
        await serializer.SaveAsync(
            workbook,
            stream,
            new OpenXmlExportOptions());
        stream.Position = 0;

        var loaded = await serializer.LoadAsync(
            stream,
            new OpenXmlImportOptions
            {
                LoadCachedFormulaValues = false,
            });

        var cell =
            loaded.Worksheets[0].GetCell(default);
        Assert.AreEqual("=1+1", cell.Formula);
        Assert.IsTrue(cell.Value.IsBlank);
    }

    [TestMethod]
    public async Task NewWorkbookCanSaveWithUnknownPartPreservationEnabled()
    {
        var serializer =
            new NeraOpenXmlWorkbookSerializer();
        var workbook = new Workbook();
        workbook.Worksheets[0].SetValue(
            default,
            "preserved-baseline");
        await using var stream = new MemoryStream();

        await serializer.SaveAsync(
            workbook,
            stream,
            new OpenXmlExportOptions
            {
                PreserveUnknownParts = true,
            });

        Assert.IsTrue(
            serializer.Capabilities.PreservesUnknownParts);
        stream.Position = 0L;
        var loaded = await serializer.LoadAsync(
            stream,
            new OpenXmlImportOptions
            {
                PreserveUnknownParts = true,
            });
        Assert.AreEqual(
            "preserved-baseline",
            loaded.Worksheets[0]
                .GetCell(default)
                .Value.RawValue);
    }

    [TestMethod]
    public async Task DocumentSaveRestoresSeekableReadableDestinationWhenWriteFails()
    {
        var original = System.Text.Encoding.UTF8.GetBytes(
            "existing destination bytes");
        await using var destination = new FailingWriteStream(original);
        var workbook = new Workbook();
        workbook.Worksheets[0].SetValue(
            default,
            "new package");
        var serializer = new NeraOpenXmlDocumentSerializer();

        await Assert.ThrowsExactlyAsync<IOException>(async () =>
            await serializer.SaveAsync(
                workbook,
                destination,
                new OpenXmlExportOptions()));

        CollectionAssert.AreEqual(
            original,
            destination.ToArray());
    }

    private sealed class FailingWriteStream : Stream
    {
        private readonly MemoryStream _inner;
        private bool _failNextWrite = true;

        public FailingWriteStream(byte[] bytes)
        {
            _inner = new MemoryStream(bytes.ToArray());
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => true;

        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public byte[] ToArray() => _inner.ToArray();

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public override int Read(byte[] buffer, int offset, int count) =>
            _inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) =>
            _inner.Seek(offset, origin);

        public override void SetLength(long value) =>
            _inner.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count)
        {
            ThrowOnce();
            _inner.Write(buffer, offset, count);
        }

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ThrowOnce();
            return _inner.WriteAsync(buffer, cancellationToken);
        }

        private void ThrowOnce()
        {
            if (!_failNextWrite)
            {
                return;
            }
            _failNextWrite = false;
            throw new IOException("Simulated destination write failure.");
        }
    }
}
