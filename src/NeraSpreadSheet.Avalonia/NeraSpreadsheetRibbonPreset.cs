using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Avalonia;

/// <summary>
/// Spreadsheet-oriented command placement shared by Avalonia consumers. This is
/// a projection of a caller's registered capabilities, not another command model.
/// Missing capabilities and their empty groups/tabs are omitted, never simulated.
/// </summary>
public static class NeraSpreadsheetRibbonPreset
{
    /// <summary>
    /// Builds an Excel-style arrangement over the supplied registry. Built-in IDs
    /// retain their session handlers. Optional Ui.* IDs are host actions illustrated
    /// by the sample (font choices, page setup, help and view controls); this factory
    /// does not register, execute or enable them. Existing sample aliases remain
    /// accepted. Call again if the registered capability set changes.
    /// </summary>
    public static RibbonDefinition Create(CommandRegistry registry,
        Func<CommandItem, RibbonGalleryPreview?>? tableStylePreview = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        var tabs = new List<RibbonTabDefinition>();
        AddTab("home", "Trang đầu",
            Group("clipboard", "Bảng tạm", 100,
                Item("Edit.Paste", large: true), Item("Edit.Cut"), Item("Edit.Copy")),
            Group("font", "Phông chữ", 90,
                Item("Ui.FontFamily", RibbonItemKind.ComboBox, width: 116),
                Item("Ui.FontSize", RibbonItemKind.ComboBox, width: 56), Item("Ui.Borders", RibbonItemKind.DropDown),
                Item("Cell.Format.Bold", RibbonItemKind.Toggle, alias: "Cell.Bold"),
                Item("Cell.Format.Italic", RibbonItemKind.Toggle, alias: "Cell.Italic"),
                Item("Ui.Underline", RibbonItemKind.Toggle),
                Item("Ui.Fill", RibbonItemKind.ColorPicker, width: 120),
                Item("Ui.FontColor", RibbonItemKind.ColorPicker, width: 120)),
            Group("alignment", "Căn chỉnh", 80,
                Item("Ui.Align.Left", RibbonItemKind.Toggle), Item("Ui.Align.Center", RibbonItemKind.Toggle),
                Item("Ui.Align.Right", RibbonItemKind.Toggle), Item("Ui.Wrap", RibbonItemKind.Toggle),
                Item("Cell.Merge"), Item("Cell.Unmerge")),
            Group("number", "Số", 70,
                Item("Ui.Number", RibbonItemKind.ComboBox, width: 126), Item("Ui.Percent"), Item("Ui.Decimal")),
            Group("cells", "Ô", 40,
                Item("Structure.Row.Insert"), Item("Structure.Row.Delete"), Item("Structure.Row.Hide"),
                Item("Structure.Column.Insert"), Item("Structure.Column.Delete"), Item("Structure.Column.Hide")),
            Group("editing", "Chỉnh sửa", 20,
                Item("Cell.ClearContents", alias: "Cell.Clear"), Item("Edit.Undo"), Item("Edit.Redo")));
        AddTab("insert", "Chèn",
            Group("tables", "Bảng", 100, Item("Table.Create", large: true), Item("Insert.Pivot.Sum", large: true)),
            Group("charts", "Biểu đồ", 90,
                Item("Insert.Chart.Column", large: true), Item("Insert.Chart.Bar"), Item("Insert.Chart.Line"), Item("Insert.Chart.Pie")),
            Group("axes", "Hàng và cột", 50, Item("Structure.Row.Insert"), Item("Structure.Column.Insert")));
        AddTab("page-layout", "Bố trí trang",
            Group("page-setup", "Thiết lập trang", 100,
                Item("Ui.Orientation", RibbonItemKind.ComboBox, width: 100), Item("Ui.Paper", RibbonItemKind.ComboBox, width: 80),
                Item("Ui.Margins", RibbonItemKind.ComboBox, width: 108)),
            Group("print-options", "Tùy chọn in", 60, Item("Ui.PrintGrid", RibbonItemKind.Toggle), Item("Ui.PrintHeadings", RibbonItemKind.Toggle)),
            Group("print-preview", "Bản in", 80, Item("Ui.PrintPreview", large: true)));
        AddTab("formulas", "Công thức",
            Group("functions", "Thư viện hàm", 100, Item("Ui.FormulaHelp", large: true),
                Item("Ui.FormulaSum"), Item("Ui.FormulaAverage"), Item("Ui.FormulaIf"), Item("Ui.FormulaLookup")),
            Group("calculation", "Tính toán", 90, Item("Formula.RecalculateWorkbook", large: true, alias: "Formula.Calculate")),
            Group("formula-audit", "Kiểm tra công thức", 50, Item("Ui.Errors")));
        AddTab("data", "Dữ liệu",
            Group("sort", "Sắp xếp", 100, Item("Data.SortAscending", large: true), Item("Data.SortDescending")),
            Group("data-tools", "Công cụ dữ liệu", 60, Item("Ui.Statistics"), Item("Insert.Pivot.Sum")));
        AddTab("review", "Xem lại",
            Group("audit", "Kiểm tra dữ liệu", 100, Item("Ui.Errors", large: true), Item("Ui.Statistics")),
            Group("help", "Trợ giúp", 50, Item("Ui.FormulaHelp", large: true)));
        AddTab("view", "Xem",
            Group("show", "Hiển thị", 100, Item("Ui.Gridlines", RibbonItemKind.Toggle), Item("Ui.Headers", RibbonItemKind.Toggle),
                Item("Ui.Menu", RibbonItemKind.Toggle)),
            Group("zoom", "Thu phóng", 80, Item("Ui.Zoom", RibbonItemKind.ComboBox, width: 88),
                Item("View.ZoomIn"), Item("View.ZoomOut"), Item("View.ZoomReset")),
            Group("window", "Cửa sổ", 90, Item("View.FreezePanes", large: true, alias: "View.Freeze"),
                Item("View.UnfreezePanes", alias: "View.Unfreeze"), Item("View.Split.Both"), Item("View.Split.Vertical"),
                Item("View.Split.Horizontal"), Item("View.Split.None"), Item("View.Split.Undo"), Item("View.Split.Redo")),
            Group("appearance", "Giao diện", 40, Item("Ui.Theme", RibbonItemKind.ComboBox, width: 148), Item("View.Theme")));

        // The existing contextual Table definition remains canonical. Applications
        // opt in only by registering their actual Table command handlers.
        var tableTab = RibbonProductionCommandCatalog.CreateDefaultDefinition(tableStylePreview)
            .Tabs.Single(tab => tab.Id == "table-design");
        var tableGroups = tableTab.Groups.Select(group => new RibbonGroupDefinition(group.Id, group.Caption,
            group.Items.Where(item => Available(item.CommandId)), group.Order, group.CollapsePriority)
            { CaptionResourceKey = group.CaptionResourceKey }).Where(group => group.Items.Count > 0).ToArray();
        if (tableGroups.Length > 0) tabs.Add(new RibbonTabDefinition(tableTab.Id, tableTab.Caption, tableGroups)
            { CaptionResourceKey = tableTab.CaptionResourceKey });

        return new RibbonDefinition(tabs,
            tableGroups.Length > 0 ? [new RibbonContextualTabRule("table-design", RibbonContextRequirement.Table, "TB")] : [],
            Surface(("Shell.Save", "1"), ("Edit.Undo", "2"), ("Edit.Redo", "3")),
            Surface(("Shell.Open", "O"), ("Shell.Save", "S"), ("Ui.PrintPreview", "P"), ("Ui.Statistics", "I")));

        bool Available(CommandId id) => registry.TryResolve(id, out _, out _);
        RibbonItemDefinition? Item(string id, RibbonItemKind kind = RibbonItemKind.Button,
            bool large = false, double? width = null, string? alias = null)
        {
            var resolved = Available(id) ? id : alias is not null && Available(alias) ? alias : null;
            return resolved is null ? null : new RibbonItemDefinition(resolved, kind, isLarge: large,
                measurement: width is { } requested ? _ => requested : null);
        }
        static RibbonGroupDefinition Group(string id, string caption, int priority, params RibbonItemDefinition?[] items) =>
            new(id, caption, items.OfType<RibbonItemDefinition>(), 0, priority) { CaptionResourceKey = caption };
        void AddTab(string id, string caption, params RibbonGroupDefinition[] groups)
        {
            var populated = groups.Where(group => group.Items.Count > 0).ToArray();
            if (populated.Length > 0) tabs.Add(new RibbonTabDefinition(id, caption, populated) { CaptionResourceKey = caption });
        }
        IEnumerable<RibbonCommandSurfaceItem> Surface(params (string Id, string Tip)[] candidates) =>
            candidates.Where(item => Available(item.Id)).Select(item => new RibbonCommandSurfaceItem(item.Id, item.Tip));
    }
}
