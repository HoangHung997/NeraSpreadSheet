# NeraSpreadSheet current implementation status

This file is the source of truth for the current development branch. A capability is implemented only when executable source, automated tests and the applicable build/runtime gate exist.

## Product rules

NeraSpreadSheet is an independent spreadsheet SDK.

- No runtime dependency on Microsoft Excel, LibreOffice or DevExpress.
- No native UI control per cell.
- Workbook, formulas, dynamic arrays, editing, layout, scrolling, printing and commands remain independent from WPF, WinForms and MAUI.
- Viewports and print previews use continuous `double` pixel offsets.
- Desktop and GPU hosts consume the same workbook, viewport and display-list semantics.
- Document-format dependencies stay inside adapter projects; OpenXml types do not enter Core public contracts.
- Native presenters translate platform input/focus only; production mutations use platform-neutral controllers and history.
- Dynamic-array children are derived output owned by one top-left formula, not independent formulas or clipboard values.

## Implemented

### Core workbook, editing and structure

- Excel-size sparse worksheets, multiple sheets, values, formulas, direct styles, dimensions and merged ranges.
- Immutable snapshots and bounded caches.
- Selection, spill-aware clipboard, reusable editor, commands, sort and data/view Undo/Redo.
- Structural insert/delete/reorder with overflow preflight, formula/reference/rule/Table/filter/spill mapping and atomic rollback.
- Sparse whole-row/column style patches without materializing logical axes.

### Formula parser, dependencies and Formula Surface I

- Tokenizer, parser and AST for arithmetic, comparison, concatenation, A1 references/ranges, functions and basic cross-sheet references.
- Dependency graph, circular-reference detection and affected-only recalculation.
- Shared-formula import/export, structured references, Table/column rename rewrite and calculated-column propagation.
- Shared coercion/error layer, including `#NUM!` and explicit `#SPILL!` mapping.
- **92 eager registry functions** plus **12 AST/reference-aware functions**, for **104 scalar/reference function names**.
- Logical/error/lazy functions, aggregates, math/rounding/trigonometry, text/Unicode, date/time and basic lookup/reference functions.
- `TODAY`/`NOW` can use a deterministic clock context.
- `INDEX`, `MATCH`, `XLOOKUP`, `VLOOKUP` and `HLOOKUP` capture inspected ranges as dependencies.
- Numeric aggregates propagate formula errors; `MIN/MAX` return `0` on a numeric-empty set, while `AVERAGE` returns `#DIV/0!`.
- Existing filter-aware `SUBTOTAL` and filter-source dependencies remain intact.

Full contract: `docs/formula-surface-i-contract.md`.

### Dynamic Arrays Foundation

- Immutable, rectangular, row-major `FormulaArrayValue` with a one-million-cell safety limit.
- Stable spill owner/child identity through `FormulaSpillRange`, worksheet APIs and immutable snapshots.
- Spill preflight rejects non-blank values, formulas, other spills, merged ranges, Tables and worksheet-bound overflow.
- Direct style-only target cells do not block a spill; direct child styles survive materialization, resize and clear.
- Blocked output commits `#SPILL!` to the owner without changing the blocker or losing the owner formula.
- Atomic materialization/replacement clears obsolete derived children and replaces ownership only after complete preflight.
- Supported functions: `SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT` and `UNIQUE`.
- The complete formula subsystem therefore recognizes **109 names**: 104 Formula Surface I names plus five dynamic-array names.
- Scalar compatibility returns the array top-left value while preserving source dependencies.
- Dynamic-array calculation records source/range dependencies, performs affected-only dependent recalculation and is bounded to eight stabilization passes.
- Committed spill values are frozen during dependent recalculation so consumers observe the materialized owner/children, including `#SPILL!`.
- Source edits may resize output; clearing a blocker allows recovery.
- Spill children cannot be edited directly; partial clear/copy/cut and paste-over-spill are rejected before history mutation.
- Copying a complete spill stores the owner formula once and omits derived child values while preserving direct child styles.
- Undo/Redo restores owner formulas and rematerializes output.
- Row/column structural changes remove derived children from canonical state, transform owner formulas, then rematerialize mapped output.
- `NeraOpenXmlDocumentSerializer` retains owner formulas, omits derived child values/formulas, preserves direct child styles and supports load-then-recalculate rematerialization.

Full contract: `docs/dynamic-arrays-contract.md`.

### Fractional viewport and multi-host rendering

