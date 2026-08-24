# Financial Functions Foundation contract

This document defines the validated financial-function family owned by NeraSpreadSheet. Excel and LibreOffice are compatibility references only; they are not runtime dependencies.

## 1. Architecture boundary

- All functions are registered through Function Extension SDK API `1.0`.
- Every descriptor uses namespace `NERA.BUILTIN`, implementation version `1.0.0` and logical-argument counting.
- Financial semantics remain independent from UI and OpenXml projects.
- Current financial functions return scalars only.
- Root solvers, schedules and numerical primitives are deterministic, bounded and fail closed.
- `StandardFormulaFunctions.CreateAll()` is the single built-in aggregation path.

## 2. Registered functions

Twenty-three financial names are registered:

- `PV`, `FV`, `PMT`, `NPER`, `RATE`;
- `NPV`, `IRR`, `XNPV`, `XIRR`;
- `IPMT`, `PPMT`, `CUMIPMT`, `CUMPRINC`, `ISPMT`;
- `SLN`, `SYD`, `DB`, `DDB`, `VDB`;
- `EFFECT`, `NOMINAL`, `RRI`, `PDURATION`.

The eager/versioned registry contains **196 names**. Together with 18 AST/reference-aware and five dynamic-array names, the built-in subsystem recognizes **219 names**.

## 3. Coercion and errors

- Required scalars accept finite number/date values, Boolean coercion and invariant numeric text.
- Unsupported range use or nonnumeric scalar text returns `#VALUE!`.
- Supplied formula errors propagate before invocation.
- Invalid domains, excessive budgets, non-finite results and numerical non-convergence return `#NUM!`.

## 4. Existing annuity, cash-flow, schedule and depreciation contracts

Earlier validated behavior remains unchanged:

- `PV`, `FV`, `PMT`, `NPER`, `RATE` share annuity signs/timing and bounded roots.
- `NPV`, `IRR`, `XNPV`, `XIRR` preserve ordering, dated schedules, dependencies and resource limits.
- `IPMT`, `PPMT`, `CUMIPMT`, `CUMPRINC` share payment/balance equations and reconciliation.
- `SLN`, `SYD`, `DB`, `DDB`, `VDB` preserve salvage caps, optional factors, partial intervals and bounded schedules.

## 5. Scalar helper batch

### `ISPMT(rate, per, nper, pv)`

- scalar-only deterministic/pure function;
- models equal principal repayment rather than equal total payment;
- requires finite `nper > 0` and `0 <= per <= nper`;
- period coordinate is zero-based;
- returns:

```text
pv * rate * (per / nper - 1)
```

- positive principal/rate normally produces negative interest; a negative principal flips the sign.

### `EFFECT(nominal_rate, npery)`

- requires `nominal_rate > 0`;
- truncates `npery` toward zero and requires the result to be at least 1;
- computes:

```text
expm1(npery * log1p(nominal_rate / npery))
```

- overflow or non-finite results return `#NUM!`.

### `NOMINAL(effect_rate, npery)`

- requires `effect_rate > 0`;
- uses the same truncated `npery` contract;
- computes:

```text
npery * expm1(log1p(effect_rate) / npery)
```

- automated tests require `NOMINAL(EFFECT(r,n),n) == r` within floating-point tolerance.

### `RRI(nper, pv, fv)`

- requires positive finite `nper`, `pv` and `fv`;
- computes the equivalent periodic growth rate:

```text
expm1(log(fv / pv) / nper)
```

- uses a stable logarithmic ratio path; equal `pv`/`fv` returns zero.

### `PDURATION(rate, pv, fv)`

- requires positive finite `rate`, `pv` and `fv`;
- computes:

```text
log(fv / pv) / log1p(rate)
```

- equal `pv`/`fv` returns zero;
- tests require `PDURATION(RRI(...),...)` and `RRI(PDURATION(...),...)` round trips.

## 6. Financial `log1p` primitive

To avoid cancellation in `log(1+x)`:

- for `|x| <= 0.5`, Nera evaluates the alternating Taylor series for 64 terms;
- outside that interval it uses `Math.Log(1+x)`;
- `expm1` retains a bounded series near zero;
- this path is used by `RATE`, `EFFECT`, `NOMINAL`, `RRI` and `PDURATION`;
- regression includes nominal rates at `1e-12` with one million compounding periods.

## 7. SDK metadata

All 23 financial descriptors are deterministic/pure, return one scalar and use logical argument counting. `NPV`, `IRR`, `XNPV`, `XIRR` accept ranges; all other current financial functions are scalar-only.

## 8. Automated validation

Financial promotion requires:

1. annuity/root and dated cash-flow regressions;
2. payment and cumulative reconciliation;
3. depreciation references and schedule budgets;
4. ISPMT zero-based reference/sign/domain tests;
5. EFFECT/NOMINAL references, truncation and inverse round trips;
6. RRI/PDURATION references, equal-value endpoints and inverse round trips;
7. near-zero numerical-stability tests;
8. descriptor/capability/coercion/error and registry-count regressions;
9. complete Core/architecture/Windows/Android/iOS/Mac Catalyst/MAUI Windows matrix.

## 9. Deliberately pending

- shared financial basis/calendar layer;
- `YEARFRAC` and coupon-date functions;
- AMOR, bond/coupon, treasury, price, yield and duration families;
- odd-first/odd-last coupon periods;
- external Excel/LibreOffice differential corpus and financial fuzzing;
- exhaustive symbolic root discovery.

PR #1 remains Draft while a newer exact-head CI is red or unknown.
