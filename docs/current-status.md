# NeraSpreadSheet current implementation status

This file is the handoff source of truth for the current development branch. A capability is classified as implemented only when executable source, automated tests and the applicable runtime gate exist.

## Product rules

NeraSpreadSheet is an independent spreadsheet SDK.

- No runtime dependency on Microsoft Excel, LibreOffice or DevExpress.
- No UNO/Excel command identifiers in Nera public contracts.
- No UI control per cell.
- Workbook, formulas, editing, layout, scrolling and commands remain independent from WPF, WinForms and MAUI.
- Viewports use continuous `double` pixel offsets and may stop between row/column boundaries.
- Desktop and GPU hosts consume the same workbook, viewport and display-list semantics.
- Document-format dependencies stay inside adapter projects; OpenXml types do not enter Core public contracts.

## Implemented

### Core workbook, formulas and editing

- Excel-size sparse worksheets, multiple worksheets, values, formulas, direct styles, dimensions and native merged ranges.
- Immutable snapshots and bounded caches for viewport/rendering work.
- Selection, clipboard, reusable editor, commands, sort and data/view Undo/Redo.
- Structural insert/delete/reorder with overflow preflight, formula/reference/rule/Table mapping and atomic rollback.
- Formula tokenizer, parser and AST for arithmetic, comparison, concatenation, A1 references/ranges and basic cross-sheet references.
- Dependency graph, circular-reference detection and affected-only recalculation.

### Sparse styles, viewport and rendering

- Sparse whole-row/column style patches compose chronologically without materializing logical axes.
- Fractional pixel scrolling, freeze panes, split panes, independent pane offsets and pane-local scrollbars.
- Snapshot/tile caching and split-aware dirty-region projection.
- WPF DrawingContext/D3DImage, WinForms GDI+/Direct2D/D3D11-DXGI and shared Skia/MAUI GPU rendering.
- Loaded desktop and MAUI Windows device/context recreation and scale/orientation gates.

### Shared formulas, Conditional Formatting and Data Validation

- Shared-formula import/export with relative, mixed and absolute A1 translation and deterministic fallback.
- `CellIs` and `Expression` Conditional Formatting with differential styles, priority, `StopIfTrue`, structural history, rendering and XLSX round-trip.
- Whole/decimal/date/time/text-length/list/custom Data Validation with candidate-aware evaluation, Stop/Warning/Information editor policy, diagnostics, rendering, structural history and standard XLSX round-trip.

### Table Core model and structured references

- `SpreadsheetTable` and `SpreadsheetTableColumn` use stable `Guid` identities and remain independent from UI/OpenXml.
- Table names are unique across a workbook; column names and identities are unique inside a Table; Tables on one worksheet cannot overlap.
- Header, data and totals ranges derive from one canonical Table range.
- Table state participates in immutable snapshots, structural state, rollback and production Undo/Redo.
- Supported structured references include `Table[Column]`, `#All`, `#Data`, `#Headers`, `#Totals`, `#This Row` and `[@Column]`.
- Structured references expand to absolute A1 references before the existing parser/evaluator runs.
- Expanded ranges enter the dependency graph and participate in affected-only recalculation.
- Table/column rename rewrites cell formulas and calculated/totals metadata across the workbook in one transaction.

Full contract: `docs/table-structured-reference-contract.md`.

### Calculated-column formula propagation

- `SpreadsheetTableFormulaProjection` projects each column's calculated formula across the Table data range.
- Structured formulas such as `=[@Quantity]*[@Price]` retain one metadata expression while every data row receives a formula cell.
- Relative/mixed/absolute A1 formulas are translated from the first data-row anchor to each destination row.
- Formula projection preserves existing cell styles and cached values when the formula text is unchanged.
- Changing formula metadata invalidates stale cached values before recalculation.
- Removing calculated metadata converts projected formula cells to their current values instead of discarding results.
- Add Table, metadata mutation, Undo and Redo restore Table metadata and projected cells together.
- Insert/delete/reorder followed by normal workbook recalculation fills newly created data rows automatically.
- Before projection, the engine normalizes the majority of existing projected formulas back to the first-row anchor. This preserves structurally rewritten A1 metadata and avoids a stale metadata formula overwriting correctly moved cells.
- Projection is bounded to `1,000,000` formula/label cells per operation. Oversized requests fail before logical-axis materialization and roll back without entering history.

### Totals-row execution and filter-aware SUBTOTAL

- Totals labels and formulas are projected into the canonical totals row.
- `SpreadsheetSession.Tables` exposes:
  - `SetCalculatedColumnFormula`;
  - `SetTotalsRowFormula`;
  - `SetTotalsRowLabel`;
  - `SetTotalsRowFunction`.
- Built-in totals functions generate standard structured `SUBTOTAL` formulas.
- Implemented function codes are `1/101` Average, `2/102` Count Numbers, `3/103` Count Nonblank, `4/104` Maximum, `5/105` Minimum and `9/109` Sum.
- Filtered-out Table rows are excluded for all supported codes.
- `SUBTOTAL` records both the referenced data range and every active filter-source column range as formula dependencies.
- Changing a filter-source cell therefore triggers affected-only recalculation of totals even when that cell is outside the aggregated column.
- Filter predicates may read formula-backed filter cells through the same recursive calculation context.
- Clearing a totals formula preserves its current result as a static value unless a totals label replaces it.

### AutoFilter and compressed row projection

