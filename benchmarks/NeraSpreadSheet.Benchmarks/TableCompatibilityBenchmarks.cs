using BenchmarkDotNet.Attributes;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Benchmarks;

/// <summary>Headless small-Table work under unrelated sparse-sheet occupancy.</summary>
[MemoryDiagnoser]
public class TableCompatibilityBenchmarks
{
    private SpreadsheetSession _session = null!;
    private Guid _tableId;

    [Params(0, 100_000)]
    public int UnrelatedCells { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        for (var row = 0; row < UnrelatedCells; row++)
            sheet.SetValue(new CellAddress(row, 100), row);
        var table = new SpreadsheetTable(Guid.NewGuid(), "Sales", new CellRange(default, new CellAddress(10, 1)),
            [new SpreadsheetTableColumn(Guid.NewGuid(), "Item"), new SpreadsheetTableColumn(Guid.NewGuid(), "Amount")]);
        sheet.AddTable(table);
        _tableId = table.Id;
        _session = new SpreadsheetSession(workbook);
    }

    [Benchmark]
    public void FilterButtonToggleAndUndo()
    {
        _session.Tables.SetFilterButtons(_tableId, false);
        _session.Undo();
    }

    [Benchmark]
    public int ColumnCompletion() => SpreadsheetFormulaEditingAssistant.GetStructuredReferenceSuggestions(
        "=Sales[Am", 9, _session.Workbook, _session.ActiveWorksheet, new CellAddress(1, 0)).Count;
}
