using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.OpenXml;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Wpf.Sample;

public sealed partial class RibbonPreviewWindow
{
    private void RegisterPreviewCommands()
    {
        var captions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Edit.Undo"] = "Hoàn tác", ["Edit.Redo"] = "Làm lại", ["Edit.Copy"] = "Sao chép",
            ["Edit.Cut"] = "Cắt", ["Edit.Paste"] = "Dán", ["Cell.ClearContents"] = "Xóa nội dung",
            ["Cell.Format.Bold"] = "Đậm", ["Cell.Format.Italic"] = "Nghiêng", ["Cell.Merge"] = "Gộp ô",
            ["Cell.Unmerge"] = "Tách ô", ["Formula.RecalculateWorkbook"] = "Tính lại workbook",
            ["Data.SortAscending"] = "Sắp xếp tăng", ["Data.SortDescending"] = "Sắp xếp giảm",
            ["Structure.Row.Insert"] = "Chèn hàng", ["Structure.Row.Delete"] = "Xóa hàng",
            ["Structure.Column.Insert"] = "Chèn cột", ["Structure.Column.Delete"] = "Xóa cột",
            ["Structure.Row.Hide"] = "Ẩn hàng", ["Structure.Row.Unhide"] = "Hiện hàng",
            ["Structure.Column.Hide"] = "Ẩn cột", ["Structure.Column.Unhide"] = "Hiện cột",
            ["View.FreezePanes"] = "Cố định khung", ["View.UnfreezePanes"] = "Bỏ cố định",
            ["View.Split.Undo"] = "Hoàn tác chia khung", ["View.Split.Redo"] = "Làm lại chia khung",
            ["Insert.Chart.Column"] = "Biểu đồ cột", ["Insert.Chart.Bar"] = "Biểu đồ thanh",
            ["Insert.Chart.Line"] = "Biểu đồ đường", ["Insert.Chart.Pie"] = "Biểu đồ tròn",
            ["Insert.Pivot.Sum"] = "Bảng tổng hợp",
        };
        foreach (var id in _session.Commands.RegisteredCommandIds)
        {
            if (!_session.Commands.TryResolve(id, out var descriptor, out var handler) || descriptor is null || handler is null)
                throw new InvalidOperationException($"Missing session command {id}.");
            _commands.Register(new CommandDescriptor(id, captions.GetValueOrDefault(id.Value, descriptor.Caption),
                captions.GetValueOrDefault(id.Value, descriptor.Tooltip ?? descriptor.Caption), descriptor.IconKey, descriptor.Shortcut),
                id.Value is "Edit.Undo" or "Edit.Redo" ? new LocalizedHistoryHandler(handler) : handler);
        }
        Add("Sample.Font", "Phông chữ", "font.family", value => ApplyStyle(s => s with { Font = s.Font with { Family = value! } }),
            () => ChoiceState(_session.Styles.ActiveCellStyle.Font.Family, "Segoe UI", "Arial", "Calibri", "Times New Roman"));
        Add("Sample.FontSize", "Cỡ chữ", "font.size", value => ApplyStyle(s => s with { Font = s.Font with { Size = ParseNumber(value!) } }),
            () => ChoiceState(_session.Styles.ActiveCellStyle.Font.Size.ToString(CultureInfo.InvariantCulture), "10", "11", "12", "14", "16", "18", "24"));
        Add("Sample.Underline", "Gạch chân", "font.underline", _ => ApplyStyle(s => s with { Font = s.Font with { Underline = !s.Font.Underline } }),
            () => new CommandState(true, _session.Styles.ActiveCellStyle.Font.Underline));
        Add("Sample.Fill", "Màu nền", "fill.color", value => _session.Styles.SetFill(ParseColor(value!)), () => ColorState(_session.Styles.ActiveCellStyle.Fill.Color));
        Add("Sample.FontColor", "Màu chữ", "font.color", value => _session.Styles.SetFontColor(ParseColor(value!)), () => ColorState(_session.Styles.ActiveCellStyle.Font.Color));
        foreach (var alignment in new[] { CellHorizontalAlignment.Left, CellHorizontalAlignment.Center, CellHorizontalAlignment.Right })
        {
            var caption = alignment switch { CellHorizontalAlignment.Left => "Căn trái", CellHorizontalAlignment.Center => "Căn giữa", _ => "Căn phải" };
            Add($"Sample.Align.{alignment}", caption, $"align.{alignment.ToString().ToLowerInvariant()}",
                _ => ApplyStyle(s => s with { Alignment = s.Alignment with { Horizontal = alignment } }),
                () => new CommandState(true, _session.Styles.ActiveCellStyle.Alignment.Horizontal == alignment));
        }
        Add("Sample.Wrap", "Ngắt dòng", "align.wrap", _ => ApplyStyle(s => s with { Alignment = s.Alignment with { WrapText = !s.Alignment.WrapText } }),
            () => new CommandState(true, _session.Styles.ActiveCellStyle.Alignment.WrapText));
        Add("Sample.Number", "Định dạng số", "number.format", value => _session.Styles.SetNumberFormat(value!),
            () => new CommandState(true, null, null, _session.Styles.ActiveCellStyle.NumberFormat.FormatCode,
                [new("General", "Chung"), new("#,##0", "Số nguyên"), new("#,##0.00", "Hai số thập phân"), new("0%", "Phần trăm"), new("dd/mm/yyyy", "Ngày tháng")]));
        Add("Sample.Percent", "Phần trăm", "number.percent", _ => _session.Styles.SetNumberFormat("0%"));
        Add("Sample.Decimal", "Hai số thập phân", "number.decimal-increase", _ => _session.Styles.SetNumberFormat("#,##0.00"));
        Add("Sample.Borders", "Đường viền", "border.all", _ => _session.Styles.SetAllBorders(CellBorderLineStyle.Thin, new ColorRgba(105, 120, 132)));
        Add("Sample.Filter", "Mở bộ lọc", "data.filter", _ =>
        {
            _filterPopup ??= new NeraAutoFilterPagedPopupPresenter(_sheet);
            if (!_filterPopup.TryOpenForActiveCell()) SetStatus("Chọn một ô trong bảng có hàng tiêu đề để mở bộ lọc.");
        });
        Add("Sample.FilterClear", "Xóa bộ lọc", "data.filter-clear", _ =>
        {
            if (CurrentTable is { } table) _session.Tables.ClearAutoFilter(table.Id);
            else _session.WorksheetFilter.ClearCriteria();
        }, () => new CommandState(_session.TryResolveActiveAutoFilterTarget(out _)));
        Add("Sample.FilterReapply", "Áp dụng lại", "data.filter-reapply", _ =>
        {
            if (_session.TryResolveActiveAutoFilterTarget(out var target)) _session.Sort.ReapplyAutoFilter(target);
        }, () => new CommandState(_session.TryResolveActiveAutoFilterTarget(out _)));
        Add("Sample.FormulaHelp", "Trợ giúp hàm", "formula.insert", _ => ShowFormulaHelp());
        Add("Sample.FormulaSum", "Chèn hàm SUM", "formula.autosum", _ => _sheet.BeginEdit("=SUM("));
        Add("Sample.FormulaAverage", "Chèn AVERAGE", "formula.statistical", _ => _sheet.BeginEdit("=AVERAGE("));
        Add("Sample.FormulaIf", "Chèn hàm IF", "formula.logical", _ => _sheet.BeginEdit("=IF("));
        Add("Sample.FormulaLookup", "Chèn XLOOKUP", "formula.lookup", _ => _sheet.BeginEdit("=XLOOKUP("));
        Add("Sample.Orientation", "Hướng giấy", "page.orientation", value => SetPageSetup(setup => setup with
            { Orientation = value == "landscape" ? SpreadsheetPageOrientation.Landscape : SpreadsheetPageOrientation.Portrait }),
            () => new CommandState(true, null, null, _session.ActiveWorksheet.GetPrintSettings().PageSetup.Orientation == SpreadsheetPageOrientation.Landscape ? "landscape" : "portrait",
                [new("portrait", "Dọc"), new("landscape", "Ngang")]));
        Add("Sample.Paper", "Khổ giấy", "page.size", value => SetPageSetup(setup => setup with { PaperSize = value == "A3" ? SpreadsheetPaperSize.A3 : SpreadsheetPaperSize.A4 }),
            () => ChoiceState(_session.ActiveWorksheet.GetPrintSettings().PageSetup.PaperSize.Name, "A4", "A3"));
        Add("Sample.Margins", "Lề trang", "page.margins", value => SetPageSetup(setup => setup with { Margins = value == "narrow" ? SpreadsheetPageMargins.Narrow : SpreadsheetPageMargins.Normal }),
            () => new CommandState(true, null, null, _session.ActiveWorksheet.GetPrintSettings().PageSetup.Margins == SpreadsheetPageMargins.Narrow ? "narrow" : "normal", [new("normal", "Bình thường"), new("narrow", "Hẹp")]));
        Add("Sample.PrintGrid", "In đường lưới", "page.gridlines", _ => SetPageSetup(setup => setup with { PrintGridlines = !setup.PrintGridlines }),
            () => new CommandState(true, _session.ActiveWorksheet.GetPrintSettings().PageSetup.PrintGridlines));
        Add("Sample.PrintHeadings", "In tiêu đề", "page.headings", _ => SetPageSetup(setup => setup with { PrintHeadings = !setup.PrintHeadings }),
            () => new CommandState(true, _session.ActiveWorksheet.GetPrintSettings().PageSetup.PrintHeadings));
        Add("Sample.PrintPreview", "Xem trước in", "file.print", _ => ShowPrintPreview());
        Add("Sample.Statistics", "Thống kê workbook", "review.statistics", _ => ShowStatistics());
        Add("Sample.Errors", "Kiểm tra lỗi ô", "formula.error-check", _ => ShowCellErrors());
        Add("Sample.Gridlines", "Đường lưới", "view.gridlines", _ =>
        {
            _showGridlines = !_showGridlines;
            _sheet.RenderTheme = _sheet.RenderTheme with { GridLine = _showGridlines ? ColorRgba.GridLine : new ColorRgba(0, 0, 0, 0) };
            _sheet.InvalidateVisual();
        }, () => new CommandState(true, _showGridlines));
        Add("Sample.Headers", "Tiêu đề hàng cột", "view.show", _ => { _sheet.RenderTheme = _sheet.RenderTheme with { ShowHeaders = !_sheet.RenderTheme.ShowHeaders }; _sheet.InvalidateVisual(); },
            () => new CommandState(true, _sheet.RenderTheme.ShowHeaders));
        Add("Sample.Zoom", "Thu phóng", "view.zoom", value => _sheet.Zoom = ParseNumber(value!) / 100,
            () => ChoiceState((_sheet.Zoom * 100).ToString(CultureInfo.InvariantCulture), "75", "100", "125", "150", "200"));
        Add("Sample.ZoomReset", "100%", "view.zoom-100", _ => _sheet.Zoom = 1);
        Add("Sample.New", "Cửa sổ mới", "file.new", _ => new RibbonPreviewWindow().Show());
        _commands.Register(new CommandDescriptor("Sample.Open", "Mở workbook", iconKey: "file.open"), new AsyncPreviewHandler(OpenWorkbookAsync));
        _commands.Register(new CommandDescriptor("Sample.Save", "Lưu bản sao", iconKey: "file.save-as"), new AsyncPreviewHandler(SaveWorkbookAsync));
    }

    private SpreadsheetTable? CurrentTable => _session.ActiveWorksheet.Tables.FirstOrDefault(table => table.Range.Contains(_session.Selection.ActiveCell));
    private void ApplyStyle(Func<CellStyle, CellStyle> transform) => _session.Styles.ApplyToSelection(transform, "Định dạng từ Ribbon");
    private static CommandState ChoiceState(string selected, params string[] values) => new(true, null, null, selected, values.Select(value => new CommandItem(value, value)));
    private static CommandState ColorState(ColorRgba selected) => new(true, null, null,
        selected.Alpha == 0 ? "#00000000" : $"#{selected.Red:X2}{selected.Green:X2}{selected.Blue:X2}",
        [new("#00000000", "Không màu"), new("#217346", "Xanh lá"), new("#156082", "Xanh lam"), new("#FFC000", "Vàng"), new("#C00000", "Đỏ"), new("#FFFFFF", "Trắng"), new("#000000", "Đen")]);
    private static ColorRgba ParseColor(string text)
    {
        var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(text);
        return new ColorRgba(color.R, color.G, color.B, color.A);
    }

    private void Add(string id, string caption, string icon, Action<string?> action, Func<CommandState>? state = null) =>
        _commands.Register(new CommandDescriptor(id, caption, caption, icon), new PreviewHandler(action, state));

    private void SetPageSetup(Func<SpreadsheetPageSetup, SpreadsheetPageSetup> change)
    {
        var sheet = _session.ActiveWorksheet;
        var before = sheet.GetPrintSettings();
        var after = before with { PageSetup = change(before.PageSetup) };
        _session.Execute(new PrintSettingsOperation(sheet, before, after));
        SetStatus("Đã cập nhật thiết lập in · có thể Hoàn tác");
    }

    private void ShowPrintPreview()
    {
        var snapshot = WorksheetSnapshot.Capture(_session.ActiveWorksheet);
        var settings = _session.ActiveWorksheet.GetPrintSettings();
        var plan = SpreadsheetPageLayoutPlanner.CreatePlan(snapshot, settings.PrintArea ?? new CellRange(default, new CellAddress(32, 4)), settings.PageSetup);
        new Window { Owner = this, Title = "Xem trước in", Width = 900, Height = 700,
            Content = new NeraPrintPreviewControl { Session = new SpreadsheetPrintPreviewSession(snapshot, plan, _session.Workbook.Styles) } }.Show();
    }

    private void ShowFormulaHelp()
    {
        var help = _session.FormulaEditing.GetFunctionHelp("=SUM(", 5)?.Function;
        MessageBox.Show(this, help is null ? "Nhập dấu = trong ô để mở gợi ý hàm và đối số." :
            $"{help.Signature}\n\n{help.Description}\n\n{string.Join(Environment.NewLine, help.Arguments.Select(argument => $"{argument.Name}: {argument.Description}"))}", "Trợ giúp công thức");
    }
    private void ShowStatistics() => MessageBox.Show(this,
        $"Trang tính: {_session.Workbook.Worksheets.Count}\nÔ có dữ liệu: {_session.ActiveWorksheet.EnumerateUsedCells().Count()}\nBảng: {_session.ActiveWorksheet.Tables.Count}", "Thống kê workbook");
    private void ShowCellErrors() => MessageBox.Show(this,
        string.Join(Environment.NewLine, _session.ActiveWorksheet.EnumerateUsedCells().Where(pair => pair.Value.Value.Kind == CellValueKind.Error).Take(100).Select(pair => $"{pair.Key}: {pair.Value.Value}")) is { Length: > 0 } errors ? errors : "Không có ô lỗi trong trang tính.", "Kiểm tra lỗi ô");
    private async Task OpenWorkbookAsync()
    {
        var dialog = new OpenFileDialog { Filter = "Workbook Excel (*.xlsx)|*.xlsx", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        await using var stream = File.OpenRead(dialog.FileName);
        var loaded = await new NeraOpenXmlSpreadsheetSessionSerializer().LoadSessionAsync(stream, new OpenXmlImportOptions()).ConfigureAwait(false);
        await Dispatcher.InvokeAsync(() =>
        {
            var window = new Window { Owner = this, Title = "Workbook đã mở", Width = 1100, Height = 720 };
            var spreadsheet = new NeraSpreadsheetControl { Session = loaded };
            window.Content = new System.Windows.Documents.AdornerDecorator { Child = spreadsheet };
            window.Closed += (_, _) => spreadsheet.Dispose();
            window.Show();
        });
    }

    private async Task SaveWorkbookAsync()
    {
        var dialog = new SaveFileDialog { Filter = "Workbook Excel (*.xlsx)|*.xlsx", FileName = "Nera-Ribbon.xlsx", AddExtension = true };
        if (dialog.ShowDialog(this) != true) return;
        var destination = Path.GetFullPath(dialog.FileName);
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, Path.GetRandomFileName());
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await new NeraOpenXmlSpreadsheetSessionSerializer().SaveSessionAsync(_session, stream, new OpenXmlExportOptions()).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
            }
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private sealed class PreviewHandler(Action<string?> action, Func<CommandState>? state) : IStatefulCommandHandler
    {
        public bool CanExecute(CommandContext context) => GetState(context).IsEnabled;
        public CommandState GetState(CommandContext context) => state?.Invoke() ?? CommandState.Enabled;
        public ValueTask ExecuteAsync(CommandContext context)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            action((context.Parameter as RibbonItemActivation)?.SelectedValue);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class PrintSettingsOperation(Worksheet worksheet, WorksheetPrintSettings before, WorksheetPrintSettings after) : ISpreadsheetEditOperation
    {
        public Worksheet Worksheet => worksheet;
        public CellRange AffectedRange => new(default, default);
        public string Description => "Thiết lập trang in";
        public bool AffectsCalculation => false;
        public void Execute() => worksheet.SetPrintSettings(after);
        public void Undo() => worksheet.SetPrintSettings(before);
    }

    private sealed class AsyncPreviewHandler(Func<Task> execute) : ICommandHandler
    {
        public bool CanExecute(CommandContext context) => true;
        public async ValueTask ExecuteAsync(CommandContext context)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            await execute().ConfigureAwait(true);
        }
    }

    private sealed class LocalizedHistoryHandler(ICommandHandler inner) : IStatefulCommandHandler
    {
        public bool CanExecute(CommandContext context) => inner.CanExecute(context);
        public ValueTask ExecuteAsync(CommandContext context) => inner.ExecuteAsync(context);
        public CommandState GetState(CommandContext context)
        {
            var state = inner is IStatefulCommandHandler stateful ? stateful.GetState(context) : new CommandState(inner.CanExecute(context));
            return new CommandState(state.IsEnabled, state.IsChecked, null, state.SelectedValue, state.ItemsSource);
        }
    }
}
