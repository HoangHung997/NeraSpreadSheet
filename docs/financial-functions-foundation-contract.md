# Financial Functions Foundation contract

This document defines the validated financial-function family owned by NeraSpreadSheet. Excel and LibreOffice are compatibility references only; they are not runtime dependencies.

## 1. Architecture boundary

- All functions are registered through Function Extension SDK API `1.0`.
- Every descriptor uses namespace `NERA.BUILTIN`, implementation version `1.0.0` and logical-argument counting.
- `NeraFormulaEngine` preserves scalar/range identity and source dependencies before invocation.
- Financial semantics remain independent from WPF, WinForms, MAUI, rendering and OpenXml.
- Functions return scalars only; array-valued financial schedules are outside this milestone.
- Root solvers are deterministic, bounded and fail closed rather than returning an unchecked approximation.

## 2. Registered functions

Thirteen names are registered in the eager/versioned registry:

- `PV`, `FV`, `PMT`, `NPER`, `RATE`;
- `NPV`, `IRR`, `XNPV`, `XIRR`;
- `IPMT`, `PPMT`;
- `SLN`, `SYD`.

The complete eager/versioned registry contains **186 names**. Together with 18 AST/reference-aware and five dynamic-array names, the built-in formula subsystem recognizes **209 names**.

## 3. Scalar coercion and timing

Scalar financial arguments:

- accept finite `Number` and `DateTime` values;
- coerce Boolean to `1` or `0`;
- permit invariant numeric text;
- reject nonnumeric text with `#VALUE!`;
- propagate supplied formula errors before invocation.

Payment timing (`type`) must be integer `0` for end-of-period or `1` for beginning-of-period. Any other value returns `#VALUE!`.

Rates must be finite and greater than `-1`. Invalid rates, invalid financial domains and non-finite results return `#NUM!`.

## 4. Time-value functions

### `PV(rate, nper, pmt, [fv], [type])`

Returns present value using standard annuity cash-flow signs. `nper` may be zero or positive. At zero rate, calculation is linear.

### `FV(rate, nper, pmt, [pv], [type])`

Returns future value with the same sign and timing convention. `nper` may be zero or positive.

### `PMT(rate, nper, pv, [fv], [type])`

Returns the periodic payment. `nper` must be positive. Zero-rate payment is `-(pv + fv) / nper`.

### `NPER(rate, pmt, pv, [fv], [type])`

Returns the nonnegative number of periods. At zero rate, zero payment returns `#DIV/0!`. A logarithmic domain without a real nonnegative solution returns `#NUM!`.

### `RATE(nper, pmt, pv, [fv], [type], [guess])`

- solves the same annuity equation used by `PV`, `FV` and `PMT`;
- requires positive finite `nper`;
- defaults `fv` to `0`, `type` to `0` and `guess` to `0.1`;
- supports valid negative rates greater than `-1`;
- has a dedicated zero-rate equation and derivative path;
- uses stable `log(1+r)` and `exp(x)-1` approximations near zero;
- evaluates a bounded Newton candidate with backtracking;
- independently evaluates transformed-rate brackets and bisection;
- compares converged candidates by absolute distance to the supplied guess;
- permits rates up to `1e10` inside the bounded solver;
- limits root iterations to 100, bracket sampling to 128 intervals and Newton backtracking to 20 reductions;
- returns `#NUM!` when no admissible root reaches the residual tolerance.

`RATE` can have zero, one or multiple mathematical solutions. The implementation selects the nearest root among the bounded candidates it discovers; it does not claim exhaustive symbolic root discovery.

## 5. Periodic cash-flow functions

### `NPV(rate, value1, [value2], ...)`

- discounts the first retained cash flow at period `1`;
- accepts scalar and range cash-flow arguments;
- range number/date cells participate while range blank/text/Boolean cells are ignored;
- scalar Boolean and invariant numeric text may coerce;
- preserves logical argument and row-major range order;
- permits at most 2,000,000 retained cash-flow values;
- uses compensated summation;
- returns `0` when no numeric cash flow remains.

### `IRR(values, [guess])`

- treats the first retained cash flow as period `0`;
- requires at least one positive and one negative value;
- defaults `guess` to `0.1`;
- accepts rates greater than `-1` and no greater than `1e10`;
- limits retained values to 100,000;
- evaluates bounded Newton and transformed-rate bracket/bisection candidates;
- limits each solver phase to 100 iterations and bracket sampling to 64 intervals;
- selects the converged candidate nearest the supplied guess;
- returns `#NUM!` when no admissible root converges.

Regression coverage includes multiple-root cash flows where an unguarded Newton path crosses to a root farther from the caller's guess.

