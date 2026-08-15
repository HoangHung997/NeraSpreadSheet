using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.OpenXml;
using NeraSpreadsheetControl = NeraSpreadSheet.WinForms.NeraSpreadsheetControl;

namespace NeraSpreadSheet.WinForms.Sample;

public sealed class MainForm : Form
{
    private readonly NeraOpenXmlWorkbookSerializer _serializer = new();
    private readonly NeraSpreadsheetControl _spreadsheet = new() { Dock = DockStyle.Fill };

    public MainForm()
    {
        Text = "NeraSpreadSheet WinForms Sample";
        Width = 1200;
        Height = 760;
        StartPosition = FormStartPosition.CenterScreen;

        var toolbar = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };
        toolbar.Items.Add(CreateButton("Open XLSX", OpenClick));
        toolbar.Items.Add(CreateButton("Save XLSX", SaveClick));
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(CreateButton("Bold", BoldClick));
        toolbar.Items.Add(CreateButton("Italic", ItalicClick));
        toolbar.Items.Add(CreateButton("Merge", MergeClick));
        toolbar.Items.Add(CreateButton("Unmerge", UnmergeClick));
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(new ToolStripLabel("F2/double-click = edit · Ctrl+C/X/V · Ctrl+B/I · wheel/Shift+wheel"));

        Controls.Add(_spreadsheet);
        Controls.Add(toolbar);
        _spreadsheet.Session = CreateSampleSession();
    }

    private static ToolStripButton CreateButton(string text, EventHandler handler)
    {
        var button = new ToolStripButton(text) { DisplayStyle = ToolStripItemDisplayStyle.Text };
        button.Click += handler;
        return button;
    }

    private async void OpenClick(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Excel workbook (*.xlsx)|*.xlsx|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await using var stream = File.OpenRead(dialog.FileName);
        var workbook = await _serializer.LoadAsync(stream, new OpenXmlImportOptions());
        _spreadsheet.Session = new SpreadsheetSession(workbook);
    }

    private async void SaveClick(object? sender, EventArgs e)
    {
        if (_spreadsheet.Session is not { } session)
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "Excel workbook (*.xlsx)|*.xlsx",
            AddExtension = true,
            DefaultExt = "xlsx",
            FileName = "NeraSpreadSheet.xlsx",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await using var stream = File.Create(dialog.FileName);
        await _serializer.SaveAsync(session.Workbook, stream, new OpenXmlExportOptions());
    }

    private void BoldClick(object? sender, EventArgs e) => _spreadsheet.Session?.Styles.ToggleBold();
    private void ItalicClick(object? sender, EventArgs e) => _spreadsheet.Session?.Styles.ToggleItalic();
    private void MergeClick(object? sender, EventArgs e) => _spreadsheet.Session?.Merge.MergeSelection();
    private void UnmergeClick(object? sender, EventArgs e) => _spreadsheet.Session?.Merge.UnmergeActiveCell();

    private static SpreadsheetSession CreateSampleSession()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(0, 0), "NeraSpreadSheet — native spreadsheet SDK");
        sheet.MergeCells(new CellRange(new CellAddress(0, 0), new CellAddress(0, 4)));
        sheet.Dimensions.SetRowHeight(0, 34d);
        sheet.Dimensions.SetColumnWidth(0, 180d);
        for (var column = 1; column <= 4; column++)
        {
            sheet.Dimensions.SetColumnWidth(column, 120d);
        }

        sheet.SetValue(new CellAddress(2, 0), "Item");
        sheet.SetValue(new CellAddress(2, 1), "Quantity");
        sheet.SetValue(new CellAddress(2, 2), "Unit price");
        sheet.SetValue(new CellAddress(2, 3), "Amount");
        sheet.SetValue(new CellAddress(3, 0), "Material A");
        sheet.SetValue(new CellAddress(3, 1), 12d);
        sheet.SetValue(new CellAddress(3, 2), 25.5d);
        sheet.SetFormula(new CellAddress(3, 3), "=B4*C4");
        sheet.SetValue(new CellAddress(4, 0), "Material B");
        sheet.SetValue(new CellAddress(4, 1), 8d);
        sheet.SetValue(new CellAddress(4, 2), 40d);
        sheet.SetFormula(new CellAddress(4, 3), "=B5*C5");
        sheet.SetValue(new CellAddress(6, 2), "Total");
        sheet.SetFormula(new CellAddress(6, 3), "=SUM(D4:D5)");

        var titleStyleId = workbook.Styles.Intern(CellStyle.Default with
        {
            Font = CellStyle.Default.Font with { Size = 18d, Weight = 700, Color = new ColorRgba(25, 70, 130) },
            Fill = new CellFillStyle { IsVisible = true, Color = new ColorRgba(225, 236, 250) },
        });
        sheet.SetStyle(new CellAddress(0, 0), titleStyleId);

        var headerStyleId = workbook.Styles.Intern(CellStyle.Default with
        {
            Font = CellStyle.Default.Font with { Weight = 700 },
            Fill = new CellFillStyle { IsVisible = true, Color = new ColorRgba(235, 235, 235) },
        });
        for (var column = 0; column <= 3; column++)
        {
            sheet.SetStyle(new CellAddress(2, column), headerStyleId);
        }

        var session = new SpreadsheetSession(workbook);
        session.Recalculate();
        return session;
    }
}
