using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Wpf.Sample;

public sealed partial class RibbonPreviewWindow
{
    private RibbonDefinition CreatePreviewDefinition() => new(
    [
        new RibbonTabDefinition("home", "Trang đầu",
        [
            Group("clipboard", "Bảng tạm", 100, Large("Edit.Paste"), Item("Edit.Cut"), Item("Edit.Copy"), Item("Cell.ClearContents")),
            Group("font", "Phông chữ", 90, Choice("Sample.Font", 122), Choice("Sample.FontSize", 64), Item("Sample.Borders"),
                Toggle("Cell.Format.Bold"), Toggle("Cell.Format.Italic"), Toggle("Sample.Underline"),
                Color("Sample.Fill"), Color("Sample.FontColor")),
            Group("alignment", "Căn chỉnh", 80, Toggle("Sample.Align.Left"), Toggle("Sample.Align.Center"), Toggle("Sample.Align.Right"),
                Toggle("Sample.Wrap"), Item("Cell.Merge"), Item("Cell.Unmerge")),
            Group("number", "Số", 60, Choice("Sample.Number", 130), Item("Sample.Percent"), Item("Sample.Decimal")),
            Group("cells", "Ô", 30, Item("Structure.Row.Insert"), Item("Structure.Row.Delete"), Item("Structure.Row.Hide"),
                Item("Structure.Column.Insert"), Item("Structure.Column.Delete"), Item("Structure.Column.Hide")),
            Group("editing", "Chỉnh sửa", 20, Item("Edit.Undo"), Item("Edit.Redo"), Item("Sample.Filter")),
        ]),
        new RibbonTabDefinition("insert", "Chèn",
        [
            Group("tables", "Bảng tổng hợp", 100, Large("Insert.Pivot.Sum")),
            Group("charts", "Biểu đồ", 80, Large("Insert.Chart.Column"), Item("Insert.Chart.Bar"), Item("Insert.Chart.Line"), Item("Insert.Chart.Pie")),
            Group("rows-columns", "Hàng và cột", 60, Item("Structure.Row.Insert"), Item("Structure.Column.Insert")),
        ]),
        new RibbonTabDefinition("page-layout", "Bố trí trang",
        [
            Group("page-setup", "Thiết lập trang", 100, Choice("Sample.Orientation", 105), Choice("Sample.Paper", 90), Choice("Sample.Margins", 105)),
            Group("print-options", "Tùy chọn in", 60, Toggle("Sample.PrintGrid"), Toggle("Sample.PrintHeadings")),
            Group("print-preview", "Bản in", 80, Large("Sample.PrintPreview")),
        ]),
        new RibbonTabDefinition("formulas", "Công thức",
        [
            Group("functions", "Thư viện hàm", 100, Large("Sample.FormulaHelp"), Item("Sample.FormulaSum"), Item("Sample.FormulaAverage"),
                Item("Sample.FormulaIf"), Item("Sample.FormulaLookup")),
            Group("calculation", "Tính toán", 80, Large("Formula.RecalculateWorkbook")),
            Group("formula-audit", "Kiểm tra công thức", 50, Item("Sample.Errors")),
        ]),
        new RibbonTabDefinition("data", "Dữ liệu",
        [
            Group("sort-filter", "Sắp xếp và lọc", 100, Large("Sample.Filter"), Item("Data.SortAscending"), Item("Data.SortDescending"),
                Item("Sample.FilterClear"), Item("Sample.FilterReapply")),
            Group("data-tools", "Công cụ dữ liệu", 60, Large("Insert.Pivot.Sum"), Item("Sample.Statistics")),
        ]),
        new RibbonTabDefinition("review", "Xem lại",
        [
            Group("audit", "Kiểm tra dữ liệu", 100, Large("Sample.Errors"), Item("Sample.Statistics")),
            Group("help", "Trợ giúp", 50, Large("Sample.FormulaHelp")),
        ]),
        new RibbonTabDefinition("view", "Xem",
        [
            Group("show", "Hiển thị", 100, Toggle("Sample.Gridlines"), Toggle("Sample.Headers")),
            Group("zoom", "Thu phóng", 80, Choice("Sample.Zoom", 88), Item("Sample.ZoomReset")),
            Group("window", "Cửa sổ", 70, Large("View.FreezePanes"), Item("View.UnfreezePanes"), Item("View.Split.Undo"), Item("View.Split.Redo")),
            Group("visibility", "Hàng và cột", 30, Item("Structure.Row.Unhide"), Item("Structure.Column.Unhide")),
        ]),
        new RibbonTabDefinition("table-design", "Thiết kế Bảng",
        [
            Group("table-properties", "Thuộc tính", 100, Large("Sample.TableInfo"), Item("Sample.TableRename"), Choice("Sample.TableTotals", 132)),
            Group("table-filter", "Dữ liệu bảng", 80, Item("Sample.Filter"), Item("Sample.FilterClear"), Item("Sample.FilterReapply")),
            Group("table-styles", "Xem trước kiểu bảng", 60,
                new RibbonItemDefinition("Sample.TableStylesPreview", RibbonItemKind.Gallery, measurement: context => context.Size switch
                { RibbonItemSize.Large => 420, RibbonItemSize.Small => 300, _ => 220 }) { GalleryPreview = CreateStylePreview }),
        ]),
    ],
    [new RibbonContextualTabRule("table-design", RibbonContextRequirement.Table, "TB")],
    [new("Sample.Save", "1"), new("Edit.Undo", "2"), new("Edit.Redo", "3")],
    [new("Sample.New", "N"), new("Sample.Open", "O"), new("Sample.Save", "S"), new("Sample.PrintPreview", "P"), new("Sample.Statistics", "I")]);

    private static RibbonGroupDefinition Group(string id, string caption, int priority, params RibbonItemDefinition[] items) =>
        new(id, caption, items, 0, priority);
    private static RibbonItemDefinition Item(string id) => new(id);
    private static RibbonItemDefinition Large(string id) => new(id, IsLarge: true);
    private static RibbonItemDefinition Toggle(string id) => new(id, RibbonItemKind.Toggle);
    private static RibbonItemDefinition Choice(string id, double width) => new(id, RibbonItemKind.ComboBox,
        measurement: _ => width);
    private static RibbonItemDefinition Color(string id) => new(id, RibbonItemKind.ColorPicker,
        measurement: context => Math.Max(122d, context.DefaultWidth));
}
