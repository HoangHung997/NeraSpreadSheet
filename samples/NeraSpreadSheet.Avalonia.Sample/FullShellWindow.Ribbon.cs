using System.Globalization;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Iconography;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Avalonia.Sample;

public sealed partial class FullShellWindow
{
    private static readonly CommandItem[] FontChoices = [new("Segoe UI", "Segoe UI"), new("Inter", "Inter"), new("Arial", "Arial"), new("Calibri", "Calibri"), new("Times New Roman", "Times New Roman")];
    private static readonly CommandItem[] SizeChoices = [new("10", "10"), new("11", "11"), new("12", "12"), new("14", "14"), new("16", "16"), new("18", "18"), new("24", "24")];
    private static readonly CommandItem[] NumberChoices = [new("General", "Chung"), new("#,##0", "Số nguyên"), new("#,##0.00", "Hai số thập phân"), new("0%", "Phần trăm"), new("dd/mm/yyyy", "Ngày tháng")];
    private static readonly CommandItem[] ColorChoices = [new("none", "Không màu"), new("#217346", "Xanh lá"), new("#156082", "Xanh lam"), new("#FFC000", "Vàng"), new("#C00000", "Đỏ"), new("#FFFFFF", "Trắng"), new("#000000", "Đen")];
    private static readonly CommandItem[] BorderChoices = [new("all", "Tất cả đường viền", iconKey: "border.all"), new("none", "Không viền", iconKey: "border.none"), new("bottom", "Viền dưới", iconKey: "border.bottom")];
    private static readonly CommandItem[] OrientationChoices = [new("portrait", "Dọc"), new("landscape", "Ngang")];
    private static readonly CommandItem[] PaperChoices = [new("A4", "A4"), new("A3", "A3"), new("Letter", "Letter")];
    private static readonly CommandItem[] MarginChoices = [new("normal", "Bình thường"), new("narrow", "Hẹp")];
    private static readonly CommandItem[] ZoomChoices = [new("75", "75%"), new("100", "100%"), new("125", "125%"), new("150", "150%"), new("200", "200%")];
    private static readonly CommandItem[] ThemeChoices = [new("Light", "Sáng"), new("Dark", "Tối"), new("HighContrastLight", "Tương phản sáng"), new("HighContrastDark", "Tương phản tối")];
    private static readonly (string Id, string Caption)[] ForwardedCommands =
    [
        ("Cell.Merge", "Gộp ô"), ("Cell.Unmerge", "Tách ô"),
        ("Structure.Row.Insert", "Chèn hàng"), ("Structure.Row.Delete", "Xóa hàng"),
        ("Structure.Row.Hide", "Ẩn hàng"), ("Structure.Row.Unhide", "Hiện hàng"),
        ("Structure.Column.Insert", "Chèn cột"), ("Structure.Column.Delete", "Xóa cột"),
        ("Structure.Column.Hide", "Ẩn cột"), ("Structure.Column.Unhide", "Hiện cột"),
        ("Data.SortAscending", "Sắp xếp tăng"), ("Data.SortDescending", "Sắp xếp giảm"),
        ("Insert.Chart.Column", "Biểu đồ cột"), ("Insert.Chart.Bar", "Biểu đồ thanh"),
        ("Insert.Chart.Line", "Biểu đồ đường"), ("Insert.Chart.Pie", "Biểu đồ tròn"),
    ];
    private bool _showGridlines = true;

