using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.OpenXml;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Avalonia.Sample;

public sealed partial class FullShellWindow
{
    private static readonly (string Id, string Caption)[] QaTableCommands =
    [
        ("Table.Create", "Tạo Bảng"),
        ("Table.HeaderRow", "Hàng tiêu đề"),
        ("Table.TotalsRow", "Hàng tổng"),
        ("Table.FirstColumn", "Cột đầu tiên"),
        ("Table.LastColumn", "Cột cuối cùng"),
        ("Table.BandedRows", "Hàng xen kẽ"),
        ("Table.BandedColumns", "Cột xen kẽ"),
        ("Table.FilterButtons", "Nút lọc"),
        ("Table.Style", "Kiểu Bảng"),
        ("Table.TotalsFunction", "Hàm tổng"),
        ("Table.Row.Insert", "Chèn hàng Bảng"),
        ("Table.Row.Delete", "Xóa hàng Bảng"),
        ("Table.Column.Insert", "Chèn cột Bảng"),
        ("Table.Column.Delete", "Xóa cột Bảng"),
        ("Table.RemoveDuplicates", "Loại bỏ trùng lặp"),
        ("Table.ConvertToRange", "Chuyển thành phạm vi"),
    ];

    private static readonly CommandItem[] CellFormatChoices =
    [
        new("row-hide", "Ẩn hàng", iconKey: "row.hide"),
        new("row-unhide", "Hiện hàng", iconKey: "row.unhide"),
        new("column-hide", "Ẩn cột", iconKey: "column.hide"),
        new("column-unhide", "Hiện cột", iconKey: "column.unhide"),
    ];

    private NeraWorksheetViewStateBinding? _worksheetViewBinding;
    private SpreadsheetSession? _qaContextSession;
    private NeraAutoFilterWindow? _filterWindow;
    private bool _qaRuntimeAttached;

    private void RegisterQaGapCommands()
    {
        foreach (var entry in QaTableCommands) ForwardQaSessionCommand(entry.Id, entry.Caption);
        RegisterCellFormatCommand();
        AddAsync("Ui.Filter", "Bộ lọc", ShowActiveFilterAsync, icon: "data.filter", state: FilterCommandState);
        AddAsync("Ui.FilterClear", "Xóa bộ lọc", ClearActiveFilterAsync, icon: "data.filter-clear", state: FilterCommandState);
        AddAsync("Ui.FilterReapply", "Áp dụng lại bộ lọc", ReapplyActiveFilterAsync, icon: "data.filter-reapply", state: FilterCommandState);
        Opened += OnQaShellOpened;
        Closed += OnQaShellClosed;
    }

    private void ForwardQaSessionCommand(string id, string caption)
    {
        if (!Session.Commands.TryResolve(id, out var descriptor, out _) || descriptor is null)
            throw new InvalidOperationException("The built-in Table command is missing: " + id);
        _registry.Register(new CommandDescriptor(id, caption, caption, descriptor.IconKey, descriptor.Shortcut)
        {
            CaptionResourceKey = caption,
            TooltipResourceKey = caption,
        }, new ShellHandler(this,
            context => ResolveSessionHandler(id).ExecuteAsync(context),
            context => RestrictMetadataCommand(id)
                ? CommandState.Disabled
                : ResolveSessionHandler(id) is IStatefulCommandHandler stateful
                    ? stateful.GetState(context)
                    : new CommandState(ResolveSessionHandler(id).CanExecute(context))));
    }

    private void RegisterCellFormatCommand()
    {
        _registry.Register(new CommandDescriptor("Ui.CellsFormat", "Định dạng hàng/cột", "Ẩn hoặc hiện hàng và cột", "row.hide")
        {
            CaptionResourceKey = "Định dạng hàng/cột",
            TooltipResourceKey = "Ẩn hoặc hiện hàng và cột",
        }, new ShellHandler(this, async context =>
        {
            if (context.Parameter is not RibbonItemActivation { SelectedValue: { } selected })
                throw new InvalidOperationException("Chọn thao tác hàng/cột trước khi thực hiện.");
            var id = selected switch
            {
                "row-hide" => "Structure.Row.Hide",
                "row-unhide" => "Structure.Row.Unhide",
                "column-hide" => "Structure.Column.Hide",
                "column-unhide" => "Structure.Column.Unhide",
                _ => throw new InvalidOperationException("Thao tác hàng/cột không hợp lệ."),
            };
            await ResolveSessionHandler(id).ExecuteAsync(context with { Parameter = null });
        }, context => new CommandState(
            !RestrictMetadataCommand("Structure.Row.Hide") && CellFormatChoices.Any(choice => ChoiceEnabled(choice.Value, context)),
            IsChecked: null,
            DisplayText: null,
            SelectedValue: null,
            ItemsSource: CellFormatChoices.Select(choice => new CommandItem(
                choice.Value,
                choice.Caption,
                ChoiceEnabled(choice.Value, context),
                iconKey: choice.IconKey)))));
    }

