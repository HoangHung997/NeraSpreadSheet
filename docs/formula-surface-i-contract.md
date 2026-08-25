# Formula Surface I contract

Tài liệu này định nghĩa validated scalar/reference formula behavior. Dynamic arrays và third-party extension contracts được đặc tả riêng.

## 1. Architecture boundary

- Parser/AST sở hữu syntax.
- `NeraFormulaEngine` sở hữu evaluation, lazy branches, references, dependencies và errors.
- `BuiltInFormulaFunctionRegistry` delegate tới một internal versioned registry.
- `StandardFormulaFunctions.CreateAll()` là sole built-in aggregation path.
- `FinancialDateMath` sở hữu financial date normalization, basis và coupon/quasi-coupon semantics.
- `DateCompatibilityFormulaFunctions` sở hữu DATEDIF/DAYS360/week-number behavior độc lập với UI host.
- Platform hosts và OpenXml adapters không implement formula semantics.

## 2. Counts

- Eager/versioned built-ins: **233**.
- AST/reference-aware built-ins: **18**.
- Scalar/reference total: **251**.
- Dynamic-array built-ins: **5**.
- Complete built-in subsystem: **256 names**.
- Automated formula suite: **224/224**.

## 3. Families

- Logical, information, aggregate, math, text/Unicode và date/time foundations.
- Lookup/reference và conditional aggregate foundations.
- Descriptive/order statistics, covariance/regression và advanced distributions.
- Fifty-six financial functions qua F006.
- Date compatibility: `DATEDIF`, `DAYS360`, `ISOWEEKNUM`, `WEEKNUM`.
- Nineteen engineering functions và twelve database aggregates.

## 4. F005/F006 financial behavior

- `AMORLINC` và `AMORDEGRC` dùng bounded French-depreciation states.
- `ODDFPRICE` và `ODDFYIELD` chia odd-first period thành bounded quasi-coupon ratios và dùng cùng exact equation.
- `ODDLPRICE` và `ODDLYIELD` dùng cùng odd-last date state và là algebraic inverses.
- Odd-coupon functions chỉ nhận scalar arguments, frequency `1/2/4`, basis `0..4` và strict date order.
- Yield/domain/schedule operations đều finite-checked và bounded.

Full financial contract: `docs/financial-functions-foundation-contract.md`.

## 5. F006 date compatibility behavior

### `DATEDIF(start_date,end_date,unit)`

- Units không phân biệt hoa thường: `Y`, `M`, `D`, `MD`, `YM`, `YD`.
- `Y` và `M` trả completed units; `D` trả actual whole days.
- `MD`, `YM`, `YD` giữ legacy residual-unit behavior.
- `MD` có thể trả âm trong known month-end scenarios để giữ compatibility.
- start lớn hơn end hoặc unit không hợp lệ trả `#NUM!`.

### `DAYS360(start_date,end_date,[method])`

- method false/omitted: US NASD 30/360.
- method true: European 30/360.
- Date-only normalization.
- Reversed interval đổi dấu kết quả.

### `ISOWEEKNUM(date)`

ISO 8601 week number với Monday-start và first week chứa ít nhất bốn ngày của năm mới.

### `WEEKNUM(date,[return_type])`

- System-one modes: `1`, `2`, `11..17`.
- ISO system-two mode: `21`.
- Invalid return type trả `#NUM!`.

## 6. Errors và dependencies

Unsupported argument kinds hoặc failed coercion trả `#VALUE!`. Invalid domains, exhausted budgets và non-finite results trả `#NUM!`. DOLLAR denominators truncate dưới một trả `#DIV/0!`. Range-aware functions giữ source identity và tham gia affected-only recalculation. `FVSCHEDULE` và `MIRR` capture range dependencies; current security/date functions không có hidden dependency.

## 7. Pending

- F007: NETWORKDAYS/WORKDAY families và NUMBERVALUE.
- Statistical hypothesis tests và confidence intervals.
- Advanced lookup/reference, arrays, LET/LAMBDA, special engineering và compatibility aliases.
- External providers, trust/isolation và offline behavior.
- Full Microsoft/OpenFormula catalog audit và differential/fuzz corpus.

## 8. Gates

F006 yêu cầu published/reference values, inverse/reconciliation regressions, date/week edge tests, domain/coercion/descriptor tests, shared registry counts và complete hosted CI matrix.

PR #1 giữ Draft khi exact-head CI red hoặc unknown.
