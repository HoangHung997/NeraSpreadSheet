# Formula Surface I contract

Tài liệu này định nghĩa validated scalar/reference formula behavior. Dynamic arrays, business calendar và third-party extension contracts có tài liệu bổ sung riêng.

## 1. Architecture boundary

- Parser/AST sở hữu syntax, missing arguments và parenthesized reference unions.
- `NeraFormulaEngine` sở hữu evaluation, lazy branches, references, dependencies và errors.
- `NeraDynamicArrayFormulaEngine` sở hữu supported array projection và spill-shaped results.
- `BuiltInFormulaFunctionRegistry` delegate tới một versioned registry nội bộ.
- `StandardFormulaFunctions.CreateAll()` là eager built-in aggregation path duy nhất.
- Platform hosts và OpenXml adapters không triển khai formula semantics.

## 2. Counts

- Eager/versioned built-ins: **239**.
- AST/reference-aware built-ins: **20**.
- Scalar/reference total: **259**.
- Dynamic-array built-ins: **7**.
- Complete built-in subsystem: **266 names**.

## 3. Families

- Logical, information, aggregate, math, text/Unicode và date/time foundations.
- Lookup/reference, lazy selection và conditional aggregate foundations.
- Descriptive/order statistics, covariance/regression và advanced distributions.
- Fifty-six financial functions qua F006.
- F007 business calendar và locale-number parsing.
- F008 reference selection và dynamic-array projection.
- Nineteen engineering functions và twelve database aggregates.

## 4. F008 behavior

- `ADDRESS` là scalar-only versioned function cho A1/R1C1 text, abs modes và optional sheet prefix.
- `AREAS` đếm reference geometry/union mà không đọc static cell values.
- `CHOOSE` truncate scalar selector, chỉ đánh giá selected branch và giữ selected range identity.
- Range được chọn bởi CHOOSE có thể vào eager range-aware function mà không flatten mất dependency source.
- Top-level CHOOSE có dynamic spill bridge cho selected range/supported nested array.
- `CHOOSECOLS` và `CHOOSEROWS` giữ requested order, duplicate và negative-from-end indices.
- Projection index arguments có thể là scalar/range/supported dynamic array; output bị giới hạn 1.000.000 cells.

Full contract: `docs/reference-selection-and-projection-contract.md`.

## 5. Errors và dependencies

- Unsupported argument kinds, invalid reference context hoặc failed coercion trả `#VALUE!`.
- Invalid index, zero/out-of-range projection và malformed union-as-value trả `#VALUE!`.
- Resource/shape overflow trả `#NUM!`.
- Lazy functions capture selector và selected-branch dependencies only.
- Dynamic projection preserves source/index dependencies and uses existing spill recalculation.
- Formula families không khai báo hidden volatile dependency.

## 6. Pending

- F009: COLUMN, COLUMNS, DROP, EXPAND và FORMULATEXT.
- Reference intersection, `A1#`, `@`, array constants và selector-array CHOOSE.
- Remaining advanced lookup/reference/projection, LET/LAMBDA và higher-order functions.
- Statistical hypothesis tests và confidence intervals.
- Full text/regex/byte-width, special engineering, compatibility aliases và external providers.

## 7. Gates

F008 yêu cầu ADDRESS descriptor/bounds/missing-argument tests, AREAS reference-union tests, CHOOSE lazy/dependency/spill tests, projection shape/index tests, shared registry count, 234/234 formula tests và complete hosted CI matrix.

PR #1 giữ Draft trong khi exact-head CI mới nhất đỏ hoặc unknown.