    private bool ChoiceEnabled(string value, CommandContext context)
    {
        var id = value switch
        {
            "row-hide" => "Structure.Row.Hide",
            "row-unhide" => "Structure.Row.Unhide",
            "column-hide" => "Structure.Column.Hide",
            "column-unhide" => "Structure.Column.Unhide",
            _ => string.Empty,
        };
        return id.Length > 0 && !RestrictMetadataCommand(id) && ResolveSessionHandler(id).CanExecute(context);
    }

    private CommandState FilterCommandState()
    {
        var allowed = !OpenXmlImportDiagnostics.Get(Session.Workbook).RequiresMetadataPreservation &&
            Session.TryResolveActiveAutoFilterTarget(out _);
        return new CommandState(allowed);
    }

    private async Task ShowActiveFilterAsync()
    {
        if (!FilterCommandState().IsEnabled || !Session.TryResolveActiveAutoFilterTarget(out var target))
        {
            ShowInformation("Bộ lọc", "Chọn một ô trong Bảng hoặc vùng AutoFilter trước khi mở bộ lọc.");
            return;
        }
        _filterWindow?.Close();
        var session = Session;
        var window = new NeraAutoFilterWindow(session, target, _runtime.Localization);
        _filterWindow = window;
        window.Closed += (_, _) => { if (ReferenceEquals(_filterWindow, window)) _filterWindow = null; RefreshQaRibbonContext(); };
        await window.InitializeAsync();
        if (_closed || !ReferenceEquals(Session, session)) { window.Dispose(); return; }
        TrackWindow(window);
        window.Show(this);
    }

    private async Task ClearActiveFilterAsync()
    {
        if (!FilterCommandState().IsEnabled || !Session.TryResolveActiveAutoFilterTarget(out var target)) return;
        using var presenter = new SpreadsheetAutoFilterPagedPresenter(Session, target);
        await presenter.InitializeAsync();
        await presenter.ClearColumnFilterAsync();
        RefreshQaRibbonContext();
    }

    private async Task ReapplyActiveFilterAsync()
    {
        if (!FilterCommandState().IsEnabled || !Session.TryResolveActiveAutoFilterTarget(out var target)) return;
        using var presenter = new SpreadsheetAutoFilterPagedPresenter(Session, target);
        await presenter.InitializeAsync();
        await presenter.ReapplyAsync();
        RefreshQaRibbonContext();
    }

    private void OnQaShellOpened(object? sender, EventArgs e)
    {
        if (_qaRuntimeAttached || _closed) return;
        _qaRuntimeAttached = true;
        _worksheetViewBinding = new NeraWorksheetViewStateBinding(_split);
        _split.SessionChanged += OnQaSessionChanged;
        BindQaContextSession(Session);
        RefreshQaRibbonContext();
    }

    private void OnQaSessionChanged(object? sender, EventArgs e)
    {
        if (_closed) return;
        BindQaContextSession(Session);
        RefreshQaRibbonContext();
    }

    private void BindQaContextSession(SpreadsheetSession session)
    {
        if (ReferenceEquals(_qaContextSession, session)) return;
        if (_qaContextSession is not null)
        {
            _qaContextSession.Selection.Changed -= OnQaContextChanged;
            _qaContextSession.ActiveWorksheetChanged -= OnQaContextChanged;
            _qaContextSession.TableDesign.ContextChanged -= OnQaContextChanged;
        }
        _qaContextSession = session;
        _qaContextSession.Selection.Changed += OnQaContextChanged;
        _qaContextSession.ActiveWorksheetChanged += OnQaContextChanged;
        _qaContextSession.TableDesign.ContextChanged += OnQaContextChanged;
    }

    private void OnQaContextChanged(object? sender, EventArgs e)
    {
        if (!_closed) RefreshQaRibbonContext();
    }

    private void RefreshQaRibbonContext()
    {
        if (_closed || !_qaRuntimeAttached) return;
        var hasSelection = Session.Selection.Ranges.Count > 0;
        var isInTable = hasSelection && Session.ActiveWorksheet.TryGetTable(Session.Selection.ActiveCell, out var table) && table is not null;
        var next = new RibbonSelectionContext(hasSelection, isInTable);
        if (_runtime.SelectionContext != next) _runtime.SetSelectionContext(next);
        else _runtime.Refresh();
        _menu.Runtime.Refresh();
    }

    private void OnQaShellClosed(object? sender, EventArgs e)
    {
        _filterWindow?.Close();
        _filterWindow = null;
        _worksheetViewBinding?.Dispose();
        _worksheetViewBinding = null;
        _split.SessionChanged -= OnQaSessionChanged;
        if (_qaContextSession is not null)
        {
            _qaContextSession.Selection.Changed -= OnQaContextChanged;
            _qaContextSession.ActiveWorksheetChanged -= OnQaContextChanged;
            _qaContextSession.TableDesign.ContextChanged -= OnQaContextChanged;
            _qaContextSession = null;
        }
        Opened -= OnQaShellOpened;
        Closed -= OnQaShellClosed;
    }
}
