# Statistical Functions Foundation milestone

## Validated implementation head

- Implementation commit: `6aa9b1a05f7a370d393d3222b533b3bee0088c9a`
- GitHub Actions: CI `#779`, run `32636739544`, success
- PR #1 remains Draft and unmerged into `develop`.

## Implemented source surface

### Versioned built-ins

Eleven `NERA.BUILTIN` SDK v1 functions were added:

- `MEDIAN`;
- `MODE.SNGL`;
- `PERCENTILE.INC`;
- `QUARTILE.INC`;
- `VAR.P`, `VAR.S`;
- `STDEV.P`, `STDEV.S`;
- `RANK.EQ`;
- `LARGE`, `SMALL`.

Every descriptor uses implementation version `1.0.0`, host API `1.0`, logical arguments, scalar/range capabilities, scalar return, deterministic volatility, pure state and engine-captured dependencies.

### Statistical value policy

- Numeric and DateTime range cells participate.
- Blank/text/Boolean range cells are ignored.
- Scalar Boolean may coerce to `1`/`0`.
- Scalar invariant numeric text may coerce.
- Invalid scalar text returns `#VALUE!`.
- Formula errors propagate before invocation.
- At most 2,000,000 numeric/date values are collected per invocation.

### Algorithms

- Median: sorted odd/even middle calculation.
- Mode: frequency map, lowest tied mode, `#N/A` without repetition.
- Inclusive percentile/quartile: sorted linear interpolation.
- Population/sample variance: Welford online accumulation.
- Standard deviation: square root of corresponding variance.
- Rank: equal rank with ascending/descending order.
- Large/small: bounded one-based order statistics.

## Automated tests

- Odd/even median and scalar/range coercion.
- Mode ties and no-mode error.
- Percentile endpoints/interpolation and quartiles.
- Population/sample variance and standard deviation.
- Duplicate rank plus large/small indexes.
- Error propagation and domain/insufficient-sample outcomes.
- Dependency graph and affected-only recalculation.
- SDK identity/version/API/capability/state/argument-policy metadata.
- Updated built-in descriptor count from 92 to 103.

## Formula counts

- Eager registry: 103 names.
- AST/reference-aware: 18 names.
- Dynamic-array: 5 names.
- Total built-in subsystem: 126 names.

## Exact implementation validation

CI #779 passed:

1. Core restore/build/tests.
2. Architecture verification.
3. Full Windows restore/build/tests.
4. Windows desktop GPU runtime smoke.
5. MAUI Android build.
6. MAUI iOS and Mac Catalyst builds.
7. MAUI Windows build and handler resolution.
8. Loaded Table-filter, runtime-context and scale/orientation smokes.

## Deliberately pending

- Exclusive percentile/quartile.
- MODE.MULT and RANK.AVG.
- Percent-rank, covariance, correlation and regression.
- Statistical distributions and hypothesis tests.
- Complete Excel coercion/tie/locale compatibility.
- Large-range streaming selection algorithms.
- External corpus, target-hardware budgets and fuzzing.

## Next implementation order

1. Financial Functions Foundation.
2. Engineering/database families.
3. Advanced statistics/distributions.
4. Advanced lookup/dynamic arrays.
5. Plugin packaging/isolation and release hardening.
