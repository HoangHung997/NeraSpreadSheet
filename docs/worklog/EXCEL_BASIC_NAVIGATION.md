# EXCEL-BASIC-NAV-002 worklog

## Scope

- Keep the worksheet's physical Excel-compatible row and column limits intact.
- Add an opt-in compact scrollbar extent based on the used range and active
  navigation cell.
- Keep keyboard navigation visible by scrolling the viewport when the active
  cell crosses its edges.
- Let an empty tail expand while it is being navigated and contract after the
  active cell returns, unless workbook content still owns that tail.

## Implementation

- `SpreadsheetViewportEngine.GetAdaptiveNavigationExtent` derives the compact
  extent from sparse cells, merges, tables, row/column dimension overrides and
  the active navigation cell.
- The used-range portion is cached by worksheet and dimension versions so raw
  scrolling does not enumerate the sparse store.
- WPF and WinForms expose opt-in `UseAdaptiveNavigationExtent` and public
  `ScrollCellIntoView` APIs.
- Left/Right/Up/Down, Enter and Tab navigation keep the active cell visible.
- The external Windows 11 demo enables the adaptive behavior and refreshes its
  visible scrollbars after selection, cell and worksheet changes.

## Validation

- Viewport: 58/58 passed.
- Focused loaded WPF/WinForms adaptive keyboard smoke: 2/2 passed.
- Core solution: 1243/1243 passed; build/analyzers 0 warnings, 0 errors.
- Architecture verification: passed.
- External demo build and internal smoke: passed.
- Supplied `Excel_Thuan Thanh 6789.xlsx` Load -> Save -> Load smoke: passed;
  source workbook remained unchanged.

## Boundaries

- Split panes retain the existing full physical extent; per-pane adaptive
  scrollbars need a separate contract.
- MAUI does not yet expose this host opt-in.
- Whole-row and whole-column style-only tails are not enumerable through the
  current public used-range sources.

## Status

Local implementation complete. Exact-head GitHub Actions evidence is pending.
