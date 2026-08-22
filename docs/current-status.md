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
- Native presenters translate platform input and focus only; production mutations continue through platform-neutral controllers and history.

## Implemented

### Core workbook, formulas and editing

- Excel-size sparse worksheets, multiple worksheets, values, formulas, direct styles, dimensions and native merged ranges.
- Immutable snapshots and bounded caches for viewport/rendering work.
- Selection, clipboard, reusable editor, commands, sort and data/view Undo/Redo.
- Structural insert/delete/reorder with overflow preflight, formula/reference/rule/Table/worksheet-filter mapping and atomic rollback.
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

Full model contract: `docs/table-structured-reference-contract.md`.

### Calculated-column formula propagation

- `SpreadsheetTableFormulaProjection` projects each column's calculated formula across the Table data range.
- Structured formulas retain one metadata expression while every data row receives a formula cell.
- Relative/mixed/absolute A1 formulas are translated from the first data-row anchor to each destination row.
- Formula projection preserves existing cell styles and cached values when formula text is unchanged.
- Changing formula metadata invalidates stale cached values before recalculation.
- Removing calculated metadata converts projected formula cells to their current values instead of discarding results.
- Add Table, metadata mutation, Undo and Redo restore Table metadata and projected cells together.
- Insert/delete/reorder followed by normal workbook recalculation fills newly created data rows automatically.
- Projection is bounded to `1,000,000` formula/label cells per operation and rolls back before logical-axis materialization when exceeded.

### Totals-row execution and filter-aware SUBTOTAL

- Totals labels and formulas are projected into the canonical totals row.
- `SpreadsheetSession.Tables` exposes calculated-column and totals metadata commands through production history.
- Built-in totals functions generate standard structured `SUBTOTAL` formulas.
- Implemented function codes are `1/101` Average, `2/102` Count Numbers, `3/103` Count Nonblank, `4/104` Maximum, `5/105` Minimum and `9/109` Sum.
- AutoFilter-hidden Table rows are excluded for all supported codes.
- `SUBTOTAL` records both the referenced data range and every active Table filter-source range as dependencies.
- Filter predicates may read formula-backed filter cells through the same recursive calculation context.

### Rich shared filter predicates

- Table and direct worksheet filters share one predicate implementation.
- Supported comparison predicates include equal/not-equal, greater/less variants, begins-with, ends-with, contains, does-not-contain, blank and nonblank.
- Supported relative date predicates include on/before/after date plus this/last/next week, month and year when an explicit reference date is supplied.
- Blank values are not coerced to zero or empty text during predicate evaluation.
- Multiple conditions may combine with AND or OR; multiple filtered columns combine with AND at row level.

### Direct worksheet AutoFilter

- A worksheet may own one direct `WorksheetAutoFilter` range independent from Tables.
- The range may exist with no active criteria and may include one header row plus a sparse data range.
- Filter columns use range-relative column offsets and reuse `TableFilterColumn` semantics.
- `SpreadsheetSession.WorksheetFilters` provides set-range, value-filter, custom-filter, clear-column, clear-criteria and remove-filter commands through production Undo/Redo.
- Structural insert/delete maps the filter range and column criteria; deleting only the header row is rejected before mutation.
- Axis reorder is allowed only when the entire filter range remains one uniform translation.
- Direct worksheet filters cannot overlap Tables or merged cells in the current conservative contract.
- Worksheet snapshots include the direct filter and invalidate its range when source values or criteria change.

### Compressed filtered-row projection

- Direct worksheet and Table filters share `WorksheetSnapshot.GetFilteredOutRowSpans()`.
- Adjacent hidden rows are compressed into sparse spans rather than per-row overrides.
- Spans from supported filter owners are merged safely before layout consumption.
- Filtered rows consume no viewport extent, do not create row slots and are skipped by hit testing while original row sizes remain recoverable.
- Filter/source-value changes and Undo/Redo refresh row visibility.

### Table manager and native presenters

