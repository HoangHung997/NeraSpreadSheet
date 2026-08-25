# NeraSpreadSheet current implementation status

Tài liệu này là nguồn sự thật cho nhánh phát triển hiện tại. Một capability chỉ được tính là implemented khi có executable source, automated tests và build/runtime gate phù hợp.

## Product rules

- Independent spreadsheet SDK; không phụ thuộc runtime Excel, LibreOffice hoặc DevExpress.
- Formula, dynamic-array, editing, layout, scrolling, calendar, locale-number và printing semantics đều platform-neutral.
- Extension functions phải qua API, capability, state, dependency, conflict và resource validation trước khi đăng ký.
- Numerical solvers, schedule loops, calendar shifting, recursion/array traversal và special-function primitives đều deterministic, bounded và fail closed.
- Built-ins dùng một authoritative aggregation path.
- Financial date/basis và quasi-coupon semantics nằm trong `FinancialDateMath`; business-day semantics nằm trong `BusinessDayCalendarMath`; locale defaults đi qua `IFormulaLocaleEvaluationContext`.

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
- Built-in eager/versioned registry: **238 names**.
- AST/reference-aware built-ins: **18 names**.
- Dynamic-array built-ins: **5 names**.
- Complete built-in subsystem: **261 names**.
- Formula test registry count dùng một shared test constant.
- Current formula suite: **229/229 passing tests**.

### Formula families

- Logical, aggregate, math, text/Unicode, date/time và lookup/reference foundations.
- Conditional aggregates, descriptive/order statistics, covariance/regression và advanced distributions.
- **56 financial functions** qua F006.
- **19 engineering functions** và **12 database aggregate functions**.
- Dynamic arrays: `SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT`, `UNIQUE`.
- Date compatibility: `DATEDIF`, `DAYS360`, `ISOWEEKNUM`, `WEEKNUM`.
- Business calendar/locale: `NETWORKDAYS`, `NETWORKDAYS.INTL`, `WORKDAY`, `WORKDAY.INTL`, `NUMBERVALUE`.

### F007 — business calendar và NUMBERVALUE

F007 bổ sung:

- `NETWORKDAYS` — inclusive business-day count với weekend Saturday/Sunday;
- `NETWORKDAYS.INTL` — signed inclusive count với weekend code hoặc Monday-first seven-character mask;
- `WORKDAY` — dịch chuyển theo ngày làm việc với holiday exclusions;
- `WORKDAY.INTL` — dịch chuyển theo custom weekend và holiday calendar;
- `NUMBERVALUE` — chuyển text thành number bằng explicit hoặc context-provided decimal/group separators.

Key contracts:

- Weekend numeric code được truncate rồi phải thuộc `1..7` hoặc `11..17`.
- Weekend string có đúng bảy ký tự `0/1` theo thứ tự Monday→Sunday; all-ones hợp lệ cho `NETWORKDAYS.INTL` và trả 0, nhưng không hợp lệ cho `WORKDAY.INTL`.
- NETWORKDAYS tính inclusive; interval đảo chiều trả kết quả đổi dấu.
- Holiday scalar/range giữ dependency source, bỏ blank, duplicate và holiday rơi vào weekend.
- Holiday list bị giới hạn ở 2.000.000 values.
- Business-day counting dùng whole weeks cộng remainder; WORKDAY dùng bounded binary search trên khoảng ngày, không quét từng ngày.
- `WORKDAY`/`WORKDAY.INTL` truncate tham số days; zero trả lại start date.
- `NUMBERVALUE` dùng ký tự đầu tiên của separator, bỏ whitespace, hỗ trợ multiple trailing percent signs và reject group separator sau decimal separator.
- Khi separator bị bỏ qua, `NUMBERVALUE` đọc deterministic defaults từ `IFormulaLocaleEvaluationContext`; fallback là invariant `.` decimal và `,` group.
- `NUMBERVALUE` giới hạn text ở 1.000.000 ký tự.
- Calendar functions có range capability để nhận holiday ranges; `NUMBERVALUE` scalar-only và `ContextReadOnly`.
- Unsupported range/coercion trả `#VALUE!`; invalid code/mask domain, out-of-range date/result hoặc exhausted budget trả `#NUM!`/`#VALUE!` theo contract.

Full contract: `docs/business-calendar-and-numbervalue-contract.md`.

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
- Advanced lookup/reference projection, LET/LAMBDA, higher-order arrays và complete spill syntax/UX còn pending.
- Drawings/charts, pivots/slicers, complete theme/style semantics và independent visual corpus còn pending.
- Plugin discovery/trust/isolation, packaging/API compatibility, localization/accessibility, security/fuzzing, recovery và release hardening còn pending.
- External Excel/LibreOffice/ODS differential corpora chưa đủ rộng cho production compatibility claim.
- Business-calendar corpus hiện khóa published/reference cases và internal edge cases, chưa thay thế broad locale/holiday differential validation.
- Dự án hiện là engineering-complete MVP foundation, chưa phải production release.

## Weighted progress estimate

- Engine/viewport/renderer foundation: khoảng `92%`.
- Basic spreadsheet MVP: khoảng `97–98%`.
- Complete professional roadmap: khoảng `83–84%`.
- Production release readiness: khoảng `61–64%`.

Đây là engineering-weighted estimates, không phải checkbox counts.

## Next implementation work

1. F008: `ADDRESS`, `AREAS`, `CHOOSE`, `CHOOSECOLS`, `CHOOSEROWS`.
2. Tiếp tục tự động qua `docs/formula-completion-master-schedule.md` theo exact five-function milestones.
3. Giữ PR #1 Draft cho tới khi formula catalog, differential/fuzz, provider-isolation và release gates hoàn tất.

## Validation state

F007 exact implementation head `95748373b9dde1f0faffe2c61d2ad1262cff7532` build với zero warnings/errors, qua architecture verification và **229/229 formula tests** trong CI #878. Public F007 completion còn yêu cầu documentation/handoff exact-head hosted matrix xanh. PR #1 vẫn Draft và chưa merge.
