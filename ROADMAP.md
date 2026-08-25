# NeraSpreadSheet roadmap

`docs/current-status.md` is the implementation source of truth. `docs/formula-completion-master-schedule.md` owns the remaining formula queue. A capability is complete only after executable source, automated tests and the applicable runtime gate pass.

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
- [x] Financial annuity/root, dated-cash-flow, payment, depreciation and rate-helper foundations.
- [x] Shared financial day-count basis `0..4`, `YEARFRAC` and coupon-date/day helpers.
- [x] F001 maturity securities: `ACCRINTM`, `DISC`, `INTRATE`, `RECEIVED`, `PRICEDISC`.
- [x] Engineering Foundation: 19 functions.
- [x] Database Foundation: 12 functions.
- [x] Function Extension SDK API `1.0` with one built-in aggregation path.
- [x] Dynamic arrays and SEQUENCE/TRANSPOSE/FILTER/SORT/UNIQUE.
- [x] Master catalog-audit schedule with exact five-function milestone reporting.
- [ ] F002: `YIELDDISC`, `PRICEMAT`, `YIELDMAT`, `ACCRINT`, `FVSCHEDULE`.
- [ ] Fixed-coupon PRICE/YIELD/DURATION/MDURATION and treasury functions.
- [ ] AMOR and odd-first/odd-last coupon families.
- [ ] Statistical hypothesis tests and confidence intervals.
- [ ] Advanced lookup/reference and dynamic-array helpers.
- [ ] LET/LAMBDA and higher-order array functions.
- [ ] Full text/regex/byte-width compatibility.
- [ ] Complex/unit/special engineering and compatibility aliases.
- [ ] Cube/web/data-type/external-state functions and provider isolation.
- [ ] Final Microsoft/OpenFormula catalog delta = zero.

## C. Financial foundations

- [x] Date-only normalization and five day-count bases.
- [x] Maturity-anchored coupon generation with end-of-month preservation.
- [x] Previous/next coupon, period day counts and remaining count.
- [x] Shared one-payment maturity-security equations and inverse regression.
- [ ] Full accrued-interest schedules and maturity-interest price/yield.
- [ ] Fixed-coupon cash-flow pricing/yield/duration.
- [ ] Odd-first/odd-last schedules and business-day calendars.

## D. XLSX, printing and PDF

- [x] Cells, formulas, styles, panes, rules, Tables/filters and current print settings.
- [x] Unknown package-part preservation.
- [x] Deterministic pagination, staged PDF and desktop print adapters.
- [ ] Full dynamic-array metadata and external producer corpus.
- [ ] Manual breaks, custom paper, independent PDF/font corpus and drawings/charts.

## E. Data, controls and hardening

- [x] Sort, validation, Tables, filters, totals and paged native presenters.
- [x] Streaming CSV/TSV and array FILTER/SORT/UNIQUE.
- [x] Exact-head multi-platform CI matrix and repository validation runner.
- [ ] Complete Table/filter manager UI, grouping, virtual data, pivots and slicers.
- [ ] MAUI IME, responsive Ribbon, themes, localization and accessibility.
- [ ] Packaging/API compatibility, security/fuzzing, recovery and release gates.

## Formula completion execution order

Every public milestone contains exactly five new function names and advances only after exact-head hosted CI is green.

1. **F001 complete:** `ACCRINTM`, `DISC`, `INTRATE`, `RECEIVED`, `PRICEDISC`.
2. **F002 next:** `YIELDDISC`, `PRICEMAT`, `YIELDMAT`, `ACCRINT`, `FVSCHEDULE`.
3. **F003:** `PRICE`, `YIELD`, `DURATION`, `MDURATION`, `MIRR`.
4. **F004:** `TBILLEQ`, `TBILLPRICE`, `TBILLYIELD`, `DOLLARDE`, `DOLLARFR`.
5. **F005:** `AMORLINC`, `AMORDEGRC`, `ODDFPRICE`, `ODDFYIELD`, `ODDLPRICE`.
6. **F006:** `ODDLYIELD`, `DATEDIF`, `DAYS360`, `ISOWEEKNUM`, `WEEKNUM`.
7. Continue automatically through the ordered pools in `docs/formula-completion-master-schedule.md`.
8. Finish differential corpora, fuzzing, catalog audit and Codex final acceptance before PR promotion.

## Weighted progress after F001

- Engine/viewport/renderer foundation: approximately `92%`.
- Basic spreadsheet MVP: approximately `96–98%`.
- Complete professional roadmap: approximately `79%`.
- Production release readiness: approximately `56–59%`.

These are engineering-weighted estimates, not checkbox counts.

## Validation rule

F001 implementation commit `3ea2ae2d576e40b72e91c02ab493f1e244ffe0bd` passed Core/architecture and 198/198 formula tests. A new exact-head hosted run is required because CI #859's Apple runner failed DNS before checkout. PR #1 remains Draft and unmerged.
