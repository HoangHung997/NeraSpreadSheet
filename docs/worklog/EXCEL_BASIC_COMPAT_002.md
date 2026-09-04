# EXCEL-BASIC-COMPAT-002 worklog

## Scope

- Reproduce the user-reported recalculation regression on the supplied
  six-sheet workbook without modifying the source file.
- Fix reusable formula and WPF SDK behavior rather than hiding failures in the
  external demo.
- Match the observed Excel desktop editing and Ctrl+wheel zoom behavior closely
  enough for the Windows 11 demo.

## Diagnosis

- OpenXML load retained cached values correctly.
- The workbook contains 1273 formula cells and 28 cached Excel error cells.
- Before this patch, Nera full recalculation produced 981 errors: 953 were new.
- `VLOOKUP` propagated any error anywhere in a table array, including cells not
  inspected or returned by the lookup.
- Formulas such as `=VLOOKUP($C$15,DL!$B:$R,2,0)` failed because whole-column
  table-array syntax was not parsed.
- Direct observation of desktop Excel showed that the active editor uses the
  cell's Arial 11 typography and wraps within a tall wrapped cell rather than
  replacing it with a single-line host font editor.

## Implementation

- `VLOOKUP` and `HLOOKUP` now own path-aware error propagation.
- The formula parser recognizes whole-column ranges.
- Whole-column `VLOOKUP` asks the workbook context for sorted sparse used rows,
  evaluates only lookup/result cells on those rows and records the full-column
  dependency. A regression places source data at the final physical worksheet
  row to prove the axis is not materialized.
- WPF exposes `Zoom`, `ZoomChanged` and `ZoomByWheel`; Ctrl+wheel uses ten
  percentage-point steps from 25% through 400%.
- The single reusable WPF editor overlay inherits effective font, weight,
  italic/underline/color, horizontal/vertical alignment and wrapping.
  Alt+Enter inserts a line break while Enter retains commit-and-move behavior.
- The external demo coalesces Ribbon/menu/context-menu refreshes onto one
  background dispatcher callback and displays the current zoom percentage.

## Validation

- Supplied workbook before/fixed recalculation: **28 / 28 errors**, **0 new**.
- Formula suite: **524/524 passed**.
- Focused WPF formula/editor/zoom smoke: **2/2 passed**.
- Core solution: **1246/1246 passed**.
- Build/analyzers: **0 warnings, 0 errors**.
- Architecture verification: **passed**.
- External Win11 demo build and supplied-workbook smoke: **passed**.
- Full Windows.Rendering: **52/54 passed locally**; only the two previously
  documented desktop DPI/activation environment assertions remain red.

## Boundaries

- The 28 error cells already present in Excel are not rewritten or masked.
- Whole-column sparse evaluation is currently implemented for `VLOOKUP`, the
  concrete compatibility case. Other functions do not silently materialize a
  full worksheet axis.
- The external demo is outside the SDK repository; only reusable formula/WPF
  behavior and its contracts/tests are committed here.
- Exact-head GitHub Actions validation remains required.