- Table AutoFilter supports explicit value sets, blank matching and one/two comparison conditions combined with AND/OR.
- Multiple filtered columns combine by AND at row level.
- Adjacent filtered rows are compressed into sparse spans rather than per-row overrides.
- Filtered rows consume no viewport extent, do not create row slots and are skipped by hit-testing while original row sizes remain recoverable.
- Filter/source-value changes and Undo/Redo refresh row visibility.

### Standard XLSX Table interoperability

- Worksheet `tableParts/tablePart` relationships and standard `TableDefinitionPart` XML are imported/exported.
- Table names, ranges, header/totals state, columns, calculated/totals metadata, totals labels, styles and current AutoFilter predicates round-trip.
- Nera-generated packages encode stable Table and column identities; foreign packages receive deterministic fallback identities.
- Generated packages pass `OpenXmlValidator(FileFormatVersions.Office2013)`.
- Malformed relationships, ranges, IDs, column counts, filter indexes and unsupported Table/filter markup are rejected before workbook restoration.
- `PreserveUnknownParts=true` refreshes owned Table parts while retaining unowned worksheet/package content and Table `extLst` payloads across repeated saves.

### Package preservation and hardening

- Values, cached formulas, sheets, dimensions, merges, panes, style state, Conditional Formatting, Data Validation and Tables round-trip.
- Unknown-part preservation uses bounded atomic copy-and-patch.
- Nested opaque parts, drawing/image relationships, custom XML/properties and package-root relationships retain exact bytes/identity where owned semantics do not replace them.
- Package preflight checks part/relationship counts, IDs, types and unsafe URIs before restoration or destination mutation.

## Implemented but intentionally conservative

- Current `SUBTOTAL` support is limited to Average, Count Numbers, Count Nonblank, Maximum, Minimum and Sum.
- Codes `1–11` versus `101–111` currently differ only in their accepted function number. Manual hidden-row metadata is not modeled yet, so both code families exclude AutoFilter-hidden rows identically.
- Nested `SUBTOTAL`/`AGGREGATE` exclusion inside referenced ranges is not implemented yet.
- Calculated columns do not yet infer new metadata from arbitrary user edits to one formula cell; metadata changes should use `SpreadsheetSession.Tables`.
- Formula projection is intentionally bounded; a future virtual calculated-column representation may lift the current one-million-cell operation limit.
- Native WPF/WinForms/MAUI Table manager and filter dropdown presenters are not implemented.
- Direct worksheet AutoFilter outside Tables and rich date/text/top/bottom/color/icon filters are not implemented.
- Dynamic arrays, complete Excel-compatible function surface and plugin function SDK remain pending.
- Data Validation named/cross-sheet/external list semantics remain pending.
- Conditional Formatting color scales, data bars and icon sets remain pending.
- Unknown-part preservation requires stable worksheet topology and remains bounded to 512 MiB.
- Hosted CI cannot prove every physical GPU driver, monitor-DPI transition or OS-controlled context-loss mode.

## Progress estimate

- Engine/viewport/renderer foundation: approximately `89%`.
- Basic spreadsheet MVP: approximately `79–82%`.
- Complete professional roadmap: approximately `52%`.
- Production release readiness: approximately `27–31%`.

These are weighted engineering estimates, not checkbox counts.

## Next implementation work

1. Platform-neutral Table manager and filter-dropdown command/presenter contracts.
2. Native WPF/WinForms Table/filter presenters, followed by responsive MAUI UX.
3. Rich text/date/top/custom-list filters and direct worksheet AutoFilter.
4. External XLSX Table/AutoFilter compatibility corpus and differential tests.
5. Formula/function surface, dynamic arrays and plugin function SDK.
6. Advanced sorting, grouping, virtualized data and subtotals/outlines.
7. Printing/page layout/PDF, drawings/charts and pivot/slicers.
8. Accessibility, packaging, fuzzing, performance budgets and release hardening.

## Validation policy

- `NeraSpreadSheet.Core.slnx` must restore, build and test on cross-platform CI.
- `NeraSpreadSheet.slnx` must restore/build on Windows and all tests must pass.
- Architecture verification and Windows desktop GPU/runtime smoke are mandatory.
- MAUI changes require real platform builds; production lifecycle/input/scale claims require loaded native Windows gates.
- Calculated-column work must prove propagation, relative A1 translation, structural refill, exact Undo/Redo and bounded rollback.
- Totals work must prove filter-aware values and dependency tracking on filter-source columns.
- Table work must continue to prove schema-valid standard parts, malformed-input rejection and repeated preservation.
- PR #1 remains Draft and must not merge while exact-head CI is red or unknown.

## Latest validated implementation milestone

CI run #505 (`32446946544`) passed at implementation commit `819fc3c3f5f72ee89438834012d9090c6f6b7032` on August 21, 2026.

- Core restore/build/tests and architecture verification passed, including calculated-column projection, structural refill, projection-limit rollback, supported `SUBTOTAL` functions and filter dependency tracking.
- Existing Table/Structured Reference/AutoFilter, Data Validation, Conditional Formatting, shared formulas, sparse styles and package preservation regressions remained green.
- Full Windows build/tests and desktop GPU runtime smoke passed.
- MAUI Android, iOS and Mac Catalyst real-target builds passed.
- MAUI Windows build/tests and both loaded runtime smokes passed.

The PR remains Draft and has not been merged into `develop`.

## Independence rule

Excel, LibreOffice and DevExpress may be used only as external behavior/coverage references. Their runtime engines, command IDs and public types are not NeraSpreadSheet dependencies.
