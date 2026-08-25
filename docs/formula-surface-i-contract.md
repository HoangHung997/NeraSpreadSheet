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

- Eager/versioned built-ins: **208**.
- AST/reference-aware built-ins: **18**.
- Scalar/reference total: **226**.
- Dynamic-array built-ins: **5**.
- Complete built-in subsystem: **231 names**.

## 3. Families

- Logical, information, aggregate, math, text/Unicode and date/time foundations.
- Lookup/reference and conditional aggregate foundations.
- Descriptive/order statistics, covariance/regression and advanced distributions.
- Thirty-five financial functions, including seven calendar/day-count and five maturity-security functions.
- Nineteen engineering functions and twelve database aggregate functions.

## 4. Maturity-security behavior

The surface includes:

- `ACCRINTM(issue,settlement,rate,[par],[basis])`;
- `DISC(settlement,maturity,price,redemption,[basis])`;
- `INTRATE(settlement,maturity,investment,redemption,[basis])`;
- `RECEIVED(settlement,maturity,investment,discount,[basis])`;
- `PRICEDISC(settlement,maturity,discount,redemption,[basis])`.

Shared contracts:

- scalar-only, deterministic/pure, logical argument counting;
- whole-date normalization and basis `0..4` after truncation;
- ordered issue/settlement/maturity dates;
- positive required par/investment/redemption/rate/discount values;
- common `FinancialDateMath.GetYearFraction` primitive;
- `#VALUE!` for unsupported argument kind/coercion and `#NUM!` for invalid financial domains;
- inverse regression between `DISC` and `PRICEDISC`.

Full contract: `docs/financial-functions-foundation-contract.md`.

## 5. Errors and dependencies

Unsupported argument kinds or failed coercion return `#VALUE!`. Invalid domains, resource exhaustion and non-convergence return `#NUM!`. Range-aware functions preserve source identity and participate in affected-only recalculation. Current maturity-security functions are scalar-only and declare no hidden dependency.

## 6. Pending

- F002: YIELDDISC, PRICEMAT, YIELDMAT, ACCRINT and FVSCHEDULE.
- Fixed-coupon bonds, treasury, AMOR and odd-coupon functions.
- Statistical hypothesis tests and confidence intervals.
- Advanced lookup/reference, arrays, LET/LAMBDA, special engineering, compatibility aliases and external providers.

## 7. Gates

Maturity-security changes require reference/domain/coercion/descriptor tests, basis/date-order tests, equation round trips, registry counts and the complete hosted CI matrix.

PR #1 remains Draft while exact-head CI is red or unknown.
