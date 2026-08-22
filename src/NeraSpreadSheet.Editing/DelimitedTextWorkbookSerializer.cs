using System.Globalization;
using System.Text;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

public sealed record DelimitedTextImportOptions
{
    public char Delimiter { get; init; } = ',';

    public char Quote { get; init; } = '"';

    public string WorksheetName { get; init; } = "Sheet1";

    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;

    public bool InferNumbers { get; init; } = true;

    public bool InferBooleans { get; init; } = true;

    public bool InferDates { get; init; }

    public bool ImportLeadingEqualsAsFormula { get; init; }

    public bool TrimUnquotedFields { get; init; }

    public int MaximumRows { get; init; } = SpreadsheetLimits.MaxRows;

    public int MaximumColumns { get; init; } = SpreadsheetLimits.MaxColumns;

    public int MaximumCellCharacters { get; init; } = 1_000_000;
}

public sealed record DelimitedTextExportOptions
{
    public char Delimiter { get; init; } = ',';

    public char Quote { get; init; } = '"';

    public string NewLine { get; init; } = "\r\n";

    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;

    public string DateTimeFormat { get; init; } = "O";

    public bool WriteFormulas { get; init; }

    public bool ProtectFormulaLikeText { get; init; } = true;

    public bool WriteUtf8Bom { get; init; }

    public CellRange? Range { get; init; }
}

public static class DelimitedTextWorkbookSerializer
{
    public static async Task<Workbook> LoadAsync(
        Stream source,
        DelimitedTextImportOptions? options = null,
        Encoding? encoding = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead)
        {
            throw new ArgumentException(
                "Source stream must be readable.",
                nameof(source));
        }