- `SpreadsheetTablePresenterController` exposes a read-only active-worksheet Table manager snapshot using stable Table/column identities.
- Filter menus enumerate distinct values and occurrence counts from one immutable worksheet snapshot.
- Default bounds are `100,000` scanned data rows and `10,000` retained distinct values.
- Search is trimmed, ordinal-ignore-case substring matching and does not discard hidden selections.
- Select-all-visible and clear-visible affect only the current search projection.
- Value/custom filters and clear commands use production Table history.
- `SpreadsheetTableFilterNavigator` preserves active value identity across search/list rebuilding.
- WPF uses a native `Popup`, WinForms a native `ToolStripDropDown`, and MAUI a responsive overlay/bottom-sheet surface.
- Loaded native gates prove Apply, compressed visibility, Undo, Redo, reopen and focus release.

Full presenter contract: `docs/table-filter-presenter-contract.md`.

### Cancellable paged Table filter sessions

- `SpreadsheetTableFilterPagedSession` owns one generation-checked Table filter-menu snapshot for asynchronous native consumption.
- `RefreshAsync` cancels a prior refresh and only publishes when its generation is still current.
- `GetPageAsync` supports search, offset, bounded page size and cancellation while preserving the published generation.
- A worksheet mutation does not alter an already published immutable menu; the next refresh publishes a new generation.
- Disposing the session cancels refresh work and rejects later refresh/page calls.
- This is the platform-neutral paging foundation; WPF, WinForms and MAUI do not yet bind their native value lists to it with true virtualization.

### Standard XLSX Table interoperability

- Worksheet `tableParts/tablePart` relationships and standard `TableDefinitionPart` XML are imported/exported.
- Table names, ranges, header/totals state, columns, calculated/totals metadata, totals labels, styles and current AutoFilter predicates round-trip.
- Nera-generated packages encode stable Table and column identities; foreign packages receive deterministic fallback identities.
- Generated packages pass `OpenXmlValidator(FileFormatVersions.Office2013)`.
- Malformed relationships, ranges, IDs, column counts, filter indexes and unsupported Table/filter markup are rejected before workbook restoration.
- `PreserveUnknownParts=true` refreshes owned Table parts while retaining unowned worksheet/package content and Table `extLst` payloads across repeated saves.

### Standard XLSX worksheet AutoFilter interoperability

- Worksheet-level `autoFilter@ref`, `filterColumn@colId`, value filters, blank matching and one/two custom comparisons import/export through `OpenXmlWorksheetAutoFilterCodec`.
- Begins-with, ends-with, contains and does-not-contain map to supported SpreadsheetML leading/trailing `*` wildcard custom filters with escaped literal `~`, `*` and `?` characters.
- Empty equal/not-equal custom values map to blank/nonblank predicates.
- Generated worksheet filters pass Office 2013 schema validation.
- Duplicate `autoFilter`, invalid ranges/indexes, conflicting filter definitions, unsupported wildcard shapes and unsupported filter children are rejected before workbook restoration.
- `top10`, dynamic/date-group, color, icon and sort-state markup remain unsupported rather than being guessed.

### Worksheet AutoFilter preservation

- `PreserveUnknownParts=true` now refreshes Nera-owned worksheet `autoFilter` markup through a dedicated copy-and-patch step.
- Preservation keeps package/worksheet opaque parts and relationships unchanged.
- An existing AutoFilter `extLst` and namespaced attributes survive when Nera replaces filter semantics.
- Repeated preserved saves update the filter value on each save, retain opaque bytes and continue to pass OpenXml validation.
- The output package is fully constructed and validated before destination mutation, preserving existing save atomicity.

### Package preservation and hardening

- Values, cached formulas, sheets, dimensions, merges, panes, style state, Conditional Formatting, Data Validation, Tables and worksheet AutoFilter round-trip.
- Unknown-part preservation uses bounded atomic copy-and-patch.
- Nested opaque parts, drawing/image relationships, custom XML/properties and package-root relationships retain exact bytes/identity where owned semantics do not replace them.
- Package preflight checks part/relationship counts, IDs, types and unsafe URIs before restoration or destination mutation.

## Implemented but intentionally conservative