    private void RegisterCommands()
    {
        AddAsync("Shell.Open", "Mở XLSX", OpenAsync, "Ctrl+O", "file.open");
        AddAsync("Shell.Save", "Lưu XLSX", SaveAsync, "Ctrl+S", "file.save");
        Add("Edit.Undo", "Hoàn tác", () => Session.Undo(), "Ctrl+Z", "edit.undo");
        Add("Edit.Redo", "Làm lại", () => Session.Redo(), "Ctrl+Y", "edit.redo");
        AddAsync("Edit.Copy", "Sao chép", async () => { await _split.ActiveSpreadsheet.CopyToClipboardAsync(); }, "Ctrl+C", "edit.copy");
        AddAsync("Edit.Cut", "Cắt", async () => { await _split.ActiveSpreadsheet.CopyToClipboardAsync(true); }, "Ctrl+X", "edit.cut");
        AddAsync("Edit.Paste", "Dán", async () => { await _split.ActiveSpreadsheet.PasteFromClipboardAsync(); }, "Ctrl+V", "edit.paste");
        Add("Cell.Bold", "Đậm", () => Session.Styles.ToggleBold(), "Ctrl+B", "font.bold", () => new CommandState(true, Session.Styles.ActiveCellStyle.Font.Weight >= 600));
        Add("Cell.Italic", "Nghiêng", () => Session.Styles.ToggleItalic(), "Ctrl+I", "font.italic", () => new CommandState(true, Session.Styles.ActiveCellStyle.Font.Italic));
        Add("Cell.Clear", "Xóa nội dung", () => Session.ClearSelection(), icon: "cell.clear");
        Add("Formula.Calculate", "Tính lại", () => Session.Recalculate(), "F9", "formula.calculate-now");
        Add("View.Freeze", "Cố định khung", () => Session.View.FreezeAtActiveCell(), icon: "view.freeze-panes");
        Add("View.Unfreeze", "Bỏ cố định", () => Session.View.Unfreeze(), icon: "view.unfreeze-panes");
        Add("View.Split.None", "Bỏ chia", () => _split.SetMode(SpreadsheetSplitViewMode.None), icon: "view.split");
        Add("View.Split.Vertical", "Chia dọc", () => _split.SetMode(SpreadsheetSplitViewMode.Vertical), icon: "view.split");
        Add("View.Split.Horizontal", "Chia ngang", () => _split.SetMode(SpreadsheetSplitViewMode.Horizontal), icon: "view.split");
        Add("View.Split.Both", "Chia bốn", () => _split.SetMode(SpreadsheetSplitViewMode.Both), icon: "view.split");
        Add("View.Split.Undo", "Hoàn tác chia khung", () => _split.UndoSplit(), icon: "edit.undo");
        Add("View.Split.Redo", "Làm lại chia khung", () => _split.RedoSplit(), icon: "edit.redo");
        Add("View.ZoomIn", "Phóng to", () => _split.SetZoom(Math.Min(4, _split.ActiveSpreadsheet.Zoom + 0.1)), icon: "view.zoom");
        Add("View.ZoomOut", "Thu nhỏ", () => _split.SetZoom(Math.Max(0.25, _split.ActiveSpreadsheet.Zoom - 0.1)), icon: "view.zoom");
        Add("View.ZoomReset", "100%", () => _split.SetZoom(1), icon: "view.zoom-100");
        Add("View.Theme", "Đổi sáng/tối", () => SetRibbonTheme(_ribbon.IconTheme == NeraIconTheme.Light ? NeraIconTheme.Dark : NeraIconTheme.Light), icon: "ribbon.customize");

        foreach (var entry in ForwardedCommands)
        {
            if (!Session.Commands.TryResolve(entry.Id, out var descriptor, out _) || descriptor is null)
                throw new InvalidOperationException("The built-in command is missing: " + entry.Id);
            _registry.Register(new CommandDescriptor(entry.Id, entry.Caption, entry.Caption, descriptor.IconKey, descriptor.Shortcut)
                { CaptionResourceKey = entry.Caption, TooltipResourceKey = entry.Caption },
                new ShellHandler(this, context => ResolveSessionHandler(entry.Id).ExecuteAsync(context),
                    context => ResolveSessionHandler(entry.Id) is IStatefulCommandHandler stateful ? stateful.GetState(context) : new CommandState(ResolveSessionHandler(entry.Id).CanExecute(context))));
        }
        Choice("Ui.FontFamily", "Phông chữ", "font.family", value => ApplyStyle(s => s with { Font = s.Font with { Family = value } }),
            () => ChoiceState(Session.Styles.ActiveCellStyle.Font.Family, FontChoices));
        Choice("Ui.FontSize", "Cỡ chữ", "font.size", value => ApplyStyle(s => s with { Font = s.Font with { Size = ParseNumber(value) } }),
            () => ChoiceState(Session.Styles.ActiveCellStyle.Font.Size.ToString(CultureInfo.InvariantCulture), SizeChoices));
        Add("Ui.Underline", "Gạch chân", () => ApplyStyle(s => s with { Font = s.Font with { Underline = !s.Font.Underline } }),
            icon: "font.underline", state: () => new CommandState(true, Session.Styles.ActiveCellStyle.Font.Underline));
        Choice("Ui.Fill", "Màu nền", "fill.color", value => { if (value == "none") Session.Styles.ClearFill(); else Session.Styles.SetFill(ParseColor(value)); },
            () => ChoiceState(Session.Styles.ActiveCellStyle.Fill.IsVisible ? ColorText(Session.Styles.ActiveCellStyle.Fill.Color) : "none", ColorChoices));
        Choice("Ui.FontColor", "Màu chữ", "font.color", value => Session.Styles.SetFontColor(value == "none" ? ColorRgba.Black : ParseColor(value)),
            () => ChoiceState(ColorText(Session.Styles.ActiveCellStyle.Font.Color), ColorChoices));
        Choice("Ui.Borders", "Đường viền", "border.all", ApplyBorder, () => ChoiceState(null, BorderChoices));
        foreach (var alignment in Enum.GetValues<CellHorizontalAlignment>().Where(value => value is CellHorizontalAlignment.Left or CellHorizontalAlignment.Center or CellHorizontalAlignment.Right))
        {
            var caption = alignment switch { CellHorizontalAlignment.Left => "Căn trái", CellHorizontalAlignment.Center => "Căn giữa", _ => "Căn phải" };
            Add("Ui.Align." + alignment, caption, () => ApplyStyle(s => s with { Alignment = s.Alignment with { Horizontal = alignment } }),
                icon: "align." + alignment.ToString().ToLowerInvariant(), state: () => new CommandState(true, Session.Styles.ActiveCellStyle.Alignment.Horizontal == alignment));
        }
        Add("Ui.Wrap", "Ngắt dòng", () => ApplyStyle(s => s with { Alignment = s.Alignment with { WrapText = !s.Alignment.WrapText } }),
            icon: "align.wrap", state: () => new CommandState(true, Session.Styles.ActiveCellStyle.Alignment.WrapText));
        Choice("Ui.Number", "Định dạng số", "number.format", value => Session.Styles.SetNumberFormat(value),
            () => ChoiceState(Session.Styles.ActiveCellStyle.NumberFormat.FormatCode, NumberChoices));
        Add("Ui.Percent", "Phần trăm", () => Session.Styles.SetNumberFormat("0%"), icon: "number.percent");
        Add("Ui.Decimal", "Hai số thập phân", () => Session.Styles.SetNumberFormat("#,##0.00"), icon: "number.decimal-increase");
        Choice("Ui.Orientation", "Hướng giấy", "page.orientation", value => SetPageSetup(setup => setup with
            { Orientation = value == "landscape" ? SpreadsheetPageOrientation.Landscape : SpreadsheetPageOrientation.Portrait }),
            () => ChoiceState(Session.ActiveWorksheet.GetPrintSettings().PageSetup.Orientation == SpreadsheetPageOrientation.Landscape ? "landscape" : "portrait", OrientationChoices));
        Choice("Ui.Paper", "Khổ giấy", "page.size", value => SetPageSetup(setup => setup with
            { PaperSize = value switch { "A3" => SpreadsheetPaperSize.A3, "Letter" => SpreadsheetPaperSize.Letter, _ => SpreadsheetPaperSize.A4 } }),
            () => ChoiceState(Session.ActiveWorksheet.GetPrintSettings().PageSetup.PaperSize.Name, PaperChoices));
        Choice("Ui.Margins", "Lề trang", "page.margins", value => SetPageSetup(setup => setup with
            { Margins = value == "narrow" ? SpreadsheetPageMargins.Narrow : SpreadsheetPageMargins.Normal }),
            () => ChoiceState(Session.ActiveWorksheet.GetPrintSettings().PageSetup.Margins == SpreadsheetPageMargins.Narrow ? "narrow" : "normal", MarginChoices));
        Add("Ui.PrintGrid", "In đường lưới", () => SetPageSetup(setup => setup with { PrintGridlines = !setup.PrintGridlines }),
            icon: "page.gridlines", state: () => new CommandState(true, Session.ActiveWorksheet.GetPrintSettings().PageSetup.PrintGridlines));
        Add("Ui.PrintHeadings", "In tiêu đề", () => SetPageSetup(setup => setup with { PrintHeadings = !setup.PrintHeadings }),
            icon: "page.headings", state: () => new CommandState(true, Session.ActiveWorksheet.GetPrintSettings().PageSetup.PrintHeadings));
        AddAsync("Ui.PrintPreview", "Xem trước in", ShowPrintPreviewAsync, icon: "file.print");
        Add("Ui.FormulaHelp", "Trợ giúp hàm", ShowFormulaHelp, icon: "formula.insert");
        Add("Ui.FormulaSum", "Chèn SUM", () => DraftOwner.BeginEdit("=SUM("), icon: "formula.autosum");
        Add("Ui.FormulaAverage", "Chèn AVERAGE", () => DraftOwner.BeginEdit("=AVERAGE("), icon: "formula.statistical");
        Add("Ui.FormulaIf", "Chèn IF", () => DraftOwner.BeginEdit("=IF("), icon: "formula.logical");
        Add("Ui.FormulaLookup", "Chèn XLOOKUP", () => DraftOwner.BeginEdit("=XLOOKUP("), icon: "formula.lookup");
        Add("Ui.Errors", "Kiểm tra lỗi ô", () => ShowInformation("Kiểm tra lỗi ô",
            string.Join(Environment.NewLine, Session.ActiveWorksheet.EnumerateUsedCells().Where(pair => pair.Value.Value.Kind == CellValueKind.Error)
                .Take(100).Select(pair => $"{pair.Key}: {pair.Value.Value}")) is { Length: > 0 } errors ? errors : "Không có ô lỗi trong trang tính."), icon: "formula.error-check");
        Add("Ui.Statistics", "Thống kê workbook", () => ShowInformation("Thống kê workbook",
            $"Trang tính: {Session.Workbook.Worksheets.Count}\nÔ lưu trữ: {Session.ActiveWorksheet.UsedCellCount}\nBảng: {Session.ActiveWorksheet.TableCount}"), icon: "review.statistics");
        Add("Ui.Gridlines", "Đường lưới", () =>
        {
            _showGridlines = !_showGridlines;
            foreach (var pane in Enum.GetValues<SpreadsheetSplitViewPane>())
            {
                var host = _split.GetPane(pane); host.RenderTheme = host.RenderTheme with { GridLine = _showGridlines ? ColorRgba.GridLine : new ColorRgba(0, 0, 0, 0) };
            }
        }, icon: "view.gridlines", state: () => new CommandState(true, _showGridlines));
        Add("Ui.Headers", "Tiêu đề hàng cột", () =>
        {
            var show = !_split.ActiveSpreadsheet.RenderTheme.ShowHeaders;
            foreach (var pane in Enum.GetValues<SpreadsheetSplitViewPane>()) { var host = _split.GetPane(pane); host.RenderTheme = host.RenderTheme with { ShowHeaders = show }; }
        }, icon: "view.show", state: () => new CommandState(true, _split.ActiveSpreadsheet.RenderTheme.ShowHeaders));
        Add("Ui.Menu", "Thanh menu", () => _menu.NativeControl.IsVisible = !_menu.NativeControl.IsVisible,
            icon: "ribbon.customize", state: () => new CommandState(true, _menu?.NativeControl.IsVisible ?? false));
        Choice("Ui.Zoom", "Thu phóng", "view.zoom", value => _split.SetZoom(ParseNumber(value) / 100),
            () => ChoiceState((_split.ActiveSpreadsheet.Zoom * 100).ToString(CultureInfo.InvariantCulture), ZoomChoices));
        Choice("Ui.Theme", "Giao diện", "ribbon.customize", value => SetRibbonTheme(Enum.Parse<NeraIconTheme>(value)),
            () => ChoiceState((_ribbon?.IconTheme ?? NeraIconTheme.Light).ToString(), ThemeChoices));
        RegisterDialogCommands();
    }

