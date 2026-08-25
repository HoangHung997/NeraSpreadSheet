# Current Work Handoff

- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` into `develop` — Draft, unmerged
- F006 implementation head: `c43bf362054110940f149a144546c4bba13387e3`
- F006 hosted CI: #874, run `32818957096` — success
- Formula tests: `224/224`
- Eager/versioned built-ins: `233`
- AST/reference-aware built-ins: `18`
- Dynamic-array built-ins: `5`
- Complete built-ins: `256`
- Financial functions: `56`
- Source of truth: `docs/current-status.md`
- Master schedule: `docs/formula-completion-master-schedule.md`

## F006 — odd-last yield và date compatibility

| Function | Result | Status |
|---|---|---|
| `ODDLYIELD` | Exact algebraic inverse của odd-last price state | Complete |
| `DATEDIF` | Legacy year/month/day và residual units | Complete |
| `DAYS360` | Signed US NASD / European 30/360 | Complete |
| `ISOWEEKNUM` | ISO 8601 week number | Complete |
| `WEEKNUM` | System-one start-day modes và ISO return type 21 | Complete |

Key contracts:

- `ODDLPRICE` và `ODDLYIELD` dùng cùng `last_coupon < settlement < maturity` state, frequency `1/2/4`, basis `0..4` và theoretical coupon boundary on-or-after maturity.
- `ODDLYIELD` yêu cầu rate không âm, price/redemption dương và giữ finite signed yield.
- `DATEDIF` units: `Y`, `M`, `D`, `MD`, `YM`, `YD`; start > end hoặc unknown unit trả `#NUM!`.
- Legacy `MD` có thể trả âm ở month-end edge cases.
- `DAYS360` mặc định US NASD; optional method true dùng European 30/360; reversed interval đổi dấu.
- `ISOWEEKNUM` và `WEEKNUM(...,21)` dùng ISO week-year; `WEEKNUM` hỗ trợ return type `1`, `2`, `11..17`, `21`.
- Tất cả descriptors scalar-only, deterministic/pure và logical-argument-counted.

## F006 validation

| Gate | Result |
|---|---|
| Build/analyzers | 0 warnings, 0 errors |
| Formula tests | 224/224 |
| Registry | 233 eager/versioned |
| Architecture verification | Pass |
| Windows build/tests + GPU smoke | Pass |
| Android build | Pass |
| iOS + Mac Catalyst builds | Pass |
| MAUI Windows build/handler | Pass |
| Table-filter loaded smoke | Pass |
| Runtime/context loaded smoke | Pass |
| Scale/orientation loaded smoke | Pass |
| Exact implementation CI | #874 — success |

## Whole-project snapshot

- Sparse Excel-size workbook, editing, clipboard, commands, sort và Undo/Redo.
- Atomic formula/rule/Table/filter/spill structural mapping.
- Parser/AST, dependency graph, shared/structured formulas và affected-only recalculation.
- Function SDK API 1.0 và một authoritative registry path.
- 256 built-ins, 56 financial functions, 19 engineering functions, 12 database functions và 5 dynamic arrays.
- Fractional pixel scrolling và WPF/WinForms/MAUI rendering hosts.
- XLSX preservation, streaming CSV/TSV, deterministic pagination, staged PDF và desktop print adapters.
- Exact-head CI matrix trên Core, Windows, Android, iOS, Mac Catalyst và MAUI Windows.

## Next five — F007

1. `NETWORKDAYS`.
2. `NETWORKDAYS.INTL`.
3. `WORKDAY`.
4. `WORKDAY.INTL`.
5. `NUMBERVALUE`.

F007 phải dùng một shared holiday/weekend calendar service, capture range dependencies, cap traversal và giữ signed behavior trước khi public names được promote. PR giữ Draft; không merge khi exact-head CI mới hơn red hoặc unknown.
