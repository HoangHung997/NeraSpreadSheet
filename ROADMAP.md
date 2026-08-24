# NeraSpreadSheet roadmap

`docs/current-status.md` is the implementation source of truth. A capability is complete only after executable source, automated tests and the applicable build/runtime gate pass.

## A. Independent spreadsheet engine

- [x] Excel-size sparse workbook/worksheet model.
- [x] Values, formulas, merges, dimensions and immutable snapshots.
- [x] Selection, spill-aware clipboard, editor, commands and data/view Undo/Redo.
- [x] Atomic structural insert/delete/reorder with formula/rule/Table/filter/spill mapping.
- [x] Sparse whole-row/column styles.
- [x] Conditional Formatting and Data Validation Core models with structural history.
- [x] Table model with stable IDs, calculated/totals metadata and structural history.
- [x] Worksheet-associated page setup, print area and spill ownership contracts.
- [ ] Sparse manual hide/group/outline metadata and complete axis property model.
- [ ] Print settings in structural history and complete named-range integration.

## B. Viewport, rendering and preview

- [x] Continuous fractional-pixel scrolling.
- [x] Freeze/split panes and independent pane scrolling.
- [x] Shared headers, resize, selection, editor and drag reorder.
- [x] Snapshot/tile caching and split-aware dirty regions.
- [x] WPF and WinForms software/GPU backends.
- [x] Shared Skia renderer and native MAUI GPU host.
- [x] Conditional/validation overlays and compressed filter spans.
- [x] Shared Table/worksheet filter-button geometry.
- [x] Shared print display-list composition.
- [x] Virtualized print-preview layout/session and native host foundations.
- [x] Immutable spill identity in worksheet snapshots.
- [x] Loaded context-recreation and scale/orientation gates.
- [ ] Dedicated spill-border/selection UX on every host.
- [ ] Dedicated loaded interaction gates for every new filter/preview/spill path.
- [ ] Enforced 60/120-Hz, 4K and large-array hardware budgets.

## C. Formula engine and extension SDK

- [x] Tokenizer, parser, AST, dependency graph and circular-reference policy.
- [x] Arithmetic, comparison, concatenation and A1/cross-sheet references.
- [x] Shared formulas and structured references.
- [x] Atomic Table/column rewrite and calculated-column propagation.
- [x] Shared coercion/error model including `#NUM!` values and `#SPILL!`.
- [x] Logical/error, aggregate, math, text/Unicode, date/time and lookup foundations.
- [x] Conditional aggregates: `COUNTIF(S)`, `SUMIF(S)`, `AVERAGEIF(S)`.
- [x] Statistical Foundation: median/mode, inclusive percentile/quartile, variance/deviation, rank/order statistics.
- [x] Advanced Statistical Foundation: covariance, correlation, regression, forecast and 30 transformation/distribution functions.
- [x] Financial Foundation: PV/FV/PMT/NPER, NPV/IRR, IPMT/PPMT, SLN/SYD.
- [x] Engineering Foundation: 19 bit/shift/radix/comparison functions.
- [x] Database Foundation: 12 criteria-table aggregate functions.
- [x] Function Extension SDK API `1.0` with identity/version/capability/state/dependency/conflict contracts.
- [x] Immutable dynamic arrays, spill ownership and affected recalculation.
- [x] `SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT`, `UNIQUE`.
- [ ] Statistical hypothesis tests, confidence intervals, additional distributions and broader compatibility aliases.
- [ ] Remaining finance: `RATE`, `XNPV`, `XIRR`, cumulative payment, bond/coupon/day-count and accelerated depreciation.
- [ ] Advanced lookup/reference modes and functions.
- [ ] Spill-reference `A1#`, implicit-intersection `@`, array constants and vectorized expressions.
- [ ] Advanced dynamic-array helpers and LET/LAMBDA/higher-order functions.
- [ ] Plugin packaging, discovery, signatures, compatibility tooling and isolation.
- [ ] Complex-number, unit conversion, special engineering and formula-expression database criteria.
- [ ] Cube functions.

## D. XLSX, page layout and PDF

- [x] Values, formulas, sheets, dimensions, merges, panes and styles.
- [x] Unknown package-part copy-and-patch preservation.
- [x] Shared formulas, Conditional Formatting and Data Validation round-trip.
- [x] Standard Table parts and current AutoFilter round-trip.
- [x] Print margins, paper code, orientation, scale, fit, print options and odd header/footer.
- [x] `_xlnm.Print_Area` and `_xlnm.Print_Titles`.
- [x] Deterministic pagination, repeated titles and merged-cell protection.
- [x] Staged PDF for one worksheet, selected worksheets and print tickets.
- [x] WPF paginator and WinForms `PrintDocument`.
- [x] Dynamic-array-aware owner/child document save boundary.
- [ ] Full Office dynamic-array extension metadata and external producer corpus.
- [ ] XLSX manual breaks, first/even headers and arbitrary custom paper.
- [ ] Independent PDF validator/raster visual-diff corpus and font policy.
- [ ] First-class drawings/images/charts and print/PDF pagination.
- [ ] Physical printer capability and hard-margin negotiation.

