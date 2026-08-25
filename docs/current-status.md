# NeraSpreadSheet current implementation status

Tài liệu này là nguồn sự thật cho nhánh phát triển hiện tại. Một capability chỉ được tính là implemented khi có executable source, automated tests và build/runtime gate phù hợp.

## Product rules

- Independent spreadsheet SDK; không phụ thuộc runtime Excel, LibreOffice hoặc DevExpress.
- Formula, dynamic-array, editing, layout, scrolling, calendar, locale-number và printing semantics đều platform-neutral.
- Extension functions phải qua API, capability, state, dependency, conflict và resource validation trước khi đăng ký.
- Numerical solvers, schedule loops, calendar shifting, reference selection, array projection và recursion/array traversal đều deterministic, bounded và fail closed.
- Built-ins dùng một authoritative aggregation path.
- Parser sở hữu syntax/missing arguments/reference unions; scalar engine sở hữu reference identity/lazy selection; dynamic engine sở hữu shape/spill projection.

## Whole-project implementation snapshot

### Core workbook và editing

- Excel-size sparse worksheets, values/formulas/styles/dimensions/merges, immutable snapshots và bounded caches.
- Multi-range selection, spill-aware clipboard, cell editor, commands, sort và Undo/Redo.
- Atomic structural operations với formula/rule/Table/filter/spill mapping.
- Conditional Formatting, Data Validation, stable Tables, AutoFilter, totals và paged native presenters.

### Formula engine và SDK

- Parser/AST, A1/cross-sheet references, parenthesized reference unions, dependency graph, circular detection và affected-only recalculation.
- Shared/structured formulas và Table formula rewrite/projection.
- Function Extension SDK v1.0 với identity/version/API/capability/state/dependency/conflict contracts.
- Built-in eager/versioned registry: **239 names**.
- AST/reference-aware built-ins: **20 names**.
- Dynamic-array built-ins: **7 names**.
- Complete built-in subsystem: **266 names**.
- Formula test registry count dùng một shared test constant.
- Current formula suite: **234/234 passing tests**.

### Formula families

- Logical, aggregate, math, text/Unicode, date/time và lookup/reference foundations.
- Conditional aggregates, descriptive/order statistics, covariance/regression và advanced distributions.
- **56 financial functions** qua F006.
- **19 engineering functions** và **12 database aggregate functions**.
- Dynamic arrays: `SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT`, `UNIQUE`, `CHOOSECOLS`, `CHOOSEROWS`.
- Date compatibility: `DATEDIF`, `DAYS360`, `ISOWEEKNUM`, `WEEKNUM`.
- Business calendar/locale: `NETWORKDAYS`, `NETWORKDAYS.INTL`, `WORKDAY`, `WORKDAY.INTL`, `NUMBERVALUE`.
- Reference selection: `ADDRESS`, `AREAS`, `CHOOSE`.

### F008 — reference selection và dynamic-array projection

F008 bổ sung:

- `ADDRESS` — tạo A1/R1C1 reference text với absolute/relative modes và optional sheet prefix;
- `AREAS` — đếm số area trong cell/range/parenthesized reference union hoặc reference do `CHOOSE` chọn;
- `CHOOSE` — lazy scalar/reference selection, giữ dependency của selector và nhánh được chọn;
- `CHOOSECOLS` — projection cột có thứ tự, duplicate và negative index;
- `CHOOSEROWS` — projection hàng có thứ tự, duplicate và negative index.

Key contracts:

- Parser có `MissingArgumentNode` cho optional arguments bị bỏ qua và `ReferenceUnionNode` cho parenthesized reference union.
- `ADDRESS` truncate row/column/abs_num; row/column phải nằm trong worksheet limits; abs_num `1..4`.
- `ADDRESS` scalar-only, deterministic/pure, logical-argument-counted và đi qua versioned registry.
- `AREAS` là geometry/reference-aware: static reference không tạo value dependency; `AREAS(CHOOSE(...))` chỉ capture selector dependency.
- `CHOOSE` chấp nhận tối đa 254 values; index truncate toward zero và phải trong `1..N`.
- `CHOOSE` chỉ đánh giá nhánh được chọn; nhánh không chọn không tạo error hay dependency.
- Selected range có thể đi vào range-aware eager function và giữ exact source dependency.
- Top-level `CHOOSE` có spill bridge khi nhánh được chọn là range/dynamic array.
- `CHOOSECOLS`/`CHOOSEROWS` nhận source range/scalar/supported nested array và scalar/range/dynamic index arguments.
- Negative index đếm từ cuối; zero/out-of-range trả `#VALUE!`; duplicate và requested order được giữ.
- Projection output dùng `FormulaArrayValue` và giới hạn 1.000.000 cells.
- Unsupported reference union in value/array context trả `#VALUE!`.

Full contract: `docs/reference-selection-and-projection-contract.md`.

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
- CHOOSE selector-array behavior, reference intersection, `A1#`, `@`, array constants và complete reference-return algebra còn pending.
- Remaining advanced lookup/reference projection, LET/LAMBDA và higher-order arrays còn pending.
- Drawings/charts, pivots/slicers, complete theme/style semantics và independent visual corpus còn pending.
- Plugin discovery/trust/isolation, packaging/API compatibility, localization/accessibility, security/fuzzing, recovery và release hardening còn pending.
- External Excel/LibreOffice/ODS differential corpora chưa đủ rộng cho production compatibility claim.
- Dự án hiện là engineering-complete MVP foundation, chưa phải production release.

## Weighted progress estimate

- Engine/viewport/renderer foundation: khoảng `92%`.
- Basic spreadsheet MVP: khoảng `97–98%`.
- Complete professional roadmap: khoảng `84–85%`.
- Production release readiness: khoảng `62–65%`.

Đây là engineering-weighted estimates, không phải checkbox counts.

## Next implementation work

1. F009: `COLUMN`, `COLUMNS`, `DROP`, `EXPAND`, `FORMULATEXT`.
2. Tiếp tục tự động qua `docs/formula-completion-master-schedule.md` theo exact five-function milestones.
3. Giữ PR #1 Draft cho tới khi formula catalog, differential/fuzz, provider-isolation và release gates hoàn tất.

## Validation state

F008 exact implementation head `775a24dfa2fa9dc059896d5445179077b4ffe641` build với zero warnings/errors, qua architecture verification và **234/234 formula tests** trong CI #880. Public F008 completion còn yêu cầu documentation/handoff exact-head hosted matrix xanh. PR #1 vẫn Draft và chưa merge.
