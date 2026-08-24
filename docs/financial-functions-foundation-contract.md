# Financial Functions Foundation contract

This document defines the validated financial-function family owned by NeraSpreadSheet. Excel and LibreOffice are compatibility references only; they are not runtime dependencies.

## 1. Architecture boundary

- All functions are registered through Function Extension SDK API `1.0`.
- Every descriptor uses namespace `NERA.BUILTIN`, implementation version `1.0.0` and logical-argument counting.
- Financial semantics remain independent from UI and OpenXml projects.
- Current financial functions return scalars only.
- Root solvers, schedules and numerical primitives are deterministic, bounded and fail closed.
- `StandardFormulaFunctions.CreateAll()` is the single built-in aggregation path.
- `FinancialDateMath` is the single source for basis, coupon dates and coupon-period day counts used by future security functions.

## 2. Registered functions

Thirty financial names are registered:

- `PV`, `FV`, `PMT`, `NPER`, `RATE`;
- `NPV`, `IRR`, `XNPV`, `XIRR`;
- `IPMT`, `PPMT`, `CUMIPMT`, `CUMPRINC`, `ISPMT`;
- `SLN`, `SYD`, `DB`, `DDB`, `VDB`;
- `EFFECT`, `NOMINAL`, `RRI`, `PDURATION`;
- `YEARFRAC`, `COUPDAYBS`, `COUPDAYS`, `COUPDAYSNC`, `COUPNCD`, `COUPPCD`, `COUPNUM`.

The eager/versioned registry contains **203 names**. Together with 18 AST/reference-aware and five dynamic-array names, the built-in subsystem recognizes **226 names**.

## 3. Coercion and errors

- Required scalars accept finite number/date values, Boolean coercion and invariant numeric/date text where shared coercion allows it.
- Financial dates are reduced to date-only values before day-count evaluation.
- Unsupported range use or failed scalar coercion returns `#VALUE!`.
- Supplied formula errors propagate before invocation.
- Invalid basis/frequency/date order, excessive budgets, non-finite results and numerical non-convergence return `#NUM!`.
- Basis and frequency arguments are truncated toward zero before validation.

## 4. Existing annuity, cash-flow, schedule and depreciation contracts

Earlier validated behavior remains unchanged:

- `PV`, `FV`, `PMT`, `NPER`, `RATE` share annuity signs/timing and bounded roots.
- `NPV`, `IRR`, `XNPV`, `XIRR` preserve ordering, dated schedules, dependencies and resource limits.
- `IPMT`, `PPMT`, `CUMIPMT`, `CUMPRINC` share payment/balance equations and reconciliation.
- `ISPMT` uses a zero-based equal-principal schedule.
- `SLN`, `SYD`, `DB`, `DDB`, `VDB` preserve salvage caps, optional factors, partial intervals and bounded schedules.
- `EFFECT`, `NOMINAL`, `RRI`, `PDURATION` use stable logarithmic/exponential primitives and inverse round trips.

## 5. Financial day-count basis

The shared basis enumeration is:

| Basis | Convention | Numerator | Denominator / coupon period |
|---:|---|---|---|
| 0 | US NASD 30/360 | US 30/360 with February/end-of-month rules | 360 |
| 1 | Actual/Actual | Actual calendar days | leap-aware actual year or multi-year average |
| 2 | Actual/360 | Actual calendar days | 360 |
| 3 | Actual/365 | Actual calendar days | 365 |
| 4 | European 30/360 | both day values capped at 30 | 360 |

`YEARFRAC(start_date,end_date,[basis])` returns the signed fraction between dates. Equal dates return zero. Reversed dates return the negative of the corresponding ordered interval under Nera's explicit signed-interval contract.

Actual/Actual behavior:

- one calendar year wholly inside a leap year uses 366;
- a short cross-year interval uses 366 when it includes February 29, otherwise 365;
- spans longer than one year divide by the average length of the covered calendar years.

## 6. Coupon schedule construction

Coupon functions accept:

```text
(settlement, maturity, frequency, [basis])
```

Contracts:

- settlement must be strictly earlier than maturity;
- frequency is truncated and must be `1`, `2` or `4`;
- basis defaults to 0 and must be in `0..4`;
- coupon months are `12/frequency`;
- dates are generated directly from the maturity anchor for every step;
- an end-of-month maturity remains end-of-month in every coupon month, including leap-year February;
- search is bounded at 100.000 coupon periods;
- the previous coupon may equal settlement; the next coupon is always strictly after settlement.

Anchoring every candidate to maturity, rather than repeatedly subtracting from the prior candidate, prevents permanent day drift after a short month.

## 7. Coupon functions

### `COUPPCD`

Returns the coupon date on or immediately before settlement.

### `COUPNCD`

Returns the coupon date immediately after settlement.

### `COUPNUM`

Returns the number of coupon dates strictly after settlement through maturity. The count is derived from the same bounded schedule used by PCD/NCD.

### `COUPDAYBS`

Returns the basis-specific day count from previous coupon to settlement.

### `COUPDAYS`

Returns days in the coupon period:

- basis 1: actual days from PCD to NCD;
- bases 0, 2 and 4: `360/frequency`;
- basis 3: `365/frequency`.

### `COUPDAYSNC`

Returns days from settlement to next coupon:

- bases 1, 2 and 3: actual/basis day count from settlement to NCD;
- bases 0 and 4: fixed coupon-period days minus `COUPDAYBS`.

## 8. SDK metadata

All 30 financial descriptors are deterministic/pure, scalar-returning and logical-argument-counted. `NPV`, `IRR`, `XNPV`, `XIRR` accept ranges; all other current financial functions are scalar-only. Calendar functions declare no hidden or volatile dependency.

## 9. Automated validation

Financial calendar promotion requires:

1. `YEARFRAC` references for every basis;
2. signed and equal-date intervals;
3. leap-year, February last-day and US/European 30/360 differences;
4. official semiannual PCD/NCD/day/count references;
5. basis-specific coupon-period days;
6. maturity-anchored end-of-month behavior across February;
7. exact-coupon-date PCD/NCD/count behavior;
8. frequency/basis truncation and validation;
9. scalar-only, coercion, descriptor and registry-count regressions;
10. complete Core/architecture/Windows/Android/iOS/Mac Catalyst/MAUI Windows matrix.

## 10. Deliberately pending

- discount/maturity security functions;
- fixed-coupon price, yield and duration functions;
- treasury, AMOR and odd-first/odd-last coupon periods;
- business-day/holiday adjustment conventions;
- external Excel/LibreOffice differential corpus and financial fuzzing;
- exhaustive symbolic root discovery.

PR #1 remains Draft while a newer exact-head CI is red or unknown.
