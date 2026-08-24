# NeraSpreadSheet roadmap

`docs/current-status.md` is the implementation source of truth. A capability is complete only after executable source, automated tests and the applicable build/runtime gate pass.

## A. Independent spreadsheet engine

- [x] Excel-size sparse workbook/worksheet model.
- [x] Values, formulas, merges, dimensions and immutable snapshots.
- [x] Selection, spill-aware clipboard, editor, commands and Undo/Redo.
- [x] Atomic structural insert/delete/reorder with formula/rule/Table/filter/spill mapping.
- [x] Sparse whole-row/column styles.
- [x] Conditional Formatting, Data Validation, Tables and worksheet page setup foundations.
- [ ] Sparse manual hide/group/outline metadata and complete axis property model.
- [ ] Print settings in structural history and complete named-range integration.

## B. Viewport, rendering and preview

- [x] Continuous fractional-pixel scrolling.
- [x] Freeze/split panes and independent pane scrolling.
- [x] Shared headers, resize, selection, editor and drag reorder.
- [x] Snapshot/tile caching and split-aware dirty regions.
- [x] WPF/WinForms software/GPU and native MAUI GPU hosts.
- [x] Conditional/validation overlays, compressed filters and shared print display lists.
- [x] Virtualized print preview and loaded runtime/scale gates.
- [ ] Dedicated spill-border/selection UX on every host.
- [ ] Enforced 60/120-Hz, 4K and large-array hardware budgets.

## C. Formula engine and extension SDK

- [x] Parser, AST, dependency graph and circular-reference policy.
- [x] Shared/structured formulas and atomic Table formula propagation.
- [x] Shared coercion/error model including `#NUM!` and `#SPILL!`.
- [x] Logical/error, aggregate, math, text/Unicode, date/time and lookup foundations.
- [x] Conditional aggregate foundation.
- [x] Statistical and Advanced Statistical foundations.
- [x] Financial annuities and roots: PV/FV/PMT/NPER/RATE, NPV/IRR/XNPV/XIRR.
- [x] Financial payment decomposition: IPMT/PPMT/CUMIPMT/CUMPRINC.
- [x] Financial depreciation: SLN/SYD/DB/DDB/VDB.
- [x] Engineering Foundation: 19 bit/shift/radix/comparison functions.
- [x] Database Foundation: 12 criteria-table aggregate functions.
- [x] Function Extension SDK API `1.0` with one authoritative built-in registry path.
- [x] Dynamic arrays and SEQUENCE/TRANSPOSE/FILTER/SORT/UNIQUE.
- [ ] Scalar finance helpers: ISPMT/EFFECT/NOMINAL/RRI/PDURATION.
- [ ] AMOR/date-basis and bond/coupon/treasury/price/yield/duration families.
- [ ] Statistical hypothesis tests, confidence intervals and additional distributions.
- [ ] Advanced lookup/reference and dynamic-array helpers.
- [ ] Plugin packaging, discovery, signatures, compatibility tooling and isolation.
- [ ] Complex-number, unit conversion, special engineering and formula-expression database criteria.
- [ ] Cube functions.

## D. XLSX, page layout and PDF

- [x] Values, formulas, sheets, dimensions, merges, panes and styles.
- [x] Unknown package-part preservation.
- [x] Shared formulas, Conditional Formatting, Data Validation, Tables and AutoFilter round-trip.
- [x] Current print settings, print areas/titles, deterministic pagination and staged PDF.
- [x] WPF paginator, WinForms print adapter and dynamic-array-aware save boundary.
- [ ] Full Office dynamic-array metadata and external producer corpus.
- [ ] Manual breaks, first/even headers and arbitrary custom paper.
- [ ] Independent PDF validator/raster visual-diff corpus and font policy.
- [ ] Drawings/images/charts and print/PDF pagination.
- [ ] Physical printer capability/hard-margin negotiation.

## E. Data and analysis

- [x] Basic bounded sort, validation, Tables, filters and totals.
- [x] Generation-guarded paged filter sessions and native presenter foundations.
- [x] Streaming CSV/TSV and first-generation array FILTER/SORT/UNIQUE.
- [x] Bounded criteria-table database aggregates.
- [ ] Complete Table design/resize/style manager UI.
- [ ] Incremental distinct-value publication and rich AutoFilter markup.
- [ ] Advanced multi-key sort, grouping, outlines and subtotals.
- [ ] Pivot tables, slicers and calculated fields.
- [ ] External/virtualized data and incremental loading.
- [ ] Indexed database criteria execution.

## F. Cross-platform controls

- [x] Platform-neutral command, Ribbon, Bars and DataGrid Core contracts.
- [x] Public WPF/WinForms spreadsheet hosts and MAUI handler/touch lifecycle.
- [x] Native filter/preview foundations and desktop print adapters.
- [x] Shared spill child edit/clear/copy/cut/paste protection.
- [ ] Native spill range UX, validation presenters and full Table manager.
- [ ] MAUI virtual keyboard/IME.
- [ ] Responsive Ribbon/toolbar/menu presenters.
- [ ] Theme, localization, high-contrast, accessibility and designer support.

## G. Product hardening

- [x] Broad exact-head multi-platform CI matrix.
- [x] Atomic PDF/delimited output limits and repository validation runner.
- [x] Formula SDK, criteria, statistical, financial, engineering, database and dynamic-array gates.
- [ ] API compatibility and package-version checks.
- [ ] NuGet/plugin packaging, symbols and source link.
- [ ] Plugin publisher verification and isolation/resource policy.
- [ ] Crash recovery, safe mode and support bundle.
- [ ] Security review and fuzzing.
- [ ] Performance budgets enforced in CI.
- [ ] Target printer/device/DPI/accessibility matrix.
- [ ] Alpha → Beta → RC → Production release gates.

## Immediate execution order

1. Scalar financial helpers: `ISPMT`, `EFFECT`, `NOMINAL`, `RRI`, `PDURATION`.
2. AMOR/date-basis and bond/coupon/treasury/price/yield/duration families.
3. Statistical hypothesis tests, confidence intervals and remaining distribution compatibility.
4. Advanced lookup/reference and dynamic-array helpers.
5. Plugin packaging/discovery, API compatibility and isolation.
6. Native spill UX, drawings/images/charts and print/PDF pagination.
7. Advanced data, grouping/outlines, virtual data, pivot and slicers.
8. Remaining print/XLSX/PDF/font/external corpora.
9. MAUI IME/accessibility/localization/theme and release hardening.
10. Execute final Codex acceptance before PR promotion.

## Weighted progress after cumulative payment and declining-balance depreciation

- Engine/viewport/renderer foundation: approximately `92%`.
- Basic spreadsheet MVP: approximately `96–98%`.
- Complete professional roadmap: approximately `76%`.
- Production release readiness: approximately `53–56%`.

These are engineering-weighted estimates, not checkbox counts.

## Validation rule

Implementation commit `ea61fe227919358539355b814d4c2baf5f05b538` passed CI `#844`, run `32734262232`. PR #1 remains Draft and must not merge while a newer exact-head run is red or unknown.
