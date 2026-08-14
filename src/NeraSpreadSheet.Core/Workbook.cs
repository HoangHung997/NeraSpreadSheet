namespace NeraSpreadSheet.Core;

public sealed class Workbook
{
    private static readonly char[] InvalidWorksheetNameCharacters = ['[', ']', ':', '*', '?', '/', '\\'];
    private readonly List<Worksheet> _worksheets = [];

    public Workbook()
        : this(createDefaultWorksheet: true)
    {
    }

    public Workbook(bool createDefaultWorksheet)
    {
        if (createDefaultWorksheet)
        {
            _worksheets.Add(new Worksheet("Sheet1"));
        }
    }

    public IReadOnlyList<Worksheet> Worksheets => _worksheets;

    public long Version { get; private set; }

    public Worksheet AddWorksheet(string? requestedName = null)
    {
        var name = requestedName is null ? GenerateUniqueName("Sheet") : ValidateWorksheetName(requestedName);

        if (_worksheets.Any(sheet => string.Equals(sheet.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"A worksheet named '{name}' already exists.");
        }

        var worksheet = new Worksheet(name);
        _worksheets.Add(worksheet);
        Version++;
        return worksheet;
    }

    public void RenameWorksheet(Worksheet worksheet, string newName)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        var validated = ValidateWorksheetName(newName);

        if (!_worksheets.Contains(worksheet))
        {
            throw new InvalidOperationException("Worksheet does not belong to this workbook.");
        }

        if (_worksheets.Any(sheet =>
                !ReferenceEquals(sheet, worksheet) &&
                string.Equals(sheet.Name, validated, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"A worksheet named '{validated}' already exists.");
        }

        if (string.Equals(worksheet.Name, validated, StringComparison.Ordinal))
        {
            return;
        }

        worksheet.Name = validated;
        Version++;
    }

    public void RemoveWorksheet(Worksheet worksheet)
    {
        ArgumentNullException.ThrowIfNull(worksheet);

        if (_worksheets.Count <= 1)
        {
            throw new InvalidOperationException("A workbook must contain at least one worksheet.");
        }

        if (!_worksheets.Remove(worksheet))
        {
            throw new InvalidOperationException("Worksheet does not belong to this workbook.");
        }

        Version++;
    }

    public Worksheet GetWorksheet(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _worksheets.FirstOrDefault(sheet =>
                   string.Equals(sheet.Name, name, StringComparison.OrdinalIgnoreCase))
               ?? throw new KeyNotFoundException($"Worksheet '{name}' was not found.");
    }

    private string GenerateUniqueName(string prefix)
    {
        for (var index = 1; ; index++)
        {
            var candidate = $"{prefix}{index}";
            if (_worksheets.All(sheet =>
                    !string.Equals(sheet.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }
        }
    }

    private static string ValidateWorksheetName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalized = name.Trim();

        if (normalized.Length > SpreadsheetLimits.MaxWorksheetNameLength)
        {
            throw new ArgumentException(
                $"Worksheet names cannot exceed {SpreadsheetLimits.MaxWorksheetNameLength} characters.",
                nameof(name));
        }

        if (normalized.IndexOfAny(InvalidWorksheetNameCharacters) >= 0)
        {
            throw new ArgumentException("Worksheet name contains an invalid character.", nameof(name));
        }

        if (normalized.StartsWith("'", StringComparison.Ordinal) ||
            normalized.EndsWith("'", StringComparison.Ordinal))
        {
            throw new ArgumentException("Worksheet name cannot start or end with an apostrophe.", nameof(name));
        }

        return normalized;
    }
}
