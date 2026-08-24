# NeraSpreadSheet roadmap

`docs/current-status.md` is the implementation source of truth. `docs/formula-completion-master-schedule.md` is the source of truth for the remaining formula queue. A capability is complete only after executable source, automated tests and the applicable runtime gate pass.

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
- [x] Shared financial day-count basis `0..4`.
- [x] `YEARFRAC` and coupon-date/day helpers.
- [x] Engineering Foundation: 19 functions.
- [x] Database Foundation: 12 functions.
- [x] Function Extension SDK API `1.0` with one built-in aggregation path.
- [x] Dynamic arrays and SEQUENCE/TRANSPOSE/FILTER/SORT/UNIQUE.
- [x] Master catalog-audit schedule with exact five-function milestone reporting.
- [ ] Discount/maturity securities.
- [ ] Fixed-coupon PRICE/YIELD/DURATION/MDURATION and treasury functions.
- [ ] AMOR and odd-first/odd-last coupon families.
- [ ] Statistical hypothesis tests and confidence intervals.
- [ ] Advanced lookup/reference and dynamic-array helpers.
- [ ] LET/LAMBDA and higher-order array functions.
- [ ] Full text/regex/byte-width compatibility.
- [ ] Complex/unit/special engineering functions.
- [ ] Compatibility/legacy aliases.
- [ ] Cube/web/data-type/external-state functions and provider isolation.
- [ ] Final Microsoft/OpenFormula catalog delta = zero.

## C. Financial calendar foundation

- [x] Date inputs normalized to date-only values.
- [x] Frequency validation for annual, semiannual and quarterly schedules.
- [x] Basis 0 US NASD 30/360.
- [x] Basis 1 Actual/Actual with leap-year and multi-year denominator rules.
- [x] Basis 2 Actual/360.
- [x] Basis 3 Actual/365.
- [x] Basis 4 European 30/360.
- [x] Maturity-anchored coupon generation with end-of-month preservation.
- [x] Previous/next coupon and remaining-coupon count.
- [x] Bounded 100.000-period coupon search.
- [ ] Odd-first/odd-last schedules.
- [ ] Business-day calendars and holiday adjustment conventions.

## D. XLSX, printing and PDF

- [x] Cells, formulas, styles, panes, rules, Tables/filters and current print settings.
- [x] Unknown package-part preservation.
- [x] Deterministic pagination, staged PDF and desktop print adapters.
- [ ] Full dynamic-array metadata and external producer corpus.
- [ ] Manual breaks, first/even headers, custom paper and real-printer negotiation.
- [ ] Independent PDF/font visual-diff corpus.
- [ ] Drawings/images/charts and pagination.

## E. Data and controls

- [x] Sort, validation, Tables, filters, totals and paged native presenters.
- [x] Streaming CSV/TSV and array FILTER/SORT/UNIQUE.
- [ ] Complete Table/filter manager UI and rich XLSX markup.
- [ ] Grouping/outlines, virtual data, pivots and slicers.
- [ ] MAUI IME, responsive Ribbon, themes, localization and accessibility.

## F. Product hardening

- [x] Exact-head multi-platform CI matrix.
- [x] Atomic export limits and repository validation runner.
- [x] Formula family result/domain/numerical/resource gates.
- [ ] API/package compatibility and NuGet/plugin packaging.
- [ ] Security review, fuzzing, crash recovery and safe mode.
- [ ] Performance budgets and target-device/printer matrix.
- [ ] Alpha → Beta → RC → Production gates.

## Formula completion execution order

Every formula milestone contains exactly five newly completed public function names. Refactors, provider work and tests are supporting work and do not replace a name in that group of five. A batch advances only after exact-head hosted CI is green.

1. **F001:** `ACCRINTM`, `DISC`, `INTRATE`, `RECEIVED`, `PRICEDISC`.
2. **F002:** `YIELDDISC`, `PRICEMAT`, `YIELDMAT`, `ACCRINT`, `FVSCHEDULE`.
3. **F003:** `PRICE`, `YIELD`, `DURATION`, `MDURATION`, `MIRR`.
4. **F004:** `TBILLEQ`, `TBILLPRICE`, `TBILLYIELD`, `DOLLARDE`, `DOLLARFR`.
5. **F005:** `AMORLINC`, `AMORDEGRC`, `ODDFPRICE`, `ODDFYIELD`, `ODDLPRICE`.
6. **F006:** `ODDLYIELD`, `DATEDIF`, `DAYS360`, `ISOWEEKNUM`, `WEEKNUM`.
7. Continue automatically through the ordered pools in `docs/formula-completion-master-schedule.md`: business-day/date → lookup/reference/dynamic arrays → LET/LAMBDA → text/regex → math/matrix → statistical tests/forecast → compatibility aliases → complex/unit engineering → information/introspection → cube/web/external-state → final OpenFormula/Microsoft catalog delta.
8. After every five-function exact-head success, publish one progress table and lock the next five Pending names.
9. Complete external Excel/LibreOffice/ODS differential corpus, fuzzing and final catalog audit.
10. Execute Codex final acceptance before PR promotion.

A registry audit runs before each batch. If a scheduled name already exists, it is skipped and replaced by the next Pending name, preserving the exact five-new-name milestone.

## Weighted progress after financial calendar/day-count foundation

- Engine/viewport/renderer foundation: approximately `92%`.
- Basic spreadsheet MVP: approximately `96–98%`.
- Complete professional roadmap: approximately `78%`.
- Production release readiness: approximately `55–58%`.

These are engineering-weighted estimates, not checkbox counts.

## Validation rule

Implementation commit `eeb74ad4ee596f7cb56343b8459f2311538c8243` passed CI `#854`, run `32745296544`, including 192 formula tests and the hosted matrix. The current formula queue is locked in `docs/formula-completion-master-schedule.md`. PR #1 remains Draft and must not merge while a newer exact-head run is red or unknown.