## 6. Irregular dated cash flows

### Shared schedule contract

`XNPV` and `XIRR` use positional value/date pairing:

- value and date arguments must contain the same nonzero number of flattened positions;
- scalar text may coerce; range text, Boolean and blank positions are invalid and return `#VALUE!`;
- dates accept `DateTime` or valid OLE date serials;
- numeric dates are truncated to whole days before calculation;
- the first date is the schedule baseline;
- no later position may contain a date earlier than the first date;
- positions after the first may occur in any order;
- each exponent uses `(date_i - date_0) / 365`;
- at least one positive and one negative cash flow is required;
- explicit value/date range dependencies participate in affected-only recalculation.

### `XNPV(rate, values, dates)`

- requires a finite rate greater than `-1`;
- discounts each paired cash flow to the first schedule date;
- uses compensated summation;
- permits at most 2,000,000 paired positions;
- returns `#NUM!` for mismatched lengths, an earlier date, an invalid rate, missing sign diversity or a non-finite result.

### `XIRR(values, dates, [guess])`

- requires at least two paired positions, sign diversity and at least one date later than the first date;
- defaults `guess` to `0.1`;
- permits at most 100,000 paired positions;
- uses the same bounded Newton-with-backtracking plus transformed-rate bracket/bisection solver as `RATE`;
- calculates the residual and derivative directly from the 365-day schedule;
- selects a converged candidate nearest the supplied guess;
- returns `#NUM!` when the schedule is invalid or no admissible root converges.

## 7. Payment decomposition

### `IPMT(rate, per, nper, pv, [fv], [type])`

Returns the interest portion for one-based integer period `per`. `per` must be within the positive payment horizon. Beginning-of-period payment one has zero interest.

### `PPMT(rate, per, nper, pv, [fv], [type])`

Returns `PMT - IPMT` under the same validation and sign conventions. Tests require `IPMT + PPMT == PMT` within floating-point tolerance.

## 8. Depreciation

### `SLN(cost, salvage, life)`

Returns straight-line depreciation `(cost - salvage) / life`. `life` must be positive.

### `SYD(cost, salvage, life, per)`

Returns sum-of-years-digits depreciation. `life` and `per` must be positive and `per <= life`; otherwise the result is `#NUM!`.

## 9. Error and dependency behavior

- unsupported scalar/range kind, invalid timing or nonnumeric required schedule position: `#VALUE!`;
- invalid financial domain, mismatched schedule, excessive budget or non-convergence: `#NUM!`;
- zero-rate `NPER` with zero payment: `#DIV/0!`;
- formula errors in supplied arguments propagate through the SDK error policy;
- `NPV`, `IRR`, `XNPV` and `XIRR` range/scalar references enter the dependency graph;
- affected-only recalculation responds to edits inside value or date ranges.

## 10. SDK metadata

All thirteen descriptors declare:

- identity namespace `NERA.BUILTIN`;
- implementation version `1.0.0`;
- minimum host API `1.0`;
- logical argument counting;
- scalar return;
- deterministic volatility;
- pure security classification;
- engine-captured dependencies.

`NPV`, `IRR`, `XNPV` and `XIRR` declare scalar and range arguments. The remaining financial functions are scalar-only.

## 11. Automated validation

Promotion requires:

1. PV/FV/PMT/NPER consistency and zero-rate tests;
2. RATE positive/negative/zero-root references, PMT round trips, timing and non-convergence tests;
3. NPV ordering and IRR convergence/multiple-root tests;
4. XNPV/XIRR 365-day references and forward/inverse round trips;
5. positional schedule, numeric-date truncation, post-first ordering and invalid-date tests;
6. IPMT/PPMT reconciliation and payment-timing tests;
7. SLN/SYD boundary tests;
8. descriptor/capability/coercion/error tests;
9. dependency and affected-recalculation tests for value and date ranges;
10. iteration/value-budget tests;
11. complete Core, architecture, Windows, Android, iOS, Mac Catalyst and MAUI Windows loaded-runtime gates.

## 12. Deliberately pending

- `CUMIPMT`, `CUMPRINC` and `ISPMT`;
- `DB`, `DDB`, `VDB`, `AMORDEGRC` and `AMORLINC`;
- bond/coupon, treasury, price, yield, duration and day-count conventions;
- odd-first/odd-last coupon periods;
- locale/date-basis compatibility beyond the explicit 365-day XNPV/XIRR contract;
- root discovery beyond the bounded candidate strategy;
- streaming/parallel solvers for larger cash-flow vectors;
- external Excel/LibreOffice differential corpus and financial fuzzing.

PR #1 remains Draft while a newer exact-head CI is red or unknown.