- Fractional pixel scrolling, freeze panes, split panes, independent pane offsets and pane-local scrollbars.
- Snapshot/tile caching and split-aware dirty-region projection.
- WPF DrawingContext/D3DImage, WinForms GDI+/Direct2D/D3D11-DXGI and shared Skia/MAUI GPU rendering.
- Loaded desktop and MAUI Windows device/context recreation and scale/orientation gates.
- Worksheet snapshots expose immutable spill owner/child metadata to host/rendering layers without consulting mutable worksheet state.

### Conditional Formatting and Data Validation

- `CellIs` and `Expression` Conditional Formatting with differential styles, priority, `StopIfTrue`, structural history, shared rendering and XLSX round-trip.
- Whole/decimal/date/time/text-length/list/custom Data Validation with candidate-aware evaluation, editor policy, diagnostics, rendering, history and XLSX round-trip.

### Tables, structured references and totals

- Stable Table/column `Guid` identities, workbook-unique Table names and per-Table unique column names.
- Header/data/totals ranges from one canonical Table range.
- Table state in snapshots, structural state, rollback and production Undo/Redo.
- Structured references including `Table[Column]`, `#All`, `#Data`, `#Headers`, `#Totals`, `#This Row` and `[@Column]`.
- Atomic Table/column rename rewrite for formulas and calculated/totals metadata.
- Calculated formula projection and current filter-aware totals.

### Table and direct worksheet AutoFilter

- Table and worksheet filters share value/blank/custom-comparison predicates.
- Multiple columns combine by AND at row level.
- Hidden rows are compressed into sparse spans and removed from viewport extent/hit testing.
- Direct worksheet filters participate in structural mapping, rollback, production history and standard worksheet `autoFilter` round-trip.
- Generation-guarded paged sessions reject stale reads/mutations and support cancellable search/refresh.
- Shared Table/worksheet button geometry and WPF/WinForms/MAUI page-based presenter foundations.
- Apply/Clear continues through production history.

### XLSX interoperability and preservation

- Values, cached formulas, sheets, dimensions, merges, panes, styles, Conditional Formatting, Data Validation, Tables and current filters round-trip.
- Standard Table parts and worksheet filter relationships are imported/exported.
- The dynamic-array-aware document serializer removes derived spill children while retaining owner formula and direct styles.
- Unknown package-part preservation uses bounded atomic copy-and-patch.
- Nested opaque parts, drawing/image relationships, custom XML and package-root relationships retain bytes/identity where owned semantics do not replace them.
- Package preflight validates counts, IDs, types and unsafe URIs.

### Page setup, pagination and printing

- Worksheet-associated print area, paper, orientation, margins, scale, fit-to-page, repeated titles, manual breaks, centering, grid/headings and odd header/footer settings.
- Deterministic pagination from immutable snapshots.
- Automatic page breaks avoid merged-cell splits; manual breaks through merges are rejected.
- Printable page grids and header/footer token formatting.
- Shared print display-list composition.
- Page ranges, odd/even parity, reverse order, copies and collated/uncollated sequencing.
- Print-job Begin/Write/Complete/Abort lifecycle.
- Virtualized preview layout/session with continuous offsets, anchored zoom and bounded page cache.
- Native WPF/WinForms preview controls and MAUI Skia preview foundation.
- WPF `DocumentPaginator` and WinForms `PrintDocument` adapters.

### PDF export

- Single worksheet, selected workbook worksheets and print-ticket sequence export.
- Shared print display list and page plan.
- Staged destination replacement.
- Page count, dimensions and byte limits.
- Byte overflow reported after native Skia callbacks return.
- Cancellation/output-limit failures preserve existing seekable destinations.

### XLSX print settings

- `_xlnm.Print_Area` and `_xlnm.Print_Titles`.
- Margins, paper codes, orientation, scale, fit width/height, centering, grid/headings and odd header/footer round-trip.
- Schema-order-safe insertion and repeated unknown-part preservation.

### CSV and TSV

- Configurable delimiter, quote, newline, culture and encoding.
- BOM detection, CR/LF/CRLF, quoted delimiter/newline and doubled-quote handling.
- Parser state survives escaped quotes and CRLF across buffer boundaries.
- Optional number/Boolean/date inference and explicit formula import.
- Value/formula export, sparse used range and cancellation.
- Formula-like text protection by default.
- Row/column/cell limits and staged atomic export with a default 512-MiB budget.

### Validation automation

- `scripts/run-complete-validation.ps1` runs broad Core, Windows and MAUI gates and writes JSON.
- `docs/CODEX_FINAL_ACCEPTANCE.md` records target hardware, large-array performance, external dynamic-array compatibility, real printer, independent PDF validation, fonts, physical DPI, mobile IME/accessibility, compatibility corpora and fuzzing work that hosted CI cannot fully prove.

