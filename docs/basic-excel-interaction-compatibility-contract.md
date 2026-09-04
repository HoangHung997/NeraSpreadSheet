# Basic Excel interaction and conditional-format compatibility contract

## Purpose

This contract defines the first reusable SDK behavior needed by a host that
wants basic Excel-like formula editing and tolerant import of real-world XLSX
conditional formatting. It does not define the demo application's sheet-tab or
scrollbar layout.

## XLSX differential-style import

- Differential `patternFill` markup may omit `patternType` when Excel supplies
  a foreground or background color. Nera imports that color as a visible solid
  fill.
- A solid differential fill may use either `fgColor` or `bgColor`; foreground
  color wins when both are present.
- Strict import continues to reject unsupported differential styles and
  conditional-format rule types.
- With `PreserveUnknownParts = true`, unsupported differential styles and rule
  types do not block workbook load. Unsupported rules remain opaque package
  content and are preserved on save.
- If one preserved worksheet contains an unsupported conditional-format rule,
  the preservation merge retains the original workbook `dxfs` table and all
  original worksheet `conditionalFormatting` elements as one consistent set.
  This avoids corrupting `dxfId` references. Editing those conditional-format
  rules is intentionally unavailable in that save cycle.

## Formula editing assistance

- `SpreadsheetFormulaEditingAssistant` exposes case-insensitive suggestions
  from the built-in formula registry and its aliases.
- Suggestions replace the identifier at the caret and add `(` when needed.
- Point mode inserts an A1 cell/range reference at the caret and replaces that
  provisional reference while the pointer drag changes.
- Cross-sheet references quote and escape worksheet names.
- `FormulaReferenceAnalyzer` extracts static references without recalculating
  the workbook. Dynamic references produced by functions such as `INDIRECT`
  still require formula evaluation.
- Exact `VLOOKUP`/`HLOOKUP` evaluation does not fail merely because an
  unrelated cell elsewhere in the table array contains an error. Errors in the
  lookup key, inspected lookup path or selected result still propagate.
- `VLOOKUP` accepts whole-column table arrays such as `DL!$B:$R`. Workbook
  calculation enumerates sparse used-row identities while retaining the full
  column dependency, so a far sparse row does not materialize an entire axis.

## Rendering and WPF host behavior

- Formula-reference outlines are appended to the existing display list; the
  cell body stays a nested display-list reference and is not flattened.
- Only reference edges intersecting the visible body are emitted.
- Selecting a formula cell outlines its same-sheet static precedents using a
  rotating theme color palette.
- The WPF in-cell editor shows function suggestions and accepts mouse drag
  point mode while keeping one reusable editor overlay.
- The WPF editor inherits the effective cell font, weight, style, color,
  alignment and wrapping. `Alt+Enter` inserts a line break; plain `Enter`
  commits and moves down.
- WPF `Ctrl+mouse-wheel` changes spreadsheet zoom in ten percentage-point
  steps between 25% and 400%. The reusable control exposes `Zoom`,
  `ZoomChanged` and `ZoomByWheel` for host chrome such as a status indicator.
- No native control is created per cell.

## Explicitly outside this change

- Visible horizontal/vertical scrollbar chrome in a particular demo app.
- Excel-style horizontal sheet tabs and their reordering UI.
- Semantic editing of unsupported third-party conditional-format rule types.
- Full formula-token coloring, structured-reference point mode and dynamic
  reference discovery before evaluation.