## E. Data and analysis

- [x] Basic bounded in-memory sort.
- [x] Current Data Validation evaluator and editor gate.
- [x] Table and direct worksheet AutoFilter predicates.
- [x] Table operations and filter-aware totals with Undo/Redo.
- [x] Platform-neutral Table manager/filter snapshots.
- [x] Generation-guarded paged Table/worksheet filter sessions.
- [x] Random-access page cache and cancellable search.
- [x] Native WPF/WinForms/MAUI paged-filter foundations.
- [x] Streaming CSV/TSV and staged atomic output.
- [x] First-generation array FILTER/SORT/UNIQUE.
- [x] Bounded criteria-table database aggregates with dependency tracking.
- [ ] Complete Table design/resize/style manager UI.
- [ ] Incremental distinct-value publication.
- [ ] Rich XLSX AutoFilter markup and `sortState`.
- [ ] Advanced multi-key sort, grouping, outlines and general subtotals.
- [ ] Pivot tables, slicers and calculated fields.
- [ ] External/virtualized data and incremental loading.
- [ ] Indexed database criteria execution for very large in-memory tables.

## F. Cross-platform controls

- [x] Platform-neutral command, Ribbon Core, Bars Core and DataGrid Core contracts.
- [x] Public WPF/WinForms spreadsheet hosts.
- [x] MAUI handler, touch state machine and pinch zoom.
- [x] Native filter and print-preview foundations.
- [x] WPF paginator and WinForms print adapter.
- [x] Shared spill child edit/clear/copy/cut/paste protection.
- [ ] Native spill-range border, selection and error affordances.
- [ ] Dedicated loaded smokes for every new native control path.
- [ ] Full Table manager and general column/context menus.
- [ ] Native validation presenters.
- [ ] MAUI virtual keyboard and IME lifecycle.
- [ ] Responsive Ribbon/toolbar/menu/context-menu presenters.
- [ ] Production standalone DataGrid presenter.
- [ ] Complete theme, localization, high-contrast, accessibility and designer support.

## G. Product hardening

- [x] Broad exact-head multi-platform CI matrix.
- [x] Staged atomic PDF and delimited-text output limits.
- [x] Repository-wide validation runner and Codex final-acceptance plan.
- [x] Formula SDK, criteria, statistical, advanced-statistical, financial, engineering, database and dynamic-array automated gates.
- [ ] API compatibility and package-version checks.
- [ ] NuGet/plugin packaging, symbols and source link.
- [ ] Plugin publisher verification and isolation/resource policy.
- [ ] Crash recovery, safe mode and support bundle.
- [ ] Security review and fuzzing for formulas, SDK extensions, XLSX, CSV and clipboard.
- [ ] Performance budgets enforced in CI.
- [ ] Target printer/device/DPI/accessibility compatibility matrix.
- [ ] Alpha → Beta → RC → Production release gates.

## Immediate execution order

1. Remaining financial families: `RATE`, `XNPV`, `XIRR`, cumulative payment, bond/coupon/day-count and accelerated depreciation.
2. Statistical hypothesis tests, confidence intervals and remaining distribution compatibility.
3. Advanced lookup/reference and dynamic-array helpers.
4. Plugin packaging/discovery, API compatibility and isolation policy.
5. Native spill UX, drawings/images/charts and print/PDF pagination.
6. Advanced data analysis, grouping/outlines, virtual data, pivot and slicers.
7. Remaining print/XLSX/PDF/font/external formula corpora.
8. MAUI IME/accessibility/localization/theme and release hardening.
9. Execute final Codex acceptance before PR promotion.

## Weighted progress after Advanced Statistical Functions Foundation

- Engine/viewport/renderer foundation: approximately `92%`.
- Basic spreadsheet MVP: approximately `96–98%`.
- Complete professional roadmap: approximately `74%`.
- Production release readiness: approximately `51–54%`.

These are engineering-weighted estimates, not checkbox counts.

## Validation rule

Implementation commit `e713182d460f5c280e2c29e5642769eedf190d2f` passed CI `#835`, run `32720631933`. PR #1 remains Draft and must not merge while a newer exact-head run is red or unknown.
