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
        "Insert.Chart.Pie", "Insert.Pivot.Sum",
    ]);

    /// <summary>Creates the complete built-in command placement used for audit and host bootstrap.</summary>
    public static RibbonDefinition CreateDefaultDefinition() => new(
        [
            CreateTab("home", "Trang đầu", "clipboard", "Bảng tạm",
                "Edit.Paste", "Edit.Cut", "Edit.Copy", "Edit.Undo", "Edit.Redo"),
            CreateTab("format", "Định dạng", "cells", "Ô",
                "Cell.ClearContents", "Cell.Format.Bold", "Cell.Format.Italic",
                "Cell.Merge", "Cell.Unmerge"),
            CreateTab("insert", "Chèn", "analytics", "Phân tích",
                "Insert.Chart.Column", "Insert.Chart.Bar", "Insert.Chart.Line",
                "Insert.Chart.Pie", "Insert.Pivot.Sum"),
            CreateTab("data", "Dữ liệu", "data-tools", "Công cụ dữ liệu",
                "Data.SortAscending", "Data.SortDescending", "Formula.RecalculateWorkbook"),
            CreateTab("structure", "Cấu trúc", "axes", "Hàng và cột",
                "Structure.Row.Insert", "Structure.Row.Delete", "Structure.Row.Hide",
                "Structure.Row.Unhide", "Structure.Column.Insert", "Structure.Column.Delete",
                "Structure.Column.Hide", "Structure.Column.Unhide"),
            CreateTab("view", "Xem", "window", "Cửa sổ",
                "View.FreezePanes", "View.UnfreezePanes", "View.Split.Undo",
                "View.Split.Redo"),
            new RibbonTabDefinition("table-design", "Thiết kế Bảng", []),
        ],
        [new RibbonContextualTabRule("table-design", RibbonContextRequirement.Table, "TB")],
        [
            new RibbonCommandSurfaceItem("Edit.Undo", "1"),
            new RibbonCommandSurfaceItem("Edit.Redo", "2"),
            new RibbonCommandSurfaceItem("Edit.Copy", "3"),
        ],
        []);

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
}
