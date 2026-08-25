# Formula Surface I contract

Tài liệu này định nghĩa validated scalar/reference formula behavior. Dynamic arrays, business calendar và third-party extension contracts có tài liệu bổ sung riêng.

## 1. Architecture boundary

- Parser/AST sở hữu syntax.
- `NeraFormulaEngine` sở hữu evaluation, lazy branches, references, dependencies và errors.
- `BuiltInFormulaFunctionRegistry` delegate tới một versioned registry nội bộ.
- `StandardFormulaFunctions.CreateAll()` là built-in aggregation path duy nhất.
- `FinancialDateMath` sở hữu financial date/quasi-coupon semantics.
- `BusinessDayCalendarMath` sở hữu business-day counting/shifting.
- Platform hosts và OpenXml adapters không triển khai formula semantics.

## 2. Counts

- Eager/versioned built-ins: **238**.
- AST/reference-aware built-ins: **18**.
- Scalar/reference total: **256**.
- Dynamic-array built-ins: **5**.
- Complete built-in subsystem: **261 names**.

## 3. Families

- Logical, information, aggregate, math, text/Unicode và date/time foundations.
- Lookup/reference và conditional aggregate foundations.
- Descriptive/order statistics, covariance/regression và advanced distributions.
- Fifty-six financial functions qua F006.
- F007 business calendar và locale-number parsing.
- Nineteen engineering functions và twelve database aggregates.

## 4. F007 behavior

- `NETWORKDAYS` và `NETWORKDAYS.INTL` đếm inclusive, hỗ trợ signed reversed interval và unique workday holidays.
- `WORKDAY` và `WORKDAY.INTL` truncate days, giữ zero=start và dùng bounded binary search thay vì per-day traversal.
- Weekend code/mask và holiday semantics dùng shared platform-neutral calendar service.
- Holiday ranges giữ source identity và được engine capture vào dependency result.
- `NUMBERVALUE` dùng explicit separators hoặc deterministic `IFormulaLocaleEvaluationContext` defaults; whitespace và trailing percent semantics được khóa.
- Calendar functions expose range capability; `NUMBERVALUE` scalar-only và `ContextReadOnly`.

Full contract: `docs/business-calendar-and-numbervalue-contract.md`.

## 5. Errors và dependencies

- Unsupported argument kinds hoặc failed coercion trả `#VALUE!`.
- Invalid numeric domains, out-of-range dates/results và exhausted budgets trả `#NUM!`.
- Malformed weekend masks và invalid locale separators trả `#VALUE!`.
- Range-aware functions preserve source identity và participate in affected-only recalculation.
- Calendar/date/security functions không khai báo hidden volatile dependency.

## 6. Pending

- F008: ADDRESS, AREAS, CHOOSE, CHOOSECOLS và CHOOSEROWS.
- Advanced lookup/reference, projection arrays, LET/LAMBDA và higher-order functions.
- Statistical hypothesis tests và confidence intervals.
- Full text/regex/byte-width, special engineering, compatibility aliases và external providers.

## 7. Gates

F007 yêu cầu reference/domain/coercion/descriptor tests, holiday range dependency, locale-context behavior, shared registry count, 229/229 formula tests và complete hosted CI matrix.

PR #1 giữ Draft trong khi exact-head CI mới nhất đỏ hoặc unknown.
