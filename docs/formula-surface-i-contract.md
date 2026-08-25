# Formula Surface I contract

This document defines validated scalar/reference formula behavior. Dynamic arrays and third-party extension contracts are specified separately.

## 1. Architecture boundary

- Parser/AST own syntax.
- `NeraFormulaEngine` owns evaluation, lazy branches, references, dependencies and errors.
- `BuiltInFormulaFunctionRegistry` delegates to one internal versioned registry.
- `StandardFormulaFunctions.CreateAll()` is the sole built-in aggregation path.
- `FinancialDateMath` owns financial date normalization, day-count basis and bounded coupon-period ratios.
- Platform hosts and OpenXml adapters do not implement formula semantics.

## 2. Counts

- Eager/versioned built-ins: **228**.
- AST/reference-aware built-ins: **18**.
- Scalar/reference total: **246**.
- Dynamic-array built-ins: **5**.
- Complete built-in subsystem: **251 names**.

## 3. Families

- Logical, information, aggregate, math, text/Unicode and date/time foundations.
- Lookup/reference and conditional aggregate foundations.
- Descriptive/order statistics, covariance/regression and advanced distributions.
- Fifty-five financial functions through F005.
- Nineteen engineering functions and twelve database aggregates.

## 4. F005 financial behavior

- `AMORLINC` and `AMORDEGRC` are scalar-only French-accounting depreciation functions with date-only normalization, supported basis restrictions and explicit period/resource bounds.
- `ODDFPRICE` and `ODDFYIELD` share one strict odd-first quasi-coupon state and one exact clean-price equation.
- `ODDFYIELD` uses bounded log-domain bisection and is round-trip tested against `ODDFPRICE`.
- `ODDLPRICE` derives odd-last period ratios from the next theoretical coupon boundary on or after maturity.
- All five F005 descriptors are deterministic/pure, scalar-returning and logical-argument-counted.
- Quasi-coupon traversal is capped at 100,000 periods; odd-first yield solving is capped at 256 iterations.

Full contract: `docs/financial-functions-foundation-contract.md`.

## 5. Errors and dependencies

Unsupported argument kinds or failed coercion return `#VALUE!`. Invalid domains, resource exhaustion and non-finite results return `#NUM!`. DOLLAR denominators that truncate below one return `#DIV/0!`. Range-aware functions preserve source identity and participate in affected-only recalculation. `FVSCHEDULE` and `MIRR` capture range dependencies; current security/calendar functions declare no hidden dependency.

## 6. Pending

- F006: `ODDLYIELD`, `DATEDIF`, `DAYS360`, `ISOWEEKNUM`, `WEEKNUM`.
- Business-day and holiday functions.
- Statistical hypothesis tests and confidence intervals.
- Advanced lookup/reference, arrays, LET/LAMBDA, special engineering, compatibility aliases and external providers.

## 7. Gates

F005 requires published-reference, domain, coercion, descriptor and round-trip regressions, bounded schedule/solver behavior, shared registry counts and the complete hosted CI matrix.

PR #1 remains Draft while exact-head CI is red or unknown.
