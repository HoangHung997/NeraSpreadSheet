# NeraSpreadSheet current implementation status

This file is the source of truth for the current development branch. A capability is implemented only when executable source, automated tests and the applicable build/runtime gate exist.

## Product rules

NeraSpreadSheet is an independent spreadsheet SDK.

- No runtime dependency on Microsoft Excel, LibreOffice or DevExpress.
- No native UI control per cell.
- Workbook, formulas, extensions, dynamic arrays, editing, layout, scrolling and printing remain independent from WPF, WinForms and MAUI.
- Viewports and print previews use continuous `double` pixel offsets.
- Document-format dependencies stay inside adapter projects; OpenXml types do not enter Core/formula public contracts.
- Spill children are derived output owned by one top-left formula.
- Extension functions must pass API, capability and state-policy validation before registration.

## Implemented

### Core workbook, editing and structure

- Excel-size sparse worksheets, multiple sheets, values, formulas, direct styles, dimensions and merged ranges.
- Immutable snapshots, bounded caches and sparse whole-axis styles.
- Selection, spill-aware clipboard, editor, commands, sort and data/view Undo/Redo.
- Structural insert/delete/reorder with overflow preflight, formula/reference/rule/Table/filter/spill mapping and atomic rollback.

### Formula parser and calculation

- Tokenizer, parser and AST for arithmetic, comparison, concatenation, A1 references/ranges, functions and basic cross-sheet references.
- Dependency graph, circular-reference detection and affected-only recalculation.
- Shared formulas, structured references, Table/column rewrite and calculated-column propagation.
- Public coercion/error layer including `#NUM!` and `#SPILL!` values.
- Lazy branches for `IF`, `IFERROR`, `IFNA`, `IFS`, `SWITCH` and `CHOOSE`.
- Current filter-aware `SUBTOTAL` and filter-source dependencies.

### Versioned Function Extension SDK v1.0

- Stable namespace/name identity.
- Independent semantic implementation version and host API version; current API is `1.0`.
- Exact lookup, highest-version resolution, side-by-side versions, exact replacement and unregister fallback.
- Global name/alias ownership and alias stability across versions.
- Scalar/range/array argument and scalar/array-return capability declarations.
- Deterministic/volatile/external-state metadata and pure/context-read-only/external-state classification.
- Engine-only or function-added dependency policy.
- Automatic/disabled argument-error propagation.
- Logical versus flattened argument-count policy.
- Immutable invocation arguments preserving range source identity and values.
- Public `FormulaValueCoercion` helpers.
- Thread-safe registry and bounded versions per identity.
- Fail-closed default policy for incompatible APIs, unsupported array capabilities and external-state functions.
- Legacy `IFormulaFunction` registration through a `LEGACY` adapter.

The eager built-in registry now contains **103 names**. The original 92 functions preserve flattened-value arity; the eleven statistical functions use logical arguments through SDK v1 metadata.

Full contract: `docs/function-extension-sdk-contract.md`.

### Conditional aggregate criteria and functions

- Shared invariant criteria parser with `=`, `<>`, `<`, `<=`, `>` and `>=`.
- Boolean, number, DateTime, error and text criteria.
- Ordinal case-insensitive text, wildcards and tilde escapes.
- Blank/non-blank matching and strict same-shape positional ranges.
- `COUNTIF`, `COUNTIFS`, `SUMIF`, `SUMIFS`, `AVERAGEIF`, `AVERAGEIFS`.
- Multiple criteria combine by AND.
- Matched aggregate errors propagate; unmatched errors are not inspected.
- Criteria, aggregate and expression dependencies enter the graph.
- Two-million positional-pass budget validated before enumeration.

Full contract: `docs/conditional-aggregates-contract.md`.

### Statistical Functions Foundation

Eleven deterministic/pure SDK v1 functions are implemented:

- `MEDIAN`;
- `MODE.SNGL`;
- `PERCENTILE.INC`;
- `QUARTILE.INC`;
- `VAR.P`, `VAR.S`;
- `STDEV.P`, `STDEV.S`;
- `RANK.EQ`;
- `LARGE`, `SMALL`.

Behavior includes:

- logical range/scalar argument boundaries;
- range values limited to numeric/date cells while scalar Boolean and invariant numeric text may coerce;
- a two-million-value safety limit;
- inclusive percentile interpolation and quartiles `0..4`;
- numerically stable online population/sample variance;
- explicit `#N/A`, `#NUM!`, `#DIV/0!` and `#VALUE!` outcomes;
- range dependency capture and affected-only recalculation;
- versioned descriptor tests for identity, API, capabilities, volatility, state and argument-count policy.

The scalar/reference surface now contains 121 built-in names: 103 eager plus 18 AST/reference-aware. Together with five dynamic-array names, the complete built-in formula subsystem recognizes **126 names**. User-registered extension functions are additional.

Full contract: `docs/statistical-functions-foundation-contract.md`.

### Dynamic Arrays Foundation

- Immutable rectangular row-major `FormulaArrayValue`, limited to one million cells.
- Stable spill owner/child identity in worksheets and immutable snapshots.
- Collision preflight for values, formulas, spills, merged ranges, Tables and worksheet bounds.
- Direct style-only cells do not block output; child styles survive materialization/resize/clear.
- Blocked output commits `#SPILL!` while retaining owner formula and blocker state.
- Atomic replacement and bounded eight-pass stabilization.
- `SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT`, `UNIQUE`.
- Source/range dependencies and committed-value dependent recalculation.
- Spill-aware edit, clear, clipboard, Undo/Redo and structural transforms.
- Dynamic-array-aware XLSX document save keeps owner formulas and removes derived child values/formulas.

