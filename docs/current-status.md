# NeraSpreadSheet current implementation status

Tài liệu này là nguồn sự thật cho nhánh phát triển hiện tại. Một capability chỉ được tính là implemented khi có executable source, automated tests và build/runtime gate phù hợp.

## Product rules

- Independent spreadsheet SDK; không phụ thuộc runtime Excel, LibreOffice hoặc DevExpress.
- Formula, dynamic-array, editing, layout, scrolling, date compatibility và printing semantics đều platform-neutral.
- Extension functions phải qua API, capability, state, dependency, conflict và resource validation trước khi đăng ký.
- Numerical solvers, schedule loops, recursion/array traversal và special-function primitives đều deterministic, bounded và fail closed.
- Built-ins dùng một authoritative aggregation path.
- Financial date/basis semantics và quasi-coupon ratios nằm trong `FinancialDateMath`; date/week compatibility nằm trong một family độc lập với host UI.

## Whole-project implementation snapshot

### Core workbook và editing

- Excel-size sparse worksheets, values/formulas/styles/dimensions/merges, immutable snapshots và bounded caches.
- Multi-range selection, spill-aware clipboard, cell editor, commands, sort và Undo/Redo.
- Atomic structural operations với formula/rule/Table/filter/spill mapping.
- Conditional Formatting, Data Validation, stable Tables, AutoFilter, totals và paged native presenters.

### Formula engine và SDK

- Parser/AST, A1/cross-sheet references, dependency graph, circular detection và affected-only recalculation.
- Shared/structured formulas và Table formula rewrite/projection.
- Function Extension SDK v1.0 với identity/version/API/capability/state/dependency/conflict contracts.
- Built-in eager/versioned registry: **233 names**.
- AST/reference-aware built-ins: **18 names**.
- Dynamic-array built-ins: **5 names**.
- Complete built-in subsystem: **256 names**.
- Formula test registry count dùng một shared test constant.
- Current formula suite: **224/224 passing tests**.

### Formula families

- Logical, aggregate, math, text/Unicode, date/time và lookup/reference foundations.
- Conditional aggregates, descriptive/order statistics, covariance/regression và advanced distributions.
- **56 financial functions** qua F006.
- **19 engineering functions** và **12 database aggregate functions**.
- Dynamic arrays: `SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT`, `UNIQUE`.
- Date compatibility: `DATEDIF`, `DAYS360`, `ISOWEEKNUM`, `WEEKNUM`.

### F006 — odd-last yield và date compatibility

F006 bổ sung:

- `ODDLYIELD` — algebraic inverse của validated `ODDLPRICE` state;
- `DATEDIF` — legacy completed year/month/day và residual-unit behavior;
- `DAYS360` — US NASD hoặc European 30/360 với signed interval;
- `ISOWEEKNUM` — ISO 8601 week number;
- `WEEKNUM` — system-one start-day modes và return type 21 cho ISO system two.

Key contracts:

- `ODDLYIELD` dùng cùng strict state `last_coupon < settlement < maturity`, frequency `1/2/4`, basis `0..4` và theoretical coupon boundary như `ODDLPRICE`.
- `ODDLYIELD` yêu cầu rate không âm, price/redemption dương; finite negative yield vẫn hợp lệ nếu phương trình tạo ra.
- `DATEDIF` nhận unit không phân biệt hoa thường: `Y`, `M`, `D`, `MD`, `YM`, `YD`; start lớn hơn end hoặc unit không hợp lệ trả `#NUM!`.
- `DATEDIF` giữ legacy `MD` behavior, kể cả kết quả âm trong một số month-end scenario.
- `DAYS360` chuẩn hóa ngày nguyên, dùng US NASD khi method false/omitted và European 30/360 khi true; interval đảo chiều trả kết quả đổi dấu.
- `WEEKNUM` hỗ trợ return type `1`, `2`, `11..17`, `21`; `ISOWEEKNUM` và type `21` dùng ISO week-year rules.
- Tất cả năm descriptor đều scalar-only, deterministic/pure và logical-argument-counted.
- Unsupported range/coercion trả `#VALUE!`; invalid domains hoặc non-finite results trả `#NUM!`.

Earlier annuity/root, cash-flow, payment, depreciation, scalar-rate, calendar và F001–F005 contracts không thay đổi.

### Rendering, hosts và scrolling

- Fractional pixel scrolling, freeze/split panes và shared display-list pipeline.
- WPF Direct2D, WinForms/GDI+ và .NET MAUI Skia GPU hosts.
- Hosted validation cho Windows desktop rendering, Android, iOS, Mac Catalyst và MAUI Windows loaded contexts.

### XLSX, data exchange, printing và PDF

- XLSX cells, formulas, styles, panes, rules, Tables/filters và current print settings.
- Unknown package-part preservation.
- Streaming CSV/TSV.
- Deterministic pagination, print preview, staged Skia PDF export và desktop print adapters.

## Conservative limitations

- Formula surface chưa đạt complete Excel/OpenFormula compatibility.
- Business-day/holiday conventions và locale-aware number parsing còn pending.
- Advanced lookup/reference projection, LET/LAMBDA, higher-order arrays và complete spill syntax/UX còn pending.
- Drawings/charts, pivots/slicers, complete theme/style semantics và independent visual corpus còn pending.
- Plugin discovery/trust/isolation, packaging/API compatibility, localization/accessibility, security/fuzzing, recovery và release hardening còn pending.
- External Excel/LibreOffice/ODS differential corpora chưa đủ rộng cho production compatibility claim.
- Dự án hiện là engineering-complete MVP foundation, chưa phải production release.

## Weighted progress estimate

- Engine/viewport/renderer foundation: khoảng `92%`.
- Basic spreadsheet MVP: khoảng `96–98%`.
- Complete professional roadmap: khoảng `82–83%`.
- Production release readiness: khoảng `60–63%`.

Đây là engineering-weighted estimates, không phải checkbox counts.

## Next implementation work

1. F007: `NETWORKDAYS`, `NETWORKDAYS.INTL`, `WORKDAY`, `WORKDAY.INTL`, `NUMBERVALUE`.
2. Tiếp tục tự động qua `docs/formula-completion-master-schedule.md` theo exact five-function milestones.
3. Giữ PR #1 Draft cho tới khi formula catalog, differential/fuzz, provider-isolation và release gates hoàn tất.

## Validation state

F006 implementation head `c43bf362054110940f149a144546c4bba13387e3` build với zero warnings/errors, qua architecture verification và **224/224 formula tests**. Public F006 completion còn yêu cầu documentation/handoff exact-head hosted matrix xanh. PR #1 vẫn Draft và chưa merge.