        options ??= new DelimitedTextImportOptions();
        ValidateImportOptions(options);
        encoding ??= new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);
        using var reader = new StreamReader(
            source,
            encoding,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 81920,
            leaveOpen: true);
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        workbook.RenameWorksheet(
            worksheet,
            options.WorksheetName.Trim());
        var parser = new DelimitedTextParser(reader, options);
        var rowIndex = 0;
        await foreach (var row in parser.ReadRowsAsync(
                           cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (rowIndex >= options.MaximumRows)
            {
                throw new InvalidDataException(
                    $"Delimited text exceeds the row limit of " +
                    $"{options.MaximumRows:N0}.");
            }
            if (row.Count > options.MaximumColumns)
            {
                throw new InvalidDataException(
                    $"Delimited text exceeds the column limit of " +
                    $"{options.MaximumColumns:N0}.");
            }

            for (var columnIndex = 0;
                 columnIndex < row.Count;
                 columnIndex++)
            {
                var field = row[columnIndex];
                if (field.Text.Length == 0)
                {
                    continue;
                }
                var address = new CellAddress(rowIndex, columnIndex);
                if (options.ImportLeadingEqualsAsFormula &&
                    field.Text.StartsWith('=') &&
                    field.Text.Length > 1)
                {
                    worksheet.SetFormula(address, field.Text);
                    continue;
                }
                worksheet.SetValue(
                    address,
                    InferValue(field, options));
            }
            rowIndex++;
        }

        return workbook;
    }

    public static async Task SaveAsync(
        Worksheet worksheet,
        Stream destination,
        DelimitedTextExportOptions? options = null,
        Encoding? encoding = null,
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

        options ??= new DelimitedTextExportOptions();
        ValidateExportOptions(options);
        encoding ??= new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: options.WriteUtf8Bom,
            throwOnInvalidBytes: true);
        var range = options.Range ?? GetUsedRange(worksheet);
        if (range is null)
        {
            return;
        }

        if (destination.CanSeek)
        {
            destination.Position = 0L;
            destination.SetLength(0L);
        }
        await using var writer = new StreamWriter(
            destination,
            encoding,
            bufferSize: 81920,
            leaveOpen: true)
        {
            NewLine = options.NewLine,
        };
        var targetRange = range.Value;
        for (var row = targetRange.Top;
             row <= targetRange.Bottom;
             row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var column = targetRange.Left;
                 column <= targetRange.Right;
                 column++)
            {
                if (column > targetRange.Left)
                {
                    await writer.WriteAsync(options.Delimiter)
                        .ConfigureAwait(false);
                }
                var address = new CellAddress(row, column);
                var field = FormatCell(
                    worksheet,
                    address,
                    options);
                await WriteFieldAsync(
                    writer,
                    field,
                    options.Delimiter,
                    options.Quote,
                    cancellationToken).ConfigureAwait(false);
            }
            if (row < targetRange.Bottom)
            {
                await writer.WriteAsync(
                    options.NewLine.AsMemory(),
                    cancellationToken).ConfigureAwait(false);
            }
        }
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static object InferValue(
        DelimitedTextField field,
        DelimitedTextImportOptions options)
    {
        var text = options.TrimUnquotedFields && !field.WasQuoted
            ? field.Text.Trim()
            : field.Text;
        if (options.InferBooleans)
        {
            if (string.Equals(
                    text,
                    "TRUE",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(
                    text,
                    "FALSE",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        if (options.InferNumbers &&
            double.TryParse(
                text,
                NumberStyles.Float | NumberStyles.AllowThousands,
                options.Culture,
                out var number) &&
            double.IsFinite(number))
        {
            return number;
        }
        if (options.InferDates &&
            DateTime.TryParse(
                text,
                options.Culture,
                DateTimeStyles.AllowWhiteSpaces |
                DateTimeStyles.RoundtripKind,
                out var dateTime))
        {
            return dateTime;
        }
        return text;
    }

    private static string FormatCell(
        Worksheet worksheet,
        CellAddress address,
        DelimitedTextExportOptions options)
    {
        var formula = worksheet.GetFormula(address);
        if (options.WriteFormulas && formula is not null)
        {
            return formula;
        }

        var value = worksheet.GetValue(address);
        var text = value switch
        {
            null => string.Empty,
            string valueText => valueText,
            bool boolean => boolean ? "TRUE" : "FALSE",
            DateTime dateTime => dateTime.ToString(
                options.DateTimeFormat,
                options.Culture),
            IFormattable formattable => formattable.ToString(
                null,
                options.Culture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty,
        };
        return options.ProtectFormulaLikeText &&
               value is string &&
               IsFormulaLike(text)
            ? $"'{text}"
            : text;
    }

    private static bool IsFormulaLike(string text)
    {
        var index = 0;
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }
        return index < text.Length &&
               text[index] is '=' or '+' or '-' or '@';
    }

    private static async Task WriteFieldAsync(
        TextWriter writer,
        string field,
        char delimiter,
        char quote,
        CancellationToken cancellationToken)
    {
        var requiresQuotes = field.IndexOfAny(
            [delimiter, quote, '\r', '\n']) >= 0 ||
            field.Length > 0 &&
            (char.IsWhiteSpace(field[0]) ||
             char.IsWhiteSpace(field[^1]));
        if (!requiresQuotes)
        {
            await writer.WriteAsync(
                field.AsMemory(),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        await writer.WriteAsync(quote).ConfigureAwait(false);
        var segmentStart = 0;
        for (var index = 0; index < field.Length; index++)
        {
            if (field[index] != quote)
            {
                continue;
            }
            if (index > segmentStart)
            {
                await writer.WriteAsync(
                    field.AsMemory(segmentStart, index - segmentStart),
                    cancellationToken).ConfigureAwait(false);
            }
            await writer.WriteAsync(quote).ConfigureAwait(false);
            await writer.WriteAsync(quote).ConfigureAwait(false);
            segmentStart = index + 1;
        }
        if (segmentStart < field.Length)
        {
            await writer.WriteAsync(
                field.AsMemory(segmentStart),
                cancellationToken).ConfigureAwait(false);
        }
        await writer.WriteAsync(quote).ConfigureAwait(false);
    }

    private static CellRange? GetUsedRange(Worksheet worksheet)
    {
        var cells = worksheet.EnumerateUsedCells().ToArray();
        if (cells.Length == 0)
        {
            return null;
        }
        return new CellRange(
            new CellAddress(
                cells.Min(static cell => cell.Key.RowIndex),
                cells.Min(static cell => cell.Key.ColumnIndex)),
            new CellAddress(
                cells.Max(static cell => cell.Key.RowIndex),
                cells.Max(static cell => cell.Key.ColumnIndex)));
    }

    private static void ValidateImportOptions(
        DelimitedTextImportOptions options)
    {
        ValidateCharacters(options.Delimiter, options.Quote);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.WorksheetName);
        ArgumentNullException.ThrowIfNull(options.Culture);
        if (options.MaximumRows <= 0 ||
            options.MaximumRows > SpreadsheetLimits.MaxRows)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "MaximumRows must be inside the spreadsheet row limit.");
        }
        if (options.MaximumColumns <= 0 ||
            options.MaximumColumns > SpreadsheetLimits.MaxColumns)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "MaximumColumns must be inside the spreadsheet column limit.");
        }
        if (options.MaximumCellCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "MaximumCellCharacters must be positive.");
        }
    }

    private static void ValidateExportOptions(
        DelimitedTextExportOptions options)
    {
        ValidateCharacters(options.Delimiter, options.Quote);
        ArgumentNullException.ThrowIfNull(options.Culture);
        if (string.IsNullOrEmpty(options.NewLine) ||
            options.NewLine.Any(character =>
                character is not '\r' and not '\n'))
        {
            throw new ArgumentException(
                "NewLine must contain only carriage-return and line-feed characters.",
                nameof(options));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(options.DateTimeFormat);
    }

    private static void ValidateCharacters(char delimiter, char quote)
    {
        if (delimiter == quote ||
            delimiter is '\r' or '\n' or '\0' ||
            quote is '\r' or '\n' or '\0')
        {
            throw new ArgumentException(
                "Delimiter and quote must be distinct printable characters.");
        }
    }

    private readonly record struct DelimitedTextField(
        string Text,
        bool WasQuoted);

    private sealed class DelimitedTextParser
    {
        private readonly TextReader _reader;
        private readonly DelimitedTextImportOptions _options;

        public DelimitedTextParser(
            TextReader reader,
            DelimitedTextImportOptions options)
        {
            _reader = reader;
            _options = options;
        }

        public async IAsyncEnumerable<IReadOnlyList<DelimitedTextField>>
            ReadRowsAsync(
                [System.Runtime.CompilerServices.EnumeratorCancellation]
                CancellationToken cancellationToken)
        {
            var row = new List<DelimitedTextField>();
            var field = new StringBuilder();
            var inQuotes = false;
            var quotePending = false;
            var quotedFieldClosed = false;
            var wasQuoted = false;
            var fieldStarted = false;
            var pendingCarriageReturn = false;
            var buffer = new char[8192];

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = await _reader.ReadAsync(
                    buffer.AsMemory(),
                    cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                for (var index = 0; index < read; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var current = buffer[index];
                    if (pendingCarriageReturn)
                    {
                        pendingCarriageReturn = false;
                        if (current == '\n')
                        {
                            continue;
                        }
                    }

                    if (inQuotes)
                    {
                        if (!quotePending)
                        {
                            if (current == _options.Quote)
                            {
                                quotePending = true;
                            }
                            else
                            {
                                Append(field, current);
                            }
                            continue;
                        }

                        if (current == _options.Quote)
                        {
                            Append(field, current);
                            quotePending = false;
                            continue;
                        }

                        inQuotes = false;
                        quotePending = false;
                        quotedFieldClosed = true;
                    }

                    if (current == _options.Delimiter)
                    {
                        AddField(
                            row,
                            field,
                            ref wasQuoted,
                            ref fieldStarted,
                            ref quotedFieldClosed);
                        continue;
                    }
                    if (current is '\r' or '\n')
                    {
                        AddField(
                            row,
                            field,
                            ref wasQuoted,
                            ref fieldStarted,
                            ref quotedFieldClosed);
                        yield return row.ToArray();
                        row.Clear();
                        if (current == '\r')
                        {
                            pendingCarriageReturn = true;
                        }
                        continue;
                    }
                    if (quotedFieldClosed)
                    {
                        if (!char.IsWhiteSpace(current))
                        {
                            throw new InvalidDataException(
                                "Unexpected content follows a quoted field.");
                        }
                        continue;
                    }
                    if (current == _options.Quote && !fieldStarted)
                    {
                        inQuotes = true;
                        wasQuoted = true;
                        fieldStarted = true;
                        continue;
                    }

                    Append(field, current);
                    fieldStarted = true;
                }
            }

            if (inQuotes)
            {
                if (quotePending)
                {
                    inQuotes = false;
                    quotePending = false;
                    quotedFieldClosed = true;
                }
                else
                {
                    throw new InvalidDataException(
                        "Delimited text ends inside a quoted field.");
                }
            }
            if (field.Length > 0 ||
                fieldStarted ||
                wasQuoted ||
                quotedFieldClosed ||
                row.Count > 0)
            {
                AddField(
                    row,
                    field,
                    ref wasQuoted,
                    ref fieldStarted,
                    ref quotedFieldClosed);
                yield return row.ToArray();
            }
        }

        private void Append(StringBuilder field, char value)
        {
            if (field.Length >= _options.MaximumCellCharacters)
            {
                throw new InvalidDataException(
                    $"A delimited-text field exceeds the character limit of " +
                    $"{_options.MaximumCellCharacters:N0}.");
            }
            field.Append(value);
        }

        private static void AddField(
            ICollection<DelimitedTextField> row,
            StringBuilder field,
            ref bool wasQuoted,
            ref bool fieldStarted,
            ref bool quotedFieldClosed)
        {
            row.Add(new DelimitedTextField(
                field.ToString(),
                wasQuoted));
            field.Clear();
            wasQuoted = false;
            fieldStarted = false;
            quotedFieldClosed = false;
        }
    }
}
