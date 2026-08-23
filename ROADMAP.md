# NeraSpreadSheet roadmap

`docs/current-status.md` is the implementation source of truth. A capability is complete only after executable source, automated tests and the applicable build/runtime gate pass.

## A. Independent spreadsheet engine

- [x] Excel-size sparse workbook/worksheet model.
- [x] Values, formulas, merges, dimensions and immutable snapshots.
- [x] Selection, spill-aware clipboard, editor, commands and data/view Undo/Redo.
- [x] Atomic structural insert/delete/reorder with formula/rule/Table/filter/spill mapping.
- [x] Sparse whole-row/column styles.
- [x] Conditional Formatting and Data Validation with structural history.
- [x] Table model with stable IDs, calculated/totals metadata and history.
- [x] Worksheet-associated page setup/print area.
- [x] Immutable formula arrays and spill ownership.
- [ ] Sparse manual hide/group/outline metadata and complete axis property model.
- [ ] Print settings in structural history and complete named-range integration.

## B. Viewport, rendering and preview

- [x] Continuous fractional-pixel scrolling.
- [x] Freeze/split panes and independent pane scrolling.
- [x] Shared headers, resize, selection, editor and drag reorder.
- [x] Snapshot/tile caching and split-aware dirty regions.
- [x] WPF/WinForms software and GPU backends.
- [x] Shared Skia renderer and native MAUI GPU host.
- [x] Conditional/validation overlays and compressed filter spans.
- [x] Shared Table/worksheet filter-button geometry.
- [x] Shared print display list and virtualized preview foundations.
- [x] Immutable spill identity in snapshots.
- [x] Loaded context-recreation and scale/orientation gates.
- [ ] Native spill-border/selection UX on every host.
- [ ] Dedicated loaded interaction gates for every new filter/preview/spill path.
- [ ] Enforced 60/120-Hz, 4K and large-array hardware budgets.

## C. Formula engine and extension SDK

- [x] Tokenizer, parser, AST, dependency graph and circular-reference policy.
- [x] Arithmetic, comparison, concatenation and A1 references/ranges.
- [x] Shared formulas and structured references.
- [x] Atomic Table/column formula rewrite.
- [x] Calculated columns and current filter-aware `SUBTOTAL`.
- [x] Shared coercion/error layer, including `#NUM!` and `#SPILL!`.
- [x] Logical/error/lazy-control functions.
- [x] Aggregate/information functions.
- [x] Math, rounding, logarithmic and trigonometric functions.
- [x] Text/search/replace/Unicode functions.
- [x] Date/time functions and deterministic clock context.
- [x] Basic `INDEX`, `MATCH`, `XLOOKUP`, `VLOOKUP`, `HLOOKUP`.
- [x] `COUNTIF(S)`, `SUMIF(S)`, `AVERAGEIF(S)` and shared criteria engine.
- [x] Immutable dynamic arrays and spill collision/dependency/history contracts.
- [x] `SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT`, `UNIQUE`.
- [x] Versioned Function Extension SDK API `1.0`.
- [x] Stable identity, versions, aliases and conflict policy.
- [x] Capability, volatility, state and dependency declarations.
- [x] Logical/range invocation metadata and public coercion helpers.
- [x] Legacy registration and flattened-range arity compatibility.
- [ ] Statistical functions foundation.
- [ ] Financial functions foundation.
- [ ] Engineering, database and cube function families.
- [ ] Advanced lookup/reference modes and locale-aware formatting.
- [ ] Spill-reference `A1#`, implicit intersection `@`, array constants and vectorized expressions.
- [ ] Advanced dynamic arrays and LET/LAMBDA/higher-order functions.
- [ ] Array-returning function extensions.
- [ ] Formula-text version pinning and plugin package manifests.
- [ ] Plugin discovery/loading, signatures, publisher policy and isolation.
- [ ] Automatic volatile recalculation scheduling.
- [ ] Complete Excel coercion/criteria compatibility and fuzzing.

## D. XLSX, page layout and PDF

