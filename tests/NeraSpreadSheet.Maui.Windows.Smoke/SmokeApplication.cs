using Microsoft.Maui;
using Microsoft.Maui.Controls;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Maui;

namespace NeraSpreadSheet.Maui.Windows.Smoke;

public sealed class SmokeApplication : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        ValidateTableFilterPresenter();
        return new Window(new SmokePage())
        {
            Title = "NeraSpreadSheet MAUI GPU smoke",
            Width = 960d,
            Height = 640d,
        };
    }

    private static void ValidateTableFilterPresenter()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var statusColumnId = Guid.NewGuid();
        var amountColumnId = Guid.NewGuid();
        var table = new SpreadsheetTable(
            Guid.NewGuid(),
            "SmokeSales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(2, 1)),
            [
                new SpreadsheetTableColumn(statusColumnId, "Status"),
                new SpreadsheetTableColumn(amountColumnId, "Amount"),
            ]);
        worksheet.SetValue(new CellAddress(0, 0), "Status");
        worksheet.SetValue(new CellAddress(0, 1), "Amount");
        worksheet.SetValue(new CellAddress(1, 0), "Open");
        worksheet.SetValue(new CellAddress(1, 1), 10d);
        worksheet.SetValue(new CellAddress(2, 0), "Closed");
        worksheet.SetValue(new CellAddress(2, 1), 20d);
        worksheet.AddTable(table);

        using var host = new NeraSpreadsheetTableHost
        {
            Workbook = workbook,
        };
        if (host.Session is null ||
            !ReferenceEquals(host.Spreadsheet.Session, host.Session))
        {
            throw new InvalidOperationException(
                "The MAUI Table host did not retain the spreadsheet session.");
        }
        if (!host.TryOpenFilter(table.Id, statusColumnId) ||
            !host.IsFilterSheetOpen)
        {
            throw new InvalidOperationException(
                "The MAUI Table filter bottom sheet did not open.");
        }
        host.CloseFilterSheet();
        if (host.IsFilterSheetOpen)
        {
            throw new InvalidOperationException(
                "The MAUI Table filter bottom sheet did not close.");
        }
    }
}
