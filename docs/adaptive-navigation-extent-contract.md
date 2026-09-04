# Adaptive navigation extent contract

## Purpose

This contract separates the physical worksheet limits from the range exposed by
a host scrollbar. A worksheet always retains Excel-compatible limits, while a
host may opt into a compact navigation extent for easier movement around small
or sparse workbooks.

## Extent rules

`SpreadsheetViewportEngine.GetAdaptiveNavigationExtent` returns an extent that
contains all of the following:

- at least the current body viewport;
- every materialized sparse cell, including formula/style-bearing cells;
- merged ranges and table ranges;
- explicit row-height and column-width overrides;
- the current navigation cell, even when that cell is empty.
- a trailing workspace of at least one viewport and, by default, 100 rows and
  20 columns after the last used or navigated cell;
- the current scrolled viewport, even when the selection remains elsewhere.

The current navigation cell is deliberately transient. Moving to an empty cell
beyond the available tail expands the extent. Returning and scrolling back
allows the far empty range to contract, unless data, formatting, a merge, a
table or a dimension override still owns that tail. The current scroll offset
is an extent floor, not another used-range boundary, so dragging a scrollbar
does not recursively append a new tail. The calculation is cached by worksheet
and dimension versions so plain scrolling never enumerates the sparse cell
store.

## Host behavior

WPF and WinForms expose `UseAdaptiveNavigationExtent`. It is opt-in to preserve
the existing full-sheet scrollbar contract for current applications.

When enabled:

- `ContentWidth` and `ContentHeight` expose the adaptive scroll extent;
- `AdaptiveNavigationTrailingRowCount` and
  `AdaptiveNavigationTrailingColumnCount` configure the minimum blank tail;
- a selection change never clamps away the viewport currently being inspected;
- scrollbar movement changes the viewport only and does not change selection;
- arrow, Enter and Tab navigation call `ScrollCellIntoView` so the active cell
  never moves outside the body viewport;
- manual hidden row/column ranges are skipped through the shared visible-cell
  navigator before `ScrollCellIntoView` is called;
- frozen row/column extents remain fixed and are excluded from the scrollable
  visibility rectangle;
- offsets remain continuous `double` values and never snap to cell boundaries.

`ScrollCellIntoView` is also public so an external formula bar, name box or
application command can use the same host behavior.

## Boundaries

- This contract does not delete rows, columns or workbook content.
- It does not change `SpreadsheetLimits` or `GetContentExtent`.
- Split-pane scrollbar topology continues to use its existing full physical
  extent; adaptive per-pane navigation is a separate future contract.
- Whole-row/whole-column style operations are not yet exposed as an enumerable
  public used-range source. Materialized cells and explicit dimension overrides
  are included today.

## Validation

- Viewport tests cover active-cell expansion, contraction, retained data,
  clearing the far tail, merges, dimension overrides and a manually scrolled
  viewport without compounded growth.
- Loaded WPF and WinForms smokes cover Left/Right/Up/Down keyboard navigation,
  automatic scrolling, independent viewport movement and the persistent
  scrollable tail.
