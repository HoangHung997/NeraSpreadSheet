# RATE, XNPV and XIRR milestone

## Validated implementation head

- Implementation commit: `c13960a403b6e249bd85ffc718ee0acdfbca7ca8`
- GitHub Actions: CI `#838`, run `32725386326`, success
- PR #1 remains Draft and unmerged into `develop`.

## Why this batch contains three functions

`RATE`, `XNPV` and `XIRR` share bounded root-finding, rate-domain and financial error concerns. They form one reviewable numerical batch. Cumulative payment/principal, bond/day-count and depreciation families were deliberately excluded to keep the implementation and regression surface stable.

## Implemented source surface

Five partial source files separate registration/evaluation, dated schedules, annuity equations, root solving and shared numerical/coercion helpers:

- `AdditionalFinancialFormulaFunctions.cs`;
- `AdditionalFinancialFormulaFunctions.Schedule.cs`;
- `AdditionalFinancialFormulaFunctions.Rate.cs`;
- `AdditionalFinancialFormulaFunctions.RootSolver.cs`;
- `AdditionalFinancialFormulaFunctions.Helpers.cs`.

`StandardFormulaFunctions.CreateAll()` now registers the module after the original financial foundation.

## RATE implementation

`RATE(nper, pmt, pv, [fv], [type], [guess])` uses the same cash-flow sign/timing equation as the existing PV/FV/PMT family.

Contracts:

- `nper > 0`;
- `type` is integer `0` or `1`;
- defaults `fv=0`, `type=0`, `guess=0.1`;
- valid solver rates are greater than `-1` and no greater than `1e10`;
- zero rate has an analytic residual and derivative path;
- near-zero rate terms use bounded series for `log(1+r)` and `exp(x)-1`;
- Newton steps must improve the residual and may backtrack at most 20 times;
- an independent transformed-rate sampler and bisection searches for sign-changing brackets;
- each root phase is limited to 100 iterations; bracket sampling is limited to 128 intervals;
- when both paths converge, the candidate nearest the supplied guess is selected;
- unresolved or invalid domains return `#NUM!`.

## XNPV implementation

`XNPV(rate, values, dates)` calculates irregular dated net present value.

Contracts:

- values and dates are flattened positionally and must have equal nonzero counts;
- range values/dates must be numeric or DateTime; invalid range kinds return `#VALUE!`;
- scalar numeric text may coerce through the shared invariant coercion surface;
- numeric date serials are truncated to whole days;
- the first date is the baseline and no other date may precede it;
- positions after the first may appear in any order;
- year fractions are exact day differences divided by 365;
- the cash-flow vector contains positive and negative values;
- the rate is finite and greater than `-1`;
- compensated summation is used;
- the paired-position budget is 2,000,000;
- invalid schedules/resource limits return `#NUM!`.

## XIRR implementation

`XIRR(values, dates, [guess])` solves the irregular dated discounted-cash-flow equation.

Contracts:

- uses the same positional schedule and day rules as `XNPV`;
- requires at least two positions, both cash-flow signs and at least one date later than the baseline;
- defaults `guess` to `0.1`;
- evaluates dated residual and derivative directly with compensated summation;
- uses the same bounded Newton/backtracking and transformed-rate bracket/bisection infrastructure as `RATE`;
- selects the converged candidate nearest the supplied guess;
- the paired-position budget is 100,000;
- invalid schedules or non-convergence return `#NUM!`.

## Automated tests

### Formula and numerical regressions

- standard positive RATE reference;
- valid negative RATE reference;
- PMT/RATE round trip for beginning-of-period timing;
- exact zero-rate path;
- invalid horizon, timing, guess, no-root and scalar-only capability cases;
- Microsoft-compatible XNPV/XIRR irregular schedule references;
- `XNPV(XIRR(...))` residual round trip;
- post-first date reordering;
- numeric date truncation;
- mismatched lengths, earlier dates, invalid range kinds, invalid rate and missing signs.

### Integration and safety regressions

- value/date range dependency identity;
- affected recalculation after a date edit;
- deterministic/pure SDK v1 metadata and capabilities;
- XIRR rejection above 100,000 positions;
- complete registry count at 186 eager/versioned names.

## CI findings

The first implementation run, CI #837, built without warnings or errors and passed all new financial regressions. Its only two failures were stale count assertions expecting 183 rather than 186 registered functions. Those assertions were corrected without changing financial algorithms, expected values or tolerances.

CI #838 then passed the exact implementation head across:

1. Core restore/build/tests.
2. Architecture verification.
3. Full Windows build/tests.
4. Windows desktop GPU runtime smoke.
5. Android build.
6. iOS and Mac Catalyst builds.
7. MAUI Windows build and handler resolution.
8. Loaded Table-filter, runtime-context and scale/orientation smokes.

## Formula counts after the milestone

- Eager/versioned: 186.
- AST/reference-aware: 18.
- Dynamic-array: 5.
- Complete built-in subsystem: 209.

## Deliberately pending

- `CUMIPMT`, `CUMPRINC`, `ISPMT`.
- Accelerated depreciation.
- Bond/coupon/day-count/duration/yield and treasury functions.
- Locale/date-basis compatibility outside the explicit 365-day contract.
- External producer financial corpus, extreme-root differential tests and fuzzing.
- Exhaustive symbolic discovery of every possible root.

## Next implementation order

1. `CUMIPMT`, `CUMPRINC`, `ISPMT` as one payment-schedule batch.
2. Accelerated depreciation.
3. Bond/coupon/day-count/duration/yield.
4. Statistical hypothesis tests and confidence intervals.
5. Advanced lookup/dynamic arrays.
6. Plugin packaging/isolation and release hardening.
