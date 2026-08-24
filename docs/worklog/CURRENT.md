# Current Work Handoff

- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` into `develop` — Draft, unmerged
- Implementation head: `c13960a403b6e249bd85ffc718ee0acdfbca7ca8`
- GitHub Actions: CI `#838`, run `32725386326`, success
- Source of truth: `docs/current-status.md`
- SDK contract: `docs/function-extension-sdk-contract.md`
- Financial contract: `docs/financial-functions-foundation-contract.md`
- Final acceptance: `docs/CODEX_FINAL_ACCEPTANCE.md`

## Batch completed: RATE, XNPV and XIRR

This batch intentionally stopped at three tightly related functions. It did not include cumulative payment/principal functions, bond conventions or accelerated depreciation.

### `RATE`

- Signature: `RATE(nper, pmt, pv, [fv], [type], [guess])`.
- Scalar-only deterministic/pure SDK v1 descriptor.
- Defaults: `fv=0`, `type=0`, `guess=0.1`.
- Positive `nper`, timing `0/1` and rate domain greater than `-1`.
- Dedicated zero-rate residual and derivative.
- Stable near-zero `log(1+r)` and `exp(x)-1` paths.
- Newton iteration with bounded backtracking.
- Independent transformed-rate bracket sampling and bisection.
- Deterministic nearest-guess selection among converged candidates.
- 100 root iterations, 128 bracket intervals, 20 backtracking reductions and `1e10` maximum solver rate.
- Non-convergence returns `#NUM!`.

### `XNPV`

- Signature: `XNPV(rate, values, dates)`.
- Scalar/range deterministic/pure SDK v1 descriptor.
- Positional value/date pairing with equal nonzero flattened counts.
- First date is the baseline; no date may precede it; later positions may be unordered.
- Numeric date serials are truncated to whole days.
- Discount exponent uses a 365-day year.
- Requires positive and negative cash-flow signs.
- Compensated summation and a 2,000,000-position budget.
- Value/date dependencies enter affected-only recalculation.

### `XIRR`

- Signature: `XIRR(values, dates, [guess])`.
- Shares positional schedule and 365-day contracts with `XNPV`.
- Requires at least two positions, sign diversity and at least one later date.
- Defaults `guess` to `0.1`.
- Uses the same bounded Newton/backtracking plus transformed-rate bracket/bisection solver as `RATE`.
- Direct dated residual/derivative evaluation.
- 100,000-position budget.
- Non-convergence returns `#NUM!`.

## Automated regressions

Tests cover:

- positive, negative and zero RATE roots;
- RATE/PMT round trips and beginning/end timing;
- invalid horizon, timing, guess, no-root and range-capability cases;
- XNPV/XIRR reference values on an irregular payment schedule;
- `XNPV(XIRR(...), ...)` round trip;
- post-first date reordering;
- numeric date truncation;
- mismatched lengths, earlier dates, invalid range kinds, invalid rates and missing sign diversity;
- value/date dependency identity and affected recalculation;
- descriptor identity/version/API/capability/volatility/security contracts;
- XIRR value-budget rejection at 100,001 positions;
- registry-count regressions at 186 eager/versioned names.

## CI sequence and findings

### CI #837 — implementation probe

- Build completed with zero warnings and zero errors.
- New RATE/XNPV/XIRR result, schedule, dependency and resource tests passed.
- 170 formula tests passed.
- Two failures remained: old registry-count assertions expected 183 while the implementation correctly registered 186 names.
- No solver tolerance or expected financial result was weakened.

### CI #838 — corrected exact implementation head

- Core restore/build/tests: success.
- Architecture verification: success.
- Full Windows build/tests: success.
- Windows desktop GPU runtime smoke: success.
- Android build: success.
- iOS and Mac Catalyst builds: success.
- MAUI Windows build and handler resolution: success.
- Loaded Table-filter, runtime/context and scale/orientation smokes: success.

## Formula counts

- Eager/versioned built-ins: 186 names.
- AST/reference-aware built-ins: 18 names.
- Dynamic-array built-ins: 5 names.
- Complete built-in subsystem: 209 names.

## Explicit limitations

- Root discovery is bounded and does not claim every mathematical root.
- External Excel/LibreOffice financial differential corpus and solver fuzzing are pending.
- Locale/date-basis compatibility beyond the explicit 365-day irregular schedule is pending.
- `CUMIPMT`, `CUMPRINC`, `ISPMT`, accelerated depreciation, bond/coupon/day-count/duration/yield and treasury functions are pending.
- Statistical hypothesis tests, advanced lookup/arrays, special engineering, plugin isolation, native spill UX, drawings/charts, advanced data/pivot and release hardening are pending.

## Progress

- Engine/viewport/renderer: about `92%`.
- Basic spreadsheet MVP: about `96–98%`.
- Complete professional roadmap: about `75%`.
- Production readiness: about `52–55%`.

## Next stable batch

1. `CUMIPMT`.
2. `CUMPRINC`.
3. `ISPMT`.
4. Shared payment-schedule iteration and sign/timing contracts.
5. Reconciliation, domain, resource and dependency regressions.
6. Exact-head Core/Windows/MAUI CI.

PR remains Draft; do not merge while a newer exact-head CI is red or unknown.
