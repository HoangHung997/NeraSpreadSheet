# Formula Surface I contract

This document defines validated scalar/reference formula behavior. Dynamic arrays and third-party extension contracts are specified separately.

## 1. Architecture boundary

- Parser/AST own syntax.
- `NeraFormulaEngine` owns evaluation, lazy branches, references, dependencies and errors.
- `BuiltInFormulaFunctionRegistry` delegates to one internal versioned registry.
- `StandardFormulaFunctions.CreateAll()` is the sole built-in aggregation path.
- Platform hosts and OpenXml adapters do not implement formula semantics.

## 2. Counts

- Eager/versioned built-ins: **196**.
- AST/reference-aware built-ins: **18**.
- Scalar/reference total: **214**.
- Dynamic-array built-ins: **5**.
- Complete built-in subsystem: **219 names**.

## 3. Families

- Logical, information, aggregate, math, text/Unicode and date/time foundations.
- Lookup/reference and conditional aggregate foundations.
- Descriptive/order statistics, covariance/regression and advanced distributions.
- Twenty-three financial functions, including scalar helpers `ISPMT`, `EFFECT`, `NOMINAL`, `RRI`, `PDURATION`.
- Nineteen engineering functions and twelve database aggregate functions.

## 4. Financial scalar behavior

- `ISPMT` uses a zero-based equal-principal schedule.
- `EFFECT` and `NOMINAL` truncate compounding periods and are inverse-tested.
- `RRI` and `PDURATION` require positive domains and are inverse-tested.
- Financial `log1p` uses a 64-term series for `|x| <= 0.5`, preventing cancellation at very small rates.
- All five functions are scalar-only, deterministic/pure and declare no hidden dependency.

Full contract: `docs/financial-functions-foundation-contract.md`.

## 5. Errors and dependencies

Unsupported argument kinds or failed coercion return `#VALUE!`. Invalid domains, resource exhaustion and non-convergence return `#NUM!`. Range-aware functions preserve source identity and participate in affected-only recalculation. Scalar helper functions enter ordinary scalar dependencies only.

## 6. Pending

- Complete locale/coercion compatibility.
- Financial calendar/day-count and coupon-date functions.
- Bond/treasury/price/yield/duration and AMOR functions.
- Statistical hypothesis tests and confidence intervals.
- Advanced lookup/reference, arrays and LET/LAMBDA.
- Special engineering, database expression criteria, cube functions and external differential corpora.

## 7. Gates

Formula changes require result/domain/coercion/descriptor tests, numerical-stability and inverse/reconciliation tests where applicable, dependency/resource tests and the complete hosted CI matrix.

PR #1 remains Draft while exact-head CI is red or unknown.
