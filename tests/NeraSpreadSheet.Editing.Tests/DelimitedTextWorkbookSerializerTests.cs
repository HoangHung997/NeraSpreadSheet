using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class DelimitedTextWorkbookSerializerTests
{
    [TestMethod]
    public async Task QuotedDelimiterEscapedQuoteAndNewlineImport()
    {
        const string csv =
            "Name,Note\r\n" +
            "Alice,\"Hello, world\"\r\n" +
            "Bob,\"Line 1\nLine 2 with \"\"quote\"\"\"";
        await using var stream = CreateUtf8Stream(csv);

        var workbook = await DelimitedTextWorkbookSerializer.LoadAsync(stream);
        var worksheet = workbook.Worksheets[0];

        Assert.AreEqual("Name", worksheet.GetValue(new CellAddress(0, 0)));
        Assert.AreEqual("Hello, world", worksheet.GetValue(new CellAddress(1, 1)));
        Assert.AreEqual(
            "Line 1\nLine 2 with \"quote\"",
            worksheet.GetValue(new CellAddress(2, 1)));
        Assert.AreEqual(6, worksheet.UsedCellCount);
    }

    [TestMethod]
    public async Task TypeInferenceAndFormulaPolicyAreExplicit()
    {
        const string csv = "42,TRUE,2026-08-22,=A1+1";
        await using var source = CreateUtf8Stream(csv);
        var valuesOnly = await DelimitedTextWorkbookSerializer.LoadAsync(
            source,
            new DelimitedTextImportOptions
            {
                InferDates = true,
            });
        var first = valuesOnly.Worksheets[0];
        Assert.AreEqual(42d, first.GetValue(new CellAddress(0, 0)));
        Assert.AreEqual(true, first.GetValue(new CellAddress(0, 1)));
        Assert.IsInstanceOfType<DateTime>(
            first.GetValue(new CellAddress(0, 2)));
        Assert.AreEqual("=A1+1", first.GetValue(new CellAddress(0, 3)));
        Assert.IsNull(first.GetFormula(new CellAddress(0, 3)));

        source.Position = 0L;
        var formulas = await DelimitedTextWorkbookSerializer.LoadAsync(
            source,
            new DelimitedTextImportOptions
            {
                ImportLeadingEqualsAsFormula = true,
            });
        Assert.AreEqual(
            "=A1+1",
            formulas.Worksheets[0].GetFormula(new CellAddress(0, 3)));
    }

    [TestMethod]
    public async Task ExportQuotesFieldsAndProtectsFormulaLikeText()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), "a,b");
        worksheet.SetValue(new CellAddress(0, 1), "line1\nline2");
        worksheet.SetValue(new CellAddress(0, 2), "=SUM(A1:A2)");
        worksheet.SetValue(new CellAddress(0, 3), "He said \"hello\"");
        await using var stream = new MemoryStream();

        await DelimitedTextWorkbookSerializer.SaveAsync(
            worksheet,
            stream,
            new DelimitedTextExportOptions());
        var text = Encoding.UTF8.GetString(stream.ToArray());

        Assert.AreEqual(
            "\"a,b\",\"line1\nline2\",'=SUM(A1:A2),\"He said \"\"hello\"\"\"",
            text);
    }

    [TestMethod]
    public async Task FormulaExportCanWriteFormulaText()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), 2d);
        worksheet.SetFormula(new CellAddress(0, 1), "=A1*2");
        await using var stream = new MemoryStream();

        await DelimitedTextWorkbookSerializer.SaveAsync(
            worksheet,
            stream,
            new DelimitedTextExportOptions
            {
                WriteFormulas = true,
            });

        Assert.AreEqual(
            "2,=A1*2",
            Encoding.UTF8.GetString(stream.ToArray()));
    }

    [TestMethod]
    public async Task TsvRoundTripPreservesSparseBoundsAndQuotedTabs()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(1, 1), "A\tB");
        worksheet.SetValue(new CellAddress(2, 2), 9d);
        await using var stream = new MemoryStream();
        var export = new DelimitedTextExportOptions
        {
            Delimiter = '\t',
            NewLine = "\n",
            Range = new CellRange(
                new CellAddress(1, 1),
                new CellAddress(2, 2)),
        };

        await DelimitedTextWorkbookSerializer.SaveAsync(
            worksheet,
            stream,
            export);
        stream.Position = 0L;
        var loaded = await DelimitedTextWorkbookSerializer.LoadAsync(
            stream,
            new DelimitedTextImportOptions
            {
                Delimiter = '\t',
            });

        Assert.AreEqual(
            "A\tB",
            loaded.Worksheets[0].GetValue(new CellAddress(0, 0)));
        Assert.AreEqual(
            9d,
            loaded.Worksheets[0].GetValue(new CellAddress(1, 1)));
    }

    [TestMethod]
    public async Task SafetyLimitsRejectOversizedInput()
    {
        await using var rows = CreateUtf8Stream("a\nb");
        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
            await DelimitedTextWorkbookSerializer.LoadAsync(
                rows,
                new DelimitedTextImportOptions
                {
                    MaximumRows = 1,
                }));

        await using var field = CreateUtf8Stream("abcd");
        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
            await DelimitedTextWorkbookSerializer.LoadAsync(
                field,
                new DelimitedTextImportOptions
                {
                    MaximumCellCharacters = 3,
                }));
    }

    [TestMethod]
    public async Task UnterminatedQuotedFieldIsRejected()
    {
        await using var source = CreateUtf8Stream("a,\"unterminated");

        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
            await DelimitedTextWorkbookSerializer.LoadAsync(source));
    }

    [TestMethod]
    public async Task PreCanceledImportDoesNotReturnPartialWorkbook()
    {
        await using var source = CreateUtf8Stream("a,b\nc,d");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await DelimitedTextWorkbookSerializer.LoadAsync(
                source,
                cancellationToken: cancellation.Token));
    }

    private static MemoryStream CreateUtf8Stream(string text) =>
        new(Encoding.UTF8.GetBytes(text));
}
