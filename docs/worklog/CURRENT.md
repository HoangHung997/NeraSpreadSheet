# Current Work Handoff

- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` into `develop` — Draft, unmerged
- Implementation head: `ea61fe227919358539355b814d4c2baf5f05b538`
- GitHub Actions: CI `#844`, run `32734262232`, success
- Formula tests: `179/179`
- Source of truth: `docs/current-status.md`
- Financial contract: `docs/financial-functions-foundation-contract.md`
- Final acceptance: `docs/CODEX_FINAL_ACCEPTANCE.md`

## Batch completed: cumulative payment and declining-balance depreciation

| Work item | Result | Status |
|---|---|---|
| Registry recovery | Restored one delegated registry and removed duplicate family registration | Complete |
| `CUMIPMT` | Inclusive cumulative interest with PMT/IPMT timing/sign consistency | Complete |
| `CUMPRINC` | Inclusive cumulative principal and PMT reconciliation | Complete |
| `DB` | Three-decimal fixed rate, first-year month and final stub | Complete |
| `DDB` | Factor-based declining charge capped at salvage | Complete |
| `VDB` | Partial periods, optional straight-line switch and no-switch path | Complete |
| Resource policy | Maximum 2,000,000 schedule/depreciation periods | Complete |
| Formula regressions | 179 passed, zero failed | Green |
| Hosted matrix | Core, Windows, Android, iOS, Mac Catalyst, MAUI Windows loaded smokes | Green |
| Pull request | Remains Draft and unmerged | Locked |

## Architecture repair

The previous head contained two parallel RATE/XNPV/XIRR code paths and rewrote the built-in registry to register families manually a second time. CI #843 correctly failed. This batch:

1. restored `BuiltInFormulaFunctionRegistry` as a thin delegate over one `VersionedFormulaFunctionRegistry`;
2. restored `StandardFormulaFunctions.CreateAll()` as the single family aggregation path;
3. replaced the duplicate `RemainingFinancialFormulaFunctions` implementation with the five functions in this batch;
4. replaced duplicate RATE/XNPV/XIRR tests with targeted cumulative/depreciation regressions.

No old tolerance or reference value was weakened.

## Functional contracts

### Cumulative loan schedules

- `CUMIPMT` and `CUMPRINC` require positive rate/nper/pv.
- Start/end are one-based whole periods and inclusive.
- Timing is `0` or `1`; beginning-of-period period 1 has zero interest.
- A single payment is calculated, then interest/principal is accumulated with compensated summation.
- More than 2,000,000 requested periods returns `#NUM!`.

### Declining-balance depreciation

- `DB` uses a three-decimal rounded fixed rate, optional month in `1..12` and an optional final stub period.
- `DDB` applies `opening_book * factor / life`, capped at remaining depreciable basis.
- `VDB` integrates full-period charges over fractional interval overlap.
- `VDB` switches once to straight-line when it is larger unless `no_switch` is true.
- All three prevent book value from falling below salvage and reject excessive schedules.

## Regression coverage

- Official cumulative-interest and cumulative-principal references.
- Cumulative interest + principal reconciliation to PMT.
- Beginning-of-period first interest.
- DB first period and final stub.
- DDB default/custom factor and late period.
- VDB daily/monthly/yearly references, multi-period intervals, partial period and switch/no-switch.
- Invalid domains, timing, factor, month, period and scalar capability.
- 2,000,001-period fail-closed boundaries.
- SDK identity/version/API/capability/volatility/security.
- Registry count at 191 eager/versioned names.

## Formula counts

- Eager/versioned built-ins: 191.
- AST/reference-aware built-ins: 18.
- Dynamic-array built-ins: 5.
- Complete built-in subsystem: 214.

## Explicit limitations

- `ISPMT`, EFFECT/NOMINAL, RRI/PDURATION remain pending.
- AMOR/date-basis and bond/coupon/treasury/price/yield/duration remain pending.
- Current DB life/period/month and DDB target period are whole-number contracts.
- External producer differential corpus and financial fuzzing remain pending.
- Statistical hypothesis tests, advanced arrays/lookups, plugin isolation, drawings/charts, pivots and release hardening remain pending.

## Progress

- Engine/viewport/renderer: about `92%`.
- Basic spreadsheet MVP: about `96–98%`.
- Complete professional roadmap: about `76%`.
- Production readiness: about `53–56%`.

## Next stable batch

1. `ISPMT`.
2. `EFFECT`.
3. `NOMINAL`.
4. `RRI`.
5. `PDURATION`.
6. Shared scalar rate/domain/coercion regressions.
7. Exact-head Core/Windows/MAUI CI.

PR remains Draft; do not merge while a newer exact-head CI is red or unknown.
