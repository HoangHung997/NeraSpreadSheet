# Advanced Statistical Functions Foundation milestone

## Validated implementation head

- Implementation commit: `e713182d460f5c280e2c29e5642769eedf190d2f`
- GitHub Actions: CI `#835`, run `32720631933`, success
- PR #1 remains Draft and unmerged into `develop`.

## Implemented source surface

Thirty-nine `NERA.BUILTIN` SDK v1 functions were added. Every descriptor uses implementation version `1.0.0`, host API `1.0`, logical argument counting, deterministic volatility and pure security classification.

### Pairwise analysis

- `COVARIANCE.P`, `COVARIANCE.S`;
- `CORREL`, `PEARSON`;
- `SLOPE`, `INTERCEPT`, `RSQ`, `STEYX`;
- `FORECAST.LINEAR`.

Pairwise calculations require equal flattened position counts, use stable online bivariate moments, ignore nonnumeric range pairs, capture dependencies and are bounded to 2,000,000 positions.

### Transformations and common distributions

- `STANDARDIZE`, `FISHER`, `FISHERINV`;
- `NORM.DIST`, `NORM.S.DIST`, `NORM.INV`, `NORM.S.INV`;
- `LOGNORM.DIST`, `LOGNORM.INV`;
- `EXPON.DIST`, `BINOM.DIST`, `POISSON.DIST`, `WEIBULL.DIST`.

The discrete cumulative path chooses a bounded tail, uses stable/log-domain arithmetic and refuses work above the one-million-term budget.

### Beta, gamma, chi-square, Student-t and F

- `BETA.DIST`, `BETA.INV`;
- `GAMMA.DIST`, `GAMMA.INV`;
- `CHISQ.DIST`, `CHISQ.DIST.RT`, `CHISQ.INV`, `CHISQ.INV.RT`;
- `T.DIST`, `T.DIST.RT`, `T.DIST.2T`, `T.INV`, `T.INV.2T`;
- `F.DIST`, `F.DIST.RT`, `F.INV`, `F.INV.RT`.

Regularized beta/gamma, continued fractions, series, bracketing and bisection all have hard iteration limits. Degrees of freedom are truncated toward zero and bounded to 10,000,000,000. Non-convergence returns `#N/A`.

## Defect discovered by the new regression suite

The first custom-bound beta round trip exposed a bisection error:

1. the midpoint CDF exactly matched the requested probability;
2. the code changed one bracket endpoint;
3. it then returned the midpoint of the newly narrowed bracket;
4. `BETA.INV(BETA.DIST(3,...),...)` returned `2.5` instead of `3`.

Inverse beta, inverse gamma and inverse Student-t shared that pattern. The implementation now returns an accepted midpoint before mutating the bracket. Chi-square and F inherit the corrected primitives. Dedicated midpoint regressions lock beta, gamma, chi-square, Student-t and F.

## Automated tests

- Pairwise regression/covariance/correlation values, shapes and degeneracy.
- Large-offset numerical stability and affected-only recalculation.
- Descriptor identity/version/API/capability/security/volatility metadata.
- Normal, log-normal, exponential, binomial, Poisson and Weibull values.
- Beta, gamma, chi-square, Student-t and F density/CDF/tail/inverse references.
- Forward/inverse round trips, exact-midpoint cases and endpoints.
- Domain, scalar capability, degrees-of-freedom and resource failures.
- Registry count and full descriptor-name coverage at 183 eager functions.

## Formula counts

- Eager registry: 183 names.
- AST/reference-aware: 18 names.
- Dynamic-array: 5 names.
- Total built-in subsystem: 206 names.

## Exact implementation validation

CI #835 passed:

1. Core restore/build/tests.
2. Architecture verification.
3. Full Windows restore/build/tests.
4. Windows desktop GPU runtime smoke.
5. MAUI Android build.
6. MAUI iOS and Mac Catalyst builds.
7. MAUI Windows build and handler resolution.
8. Loaded Table-filter, runtime-context and scale/orientation smokes.

## Deliberately pending

- Statistical hypothesis tests and confidence intervals.
- Additional discrete distributions and compatibility aliases.
- Exclusive percentile/quartile, percent-rank, `MODE.MULT`, `RANK.AVG`.
- Extreme-tail differential testing and external Excel/LibreOffice corpora.
- Target-hardware numerical/performance budgets and fuzzing.

## Next implementation order

1. `RATE`, `XNPV`, `XIRR`.
2. Cumulative payment/principal and bond/coupon/day-count functions.
3. Accelerated depreciation.
4. Remaining statistical tests and distribution compatibility.
5. Advanced lookup/dynamic arrays.
6. Plugin packaging/isolation and release hardening.
