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

- Eager/versioned built-ins: **223**.
- AST/reference-aware built-ins: **18**.
- Scalar/reference total: **241**.
- Dynamic-array built-ins: **5**.
- Complete built-in subsystem: **246 names**.

## 3. Families

- Logical, information, aggregate, math, text/Unicode and date/time foundations.
- Lookup/reference and conditional aggregate foundations.
- Descriptive/order statistics, covariance/regression and advanced distributions.
- Fifty financial functions through F004.
- Nineteen engineering functions and twelve database aggregates.

## 4. F003/F004 financial behavior

- `PRICE`, `YIELD`, `DURATION` and `MDURATION` share one regular maturity-anchored coupon state.
- `YIELD` uses a bounded inverse solve over the exact clean-price equation.
- `MIRR` is range-capable, preserves positional timing, captures source dependencies and caps the input at 2,000,000 values.
- `TBILLEQ`, `TBILLPRICE` and `TBILLYIELD` are scalar-only and use actual settlement-to-maturity days with a one-calendar-year upper boundary.
- `DOLLARDE` and `DOLLARFR` are scalar-only, truncate the denominator and preserve signed round trips.
- All ten F003/F004 descriptors are deterministic/pure and logical-argument-counted.

Full contract: `docs/financial-functions-foundation-contract.md`.

## 5. Errors and dependencies

Unsupported argument kinds or failed coercion return `#VALUE!`. Invalid domains, resource exhaustion and non-finite results return `#NUM!`. DOLLAR denominators that truncate below one return `#DIV/0!`. Range-aware functions preserve source identity and participate in affected-only recalculation. `FVSCHEDULE` and `MIRR` capture range dependencies; current security/calendar functions declare no hidden dependency.

## 6. Pending

- F005: AMORLINC, AMORDEGRC, ODDFPRICE, ODDFYIELD and ODDLPRICE.
- Remaining odd-coupon and business-day functions.
- Statistical hypothesis tests and confidence intervals.
- Advanced lookup/reference, arrays, LET/LAMBDA, special engineering, compatibility aliases and external providers.

## 7. Gates

F003/F004 require reference/domain/coercion/descriptor tests, inverse/reconciliation regressions, range/dependency/resource tests, shared registry counts and the complete hosted CI matrix.

PR #1 remains Draft while exact-head CI is red or unknown.
