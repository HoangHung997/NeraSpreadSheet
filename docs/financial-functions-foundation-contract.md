# Financial Functions Foundation contract

This document defines the validated financial-function family owned by NeraSpreadSheet. Excel and LibreOffice are compatibility references only; they are not runtime dependencies.

## 1. Architecture boundary

- All functions are registered through Function Extension SDK API `1.0`.
- Every descriptor uses namespace `NERA.BUILTIN`, implementation version `1.0.0` and logical-argument counting.
- Financial semantics remain independent from UI and OpenXml projects.
- Root solvers, schedules and numerical primitives are deterministic, bounded and fail closed.
- `StandardFormulaFunctions.CreateAll()` is the single built-in aggregation path.
- `FinancialDateMath` is the source for basis, coupon dates, coupon-period day counts and maturity-security year fractions.

## 2. Registered functions

Thirty-five financial names are registered:

- `PV`, `FV`, `PMT`, `NPER`, `RATE`;
- `NPV`, `IRR`, `XNPV`, `XIRR`;
- `IPMT`, `PPMT`, `CUMIPMT`, `CUMPRINC`, `ISPMT`;
- `SLN`, `SYD`, `DB`, `DDB`, `VDB`;
- `EFFECT`, `NOMINAL`, `RRI`, `PDURATION`;
- `YEARFRAC`, `COUPDAYBS`, `COUPDAYS`, `COUPDAYSNC`, `COUPNCD`, `COUPPCD`, `COUPNUM`;
- `ACCRINTM`, `DISC`, `INTRATE`, `RECEIVED`, `PRICEDISC`.

The eager/versioned registry contains **208 names**. Together with 18 AST/reference-aware and five dynamic-array names, the built-in subsystem recognizes **231 names**.

## 3. Coercion and errors

- Required scalars accept finite number/date values, Boolean coercion and invariant numeric/date text where shared coercion permits it.
- Financial dates are reduced to date-only values before evaluation.
- Unsupported range use or failed scalar coercion returns `#VALUE!`.
- Supplied formula errors propagate before invocation.
- Invalid basis/date order, nonpositive required financial values, non-finite results and invalid denominators return `#NUM!`.
- Basis arguments are truncated toward zero before validation in `0..4`.

## 4. Existing contracts

Earlier validated behavior remains unchanged:

- annuity roots and dated cash flows use bounded, nearest-guess and resource-limited algorithms;
- payment decomposition/cumulative schedules reconcile to PMT;
- depreciation families cap at salvage and bound schedule length;
- scalar rate/growth helpers use stable logarithmic/exponential primitives;
- financial calendar functions use maturity-anchored coupon schedules and five day-count bases.

## 5. Maturity-security equations

Let:

```text
Y = YEARFRAC(start_date, end_date, basis)
```

All functions require ordered dates and positive inputs noted below.

### `ACCRINTM(issue, settlement, rate, [par], [basis])`

- `issue < settlement`, `rate > 0`, `par > 0`;
- `par` defaults to 1000; basis defaults to 0;
- result:

```text
par × rate × YEARFRAC(issue, settlement, basis)
```

### `DISC(settlement, maturity, price, redemption, [basis])`

- `settlement < maturity`, `price > 0`, `redemption > 0`;
- result:

```text
(redemption - price) / (redemption × Y)
```

A price above redemption is permitted and produces a negative discount rate; zero or non-finite denominator fails closed.

### `INTRATE(settlement, maturity, investment, redemption, [basis])`

- investment and redemption must be positive;
- result:

```text
(redemption - investment) / (investment × Y)
```

### `RECEIVED(settlement, maturity, investment, discount, [basis])`

- investment and discount must be positive;
- result:

```text
investment / (1 - discount × Y)
```

The denominator must remain strictly positive.

### `PRICEDISC(settlement, maturity, discount, redemption, [basis])`

- discount and redemption must be positive;
- result:

```text
redemption × (1 - discount × Y)
```

The resulting price must remain strictly positive.

`DISC(PRICEDISC(...), redemption, basis)` is regression-tested to recover the original discount within floating-point tolerance.

## 6. Financial day-count basis

| Basis | Convention | Denominator |
|---:|---|---:|
| 0 | US NASD 30/360 | 360 |
| 1 | Actual/Actual | leap-aware actual year / covered-year average |
| 2 | Actual/360 | 360 |
| 3 | Actual/365 | 365 |
| 4 | European 30/360 | 360 |

The same year-fraction primitive drives `YEARFRAC` and the five maturity-security functions; no security function owns a duplicate day-count implementation.

## 7. SDK metadata

All 35 financial descriptors are deterministic/pure, scalar-returning and logical-argument-counted. `NPV`, `IRR`, `XNPV`, `XIRR` accept ranges; all other current financial functions are scalar-only and declare no hidden or volatile dependency.

## 8. Automated validation

F001 promotion requires:

1. official/reference values for all five new functions;
2. default-par behavior for `ACCRINTM`;
3. `DISC`/`PRICEDISC` inverse reconciliation;
4. basis truncation and all date/value domain errors;
5. scalar-only capability rejection;
6. descriptor identity/version/API/volatility/security checks;
7. registry-count regression at 208 eager names;
8. complete Core/architecture/Windows/Android/iOS/Mac Catalyst/MAUI Windows matrix.

## 9. Deliberately pending

- `YIELDDISC`, `PRICEMAT`, `YIELDMAT`, `ACCRINT`, `FVSCHEDULE`;
- fixed-coupon price, yield and duration functions;
- treasury, AMOR and odd-first/odd-last coupon periods;
- business-day/holiday adjustment conventions;
- external Excel/LibreOffice differential corpus and financial fuzzing.

PR #1 remains Draft while a newer exact-head CI is red or unknown.