- Current `SUBTOTAL` support is limited to Average, Count Numbers, Count Nonblank, Maximum, Minimum and Sum.
- Codes `1–11` versus `101–111` currently differ only in accepted function number because manual hidden-row metadata is not modeled.
- Nested `SUBTOTAL`/`AGGREGATE` exclusion inside referenced ranges is not implemented.
- Calculated columns do not infer new metadata from arbitrary user edits to one formula cell.
- Formula projection is intentionally bounded; a future virtual calculated-column representation may lift the one-million-cell operation limit.
- Native value lists still use bounded materialized menu snapshots; paged session binding/virtualization is not complete.
- Direct worksheet AutoFilter does not yet have shared header-button geometry or native WPF/WinForms/MAUI entry points.
- Dynamic/date-group/top10/color/icon filter markup and sort state are not represented as first-class models.
- The Table manager is not yet a complete design/resize/style management UI.
- MAUI virtual-keyboard and IME lifecycle is not complete.
- Dynamic arrays, complete Excel-compatible function surface and plugin function SDK remain pending.
- Data Validation named/cross-sheet/external list semantics remain pending.
- Conditional Formatting color scales, data bars and icon sets remain pending.
- Unknown-part preservation requires stable worksheet topology and remains bounded to 512 MiB.
- Hosted CI cannot prove every physical GPU driver, monitor-DPI transition, screen-reader combination or OS-controlled context-loss mode.

## Progress estimate

- Engine/viewport/renderer foundation: approximately `90%`.
- Basic spreadsheet MVP: approximately `84–87%`.
- Complete professional roadmap: approximately `56%`.
- Production release readiness: approximately `32–35%`.

These are weighted engineering estimates, not checkbox counts.

## Next implementation work

1. Bind `SpreadsheetTableFilterPagedSession` to virtualized/paged WPF, WinForms and MAUI value lists with stale-request cancellation.
2. Add shared direct worksheet AutoFilter header-button geometry, target resolution and native entry points.
3. Add first-class XLSX `top10`, dynamic/date-group filters and `sortState` where semantics are fully modeled.
4. Complete Table design/resize/style manager UI.
5. MAUI virtual keyboard/IME lifecycle plus broader accessibility, high-contrast, localization and theme hardening.
6. External XLSX Table/AutoFilter compatibility corpus and differential tests.
7. Formula/function surface, dynamic arrays, plugin SDK, advanced sort/grouping, printing/PDF, charts and pivot/slicers.
8. Packaging, fuzzing, performance budgets and release hardening.

## Validation policy

- `NeraSpreadSheet.Core.slnx` must restore, build and test on cross-platform CI.
- `NeraSpreadSheet.slnx` must restore/build on Windows and all tests must pass.
- Architecture verification and Windows desktop GPU/runtime smoke are mandatory.
- Worksheet AutoFilter work must prove Core visibility/history/structural behavior, schema-valid XLSX round-trip and repeated preservation.
- Paged-session work must prove immutable generations, cancellation, refresh replacement and disposed-state rejection.
- MAUI changes require real Android/iOS/Mac Catalyst/Windows builds; production lifecycle/input/scale claims require loaded native Windows gates.
- PR #1 remains Draft and must not merge while exact-head CI is red or unknown.

## Latest validated implementation milestone

CI run `#586` (`32543422821`) passed at implementation commit `023835495a5c56aea19830aff299765808ab5598` on August 22, 2026.

- Core restore/build/tests and architecture verification passed.
- Rich filter, direct worksheet AutoFilter, structural mapping/history, paged-session and OpenXml/preservation tests passed.
- Full Windows build/tests and desktop GPU runtime smoke passed.
- MAUI Android, iOS and Mac Catalyst builds passed.
- MAUI Windows build/handler checks and loaded Table-filter, context-recreation and scale/orientation smokes passed.

The PR remains Draft and has not been merged into `develop`.

## Independence rule

Excel, LibreOffice and DevExpress may be used only as external behavior/coverage references. Their runtime engines, command IDs, controls and public types are not NeraSpreadSheet dependencies.