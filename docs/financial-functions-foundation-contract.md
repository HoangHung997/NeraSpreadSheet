# Financial Functions Foundation contract

This document defines the validated financial-function family owned by NeraSpreadSheet. Excel and LibreOffice are compatibility references only; they are not runtime dependencies.

## 1. Architecture boundary

- All functions are registered through Function Extension SDK API `1.0`.
- Every descriptor uses namespace `NERA.BUILTIN`, implementation version `1.0.0` and logical-argument counting.
- Financial semantics remain independent from WPF, WinForms, MAUI, rendering and OpenXml.
- Functions return scalars only.
- Root solvers and schedule loops are deterministic, bounded and fail closed.
- `StandardFormulaFunctions.CreateAll()` is the single built-in aggregation path; financial families are not registered a second time elsewhere.

## 2. Registered functions

Eighteen financial names are registered:

- `PV`, `FV`, `PMT`, `NPER`, `RATE`;
- `NPV`, `IRR`, `XNPV`, `XIRR`;
- `IPMT`, `PPMT`, `CUMIPMT`, `CUMPRINC`;
- `SLN`, `SYD`, `DB`, `DDB`, `VDB`.

The eager/versioned registry contains **191 names**. Together with 18 AST/reference-aware and five dynamic-array names, the built-in subsystem recognizes **214 names**.

## 3. Coercion, timing and errors

- Required scalar values accept finite number/date values, Boolean coercion and invariant numeric text.
- Unsupported range use and nonnumeric scalar text return `#VALUE!`.
- Supplied formula errors propagate before invocation.
- Payment timing is integer `0` for end-of-period or `1` for beginning-of-period.
- Invalid financial domains, excessive budgets, non-finite results and numerical non-convergence return `#NUM!`.
- Zero-rate `NPER` with zero payment returns `#DIV/0!`.

## 4. Annuities and roots

`PV`, `FV`, `PMT`, `NPER` share cash-flow signs and timing. `RATE(nper,pmt,pv,[fv],[type],[guess])` solves the same annuity equation with bounded Newton/backtracking and independent transformed-rate bracket/bisection candidates. Valid negative rates remain greater than `-1`; the selected converged root is nearest the supplied guess.

## 5. Periodic and irregular cash flows

- `NPV` discounts the first retained cash flow at period 1 and uses compensated summation.
- `IRR` treats the first retained value as period 0, requires opposite signs and selects a bounded converged root nearest guess.
- `XNPV` and `XIRR` pair values/dates positionally, truncate numeric dates to whole days, use the first date as baseline and divide actual day differences by 365.
- Value/date dependencies participate in affected-only recalculation.
- Current limits are 2.000.000 retained NPV/XNPV positions and 100.000 IRR/XIRR positions.

## 6. Payment decomposition and cumulative schedules

### `IPMT` and `PPMT`

`IPMT(rate,per,nper,pv,[fv],[type])` returns one-based period interest. Beginning-of-period payment 1 has zero interest. `PPMT` returns `PMT - IPMT`.

### `CUMIPMT(rate,nper,pv,start_period,end_period,type)`

- requires `rate > 0`, `nper > 0`, `pv > 0`;
- requires whole inclusive periods with `1 <= start_period <= end_period <= nper`;
- requires `type` equal to `0` or `1`;
- computes one PMT and sums period interest using the same IPMT balance/timing equations;
- returns the lender/borrower cash-flow sign used by PMT/IPMT, normally negative for positive loan principal.

### `CUMPRINC(rate,nper,pv,start_period,end_period,type)`

Uses the same validation and schedule. Each period contributes `PMT - interest`. For the same interval:

```text
CUMIPMT + CUMPRINC = PMT * number_of_periods
```

Both cumulative functions use compensated summation and reject more than 2.000.000 inclusive periods.

## 7. Depreciation

### `SLN` and `SYD`

- `SLN(cost,salvage,life)` returns `(cost-salvage)/life`.
- `SYD(cost,salvage,life,per)` returns sum-of-years-digits depreciation.

### `DB(cost,salvage,life,period,[month])`

- current v1 contract requires whole positive `life` and `period`;
- `month` defaults to 12 and must be an integer in `1..12`;
- the fixed declining rate is `1-(salvage/cost)^(1/life)`, rounded to three decimals;
- period 1 is prorated by `month/12`;
- ordinary periods apply the rounded rate to opening book value;
- if `month < 12`, period `life+1` is the final `(12-month)/12` stub;
- every charge is capped at the remaining depreciable basis.

### `DDB(cost,salvage,life,period,[factor])`

- `factor` defaults to 2 and must be positive;
- current v1 target `period` is a positive whole period not greater than `life`;
- each charge is:

```text
min(opening_book * factor / life, opening_book - salvage)
```

- schedules longer than 2.000.000 periods are rejected.

### `VDB(cost,salvage,life,start_period,end_period,[factor],[no_switch])`

- supports fractional `start_period` and `end_period`;
- requires `0 <= start_period < end_period <= life`;
- `factor` defaults to 2;
- builds the bounded declining-balance schedule from period 0 through `end_period`;
- integrates each full-period charge by its overlap with the requested interval;
- when `no_switch` is false/omitted, switches once to straight-line when that charge becomes larger;
- when `no_switch` is true, remains declining-balance;
- never depreciates below salvage and rejects more than 2.000.000 scheduled periods.

## 8. SDK metadata

All 18 financial descriptors are deterministic/pure, return one scalar and use logical argument counting. `NPV`, `IRR`, `XNPV`, `XIRR` accept ranges; all other current financial functions are scalar-only.

## 9. Automated validation

The financial gates cover:

1. annuity consistency, zero-rate and timing;
2. RATE/IRR/XIRR convergence and multiple-root selection;
3. dated schedule references, dependencies and resource limits;
4. IPMT/PPMT and cumulative-payment reconciliation;
5. official CUMIPMT/CUMPRINC reference values;
6. DB first/stub periods and rounded-rate references;
7. DDB factor/cap references;
8. VDB daily/monthly/yearly, interval, partial-period and no-switch behavior;
9. domain, scalar capability, descriptor and registry-count regressions;
10. complete Core/architecture/Windows/Android/iOS/Mac Catalyst/MAUI Windows matrix.

## 10. Deliberately pending

- `ISPMT`, `EFFECT`, `NOMINAL`, `RRI`, `PDURATION`;
- `AMORDEGRC`, `AMORLINC` and broader date-basis compatibility;
- bond/coupon, treasury, price, yield and duration families;
- odd-first/odd-last coupon periods;
- external Excel/LibreOffice differential corpus and financial fuzzing;
- exhaustive symbolic root discovery.

PR #1 remains Draft while a newer exact-head CI is red or unknown.
