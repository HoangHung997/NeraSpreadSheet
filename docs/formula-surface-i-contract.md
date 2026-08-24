# Formula Surface I contract

This document defines validated scalar/reference formula behavior. Dynamic arrays and third-party extension contracts are specified separately.

## 1. Architecture boundary

- Parser/AST own syntax.
- `NeraFormulaEngine` owns evaluation, lazy branches, references, dependencies and errors.
- `BuiltInFormulaFunctionRegistry` delegates to one internal versioned registry.
- `StandardFormulaFunctions.CreateAll()` is the sole built-in aggregation path.
- `FinancialDateMath` owns financial date normalization, day-count basis and regular coupon schedules.
- Platform hosts and OpenXml adapters do not implement formula semantics.

## 2. Counts

- Eager/versioned built-ins: **203**.
- AST/reference-aware built-ins: **18**.
- Scalar/reference total: **221**.
- Dynamic-array built-ins: **5**.
- Complete built-in subsystem: **226 names**.

## 3. Families

- Logical, information, aggregate, math, text/Unicode and date/time foundations.
- Lookup/reference and conditional aggregate foundations.
- Descriptive/order statistics, covariance/regression and advanced distributions.
- Thirty financial functions, including calendar/day-count functions.
- Nineteen engineering functions and twelve database aggregate functions.

## 4. Financial calendar behavior

The financial formula surface now includes:

- `YEARFRAC(start_date,end_date,[basis])`;
- `COUPDAYBS(settlement,maturity,frequency,[basis])`;
- `COUPDAYS(settlement,maturity,frequency,[basis])`;
- `COUPDAYSNC(settlement,maturity,frequency,[basis])`;
- `COUPNCD(settlement,maturity,frequency,[basis])`;
- `COUPPCD(settlement,maturity,frequency,[basis])`;
- `COUPNUM(settlement,maturity,frequency,[basis])`.

Shared contracts:

- scalar-only, deterministic/pure, logical argument counting;
- whole-date normalization;
- basis `0..4` and frequencies `1/2/4` after truncation;
- settlement strictly before maturity for coupon functions;
- maturity-anchored coupon generation with end-of-month preservation;
- bounded 100.000-period search;
- explicit `#VALUE!` for unsupported argument kinds/coercion and `#NUM!` for invalid financial domains.

`COUPPCD` may equal settlement on a coupon date; `COUPNCD` is strictly later. Coupon period/day/count functions share the same generated PCD/NCD pair and cannot drift independently.

Full contract: `docs/financial-functions-foundation-contract.md`.

## 5. Errors and dependencies

Unsupported argument kinds or failed coercion return `#VALUE!`. Invalid domains, resource exhaustion and non-convergence return `#NUM!`. Range-aware functions preserve source identity and participate in affected-only recalculation. Current financial calendar functions are scalar-only and declare no hidden dependency.

## 6. Pending

- Complete locale/coercion compatibility.
- Discount/maturity security functions.
- Fixed-coupon bond price/yield/duration, treasury, AMOR and odd-coupon functions.
- Statistical hypothesis tests and confidence intervals.
- Advanced lookup/reference, arrays and LET/LAMBDA.
- Special engineering, database expression criteria, cube functions and external differential corpora.

## 7. Gates

Formula calendar changes require result/domain/coercion/descriptor tests, basis-specific references, leap-year/end-of-month/exact-coupon-date tests, schedule-bound tests, registry counts and the complete hosted CI matrix.

PR #1 remains Draft while exact-head CI is red or unknown.
