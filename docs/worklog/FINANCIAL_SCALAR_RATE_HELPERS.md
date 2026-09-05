# Financial scalar rate helpers milestone

## Validated implementation

- Commit: `e2d3bb4b296292ae83dc4c1a5e35a442f6574e4f`
- CI: `#849`, run `32740594038`
- Formula tests: `185/185`
- PR: `#1`, Draft, unmerged

## Scope

This coherent scalar-only batch implements:

- `ISPMT`
- `EFFECT`
- `NOMINAL`
- `RRI`
- `PDURATION`

All five are `NERA.BUILTIN`, SDK v1, implementation `1.0.0`, deterministic/pure and logical-argument-counted.

## Progress table

| Item | Validation | Status |
|---|---|---|
| ISPMT | Zero-based equal-principal references and sign behavior | Pass |
| EFFECT | Reference, domain and compounding-count truncation | Pass |
| NOMINAL | Reference and EFFECT inverse round trips | Pass |
| RRI | Reference, zero-growth endpoint and PDURATION inverse | Pass |
| PDURATION | Monthly/annual references and RRI inverse | Pass |
| Near-zero stability | `1e-12` nominal rate with 1,000,000 periods | Pass |
| Metadata | identity/version/API/capability/security/volatility | Pass |
| Registry count | 196 eager/versioned, 219 total | Pass |
| Full hosted CI | Core/Windows/Android/iOS/Mac Catalyst/MAUI Windows | Pass |

## Numerical hardening

A direct `Math.Log(1+x)` path lost meaningful low-order bits for small rates. The hosted `double.LogP1` probe did not improve that behavior and allowed an effective rate to collapse to zero. The final implementation uses the alternating `log1p` series for 64 terms whenever `|x| <= 0.5`; outside that interval it uses `Math.Log(1+x)`. `expm1` retains its bounded near-zero series.

This primitive is shared by RATE, EFFECT, NOMINAL, RRI and PDURATION. Regression locks both ordinary reference values and the near-zero round trip.

## Rollback

Revert commits from `ca279c4d569f37503627b1c382f81ae11e0cef37` through `e2d3bb4b296292ae83dc4c1a5e35a442f6574e4f` to remove the five helpers and restore the preceding 191-name registry. Do not retain the intermediate `double.LogP1` probe without the final series correction.

## Next handoff

Build one shared financial calendar/day-count layer, then implement `YEARFRAC` and the seven coupon-date helpers with frequency, month-end, leap-year and basis regressions.
