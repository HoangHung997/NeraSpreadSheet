# Current Work Handoff

- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` into `develop` — Draft, unmerged
- Implementation head: `eeb74ad4ee596f7cb56343b8459f2311538c8243`
- GitHub Actions: CI `#854`, run `32745296544`, success
- Formula tests: `192/192`
- Source of truth: `docs/current-status.md`
- Financial contract: `docs/financial-functions-foundation-contract.md`

## Batch completed: financial calendar and day-count foundation

| Work item | Result | Status |
|---|---|---|
| Shared date layer | Whole-date normalization and one platform-neutral service | Complete |
| Basis `0..4` | US 30/360, Actual/Actual, Actual/360, Actual/365, European 30/360 | Complete |
| Coupon frequency | Annual, semiannual and quarterly after truncation | Complete |
| `YEARFRAC` | Signed year fraction with leap/multi-year rules | Complete |
| `COUPPCD` / `COUPNCD` | Maturity-anchored previous/next coupon dates | Complete |
| `COUPDAYBS` / `COUPDAYS` / `COUPDAYSNC` | Basis-specific coupon day counts | Complete |
| `COUPNUM` | Remaining coupon count through maturity | Complete |
| End-of-month | Leap-February and month-end anchor preserved | Complete |
| Resource policy | Maximum 100.000 coupon periods | Complete |
| Formula regressions | 192 passed, zero failed | Green |
| Hosted matrix | Core, Windows, Android, iOS, Mac Catalyst, MAUI Windows loaded smokes | Green |
| Pull request | Remains Draft and unmerged | Locked |

## Functional contracts

### Dates, basis and frequency

- Date inputs are normalized to date-only values.
- Basis/frequency numeric inputs are truncated toward zero.
- Basis values: 0 US NASD 30/360, 1 Actual/Actual, 2 Actual/360, 3 Actual/365, 4 European 30/360.
- Coupon frequency is 1, 2 or 4.
- Coupon functions require settlement earlier than maturity.
- Invalid/coercion/range input returns `#VALUE!`; invalid financial domains return `#NUM!`.

### Maturity-anchored coupon schedule

Every coupon candidate is recalculated directly from maturity and its month offset. The engine does not repeatedly subtract months from the previously rounded date. This preserves an August-31 maturity as February-29/28 and August-31 coupon dates instead of drifting permanently to the 28th/29th.

- PCD is the coupon on or before settlement.
- NCD is the coupon strictly after settlement.
- Settlement exactly on a coupon date therefore has `COUPDAYBS = 0`.
- Search is capped at 100.000 periods.

### Day-count outputs

- `YEARFRAC` supports signed intervals and equal-date zero.
- `COUPDAYS` is actual PCD→NCD for basis 1, `360/frequency` for bases 0/2/4 and `365/frequency` for basis 3.
- `COUPDAYSNC` is actual/basis settlement→NCD for bases 1/2/3; bases 0/4 subtract days-before from the fixed coupon period.
- `COUPDAYBS` always uses the selected basis from PCD to settlement.

## Probe sequence and findings

### CI #852 — compile probe

- Three C# definite-assignment errors exposed uninitialized `out` parameters in the shared coupon argument reader.
- No runtime or reference adjustment was attempted before fixing the compile contract.

### CI #853 — functional probe

- Build succeeded.
- All new YEARFRAC/coupon result, domain, EOM, leap-year and metadata tests passed.
- 188/192 formula tests passed.
- The only failures were four old registry-count assertions expecting 196 instead of the correct 203.

### CI #854 — final implementation

- Registry assertions updated to 203 without changing numerical references.
- 192/192 formula tests passed.
- Architecture and full hosted matrix passed.

## Counts

- Eager/versioned built-ins: 203.
- AST/reference-aware built-ins: 18.
- Dynamic-array built-ins: 5.
- Complete built-in subsystem: 226.
- Financial functions: 30.

## Explicit limitations

- Current coupon schedules are regular maturity-anchored schedules.
- Odd-first/odd-last coupons, business-day calendars and holiday adjustment remain pending.
- Discount/maturity securities and fixed-coupon price/yield/duration remain pending.
- External producer differential corpora and financial fuzzing remain pending.
- Hypothesis tests, advanced lookup/arrays, plugin isolation, drawings/charts, pivots and release hardening remain pending.

## Progress

- Engine/viewport/renderer: about `92%`.
- Basic spreadsheet MVP: about `96–98%`.
- Complete professional roadmap: about `78%`.
- Production readiness: about `55–58%`.

## Next stable batch

1. `ACCRINTM`.
2. `DISC`.
3. `INTRATE`.
4. `RECEIVED`.
5. `PRICEDISC`.
6. `YIELDDISC`.
7. Shared maturity-security equations, basis/domain and inverse/reconciliation regressions.
8. Add `PRICEMAT`/`YIELDMAT` only if the first six remain one coherent green batch.
9. Exact-head Core/Windows/MAUI CI.

PR remains Draft; do not merge while a newer exact-head CI is red or unknown.
