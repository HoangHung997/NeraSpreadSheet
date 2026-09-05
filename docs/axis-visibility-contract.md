# Row and column visibility contract

## Purpose

This contract defines Excel-compatible manual row/column hiding independently
from filtering. Manual visibility is workbook state and is available to every
host through the SDK.

## Core model

- `WorksheetDimensions.HideRows`, `UnhideRows`, `HideColumns` and
  `UnhideColumns` store normalized sparse ranges; hiding a large range does not
  create one override per row or column.
- A hidden axis entry has an effective size of zero. Its prior custom size is
  retained and becomes effective again when the entry is unhidden.
- Setting a positive row height or column width unhides that entry. Setting a
  size to zero remains a compatibility alias for hiding one entry.
- Hidden ranges participate in insert/delete, row/column reorder, snapshots,
  print layout and viewport metrics. They follow the same logical axis identity
  as the rest of worksheet state.

## Editing and commands

`SpreadsheetSession.AxisVisibility` provides undoable range operations. The
command catalog exposes:

- `Structure.Row.Hide` and `Structure.Row.Unhide`;
- `Structure.Column.Hide` and `Structure.Column.Unhide`.

Whole-row or whole-column selections determine the operated range. Otherwise,
the active row or column is used. Undo/redo restores the exact sparse hidden
range state without recalculating formulas.

## Navigation and rendering

`SpreadsheetVisibleCellNavigation` is the shared keyboard navigation source.
WPF and WinForms, including split surfaces, use it for arrows, Enter and Tab.
Movement skips a complete hidden range in one step and never lands on a hidden
row or column. Layout, hit testing, headers and rendering allocate no extent to
manual hidden ranges.

This matches the observed Excel desktop behavior: Down from row 107 jumps to
row 149 when rows 108-148 are hidden; Right from column A jumps to column C
when column B is hidden.

## Open XML

XLSX import retains both the hidden flag and any stored custom height/width.
Export writes manual hidden ranges back to standard SpreadsheetML. Unhiding an
imported custom-sized row or column therefore restores its stored size.

## Boundaries

- Manual hide/unhide does not delete cells, formulas, styles or dimensions.
- Table/worksheet filter visibility remains a separate calculation; this
  contract does not change filter criteria or filter state.
- PR #1 remains Draft until exact-head CI passes.