- [x] Values, formulas, sheets, dimensions, merges, panes and styles.
- [x] Unknown package-part copy-and-patch preservation.
- [x] Shared formulas, Conditional Formatting and Data Validation round-trip.
- [x] Standard Table parts and current AutoFilter round-trip.
- [x] Print margins, paper, orientation, scale, fit and odd header/footer.
- [x] `_xlnm.Print_Area` and `_xlnm.Print_Titles`.
- [x] Deterministic pagination and merged-cell protection.
- [x] Staged PDF for worksheet/workbook/print tickets.
- [x] WPF paginator and WinForms `PrintDocument`.
- [x] Dynamic-array-aware owner/child XLSX document boundary.
- [ ] Full Office dynamic-array extension metadata and external producer corpus.
- [ ] XLSX manual breaks, first/even headers and arbitrary custom paper.
- [ ] Independent PDF validation/raster diff and font policy.
- [ ] First-class drawings/images/charts and print/PDF pagination.
- [ ] Physical printer capability and hard-margin negotiation.

## E. Data and analysis

- [x] Basic bounded in-memory sort.
- [x] Current Data Validation evaluator and editor gate.
- [x] Table/direct worksheet AutoFilter predicates.
- [x] Table operations and filter-aware totals with Undo/Redo.
- [x] Platform-neutral Table manager/filter snapshots.
- [x] Generation-guarded paged filter sessions and bounded page cache.
- [x] Native WPF/WinForms/MAUI paged-filter foundations.
- [x] Streaming CSV/TSV and staged atomic output.
- [x] First-generation array `FILTER`, `SORT`, `UNIQUE`.
- [x] Conditional aggregate criteria engine and six IF/IFS aggregate names.
- [ ] Complete Table design/resize/style manager UI.
- [ ] Rich XLSX AutoFilter markup and `sortState`.
- [ ] Advanced multi-key sort, grouping, outlines and subtotals.
- [ ] Criteria indexes/caches and database criteria tables.
- [ ] Pivot tables, slicers and calculated fields.
- [ ] External/virtualized data and incremental loading.

## F. Cross-platform controls

- [x] Platform-neutral command, Ribbon Core, Bars Core and DataGrid Core contracts.
- [x] Public WPF/WinForms spreadsheet hosts.
- [x] MAUI handler, touch state machine and pinch zoom.
- [x] Native filter and print-preview foundations.
- [x] WPF paginator and WinForms print adapter.
- [x] Spill child edit/clear/copy/cut/paste protection through shared controllers.
- [ ] Native spill border/selection/error affordances.
- [ ] Dedicated loaded smokes for every new native path.
- [ ] Full Table manager and general column/context menus.
- [ ] Native validation presenters.
- [ ] MAUI virtual keyboard and IME lifecycle.
- [ ] Responsive Ribbon/toolbar/menu/context-menu presenters.
- [ ] Production standalone DataGrid presenter.
- [ ] Complete theme, localization, high-contrast, accessibility and designer support.

## G. Product hardening

- [x] Broad exact-head multi-platform CI matrix.
- [x] Staged atomic PDF and delimited-text limits.
- [x] Repository validation runner and Codex final-acceptance plan.
- [x] Dynamic-array shape/collision/history/clipboard/XLSX gates.
- [x] Function SDK API/version/capability/conflict/dependency gates.
- [x] Conditional aggregate criteria/dependency/budget gates.
- [ ] API binary-compatibility and package-version checks.
- [ ] NuGet packaging, symbols and source link.
- [ ] Plugin package/signature/trust and isolation policy.
- [ ] Crash recovery, safe mode and support bundle.
- [ ] Security review and fuzzing for formulas, plugins, XLSX, CSV and clipboard.
- [ ] Performance budgets enforced in CI.
- [ ] Target printer/device/DPI/accessibility compatibility matrix.
- [ ] Alpha → Beta → RC → Production release gates.

## Immediate execution order

1. Statistical functions foundation.
2. Financial functions foundation.
3. Engineering/database functions and criteria-table support.
4. Advanced lookup/reference and dynamic-array helpers.
5. Plugin packaging/discovery/isolation and API compatibility tooling.
6. Native spill UX and drawings/images/charts.
7. Advanced data analysis, virtual data, pivot and slicers.
8. Remaining print/XLSX/PDF/font compatibility corpus.
9. Accessibility/IME/localization/theme and release hardening.
10. Execute final Codex acceptance before PR promotion.

## Weighted progress after Function SDK + Conditional Aggregates

- Engine/viewport/renderer foundation: approximately `92%`.
- Basic spreadsheet MVP: approximately `94–96%`.
- Complete professional roadmap: approximately `66%`.
- Production release readiness: approximately `43–46%`.

These are engineering-weighted estimates, not checkbox counts.

## Validation rule

Implementation commit `19e749473ce68f0b67b110ba70b37339a4c7e155` passed CI `#772`, run `32633548509`. PR #1 remains Draft and must not merge while a newer exact-head run is red or unknown.
