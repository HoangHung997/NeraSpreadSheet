# Current Work Handoff

- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` into `develop` — Draft, unmerged
- Advanced Statistical implementation head: `e713182d460f5c280e2c29e5642769eedf190d2f`
- GitHub Actions: CI `#835`, run `32720631933`, success
- Source of truth: `docs/current-status.md`
- SDK contract: `docs/function-extension-sdk-contract.md`
- Advanced Statistical contract: `docs/advanced-statistical-functions-foundation-contract.md`
- Engineering contract: `docs/engineering-functions-foundation-contract.md`
- Database contract: `docs/database-functions-foundation-contract.md`
- Final acceptance: `docs/CODEX_FINAL_ACCEPTANCE.md`

## Batch completed: Advanced Statistical Functions Foundation

### Covariance, correlation and regression — 9 functions

- `COVARIANCE.P`, `COVARIANCE.S`;
- `CORREL`, `PEARSON`;
- `SLOPE`, `INTERCEPT`, `RSQ`, `STEYX`;
- `FORECAST.LINEAR`.

Key contracts:

- equal flattened pair counts;
- numeric/DateTime range pairs participate and nonnumeric range pairs are skipped;
- stable online bivariate moments;
- explicit `#N/A`, `#DIV/0!`, `#VALUE!` and `#NUM!` paths;
- dependency capture and affected-only recalculation;
- 2,000,000-position pair budget.

### Transformations and first distribution group — 13 functions

- `STANDARDIZE`, `FISHER`, `FISHERINV`;
- `NORM.DIST`, `NORM.S.DIST`, `NORM.INV`, `NORM.S.INV`;
- `LOGNORM.DIST`, `LOGNORM.INV`;
- `EXPON.DIST`, `BINOM.DIST`, `POISSON.DIST`, `WEIBULL.DIST`.

### Continuous distribution group — 17 functions

- `BETA.DIST`, `BETA.INV`;
- `GAMMA.DIST`, `GAMMA.INV`;
- `CHISQ.DIST`, `CHISQ.DIST.RT`, `CHISQ.INV`, `CHISQ.INV.RT`;
- `T.DIST`, `T.DIST.RT`, `T.DIST.2T`, `T.INV`, `T.INV.2T`;
- `F.DIST`, `F.DIST.RT`, `F.INV`, `F.INV.RT`.

Key numerical contracts:

- bounded regularized beta/gamma series and continued fractions;
- bounded inverse bracketing and bisection;
- right-tail/two-tail paths avoid unnecessary cancellation where dedicated primitives exist;
- inverse midpoint accepted before bracket mutation;
- deterministic endpoint/domain policy;
- degrees of freedom truncated toward zero and bounded to 10,000,000,000;
- discrete cumulative summation bounded to 1,000,000 terms;
- non-convergence fails closed with `#N/A`.

### SDK and formula counts

- Built-in eager/versioned registry: 183 names.
- AST/reference-aware built-ins: 18 names.
- Dynamic-array built-ins: 5 names.
- Complete built-in subsystem: 206 names.

## Automated regressions

Tests cover:

- descriptor identity/version/API/capability/security/volatility contracts;
- covariance/correlation/regression/forecast values and degenerate data;
- large-offset pairwise stability and distribution tails;
- density, cumulative and inverse reference values;
- forward/inverse round trips and endpoint policy;
- degrees-of-freedom truncation and resource/domain failures;
- scalar-only capability rejection;
- exact-midpoint inverse regressions for beta, gamma, chi-square, Student-t and F;
- existing full formula, dependency, editing, rendering, XLSX and host matrices.

## Problems found and fixed during the batch

- Registering the final 17 continuous-distribution functions raised the eager registry from 166 to 183, while two count regressions still expected 166. Both were updated and descriptor coverage was expanded to all new names.
- The first new round-trip regression exposed a real inverse-search defect: an accepted midpoint was followed by bracket mutation and the routine returned the midpoint of the narrowed half-interval. Beta returned 2.5 instead of 3 in an exact round trip.
- The same pattern existed in inverse beta, inverse gamma and inverse Student-t. All three now return the accepted midpoint before changing either bracket; F and chi-square inherit the corrected primitives.
- The regression expectation was not weakened; targeted midpoint tests lock all affected families.

## CI #835

- Core restore/build/tests and architecture verification: success.
- Full Windows build/tests and desktop GPU runtime smoke: success.
- Android build: success.
- iOS and Mac Catalyst builds: success.
- MAUI Windows build, handler resolution and loaded Table-filter/runtime/scale smokes: success.

## Explicit limitations

- Statistical hypothesis tests, confidence intervals and additional discrete distributions are pending.
- Exclusive percentile/quartile, percent-rank, `MODE.MULT`, `RANK.AVG` and broader legacy aliases are pending.
- Extreme-tail differential corpus, external Excel/LibreOffice statistical corpus and fuzzing are pending.
- Remaining finance, advanced lookup/arrays, special engineering and plugin isolation are pending.
- Native spill UX, drawings/charts, advanced data/pivot and final release hardening are pending.

## Progress

- Engine/viewport/renderer: about `92%`.
- Basic spreadsheet MVP: about `96–98%`.
- Complete professional roadmap: about `74%`.
- Production readiness: about `51–54%`.

## Next batch

1. Remaining Financial Functions: `RATE`, `XNPV`, `XIRR`.
2. Cumulative payment and principal families.
3. Bond/coupon/day-count and accelerated depreciation.
4. Financial domain/convergence/dependency/resource regressions.
5. Exact-head Core/Windows/MAUI CI.
6. Then statistical hypothesis tests and advanced lookup/dynamic arrays.

PR remains Draft; do not merge while a newer exact-head CI is red or unknown.
