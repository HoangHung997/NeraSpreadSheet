# NeraSpreadSheet roadmap

`docs/current-status.md` is the implementation source of truth. `docs/formula-completion-master-schedule.md` owns the remaining formula queue. A capability is complete only after executable source, automated tests and the applicable runtime gate pass.

## A. Engine and viewport

- [x] Excel-size sparse workbook/worksheet model.
- [x] Selection, editing, clipboard, commands and Undo/Redo.
- [x] Atomic structural transforms with formula/rule/Table/filter/spill mapping.
- [x] Fractional scrolling, freeze/split panes and WPF/WinForms/MAUI GPU hosts.
- [ ] Manual hide/group/outline metadata and complete axis property model.
- [ ] Native spill-range UX and enforced hardware budgets.

## B. Formula engine and SDK

- [x] Parser, AST, dependency graph and circular-reference policy.
- [x] Shared/structured formulas and Table propagation.
- [x] Shared coercion/error model.
- [x] Conditional aggregate, statistical and advanced statistical foundations.
- [x] Financial annuity/root, dated-cash-flow, payment, depreciation and scalar-rate foundations.
- [x] Day-count basis `0..4`, YEARFRAC and coupon-date/day helpers.
- [x] F001: `ACCRINTM`, `DISC`, `INTRATE`, `RECEIVED`, `PRICEDISC`.
- [x] F002: `YIELDDISC`, `PRICEMAT`, `YIELDMAT`, `ACCRINT`, `FVSCHEDULE`.
- [x] F003: `PRICE`, `YIELD`, `DURATION`, `MDURATION`, `MIRR`.
- [x] F004: `TBILLEQ`, `TBILLPRICE`, `TBILLYIELD`, `DOLLARDE`, `DOLLARFR`.
- [x] Engineering Foundation: 19 functions.
- [x] Database Foundation: 12 functions.
- [x] Dynamic arrays and SEQUENCE/TRANSPOSE/FILTER/SORT/UNIQUE.
- [x] Master catalog-audit schedule with exact five-function reporting.
- [ ] F005: `AMORLINC`, `AMORDEGRC`, `ODDFPRICE`, `ODDFYIELD`, `ODDLPRICE`.
- [ ] Remaining AMOR and odd-coupon functions.
- [ ] Statistical hypothesis tests and confidence intervals.
- [ ] Advanced lookup/reference, dynamic arrays, LET/LAMBDA and higher-order functions.
- [ ] Full text/regex/byte-width compatibility.
- [ ] Complex/unit/special engineering and legacy aliases.
- [ ] Cube/web/data-type/external-state providers and isolation.
- [ ] Final Microsoft/OpenFormula catalog delta = zero.

## C. Financial foundations

- [x] Five day-count bases and maturity-anchored regular coupon schedule.
- [x] Coupon PCD/NCD/day/count helpers.
- [x] One-payment maturity-security equations.
- [x] Maturity-interest price/yield inverse equations.
- [x] Quasi-coupon accrued-interest schedule.
- [x] Variable-rate future-value range function.
- [x] Fixed-coupon price/yield/duration cash-flow engine.
- [x] Treasury-bill price/yield/equivalent-yield equations.
- [x] Fractional-dollar/decimal-dollar conversions.
- [ ] AMOR depreciation and odd-first/odd-last coupon schedules.
- [ ] Business-day/holiday calendars.

## D. XLSX, printing and PDF

- [x] Cells, formulas, styles, panes, rules, Tables/filters and current print settings.
- [x] Unknown package-part preservation.
- [x] Deterministic pagination, staged PDF and desktop print adapters.
- [ ] Full dynamic-array metadata, drawings/charts, custom paper and independent visual corpus.

## E. Data, controls and hardening

- [x] Sort, validation, Tables, filters, totals and paged native presenters.
- [x] Streaming CSV/TSV and first-generation dynamic arrays.
- [x] Exact-head multi-platform CI matrix and repository validation runner.
- [ ] Table/filter manager UI, grouping, virtual data, pivots and slicers.
- [ ] MAUI IME, responsive Ribbon, themes, localization and accessibility.
- [ ] Packaging/API compatibility, security/fuzzing, recovery and release gates.

## Formula completion execution order

Every public milestone contains exactly five new function names and advances only after exact-head hosted CI is green.

1. **F001 complete:** `ACCRINTM`, `DISC`, `INTRATE`, `RECEIVED`, `PRICEDISC`.
2. **F002 complete:** `YIELDDISC`, `PRICEMAT`, `YIELDMAT`, `ACCRINT`, `FVSCHEDULE`.
3. **F003 complete:** `PRICE`, `YIELD`, `DURATION`, `MDURATION`, `MIRR`.
4. **F004 complete:** `TBILLEQ`, `TBILLPRICE`, `TBILLYIELD`, `DOLLARDE`, `DOLLARFR`.
5. **F005 next:** `AMORLINC`, `AMORDEGRC`, `ODDFPRICE`, `ODDFYIELD`, `ODDLPRICE`.
6. **F006:** `ODDLYIELD`, `DATEDIF`, `DAYS360`, `ISOWEEKNUM`, `WEEKNUM`.
7. Continue automatically through the ordered pools in the master schedule.
8. Finish differential corpora, fuzzing, catalog audit and Codex final acceptance before PR promotion.

## Weighted progress after F004

- Engine/viewport/renderer foundation: approximately `92%`.
- Basic spreadsheet MVP: approximately `96–98%`.
- Complete professional roadmap: approximately `80–81%`.
- Production release readiness: approximately `58–61%`.

These are engineering-weighted estimates, not checkbox counts.

## Validation rule

F003 exact implementation head `48012398a3a020bfb12829bee46cfa88bc1c7fed` passed CI #866. F004 exact implementation head and its hosted CI are recorded in `docs/worklog/CURRENT.md`. PR #1 remains Draft and unmerged; public completion is reported only after the documentation/handoff exact-head matrix is green.
