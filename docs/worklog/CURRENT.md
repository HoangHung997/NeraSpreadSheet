# Current Work Handoff

- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` into `develop` — Draft, unmerged
- F001 implementation head: `3ea2ae2d576e40b72e91c02ab493f1e244ffe0bd`
- Formula tests: `198/198`
- Eager/versioned built-ins: `208`
- Complete built-ins: `231`
- Financial functions: `35`
- Source of truth: `docs/current-status.md`
- Master schedule: `docs/formula-completion-master-schedule.md`

## F001 — first five maturity-security functions

| Function | Contract | Status |
|---|---|---|
| `ACCRINTM` | Accrued interest at maturity; default par 1000; basis 0..4 | Complete |
| `DISC` | Annual discount rate from price/redemption | Complete |
| `INTRATE` | Annual interest rate from investment/redemption | Complete |
| `RECEIVED` | Maturity proceeds from investment/discount | Complete |
| `PRICEDISC` | Price of a discounted security | Complete |
| Registry | 203 → 208 eager names | Complete |
| Formula regressions | 198 passed, zero failed | Green |
| Architecture | Verification passed | Green |
| Hosted matrix | Exact-head rerun required after Apple checkout DNS failure | Pending gate |
| Pull request | Draft and unmerged | Locked |

## Functional decisions

- All five functions are deterministic/pure SDK v1, scalar-only and logical-argument-counted.
- Dates are normalized to whole dates.
- Basis is truncated toward zero and validated in `0..4`.
- All formulas reuse `FinancialDateMath.GetYearFraction`.
- Unsupported ranges/coercion return `#VALUE!`; invalid date/value/denominator domains return `#NUM!`.
- `DISC(PRICEDISC(...))` recovers the original discount within deterministic tolerance.

## CI #859 finding

Core build, architecture and 198/198 formula tests passed. The Apple job never checked out source because its hosted runner returned `Could not resolve host: github.com`; no compiler or runtime code ran there. A documentation/handoff commit triggers a fresh exact-head matrix. F001 is not publicly reported complete until that run is entirely green.

## Next five — F002

1. `YIELDDISC`.
2. `PRICEMAT`.
3. `YIELDMAT`.
4. `ACCRINT`.
5. `FVSCHEDULE`.

F002 must lock maturity-interest equations, full accrued-interest schedule behavior, range-aware schedule multiplication, inverse/reconciliation regressions and the same exact-head hosted gates.

PR remains Draft; do not merge while a newer exact-head run is red or unknown.
