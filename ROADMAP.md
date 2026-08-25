# NeraSpreadSheet roadmap

`docs/current-status.md` là nguồn sự thật về implementation. `docs/formula-completion-master-schedule.md` quản lý hàng đợi hàm còn lại. Một capability chỉ hoàn thành khi có source thực thi, automated tests và runtime/build gate phù hợp.

## A. Engine và viewport

- [x] Excel-size sparse workbook/worksheet model.
- [x] Selection, editing, clipboard, commands và Undo/Redo.
- [x] Atomic structural transforms với formula/rule/Table/filter/spill mapping.
- [x] Fractional scrolling, freeze/split panes và WPF/WinForms/MAUI GPU hosts.
- [ ] Manual hide/group/outline metadata và complete axis property model.
- [ ] Native spill-range UX và enforced hardware budgets.

## B. Formula engine và SDK

- [x] Parser, AST, dependency graph và circular-reference policy.
- [x] Shared/structured formulas và Table propagation.
- [x] Shared coercion/error model.
- [x] Conditional aggregate, statistical và advanced statistical foundations.
- [x] Financial annuity/root, dated-cash-flow, payment, depreciation và scalar-rate foundations.
- [x] Day-count basis `0..4`, YEARFRAC và coupon-date/day helpers.
- [x] F001: `ACCRINTM`, `DISC`, `INTRATE`, `RECEIVED`, `PRICEDISC`.
- [x] F002: `YIELDDISC`, `PRICEMAT`, `YIELDMAT`, `ACCRINT`, `FVSCHEDULE`.
- [x] F003: `PRICE`, `YIELD`, `DURATION`, `MDURATION`, `MIRR`.
- [x] F004: `TBILLEQ`, `TBILLPRICE`, `TBILLYIELD`, `DOLLARDE`, `DOLLARFR`.
- [x] F005: `AMORLINC`, `AMORDEGRC`, `ODDFPRICE`, `ODDFYIELD`, `ODDLPRICE`.
- [x] F006: `ODDLYIELD`, `DATEDIF`, `DAYS360`, `ISOWEEKNUM`, `WEEKNUM`.
- [x] Engineering Foundation: 19 functions.
- [x] Database Foundation: 12 functions.
- [x] Dynamic arrays và `SEQUENCE`/`TRANSPOSE`/`FILTER`/`SORT`/`UNIQUE`.
- [x] Master catalog-audit schedule với exact five-function reporting.
- [ ] F007: `NETWORKDAYS`, `NETWORKDAYS.INTL`, `WORKDAY`, `WORKDAY.INTL`, `NUMBERVALUE`.
- [ ] Statistical hypothesis tests và confidence intervals.
- [ ] Advanced lookup/reference, dynamic arrays, LET/LAMBDA và higher-order functions.
- [ ] Full text/regex/byte-width compatibility.
- [ ] Complex/unit/special engineering và legacy aliases.
- [ ] Cube/web/data-type/external-state providers và isolation.
- [ ] Final Microsoft/OpenFormula catalog delta = zero.

## C. Financial và date foundations

- [x] Năm day-count basis và maturity-anchored regular coupon schedule.
- [x] Coupon PCD/NCD/day/count helpers.
- [x] One-payment maturity-security equations.
- [x] Maturity-interest price/yield inverse equations.
- [x] Quasi-coupon accrued-interest schedule.
- [x] Variable-rate future-value range function.
- [x] Fixed-coupon price/yield/duration cash-flow engine.
- [x] Treasury-bill price/yield/equivalent-yield equations.
- [x] Fractional-dollar/decimal-dollar conversions.
- [x] French-accounting linear và accelerated depreciation.
- [x] Odd-first price/yield và odd-last price/yield trên bounded quasi-coupon ratios.
- [x] Legacy date difference, 30/360 và system-one/ISO week numbering.
- [ ] Business-day/holiday calendars.
- [ ] Locale-aware number parsing.
- [ ] External Excel/LibreOffice financial/date differential corpus và fuzzing.

## D. XLSX, printing và PDF

- [x] Cells, formulas, styles, panes, rules, Tables/filters và current print settings.
- [x] Unknown package-part preservation.
- [x] Deterministic pagination, staged PDF và desktop print adapters.
- [ ] Full dynamic-array metadata, drawings/charts, custom paper và independent visual corpus.

## E. Data, controls và hardening

- [x] Sort, validation, Tables, filters, totals và paged native presenters.
- [x] Streaming CSV/TSV và first-generation dynamic arrays.
- [x] Exact-head multi-platform CI matrix và repository validation runner.
- [ ] Table/filter manager UI, grouping, virtual data, pivots và slicers.
- [ ] MAUI IME, responsive Ribbon, themes, localization và accessibility.
- [ ] Packaging/API compatibility, security/fuzzing, recovery và release gates.

## Formula completion execution order

Mỗi public milestone chứa đúng năm tên hàm mới và chỉ tiến lên khi exact-head hosted CI xanh.

1. **F001 complete:** `ACCRINTM`, `DISC`, `INTRATE`, `RECEIVED`, `PRICEDISC`.
2. **F002 complete:** `YIELDDISC`, `PRICEMAT`, `YIELDMAT`, `ACCRINT`, `FVSCHEDULE`.
3. **F003 complete:** `PRICE`, `YIELD`, `DURATION`, `MDURATION`, `MIRR`.
4. **F004 complete:** `TBILLEQ`, `TBILLPRICE`, `TBILLYIELD`, `DOLLARDE`, `DOLLARFR`.
5. **F005 complete:** `AMORLINC`, `AMORDEGRC`, `ODDFPRICE`, `ODDFYIELD`, `ODDLPRICE`.
6. **F006 complete:** `ODDLYIELD`, `DATEDIF`, `DAYS360`, `ISOWEEKNUM`, `WEEKNUM`.
7. **F007 next:** `NETWORKDAYS`, `NETWORKDAYS.INTL`, `WORKDAY`, `WORKDAY.INTL`, `NUMBERVALUE`.
8. Tiếp tục tự động qua ordered pools trong master schedule.
9. Hoàn tất differential corpora, fuzzing, catalog audit và final acceptance trước khi PR được promote.

## Weighted progress sau F006

- Engine/viewport/renderer foundation: khoảng `92%`.
- Basic spreadsheet MVP: khoảng `96–98%`.
- Complete professional roadmap: khoảng `82–83%`.
- Production release readiness: khoảng `60–63%`.

Đây là ước lượng có trọng số kỹ thuật, không phải tỷ lệ checkbox.

## Validation rule

F006 implementation head `c43bf362054110940f149a144546c4bba13387e3` phải qua 224/224 formula tests, Core/architecture và toàn bộ hosted matrix trước khi milestone được công bố. PR #1 tiếp tục Draft và chưa merge; chỉ báo hoàn thành sau khi documentation/handoff exact-head matrix cũng xanh.