## Implemented but intentionally conservative

- Formula Surface I and Dynamic Arrays Foundation are broad but not complete Excel-compatible formula implementations.
- Spill-reference syntax such as `A1#` and implicit-intersection `@` are pending.
- Array constants and arbitrary vectorized binary expressions are pending.
- `SORT` currently supports one key; locale-specific comparison and complete Excel ordering/coercion rules are pending.
- Advanced arrays such as `SORTBY`, `TAKE`, `DROP`, `CHOOSECOLS`, `CHOOSEROWS`, `TOCOL`, `TOROW`, `HSTACK` and `VSTACK` are pending.
- `LET`, `LAMBDA`, `MAP`, `REDUCE`, `SCAN`, `MAKEARRAY`, `BYROW` and `BYCOL` are pending.
- There is not yet a dedicated spill-border/selection affordance on every native host.
- Full Microsoft Office dynamic-array extension metadata and third-party cached-spill conventions remain external corpus work.
- Range flattening does not preserve every coercion distinction between literal arguments and referenced values.
- Core currently collapses empty text to blank.
- Date arithmetic uses `.NET DateTime`/OLE Automation conversion and does not emulate Excel's fictional 1900-02-29.
- Approximate lookup assumes correctly sorted source data; advanced XLOOKUP modes and wildcard matching are pending.
- `TODAY`/`NOW` do not yet add automatic volatile recalculation scheduling.
- A versioned plugin function SDK is pending.
- Conditional aggregates, statistical, financial, engineering and database functions are pending.
- Current `SUBTOTAL` surface is incomplete and does not fully model manual hidden rows or nested subtotal exclusion.
- Newly added paged-filter/preview paths do not each have dedicated loaded interaction gates.
- XLSX does not yet round-trip manual page breaks, first/even headers/footers or arbitrary custom paper dimensions.
- Physical printer hard margins/driver behavior remain final hardware acceptance work.
- Independent PDF validation, raster visual diff and font embedding/substitution remain pending.
- Drawings/charts do not yet paginate.
- Unknown-part preservation requires stable worksheet topology and remains bounded to 512 MiB.

## Weighted progress estimate

- Engine/viewport/renderer foundation: approximately `92%`.
- Basic spreadsheet MVP: approximately `92–94%`.
- Complete professional roadmap: approximately `64%`.
- Production release readiness: approximately `41–44%`.

These are engineering-weighted estimates, not checkbox counts.

## Next implementation work

1. Versioned function-extension SDK: identity, capabilities, deterministic/volatile classification, registration and compatibility gates.
2. Conditional aggregate families: `SUMIF(S)`, `COUNTIF(S)` and `AVERAGEIF(S)`.
3. Statistical, financial, engineering and database function families.
4. Advanced dynamic-array syntax/functions, spill-reference operator and host spill UX.
5. Drawings/images/charts model and print/PDF pagination.
6. Advanced sort, grouping/outlines, virtual data, pivot tables and slicers.
7. Remaining printing/XLSX semantics and independent PDF/font/visual-diff corpus.
8. Complete Table design/resize/style manager and richer filter markup.
9. MAUI IME/virtual keyboard, accessibility, localization/theme and release hardening.
10. Execute `docs/CODEX_FINAL_ACCEPTANCE.md` before promoting PR #1.

## Validation policy

- Core restore/build/tests and architecture verification are mandatory.
- Full Windows build/tests and desktop GPU/runtime smoke are mandatory.
- MAUI changes require Android/iOS/Mac Catalyst/Windows builds; production lifecycle claims require loaded native gates.
- Formula changes require error/coercion, lazy branch, range dependency, date/time, lookup and dynamic-array regressions.
- Dynamic-array changes require shape/limit, collision, dependency, history, structure, clipboard, snapshot and XLSX boundary tests.
- XLSX changes require schema-valid round-trip, malformed-input rejection and repeated preservation tests.
- PR #1 remains Draft and must not merge while exact-head CI is red or unknown.

## Latest validated implementation milestone

Implementation commit `705afb46f05e687a7ee13147e6ed106b82944c04` passed CI `#746`, run `32624762199`, across Core, architecture, Windows, desktop GPU, Android, iOS, Mac Catalyst and MAUI Windows loaded-runtime gates.

## Independence rule

Excel, LibreOffice and DevExpress are external behavior/coverage references only. Their engines, command IDs, controls and public types are not NeraSpreadSheet dependencies.
