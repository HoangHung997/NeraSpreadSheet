# Formula Surface I contract

This document defines validated scalar/reference formula behavior. Dynamic arrays and third-party extension contracts are specified separately.

## 1. Architecture boundary

- Parser/AST own syntax.
- `NeraFormulaEngine` owns evaluation, lazy branches, references, dependencies and errors.
- `BuiltInFormulaFunctionRegistry` delegates to one internal versioned registry.
- `StandardFormulaFunctions.CreateAll()` is the sole built-in aggregation path.
- `FinancialDateMath` owns financial date normalization and day-count basis.
- Platform hosts and OpenXml adapters do not implement formula semantics.

## 2. Counts

- Eager/versioned built-ins: **213**.
- AST/reference-aware built-ins: **18**.
- Scalar/reference total: **231**.
- Dynamic-array built-ins: **5**.
- Complete built-in subsystem: **236 names**.

## 3. Families

- Logical, information, aggregate, math, text/Unicode and date/time foundations.
- Lookup/reference and conditional aggregate foundations.
- Descriptive/order statistics, covariance/regression and advanced distributions.
- Forty financial functions through F002.
- Nineteen engineering functions and twelve database aggregates.

## 4. F002 financial behavior

- `YIELDDISC` computes discounted-security yield using the shared year fraction.
- `PRICEMAT` and `YIELDMAT` share maturity value, accrued interest and settlement-to-maturity fractions and are inverse tested.
- `ACCRINT` supports frequency 1/2/4, basis 0..4, calculation method and bounded first-interest-anchored quasi-coupon schedules.
- `FVSCHEDULE` accepts scalar/range schedules, treats blanks as zero, rejects nonnumeric values, records dependencies and caps schedule size.
- All five functions are deterministic/pure and logical-argument-counted.
- `FVSCHEDULE` is range-capable; the other four are scalar-only.

Full contract: `docs/financial-functions-foundation-contract.md`.

## 5. Errors and dependencies

Unsupported argument kinds or failed coercion return `#VALUE!`. Invalid domains, resource exhaustion and non-finite results return `#NUM!`. Range-aware functions preserve source identity and participate in affected-only recalculation. `FVSCHEDULE` captures its schedule dependency; current security functions declare no hidden dependency.

## 6. Pending

- F003: PRICE, YIELD, DURATION, MDURATION and MIRR.
- Treasury, AMOR and odd-coupon functions.
- Statistical hypothesis tests and confidence intervals.
- Advanced lookup/reference, arrays, LET/LAMBDA, special engineering, compatibility aliases and external providers.

## 7. Gates

F002 requires reference/domain/coercion/descriptor tests, inverse and calculation-method regressions, range/dependency/resource tests, shared registry counts and the complete hosted CI matrix.

PR #1 remains Draft while exact-head CI is red or unknown.
