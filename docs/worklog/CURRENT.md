# Current Work Handoff

- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` into `develop` — Draft, unmerged
- Implementation head: `e2d3bb4b296292ae83dc4c1a5e35a442f6574e4f`
- GitHub Actions: CI `#849`, run `32740594038`, success
- Formula tests: `185/185`
- Source of truth: `docs/current-status.md`
- Financial contract: `docs/financial-functions-foundation-contract.md`

## Batch completed: scalar financial rate helpers

| Work item | Result | Status |
|---|---|---|
| `ISPMT` | Zero-based equal-principal interest schedule | Complete |
| `EFFECT` | Nominal-to-effective conversion with truncated compounding count | Complete |
| `NOMINAL` | Effective-to-nominal inverse conversion | Complete |
| `RRI` | Equivalent periodic growth rate | Complete |
| `PDURATION` | Period count needed to grow PV to FV | Complete |
| Numerical primitive | 64-term `log1p` series for `|x| <= 0.5` | Complete |
| Formula regressions | 185 passed, zero failed | Green |
| Hosted matrix | Core, Windows, Android, iOS, Mac Catalyst, MAUI Windows loaded smokes | Green |
| Pull request | Remains Draft and unmerged | Locked |

## Functional contracts

### ISPMT

- Scalar-only deterministic/pure SDK v1.
- Requires `nper > 0` and zero-based `per` in `0..nper`.
- Equal-principal interest formula: `pv * rate * (per/nper - 1)`.
- Sign follows principal/rate inputs.

### EFFECT and NOMINAL

- Positive rate domains.
- Compounding count is truncated toward zero and must remain at least 1.
- Stable `log1p`/`expm1` forms.
- Forward/inverse round trips are locked.

### RRI and PDURATION

- Require positive periods/rate and positive PV/FV as applicable.
- Stable logarithmic ratio avoids direct `fv/pv` overflow where possible.
- Equal PV/FV returns zero.
- Inverse round trips are locked.

## Numerical issue found and fixed

CI probes exposed two distinct precision concerns:

1. old registry-count assertions still expected 191 instead of 196;
2. `Math.Log(1+x)` lost several picounits for small monthly rates and collapsed an EFFECT/NOMINAL case at `1e-12`.

A first attempt using `double.LogP1` on the hosted runtime retained the same cancellation behavior. The final primitive uses a 64-term convergent series for `|x| <= 0.5`, and `Math.Log(1+x)` only outside that interval. No financial reference was changed to hide the defect.

## CI sequence

- CI #846: build clean; 181/185 formula tests passed; three count assertions and one PDURATION precision threshold remained.
- CI #847: counts corrected; 184/185 passed; one small-rate PDURATION case remained.
- CI #848: direct `double.LogP1` probe exposed unchanged cancellation and the near-zero round-trip failure.
- CI #849: final bounded-series implementation passed 185/185 and the complete hosted matrix.

## Counts

- Eager/versioned built-ins: 196.
- AST/reference-aware built-ins: 18.
- Dynamic-array built-ins: 5.
- Complete built-in subsystem: 219.
- Financial functions: 23.

## Explicit limitations

- Shared day-count/calendar basis is pending.
- YEARFRAC, coupon-date, AMOR, bond/treasury/price/yield/duration functions are pending.
- ISPMT currently locks the explicit zero-based compatibility contract.
- External producer differential corpora and financial fuzzing remain pending.
- Hypothesis tests, advanced lookup/arrays, plugin isolation, drawings/charts, pivots and release hardening remain pending.

## Progress

- Engine/viewport/renderer: about `92%`.
- Basic spreadsheet MVP: about `96–98%`.
- Complete professional roadmap: about `77%`.
- Production readiness: about `54–57%`.

## Next stable batch

1. Shared financial date normalization and coupon frequency.
2. Basis `0..4` day-count engine.
3. `YEARFRAC`.
4. `COUPDAYBS`, `COUPDAYS`, `COUPDAYSNC`.
5. `COUPNCD`, `COUPPCD`, `COUPNUM`.
6. Leap-year, month-end, frequency, basis and round-trip regressions.
7. Exact-head Core/Windows/MAUI CI.

PR remains Draft; do not merge while a newer exact-head CI is red or unknown.
