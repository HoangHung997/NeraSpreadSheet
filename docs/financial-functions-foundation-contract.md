# Financial Functions Foundation contract

This document defines the first validated financial-function family owned by NeraSpreadSheet. Excel and LibreOffice are compatibility references only; they are not runtime dependencies.

## 1. Architecture boundary

- All functions are registered through Function Extension SDK API `1.0`.
- Every descriptor uses namespace `NERA.BUILTIN`, implementation version `1.0.0` and logical-argument counting.
- `NeraFormulaEngine` preserves scalar/range identity and source dependencies before invocation.
- Financial semantics remain independent from WPF, WinForms, MAUI, rendering and OpenXml.
- Functions return scalars only; array-valued financial schedules are not part of this milestone.

## 2. Registered functions

Ten names are added to the eager registry:

- `PV`;
- `FV`;
- `PMT`;
- `NPER`;
- `NPV`;
- `IRR`;
- `IPMT`;
- `PPMT`;
- `SLN`;
- `SYD`.

The eager registry therefore contains 113 names. Together with 18 AST/reference-aware functions and five dynamic-array functions, the built-in formula subsystem recognizes 136 names. User-registered SDK extensions are additional.

## 3. Scalar coercion and timing

Scalar financial arguments:

- accept finite `Number` and `DateTime` values;
- coerce Boolean to `1` or `0`;
- permit invariant numeric text;
- reject nonnumeric text with `#VALUE!`;
- propagate formula errors before invocation.

Payment timing (`type`) must be integer `0` for end-of-period or `1` for beginning-of-period. Any other value returns `#VALUE!`.

Rates must be finite and greater than `-1`. Invalid rates and non-finite financial results return a `#NUM!` cell value through the current numeric/domain error path.

## 4. Time-value functions

### `PV(rate, nper, pmt, [fv], [type])`

Returns present value using standard annuity cash-flow signs. `nper` may be zero or positive. At zero rate, calculation is linear.

### `FV(rate, nper, pmt, [pv], [type])`

Returns future value with the same sign and timing convention. `nper` may be zero or positive.

### `PMT(rate, nper, pv, [fv], [type])`

Returns the periodic payment. `nper` must be positive. Zero-rate payment is `-(pv + fv) / nper`.

### `NPER(rate, pmt, pv, [fv], [type])`

Returns the nonnegative number of periods. At zero rate, zero payment returns `#DIV/0!`. A logarithmic domain without a real nonnegative solution returns `#NUM!`.

## 5. Cash-flow functions

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
- evaluates a bounded Newton candidate;
- evaluates a deterministic transformed-rate bracket/bisection candidate;
- limits each solver phase to 100 iterations and bracket sampling to 64 intervals;
- compares converged candidates by absolute distance to the supplied guess;
- returns the nearest candidate, with deterministic lower-rate tie ordering inside bracket selection;
- returns `#NUM!` when no admissible root converges.

Newton may cross several attraction basins before converging. Comparing it with the independently bracketed candidate prevents a converged but farther root from silently overriding a root nearer the caller's guess. Regression coverage includes a four-period cash-flow vector with roots near `-0.8368694674` and `1.7426259408` where the unguarded Newton path crosses to the farther positive root.

`IRR` may have more mathematical roots than the bounded sampler observes. The implementation selects the nearest root among candidates found by its bounded strategy; it does not claim every external producer's root-selection edge case.

## 6. Payment decomposition

### `IPMT(rate, per, nper, pv, [fv], [type])`

Returns the interest portion for one-based integer period `per`. `per` must be within the positive payment horizon. Beginning-of-period payment one has zero interest.

### `PPMT(rate, per, nper, pv, [fv], [type])`

Returns `PMT - IPMT` under the same validation and sign conventions. Tests require `IPMT + PPMT == PMT` within floating-point tolerance.

## 7. Depreciation

### `SLN(cost, salvage, life)`

Returns straight-line depreciation `(cost - salvage) / life`. `life` must be positive.

### `SYD(cost, salvage, life, per)`

Returns sum-of-years-digits depreciation. `life` and `per` must be positive and `per <= life`; otherwise the result is `#NUM!`.

## 8. Error and dependency behavior

- invalid scalar/range shape or timing: `#VALUE!`;
- invalid financial domain, excessive budget or non-convergence: `#NUM!`;
- zero-rate `NPER` with zero payment: `#DIV/0!`;
- formula errors in supplied arguments propagate through the SDK error policy;
- `NPV` and `IRR` range/scalar references enter the dependency graph;
- affected-only recalculation responds to edits inside referenced cash-flow ranges.

## 9. SDK metadata

All ten descriptors declare:

- identity namespace `NERA.BUILTIN`;
- implementation version `1.0.0`;
- minimum host API `1.0`;
- logical argument counting;
- scalar return;
- deterministic volatility;
- pure state classification;
- engine-captured dependencies.

`NPV` and `IRR` declare scalar and range arguments. The remaining functions declare scalar arguments only.

## 10. Deliberately pending

- `RATE`, `XNPV` and `XIRR`;
- `CUMIPMT`, `CUMPRINC`, `ISPMT` and duration/yield functions;
- `DB`, `DDB`, `VDB`, `AMORDEGRC` and `AMORLINC`;
- bond/coupon, treasury, price, yield and day-count conventions;
- odd-first/odd-last coupon periods;
- currency/locale/date-basis compatibility;
- root discovery beyond the bounded IRR sampling strategy;
- streaming/parallel solvers for larger cash-flow vectors;
- external Excel/LibreOffice differential corpus and financial fuzzing.

## 11. Validation gates

Promotion requires:

1. PV/FV/PMT/NPER consistency and zero-rate tests;
2. NPV ordering and IRR convergence tests;
3. IRR multiple-root nearest-guess and deterministic-repeat tests;
4. IPMT/PPMT reconciliation and payment-timing tests;
5. SLN/SYD boundary tests;
6. scalar/range coercion, domain and error tests;
7. cash-flow dependency and affected-recalculation tests;
8. iteration/value-budget tests;
9. versioned descriptor metadata tests;
10. the complete Core, architecture, Windows, Android, iOS, Mac Catalyst and MAUI Windows matrix.

PR #1 remains Draft while a newer exact-head CI is red or unknown.
