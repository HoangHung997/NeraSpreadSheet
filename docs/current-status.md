# NeraSpreadSheet current implementation status

This file is the source of truth for the current development branch. A capability is implemented only when executable source, automated tests and the applicable build/runtime gate exist.

## Product rules

NeraSpreadSheet is an independent spreadsheet SDK.

- No runtime dependency on Microsoft Excel, LibreOffice or DevExpress.
- No native UI control per cell.
- Workbook, formulas, extension functions, dynamic arrays, editing, layout, scrolling, printing and commands remain independent from WPF, WinForms and MAUI.
- Viewports and print previews use continuous `double` pixel offsets.
- Desktop and GPU hosts consume the same workbook, viewport and display-list semantics.
- Document-format dependencies stay inside adapter projects; OpenXml types do not enter Core/formula public contracts.
- Native presenters translate platform input/focus only; production mutations use platform-neutral controllers and history.
- Spill children are derived output owned by one top-left formula.
- Extension functions must pass API, capability and state-policy validation before registration.

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
- Shared formulas, structured references, Table/column rename rewrite and calculated-column propagation.
- Shared coercion/error layer, including `#NUM!` and `#SPILL!` mapping.
- Logical/error/lazy functions, aggregates, math/rounding/trigonometry, text/Unicode, date/time and basic lookup/reference functions.
- `TODAY`/`NOW` can use a deterministic clock context and are described as volatile/context-read-only functions.
- `INDEX`, `MATCH`, `XLOOKUP`, `VLOOKUP` and `HLOOKUP` capture inspected ranges as dependencies.
- Numeric aggregates propagate errors; counting/information functions retain explicit non-propagating contracts.
- Current filter-aware `SUBTOTAL` and filter-source dependencies remain intact.

### Versioned Function Extension SDK v1.0

- Stable `FormulaFunctionIdentity` with normalized namespace/name.
- Independent semantic implementation version and host API version; current API is `1.0`.
- Ordered version values, exact lookup and highest-version name resolution.
- Side-by-side version registration, exact replacement and unregister fallback.
- Global name/alias ownership and alias stability across versions.
- Descriptor capabilities for scalar/range/array arguments and scalar/array returns.
- Deterministic/volatile/external-state metadata.
- Pure/context-read-only/external-state classification.
- Engine-only or function-added dependency policy.
- Automatic or disabled argument-error propagation.
- Logical versus flattened argument-count policy.
- Immutable invocation arguments preserving range source identity and values.
- Public shared `FormulaValueCoercion` helpers for extension authors.
- Thread-safe registry reads/writes and bounded versions per identity.
- Fail-closed default policy rejects incompatible host APIs, unsupported array capabilities and external-state functions.
- The 92 eager built-ins are described as `NERA.BUILTIN`, version `1.0.0`, API `1.0`.
- Legacy `IFormulaFunction` registration remains source-compatible through a `LEGACY` adapter.
- Built-in/legacy flattened-range arity is preserved, while new SDK functions default to logical argument counting.

Full contract: `docs/function-extension-sdk-contract.md`.

### Conditional aggregate criteria and functions

- Shared `FormulaCriteria` supports `=`, `<>`, `<`, `<=`, `>` and `>=`.
- Invariant error, Boolean, finite-number, DateTime and text operand parsing.
- Ordinal case-insensitive text comparison.
- `*` and `?` wildcards plus tilde escapes.
- Explicit blank/non-blank and formula-error matching.
- Criteria limited to 1,024 characters; wildcard regex is culture-invariant, non-backtracking and time-bounded.
- Implemented `COUNTIF`, `COUNTIFS`, `SUMIF`, `SUMIFS`, `AVERAGEIF`, `AVERAGEIFS`.
- Strict same-shape positional range rules.
- Multiple criteria combine by AND.
- Matched aggregate errors propagate; unmatched errors are not inspected.
- Sum without a match returns zero; average without a numeric match returns `#DIV/0!`.
- Criteria ranges, aggregate ranges and criterion-expression cells all enter the dependency graph.
- Affected-only recalculation responds to both criteria and aggregate edits.
- Work is bounded to two million positional range passes per evaluation and rejected before enumeration with `#NUM!`.

These six names extend the scalar/reference surface to 110 names. Together with five dynamic-array names, the complete built-in formula subsystem recognizes **115 names**. User-registered SDK functions are not included in this count.

Full contract: `docs/conditional-aggregates-contract.md`.

### Dynamic Arrays Foundation

- Immutable rectangular row-major `FormulaArrayValue`, limited to one million cells.
- Stable spill owner/child identity through `FormulaSpillRange`, worksheet APIs and immutable snapshots.
- Spill preflight rejects non-blank values, formulas, other spills, merged ranges, Tables and worksheet-bound overflow.
- Direct style-only cells do not block a spill; child styles survive materialization, resize and clear.
- Blocked output commits `#SPILL!` while retaining the owner formula and blocker state.
- Atomic materialization/replacement clears obsolete derived children only after complete preflight.
- Supported dynamic functions: `SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT`, `UNIQUE`.
- Source/range dependencies enter the shared graph and affected-only stabilization is bounded to eight passes.
- Committed spill values are frozen during dependent recalculation.
- Spill children cannot be edited directly; partial clear/copy/cut and paste-over-spill are rejected before history mutation.
- Undo/Redo, row/column structural changes and clipboard copy/paste rematerialize output from the owner formula.
- The dynamic-array-aware document serializer retains owner formulas, removes derived child values/formulas and preserves direct child styles.

