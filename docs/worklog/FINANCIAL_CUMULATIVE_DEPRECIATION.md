# Financial cumulative payment and depreciation milestone

## Validated implementation

- Commit: `ea61fe227919358539355b814d4c2baf5f05b538`
- CI: `#844`, run `32734262232`
- Formula tests: `179/179`
- PR: `#1`, Draft, unmerged

## Scope

This stable batch contains five scalar deterministic/pure SDK v1 functions:

- `CUMIPMT`
- `CUMPRINC`
- `DB`
- `DDB`
- `VDB`

It also repairs the built-in registry so all families flow through one aggregation path and removes a duplicate RATE/XNPV/XIRR implementation that had made the prior exact head uncompilable.

## Progress table

| Item | Validation | Status |
|---|---|---|
| Registry architecture | Delegated wrapper + one `CreateAll()` path | Complete |
| CUMIPMT reference | 30-year loan, months 13–24 | Pass |
| CUMPRINC reference | Same schedule and PMT reconciliation | Pass |
| DB references | Rounded rate, month proration, final stub | Pass |
| DDB references | Default/custom factor and cap | Pass |
| VDB references | Daily/monthly/yearly, interval and partial periods | Pass |
| VDB switching | Straight-line switch and `no_switch` comparison | Pass |
| Domain/capability | Explicit `#VALUE!`/`#NUM!` paths | Pass |
| Resource boundary | 2,000,001 periods rejected before iteration | Pass |
| Registry count | 191 eager/versioned, 214 total | Pass |
| Full hosted CI | Core/Windows/Android/iOS/Mac Catalyst/MAUI Windows | Pass |

## Numerical and lifecycle decisions

- Cumulative payments reuse the established PMT and IPMT balance equations.
- Long sums use compensated summation.
- DB rounds the fixed declining rate to three decimals before scheduling.
- DDB/VDB cap depreciation at cost minus salvage minus prior depreciation.
- VDB builds the schedule from period zero so an interval starting later still receives the correct opening book value.
- Partial-period results multiply the full-period charge by interval overlap.
- Straight-line switching is sticky once selected.
- Every loop is bounded by `MaximumSchedulePeriods = 2_000_000`.

## Rollback

Revert the implementation commit to remove all five functions and restore the preceding 186-name registry. Do not partially retain the duplicate manual registry path.

## Next handoff

Implement the closed-form scalar helper batch: `ISPMT`, `EFFECT`, `NOMINAL`, `RRI`, `PDURATION`, with domain, coercion, inverse/round-trip and registry-count tests.
