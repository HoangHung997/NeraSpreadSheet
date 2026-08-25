# NeraSpreadSheet roadmap

`docs/current-status.md` là nguồn sự thật về implementation. `docs/formula-completion-master-schedule.md` sở hữu hàng đợi formula. Một capability chỉ hoàn thành khi có executable source, automated tests và runtime/build gate phù hợp.

## A. Engine, workbook và viewport

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
- [x] Function Extension SDK API `1.0` và một authoritative registry path.
- [x] Statistical, financial, engineering, database và dynamic-array foundations.
- [x] F001–F006 financial/date milestones.
- [x] F007 business calendar và locale-number parsing.
- [x] F008: `ADDRESS`, `AREAS`, `CHOOSE`, `CHOOSECOLS`, `CHOOSEROWS`.
- [ ] F009: `COLUMN`, `COLUMNS`, `DROP`, `EXPAND`, `FORMULATEXT`.
- [ ] Remaining advanced lookup/reference và dynamic-array projection.
- [ ] LET/LAMBDA, lexical scope, higher-order arrays và recursion budgets.
- [ ] Full text/regex/byte-width compatibility.
- [ ] Complex/unit/special engineering và legacy aliases.
- [ ] Cube/web/data-type/external-state providers và isolation.
- [ ] Final Microsoft/OpenFormula catalog delta = zero.

## C. Reference và dynamic-array foundations

- [x] A1/cross-sheet references, range identity và source dependency capture.
- [x] Missing optional arguments và parenthesized reference-union AST.
- [x] `AREAS` geometry counting không đọc cell values.
- [x] Lazy scalar/reference selection qua `CHOOSE`.
- [x] `CHOOSECOLS`/`CHOOSEROWS` với negative index, duplicate/order preservation và spill ownership.
- [ ] Intersection operator, `A1#`, implicit intersection `@` và full reference-return algebra.
- [ ] CHOOSE selector-array compatibility và array constants.
- [ ] Broader external Excel/LibreOffice differential corpus.

## D. Calendar và financial foundations

- [x] Day-count basis `0..4`, YEARFRAC và coupon date/day/count helpers.
- [x] Maturity securities, regular coupon, treasury bill và odd-coupon price/yield families.
- [x] French-accounting depreciation.
- [x] Date compatibility: `DATEDIF`, `DAYS360`, `ISOWEEKNUM`, `WEEKNUM`.
- [x] Business-day weekend codes/masks, holiday ranges, signed NETWORKDAYS và bounded WORKDAY shifting.
- [x] Deterministic locale-number context và `NUMBERVALUE`.
- [ ] Broader holiday/date/locale differential corpus.

## E. XLSX, data, printing và PDF

- [x] Cells, formulas, styles, panes, rules, Tables/filters và current print settings.
- [x] Unknown package-part preservation.
- [x] Streaming CSV/TSV.
- [x] Deterministic pagination, staged PDF và desktop print adapters.
- [ ] Full dynamic-array metadata, drawings/charts, custom paper và independent visual corpus.

## F. Data controls và product hardening

- [x] Sort, validation, Tables, filters, totals và paged native presenters.
- [x] Exact-head multi-platform CI matrix và repository validation runner.
- [ ] Table/filter manager UI, grouping, virtual data, pivots và slicers.
- [ ] MAUI IME, responsive Ribbon, themes, localization và accessibility.
- [ ] Packaging/API compatibility, plugin trust/isolation, security/fuzzing, recovery và release gates.

## Formula completion execution order

Mỗi public milestone chứa đúng năm tên hàm mới và chỉ hoàn thành sau exact-head hosted CI xanh.

1. **F001 complete:** `ACCRINTM`, `DISC`, `INTRATE`, `RECEIVED`, `PRICEDISC`.
2. **F002 complete:** `YIELDDISC`, `PRICEMAT`, `YIELDMAT`, `ACCRINT`, `FVSCHEDULE`.
3. **F003 complete:** `PRICE`, `YIELD`, `DURATION`, `MDURATION`, `MIRR`.
4. **F004 complete:** `TBILLEQ`, `TBILLPRICE`, `TBILLYIELD`, `DOLLARDE`, `DOLLARFR`.
5. **F005 complete:** `AMORLINC`, `AMORDEGRC`, `ODDFPRICE`, `ODDFYIELD`, `ODDLPRICE`.
6. **F006 complete:** `ODDLYIELD`, `DATEDIF`, `DAYS360`, `ISOWEEKNUM`, `WEEKNUM`.
7. **F007 complete:** `NETWORKDAYS`, `NETWORKDAYS.INTL`, `WORKDAY`, `WORKDAY.INTL`, `NUMBERVALUE`.
8. **F008 complete:** `ADDRESS`, `AREAS`, `CHOOSE`, `CHOOSECOLS`, `CHOOSEROWS`.
9. **F009 next:** `COLUMN`, `COLUMNS`, `DROP`, `EXPAND`, `FORMULATEXT`.
10. Tiếp tục theo dependency pools trong master schedule, rồi catalog audit/differential/fuzz/Codex final acceptance.

## Weighted progress after F008

- Engine/viewport/renderer foundation: khoảng `92%`.
- Basic spreadsheet MVP: khoảng `97–98%`.
- Complete professional roadmap: khoảng `84–85%`.
- Production release readiness: khoảng `62–65%`.

Đây là engineering-weighted estimates, không phải checkbox counts.

## Validation rule

F008 implementation head `775a24dfa2fa9dc059896d5445179077b4ffe641` build với zero warnings/errors, qua architecture verification và **234/234 formula tests** trong CI #880. PR #1 giữ Draft và chưa merge; public milestone chỉ được khóa sau documentation/handoff exact-head CI xanh.
