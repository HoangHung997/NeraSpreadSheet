using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Ribbon.Core;

/// <summary>
/// Audited stable command identities registered by the production spreadsheet session.
/// Hosts may use this manifest to prove that their Ribbon definition exposes every capability.
/// </summary>
public static class RibbonProductionCommandCatalog
{
    public static IReadOnlyList<CommandId> CommandIds { get; } = Array.AsReadOnly<CommandId>(
    [
        "Edit.Undo", "Edit.Redo", "Edit.Copy", "Edit.Cut", "Edit.Paste",
        "Cell.ClearContents", "Cell.Format.Bold", "Cell.Format.Italic",
        "Cell.Merge", "Cell.Unmerge", "Formula.RecalculateWorkbook",
        "Data.SortAscending", "Data.SortDescending",
        "Structure.Row.Insert", "Structure.Row.Delete", "Structure.Row.Hide",
        "Structure.Row.Unhide", "Structure.Column.Insert", "Structure.Column.Delete",
        "Structure.Column.Hide", "Structure.Column.Unhide",
        "View.FreezePanes", "View.UnfreezePanes", "View.Split.Undo", "View.Split.Redo",
        "Insert.Chart.Column", "Insert.Chart.Bar", "Insert.Chart.Line",
        "Insert.Chart.Pie", "Insert.Pivot.Sum", "Table.Create", "Table.Rename",
        "Table.Resize", "Table.HeaderRow", "Table.TotalsRow", "Table.FirstColumn",
        "Table.LastColumn", "Table.BandedRows", "Table.BandedColumns",
        "Table.FilterButtons", "Table.Style", "Table.CalculatedColumn",
        "Table.TotalsFunction", "Table.Row.Insert", "Table.Row.Delete",
        "Table.Column.Insert", "Table.Column.Delete", "Table.RemoveDuplicates",
        "Table.ConvertToRange",
    ]);

    /// <summary>Creates the complete built-in command placement used for audit and host bootstrap.</summary>
    public static RibbonDefinition CreateDefaultDefinition() => CreateDefaultDefinition(null);

    /// <summary>Creates production placement with optional host-supplied style thumbnails.</summary>
    public static RibbonDefinition CreateDefaultDefinition(
        Func<CommandItem, RibbonGalleryPreview?>? tableStylePreview) => Localized(new(
        [
            CreateTab("home", "Trang đầu", "clipboard", "Bảng tạm",
                "Edit.Paste", "Edit.Cut", "Edit.Copy", "Edit.Undo", "Edit.Redo"),
            CreateTab("format", "Định dạng", "cells", "Ô",
                "Cell.ClearContents", "Cell.Format.Bold", "Cell.Format.Italic",
                "Cell.Merge", "Cell.Unmerge"),
            CreateTab("insert", "Chèn", "analytics", "Phân tích",
                "Insert.Chart.Column", "Insert.Chart.Bar", "Insert.Chart.Line",
                "Insert.Chart.Pie", "Insert.Pivot.Sum", "Table.Create"),
            CreateTab("data", "Dữ liệu", "data-tools", "Công cụ dữ liệu",
                "Data.SortAscending", "Data.SortDescending", "Formula.RecalculateWorkbook"),
            CreateTab("structure", "Cấu trúc", "axes", "Hàng và cột",
                "Structure.Row.Insert", "Structure.Row.Delete", "Structure.Row.Hide",
                "Structure.Row.Unhide", "Structure.Column.Insert", "Structure.Column.Delete",
                "Structure.Column.Hide", "Structure.Column.Unhide"),
            CreateTab("view", "Xem", "window", "Cửa sổ",
                "View.FreezePanes", "View.UnfreezePanes", "View.Split.Undo",
                "View.Split.Redo"),
            new RibbonTabDefinition("table-design", "Thiết kế Bảng", [
                new RibbonGroupDefinition("table-properties", "Thuộc tính", [
                    Large("Table.Rename"),
                    Item("Table.Resize"),
                    Item("Table.ConvertToRange")]),
                new RibbonGroupDefinition("table-options", "Tùy chọn kiểu Bảng", [
                    Toggle("Table.HeaderRow"),
                    Toggle("Table.TotalsRow"),
                    Toggle("Table.FirstColumn"),
                    Toggle("Table.LastColumn"),
                    Toggle("Table.BandedRows"),
                    Toggle("Table.BandedColumns"),
                    Toggle("Table.FilterButtons")]),
                new RibbonGroupDefinition("table-styles", "Kiểu Bảng", [
                    new RibbonItemDefinition(
                        "Table.Style",
                        RibbonItemKind.Gallery,
                        isLarge: true) { GalleryPreview = tableStylePreview }]),
                new RibbonGroupDefinition("table-formulas", "Công thức", [
                    Item("Table.CalculatedColumn"),
                    new RibbonItemDefinition(
                        "Table.TotalsFunction",
                        RibbonItemKind.ComboBox)]),
                new RibbonGroupDefinition("table-structure", "Hàng và cột", [
                    Item("Table.Row.Insert"),
                    Item("Table.Row.Delete"),
                    Item("Table.Column.Insert"),
                    Item("Table.Column.Delete"),
                    Item("Table.RemoveDuplicates")]),
            ]),
        ],
        [new RibbonContextualTabRule("table-design", RibbonContextRequirement.Table, "TB")],
        [
            new RibbonCommandSurfaceItem("Edit.Undo", "1"),
            new RibbonCommandSurfaceItem("Edit.Redo", "2"),
            new RibbonCommandSurfaceItem("Edit.Copy", "3"),
        ],
        []));

    private static RibbonDefinition Localized(RibbonDefinition definition) => new(
        definition.Tabs.Select(tab => new RibbonTabDefinition(tab.Id, tab.Caption,
            tab.Groups.Select(group => new RibbonGroupDefinition(group.Id, group.Caption,
                group.Items, group.Order, group.CollapsePriority) { CaptionResourceKey = group.Caption }),
            tab.Order) { CaptionResourceKey = tab.Caption }),
        definition.ContextualTabs, definition.QuickAccessToolbar, definition.Backstage);

    private static RibbonTabDefinition CreateTab(
        string tabId,
        string tabCaption,
        string groupId,
        string groupCaption,
        params string[] commandIds) =>
        new(tabId, tabCaption, [new RibbonGroupDefinition(
            groupId,
            groupCaption,
            commandIds.Select(static commandId => new RibbonItemDefinition(commandId,
                IsLarge: commandId is "Edit.Paste" or "Insert.Chart.Column" or "Insert.Pivot.Sum" or
                    "Formula.RecalculateWorkbook" or "View.FreezePanes"))) ]);

    private static RibbonItemDefinition Item(string commandId) =>
        new(commandId, RibbonItemKind.Button);

    private static RibbonItemDefinition Large(string commandId) =>
        new(commandId, RibbonItemKind.Button, isLarge: true);

    private static RibbonItemDefinition Toggle(string commandId) =>
        new(commandId, RibbonItemKind.Toggle);
}
