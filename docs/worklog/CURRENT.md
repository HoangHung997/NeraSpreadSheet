# Current Work Handoff

- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` into `develop` — Draft, unmerged
- F002 implementation head: `70051299a1531016ce82df981a49753f09d1d8a6`
- Formula tests: `204/204`
- Eager/versioned built-ins: `213`
- Complete built-ins: `236`
- Financial functions: `40`
- Source of truth: `docs/current-status.md`
- Master schedule: `docs/formula-completion-master-schedule.md`

## F002 — advanced maturity security and schedule functions

| Function | Result | Status |
|---|---|---|
| `YIELDDISC` | Discounted-security yield and price relationship | Complete |
| `PRICEMAT` | Price for interest paid at maturity | Complete |
| `YIELDMAT` | Algebraic inverse of PRICEMAT | Complete |
| `ACCRINT` | Bounded quasi-coupon accrued-interest schedule | Complete |
| `FVSCHEDULE` | Range/scalar variable-rate future value | Complete |
| Registry | 208 → 213 eager names | Complete |
| Formula regressions | 204 passed, zero failed | Green |
| Architecture | Verification passed | Green |
| Formula count maintenance | Shared authoritative test constant | Complete |
| Hosted matrix | Documentation exact-head run required | Pending gate |
| Pull request | Draft and unmerged | Locked |

## Key contracts

- `YIELDDISC` uses price rather than redemption in its denominator.
- `PRICEMAT` and `YIELDMAT` share one maturity-value/accrued-interest model and round trip.
- `ACCRINT` generates every coupon boundary from the first-interest anchor, preserves end-of-month dates and caps traversal at 100,000 periods.
- `calc_method=FALSE` starts at first interest only after that date; the three published pre-first-interest examples remain compatible.
- `FVSCHEDULE` accepts scalar/range schedules, treats blank cells as zero rates, rejects text/Boolean/date cells, captures dependencies and caps at 2,000,000 values.

## CI #861

- Build succeeded with zero warnings and zero errors.
- Formula tests: 204/204.
- Architecture verification passed.
- Android and Core jobs passed; remaining hosted jobs complete before the public milestone report.

## Next five — F003

1. `PRICE`.
2. `YIELD`.
3. `DURATION`.
4. `MDURATION`.
5. `MIRR`.

F003 requires a shared fixed-coupon cash-flow engine, bounded yield root solving, duration weighting, modified-duration reconciliation and MIRR range/dependency/root-domain tests.

PR remains Draft; do not merge while a newer exact-head run is red or unknown.