Full contract: `docs/dynamic-arrays-contract.md`.

### Fractional viewport and multi-host rendering

- Fractional pixel scrolling, freeze panes, split panes, independent pane offsets and pane-local scrollbars.
- Snapshot/tile caching and split-aware dirty-region projection.
- WPF DrawingContext/D3DImage, WinForms GDI+/Direct2D/D3D11-DXGI and shared Skia/MAUI GPU rendering.
- Loaded desktop and MAUI Windows device/context recreation and scale/orientation gates.
- Immutable snapshots expose spill owner/child metadata without consulting mutable worksheet state.

### Conditional Formatting and Data Validation

- `CellIs` and `Expression` Conditional Formatting with differential styles, priority, `StopIfTrue`, structural history, shared rendering and XLSX round-trip.
- Whole/decimal/date/time/text-length/list/custom Data Validation with candidate-aware evaluation, editor policy, diagnostics, rendering, history and XLSX round-trip.

### Tables, structured references and totals

- Stable Table/column `Guid` identities, workbook-unique Table names and unique column names.
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
- The document serializer removes derived spill children while retaining owner formula and direct styles.
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
- `docs/CODEX_FINAL_ACCEPTANCE.md` records target hardware, plugin packaging/isolation, criteria compatibility, large-array performance, real printer, PDF validation, fonts, physical DPI, mobile IME/accessibility, corpora and fuzzing work that hosted CI cannot fully prove.

## Implemented but intentionally conservative

- Formula Surface I, conditional aggregates and Dynamic Arrays Foundation are broad but not complete Excel-compatible formula implementations.
- SDK v1 does not yet load plugin assemblies, pin versions in formula text, verify publisher signatures or isolate third-party code.
- The default SDK registry rejects external-state and array-capable extensions.
- Volatility metadata exists, but automatic volatile recalculation scheduling is pending.
- Conditional criteria parsing is invariant, not locale-specific.
- Conditional aggregate ranges must be canonical cell/range references and have identical shape.
- Wildcard/coercion edge cases beyond the documented contract remain external corpus work.
- Spill-reference syntax `A1#`, implicit intersection `@`, array constants and arbitrary vectorized expressions are pending.
- Advanced arrays and LET/LAMBDA/higher-order array functions are pending.
- `SORT` currently supports one key; complete locale-specific ordering is pending.
- There is no dedicated spill-border/selection affordance on every native host.
- Full Microsoft Office dynamic-array metadata remains external corpus work.
- Approximate lookup assumes correctly sorted source data; advanced XLOOKUP modes are pending.
- Current `SUBTOTAL` surface does not fully model manual hidden rows or nested subtotals.
- Statistical, financial, engineering, database and cube function families are pending.
- Newly added filter/preview paths do not each have dedicated loaded interaction gates.
- XLSX manual breaks, first/even headers/footers and arbitrary custom paper are pending.
- Physical printer hard margins/driver behavior remain final hardware acceptance work.
- Independent PDF raster diff and font embedding/substitution remain pending.
- Drawings/charts do not yet paginate.
- Unknown-part preservation requires stable worksheet topology and remains bounded to 512 MiB.

## Weighted progress estimate

- Engine/viewport/renderer foundation: approximately `92%`.
- Basic spreadsheet MVP: approximately `94–96%`.
- Complete professional roadmap: approximately `66%`.
- Production release readiness: approximately `43–46%`.

These are engineering-weighted estimates, not checkbox counts.

## Next implementation work

1. Statistical functions foundation.
2. Financial functions foundation.
3. Engineering/database functions and criteria-table support.
4. Advanced lookup/reference and dynamic-array helpers.
5. Native spill UX, drawings/images/charts and print/PDF pagination.
6. Advanced sort, grouping/outlines, virtual data, pivot tables and slicers.
7. Plugin packaging/discovery, API compatibility, isolation policy and release tooling.
8. Remaining printing/XLSX semantics and external formula/PDF/font corpora.
9. MAUI IME/accessibility/localization/theme and release hardening.
10. Execute `docs/CODEX_FINAL_ACCEPTANCE.md` before promoting PR #1.

## Validation policy

- Core restore/build/tests and architecture verification are mandatory.
- Full Windows build/tests and desktop GPU/runtime smoke are mandatory.
- MAUI changes require Android/iOS/Mac Catalyst/Windows builds; production lifecycle claims require loaded native gates.
- Function SDK changes require API/version/capability/security, conflicts, dependencies and backward-compatibility tests.
- Conditional aggregate changes require criteria, shapes, errors, dependencies, affected recalculation and budget tests.
- Dynamic-array changes require shape/limit, collision, dependency, history, structure, clipboard, snapshot and XLSX tests.
- XLSX changes require schema-valid round-trip, malformed-input rejection and repeated preservation tests.
- PR #1 remains Draft and must not merge while exact-head CI is red or unknown.

## Latest validated implementation milestone

Implementation commit `19e749473ce68f0b67b110ba70b37339a4c7e155` passed CI `#772`, run `32633548509`, across Core, architecture, Windows, desktop GPU, Android, iOS, Mac Catalyst and MAUI Windows loaded-runtime gates.

## Independence rule

Excel, LibreOffice and DevExpress are external behavior/coverage references only. Their engines, command IDs, controls and public types are not NeraSpreadSheet dependencies.
