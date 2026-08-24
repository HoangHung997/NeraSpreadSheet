# NeraSpreadSheet roadmap

`docs/current-status.md` is the implementation source of truth. A capability is complete only after executable source, automated tests and the applicable runtime gate pass.

## A. Engine and viewport

- [x] Excel-size sparse workbook/worksheet model.
- [x] Values, formulas, merges, dimensions and immutable snapshots.
- [x] Selection, editing, clipboard, commands and Undo/Redo.
- [x] Atomic structural transforms with formula/rule/Table/filter/spill mapping.
- [x] Fractional scrolling, freeze/split panes and WPF/WinForms/MAUI GPU hosts.
- [ ] Manual hide/group/outline metadata and complete axis property model.
- [ ] Native spill-range UX and enforced hardware performance budgets.

## B. Formula engine and SDK

- [x] Parser, AST, dependency graph and circular-reference policy.
- [x] Shared/structured formulas and Table propagation.
- [x] Shared coercion/error model.
- [x] Logical, aggregate, math, text, date/time and lookup foundations.
- [x] Conditional aggregate, statistical and advanced statistical foundations.
- [x] Financial annuity/root and dated-cash-flow functions.
- [x] Financial payment decomposition/cumulative schedules.
- [x] Financial depreciation DB/DDB/VDB.
- [x] Financial scalar helpers ISPMT/EFFECT/NOMINAL/RRI/PDURATION.
- [x] Engineering Foundation: 19 functions.
- [x] Database Foundation: 12 functions.
- [x] Function Extension SDK API `1.0` with one built-in aggregation path.
- [x] Dynamic arrays and SEQUENCE/TRANSPOSE/FILTER/SORT/UNIQUE.
- [ ] Shared financial day-count basis `0..4`.
- [ ] YEARFRAC and coupon-date helpers.
- [ ] Bond/treasury/price/yield/duration and AMOR families.
- [ ] Statistical hypothesis tests and confidence intervals.
- [ ] Advanced lookup/reference and dynamic-array helpers.
- [ ] Plugin packaging, discovery, signatures and isolation.
- [ ] Complex/unit/special engineering and expression database criteria.

## C. XLSX, printing and PDF

- [x] Cells, formulas, styles, panes, rules, Tables/filters and current print settings.
- [x] Unknown package-part preservation.
- [x] Deterministic pagination, staged PDF and desktop print adapters.
- [ ] Full dynamic-array metadata and external producer corpus.
- [ ] Manual breaks, first/even headers, custom paper and real-printer negotiation.
- [ ] Independent PDF/font visual-diff corpus.
- [ ] Drawings/images/charts and pagination.

## D. Data and controls

- [x] Sort, validation, Tables, filters, totals and paged native presenters.
- [x] Streaming CSV/TSV and array FILTER/SORT/UNIQUE.
- [ ] Complete Table/filter manager UI and rich XLSX markup.
- [ ] Grouping/outlines, virtual data, pivots and slicers.
- [ ] MAUI IME, responsive Ribbon, themes, localization and accessibility.

## E. Product hardening

- [x] Exact-head multi-platform CI matrix.
- [x] Atomic export limits and repository validation runner.
- [x] Formula family result/domain/numerical/resource gates.
- [ ] API/package compatibility and NuGet/plugin packaging.
- [ ] Security review, fuzzing, crash recovery and safe mode.
- [ ] Performance budgets and target-device/printer matrix.
- [ ] Alpha → Beta → RC → Production gates.

## Immediate execution order

1. Shared financial calendar/day-count basis `0..4`.
2. `YEARFRAC`, `COUPDAYBS`, `COUPDAYS`, `COUPDAYSNC`, `COUPNCD`, `COUPPCD`, `COUPNUM`.
3. Bond/treasury/price/yield/duration and AMOR functions.
4. Statistical hypothesis tests and confidence intervals.
5. Advanced lookup/reference and dynamic-array helpers.
6. Plugin isolation, native spill UX, drawings/charts and pivot/data work.
7. Remaining external corpora, accessibility/IME and release hardening.
8. Final Codex acceptance before PR promotion.

## Weighted progress after scalar financial helpers

- Engine/viewport/renderer foundation: approximately `92%`.
- Basic spreadsheet MVP: approximately `96–98%`.
- Complete professional roadmap: approximately `77%`.
- Production release readiness: approximately `54–57%`.

These are engineering-weighted estimates, not checkbox counts.

## Validation rule

Implementation commit `e2d3bb4b296292ae83dc4c1a5e35a442f6574e4f` passed CI `#849`, run `32740594038`. PR #1 remains Draft and must not merge while a newer exact-head run is red or unknown.
