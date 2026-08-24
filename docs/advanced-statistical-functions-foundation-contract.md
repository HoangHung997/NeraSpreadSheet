# Advanced Statistical Functions Foundation contract

This document defines the validated first-generation advanced-statistical behavior of NeraSpreadSheet. Excel and LibreOffice are compatibility references only and are not runtime dependencies.

## 1. Architecture boundary

- Advanced statistical functions are deterministic/pure Function Extension SDK v1 functions.
- Namespace: `NERA.BUILTIN`; implementation version: `1.0.0`; host API: `1.0`.
- Logical argument counting is used throughout.
- Covariance, correlation and regression functions accept scalar or range arguments and return one scalar.
- Transformation and distribution functions are scalar-only and reject ranges through SDK capability validation.
- Formula semantics remain platform-neutral; UI hosts and OpenXml adapters do not implement statistical algorithms.

## 2. Supported functions

### Pairwise covariance, correlation and regression

- `COVARIANCE.P`, `COVARIANCE.S`;
- `CORREL`, `PEARSON`;
- `SLOPE`, `INTERCEPT`, `RSQ`, `STEYX`;
- `FORECAST.LINEAR`.

### Transformations

- `STANDARDIZE`;
- `FISHER`, `FISHERINV`.

### Normal, log-normal, exponential, discrete and Weibull families

- `NORM.DIST`, `NORM.S.DIST`, `NORM.INV`, `NORM.S.INV`;
- `LOGNORM.DIST`, `LOGNORM.INV`;
- `EXPON.DIST`;
- `BINOM.DIST`, `POISSON.DIST`;
- `WEIBULL.DIST`.

### Beta, gamma, chi-square, Student-t and F families

- `BETA.DIST`, `BETA.INV`;
- `GAMMA.DIST`, `GAMMA.INV`;
- `CHISQ.DIST`, `CHISQ.DIST.RT`, `CHISQ.INV`, `CHISQ.INV.RT`;
- `T.DIST`, `T.DIST.RT`, `T.DIST.2T`, `T.INV`, `T.INV.2T`;
- `F.DIST`, `F.DIST.RT`, `F.INV`, `F.INV.RT`.

The batch adds 39 eager/versioned built-in names.

## 3. Pairwise data and coercion policy

- Paired arguments must contain the same number of flattened values; a shape/count mismatch returns `#N/A`.
- Numeric and DateTime range cells participate.
- Blank, text and Boolean range cells are skipped pairwise.
- Scalar inputs use shared finite-number coercion, including supported invariant numeric text and Boolean coercion.
- A pair participates only when both values participate.
- Formula errors propagate before function invocation.
- Degenerate variance, insufficient samples or undefined regression denominators return `#DIV/0!`.
- Pairwise calculations retain no worksheet-sized secondary copy and are bounded to 2,000,000 positions per invocation.

## 4. Numerical algorithms

- Covariance, variance, co-moment, slope and correlation use stable online bivariate moments.
- `STEYX` derives a bounded residual sum from those moments and clamps only tiny negative round-off residue.
- Normal CDF/density and inverse-normal primitives use finite-domain approximations with explicit probability checks.
- Binomial mass uses log-gamma arithmetic; cumulative paths sum the shorter tail in log space.
- Poisson mass uses log-gamma arithmetic and cumulative probability uses a regularized upper-gamma primitive.
- Beta and F cumulative probabilities use a bounded regularized-beta continued fraction.
- Gamma and chi-square cumulative probabilities use bounded regularized-gamma series/complement paths.
- Student-t CDF uses the regularized-beta relationship.
- Inverse beta, gamma, Student-t and F paths use bounded bracketing and bisection.
- A midpoint whose probability already satisfies the convergence tolerance is returned before either bracket endpoint is changed. This prevents an exact solution from being shifted to the midpoint of a newly narrowed half-interval.
- Continued fractions/series are capped at 512 iterations; inverse searches are capped at 256 iterations, with a separate bounded bracketing phase.
- Numerical non-convergence returns `#N/A` rather than looping or returning an unchecked approximation.

## 5. Domain and endpoint policy

- Probabilities, scales, shapes and support values are checked explicitly for each family.
- Degrees of freedom are truncated toward zero and must be in `1..10,000,000,000`.
- Discrete event/trial inputs are finite scalar numbers truncated toward zero and constrained to implementation integer bounds.
- Cumulative binomial summation is limited to 1,000,000 terms on the selected tail.
- Finite supported endpoints return their defined value; infinite mathematical endpoints that cannot be represented as a finite spreadsheet number return `#NUM!`.
- Invalid domains or resource limits return `#NUM!`.
- Unsupported argument kinds or failed scalar coercion return `#VALUE!`.

## 6. Dependencies and recalculation

- Explicit scalar and range references are captured by the formula engine.
- These functions declare no hidden dependency and read no clock, filesystem, network or external state.
- Pairwise range dependencies participate in affected-only recalculation.
- Distribution functions are deterministic and do not introduce volatile scheduling.

## 7. Automated validation

Promotion requires:

- SDK identity/version/API/capability/volatility/security metadata tests;
- covariance, correlation, regression, forecast and degenerate-data tests;
- large-offset numerical-stability regressions;
- normal/log-normal/exponential/binomial/Poisson/Weibull reference values and domain tests;
- beta/gamma/chi-square/Student-t/F density, cumulative, tail and inverse reference values;
- forward/inverse round trips, endpoint behavior and degrees-of-freedom truncation;
- exact-midpoint inverse-search regressions for beta, gamma, chi-square, Student-t and F;
- range capability, dependency, affected-recalculation and resource-boundary tests;
- complete Core, architecture, Windows desktop/GPU, Android, iOS, Mac Catalyst and MAUI Windows loaded-runtime gates.

## 8. Deliberately pending

- Hypothesis-test functions such as chi-square, z, t and F tests.
- Confidence intervals and additional discrete distributions.
- Exclusive percentile/quartile, percent-rank, `MODE.MULT`, `RANK.AVG` and broader legacy aliases.
- Complete external Excel/LibreOffice statistical corpus and extreme-tail differential testing.
- Hardware performance budgets, fuzzing and locale-specific coercion compatibility.

PR #1 remains Draft while a newer exact-head CI is red or unknown.