Full contract: `docs/dynamic-arrays-contract.md`.

### Viewport and multi-host rendering

- Fractional pixel scrolling, freeze panes, split panes and independent pane offsets.
- Snapshot/tile caching and split-aware dirty projection.
- WPF DrawingContext/D3DImage, WinForms GDI+/Direct2D/D3D11-DXGI and Skia/MAUI GPU rendering.
- Loaded desktop and MAUI Windows device/context recreation and scale/orientation gates.
- Immutable snapshots expose spill ownership to host layers.

### Rules, Tables and filtering

- `CellIs`/`Expression` Conditional Formatting with differential styles, history, rendering and XLSX.
- Whole/decimal/date/time/text-length/list/custom Data Validation with editor policy, history and XLSX.
- Stable Table/column identities, calculated columns, structured references and filter-aware totals.
- Table and worksheet AutoFilter share value/blank/comparison predicates and compressed hidden-row spans.
- Generation-guarded paged filter sessions, bounded cache and native WPF/WinForms/MAUI foundations.

### XLSX, CSV/TSV, page layout and PDF

- XLSX values, cached formulas, sheets, dimensions, merges, panes, styles, rules, Tables, filters and package preservation.
- Print area/titles, margins, paper, orientation, scale, fit, centering, headings/gridlines and odd header/footer round-trip.
- Deterministic pagination, repeat titles, merged-cell protection and virtualized native preview foundations.
- Staged worksheet/workbook/print-ticket PDF and WPF/WinForms printer adapters.
- Streaming CSV/TSV with buffer-boundary quote handling, explicit type/formula policy, injection protection and staged output limits.

### Validation automation

- `scripts/run-complete-validation.ps1` runs broad Core, Windows and MAUI gates and emits JSON/TRX evidence.
- `docs/CODEX_FINAL_ACCEPTANCE.md` records external plugin, criteria/statistics, dynamic-array, printer/PDF, device, compatibility and fuzzing work that hosted CI cannot fully prove.

## Implemented but intentionally conservative

- Formula Surface I, conditional aggregates, statistics and Dynamic Arrays Foundation are not a complete Excel-compatible formula engine.
- SDK v1 does not yet load plugin packages, pin versions in formula text, verify publishers or isolate third-party code.
- Volatility metadata exists; automatic volatile scheduling is pending.
- Statistical comparison uses exact `double` equality for mode/rank ties.
- Exclusive percentile/quartile, multi-mode, rank-average, correlation/regression and probability distributions are pending.
- Conditional criteria parsing is invariant, not locale-specific.
- Spill-reference `A1#`, implicit intersection `@`, array constants and vectorized expressions are pending.
- Advanced dynamic arrays, LET/LAMBDA and higher-order functions are pending.
- Native spill-border/selection UX is pending.
- Advanced XLOOKUP modes, complete `SUBTOTAL`, financial, engineering, database and cube families are pending.
- Newly added native paths do not each have dedicated loaded interaction gates.
- XLSX manual breaks, first/even headers and arbitrary custom paper are pending.
- Physical printer drivers, independent PDF raster diff, font embedding/substitution and drawings/charts pagination remain pending.

## Weighted progress estimate

- Engine/viewport/renderer foundation: approximately `92%`.
- Basic spreadsheet MVP: approximately `95–97%`.
- Complete professional roadmap: approximately `68%`.
- Production release readiness: approximately `45–48%`.

These are engineering-weighted estimates, not checkbox counts.

## Next implementation work

1. Financial Functions Foundation.
2. Engineering/database functions and criteria-table support.
3. Advanced statistical distributions, correlation and regression.
4. Advanced lookup/reference and dynamic-array helpers.
5. Plugin packaging/discovery, API compatibility and isolation policy.
6. Native spill UX, drawings/images/charts and print/PDF pagination.
7. Advanced sort, grouping/outlines, virtual data, pivot tables and slicers.
8. Remaining printing/XLSX/PDF/font/external formula corpora.
9. MAUI IME/accessibility/localization/theme and release hardening.
10. Execute final Codex acceptance before promoting PR #1.

## Validation policy

- Core restore/build/tests and architecture verification are mandatory.
- Full Windows build/tests and desktop GPU smoke are mandatory.
- MAUI changes require Android/iOS/Mac Catalyst/Windows builds and applicable loaded native gates.
- SDK changes require API/version/capability/security/conflict/dependency compatibility tests.
- Formula-family changes require result, coercion, error, dependency, affected-recalculation, budget and descriptor tests.
- PR #1 remains Draft and must not merge while exact-head CI is red or unknown.

## Latest validated implementation milestone

Implementation commit `6aa9b1a05f7a370d393d3222b533b3bee0088c9a` passed CI `#779`, run `32636739544`, across Core, architecture, full Windows, desktop GPU, Android, iOS, Mac Catalyst and MAUI Windows loaded-runtime gates.

## Independence rule

Excel, LibreOffice and DevExpress are behavior/coverage references only. Their engines, command IDs, controls and public types are not NeraSpreadSheet dependencies.