    private ICommandHandler ResolveSessionHandler(CommandId id) => Session.Commands.TryResolve(id, out _, out var handler) && handler is not null
        ? handler : throw new InvalidOperationException("The active session no longer provides command " + id);
    private void ApplyStyle(Func<CellStyle, CellStyle> transform) => Session.Styles.ApplyToSelection(transform, "Định dạng từ Ribbon");
    private static double ParseNumber(string text) => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
    private static string ColorText(ColorRgba color) => $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
    private static ColorRgba ParseColor(string text) { var color = Color.Parse(text); return new ColorRgba(color.R, color.G, color.B, color.A); }
    private static CommandState ChoiceState(string? selected, IEnumerable<CommandItem> choices) => new(true, null, null, selected, choices);
    private void ApplyBorder(string value)
    {
        if (value == "all") Session.Styles.SetAllBorders(CellBorderLineStyle.Thin, new ColorRgba(105, 120, 132));
        else if (value == "none") ApplyStyle(style => style with { Border = new CellBorderStyle() });
        else ApplyStyle(style => style with { Border = style.Border with { Bottom = new CellBorderSide { Style = CellBorderLineStyle.Thin, Width = 1, Color = new ColorRgba(105, 120, 132) } } });
    }
    private void SetRibbonTheme(NeraIconTheme theme)
    {
        _ribbon.IconTheme = theme;
        _menu.IconTheme = theme;
        foreach (var dialog in _dialogs)
            if (dialog.Content is NeraRibbonCustomizationControl editor) editor.IconTheme = theme;
    }
    private void SetPageSetup(Func<SpreadsheetPageSetup, SpreadsheetPageSetup> change)
    {
        var sheet = Session.ActiveWorksheet; var before = sheet.GetPrintSettings();
        Session.Execute(new PageSetupOperation(sheet, before, before with { PageSetup = change(before.PageSetup) }));
    }
    private async Task ShowPrintPreviewAsync()
    {
        var session = Session; var sheet = session.ActiveWorksheet; var snapshot = WorksheetSnapshot.Capture(sheet);
        var settings = sheet.GetPrintSettings();
        var used = sheet.EnumerateUsedCells().Select(pair => pair.Key).ToArray();
        var range = settings.PrintArea ?? (used.Length == 0 ? new CellRange(default, default) :
            new CellRange(new CellAddress(used.Min(cell => cell.RowIndex), used.Min(cell => cell.ColumnIndex)),
                new CellAddress(used.Max(cell => cell.RowIndex), used.Max(cell => cell.ColumnIndex))));
        var plan = await Task.Run(() => SpreadsheetPageLayoutPlanner.CreatePlan(snapshot, range, settings.PageSetup));
        if (_closed || !ReferenceEquals(Session, session) || !ReferenceEquals(session.ActiveWorksheet, sheet)) return;
        var preview = new NeraPrintPreviewControl { Session = new SpreadsheetPrintPreviewSession(snapshot, plan, session.Workbook.Styles) };
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(6) };
        var less = new Button { Content = "−" }; var more = new Button { Content = "+" }; var columns = new Button { Content = "Một/hai trang" };
        less.Click += (_, _) => preview.SetZoom(Math.Max(0.05, preview.Zoom / 1.1)); more.Click += (_, _) => preview.SetZoom(Math.Min(8, preview.Zoom * 1.1));
        columns.Click += (_, _) => preview.SetColumns(preview.Session!.Columns == 1 ? 2 : 1);
        tools.Children.Add(less); tools.Children.Add(more); tools.Children.Add(columns);
        var root = new DockPanel(); DockPanel.SetDock(tools, Dock.Top); root.Children.Add(tools); root.Children.Add(preview);
        var window = new Window { Title = "Xem trước in — " + sheet.Name, Width = 920, Height = 720, Content = root };
        window.Closed += (_, _) => preview.Dispose(); TrackWindow(window); window.Show(this);
    }
    private void ShowFormulaHelp()
    {
        var help = Session.FormulaEditing.GetFunctionHelp("=SUM(", 5)?.Function;
        ShowInformation("Trợ giúp công thức", help is null ? "Nhập dấu = trong ô để mở gợi ý hàm và đối số." :
            $"{help.Signature}\n\n{help.Description}\n\n{string.Join(Environment.NewLine, help.Arguments.Select(argument => $"{argument.Name}: {argument.Description}"))}");
    }
    private void ShowInformation(string title, string text)
    {
        var content = new TextBox { Text = text, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16) };
        var window = new Window { Title = title, Width = 560, Height = 360, Content = content }; TrackWindow(window); window.Show(this);
    }
    private void Add(string id, string caption, Action action, string? shortcut = null, string? icon = null, Func<CommandState>? state = null) =>
        AddAsync(id, caption, () => { action(); return Task.CompletedTask; }, shortcut, icon, state);
    private void AddAsync(string id, string caption, Func<Task> action, string? shortcut = null, string? icon = null, Func<CommandState>? state = null) =>
        _registry.Register(new CommandDescriptor(id, caption, caption, icon, shortcut) { CaptionResourceKey = caption, TooltipResourceKey = caption },
            new ShellHandler(this, _ => new ValueTask(action()), _ => state?.Invoke() ?? CommandState.Enabled));
    private void Choice(string id, string caption, string icon, Action<string> action, Func<CommandState> state) =>
        _registry.Register(new CommandDescriptor(id, caption, caption, icon) { CaptionResourceKey = caption, TooltipResourceKey = caption },
            new ShellHandler(this, context =>
            {
                if (context.Parameter is not RibbonItemActivation { SelectedValue: { } value }) throw new InvalidOperationException("Select a value before executing this command.");
                action(value); return ValueTask.CompletedTask;
            }, _ => state()));

    private sealed class ShellHandler(FullShellWindow owner, Func<CommandContext, ValueTask> action, Func<CommandContext, CommandState> state) : IStatefulCommandHandler
    {
        public bool CanExecute(CommandContext context) => GetState(context).IsEnabled;
        public CommandState GetState(CommandContext context) => state(context) with { IsEnabled = !owner._busy && !owner._closed && state(context).IsEnabled };
        public async ValueTask ExecuteAsync(CommandContext context)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (owner._split.EditingSpreadsheet is { } editor && !editor.CommitEditor()) throw new InvalidOperationException("Dữ liệu chưa hợp lệ; bản nháp vẫn được giữ.");
            await action(context); owner._commandExecutions++;
            if (!owner._closed) { owner.RefreshSelection(); owner._menu.Runtime.Refresh(); }
        }
    }
    private sealed class PageSetupOperation(Worksheet worksheet, WorksheetPrintSettings before, WorksheetPrintSettings after) : ISpreadsheetEditOperation
    {
        public Worksheet Worksheet => worksheet;
        public CellRange AffectedRange => new(default, default);
        public bool AffectsCalculation => false;
        public string Description => "Thay đổi thiết lập trang";
        public void Execute() => worksheet.SetPrintSettings(after);
        public void Undo() => worksheet.SetPrintSettings(before);
    }
}
