# Current Work Handoff

- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` into `develop` — Draft, unmerged
- F005 implementation head: `bbd4e7c70e7d8426ad79843373cc3aff744d9466`
- F005 hosted implementation CI: #872 — success
- Formula tests: `219/219`
- Eager/versioned built-ins: `228`
- AST/reference-aware built-ins: `18`
- Dynamic-array built-ins: `5`
- Complete built-ins: `251`
- Financial functions: `55`
- Source of truth: `docs/current-status.md`
- Master schedule: `docs/formula-completion-master-schedule.md`

## F005 — French depreciation and odd coupon securities

| Function | Result | Status |
|---|---|---|
| `AMORLINC` | Prorated French linear depreciation | Complete |
| `AMORDEGRC` | Accelerated French depreciation with useful-life coefficient | Complete |
| `ODDFPRICE` | Odd-first clean price over quasi-coupon ratios | Complete |
| `ODDFYIELD` | Bounded inverse of ODDFPRICE | Complete |
| `ODDLPRICE` | Odd-last clean price | Complete |

Key implementation decisions:

- AMOR uses basis `0`, `1`, `3`, `4`, whole-date normalization and truncated period/basis values.
- AMORDEGRC uses compatibility useful-life coefficients, whole-unit rounding and a 100,000-period cap.
- `FinancialDateMath` now owns bounded coupon-period ratios in addition to regular coupon dates/day counts.
- ODDF price/yield require strict date ordering and a frequency-aligned regular tail after the first coupon.
- ODDFYIELD solves the exact ODDFPRICE equation in log-periodic-yield space with at most 256 bisection iterations.
- ODDLPRICE derives period ratios from the next theoretical frequency-aligned coupon boundary on or after maturity.
- All five functions are SDK v1 scalar-only, deterministic/pure and logical-argument-counted.

## F005 regression gates

| Gate | Result |
|---|---|
| AMORLINC published reference | 360 — pass |
| AMORDEGRC published reference | 776 — pass |
| ODDFPRICE published reference | 113.59771747407883 — pass |
| ODDFYIELD published reference | 0.07724554159782439 — pass |
| ODDLPRICE published reference | 99.87828601472134 — pass |
| Short odd-first round trip | pass |
| Long odd-first round trip | pass |
| Domain/coercion/capability tests | pass |
| Registry count | 228 — pass |
| Formula suite | 219/219 — pass |
| Core build/analyzers | zero warnings/errors — pass |
| Architecture verification | pass |
| Windows desktop/GPU | pass |
| Android/iOS/Mac Catalyst | pass |
| MAUI Windows build/handler/loaded smokes | pass |
| Implementation CI | #872 — success |

## Whole-project handoff snapshot

- Sparse Excel-size workbook, editing, commands, clipboard, sort and Undo/Redo are implemented.
- Structural transforms preserve formula/rule/Table/filter/spill mapping.
- Parser/AST, dependency graph, circular detection, shared/structured formulas and affected-only recalculation are implemented.
- Function SDK v1.0 and one authoritative built-in registration path are implemented.
- Conditional rules, Tables, AutoFilter, dynamic arrays, XLSX preservation, CSV/TSV, pagination, PDF and desktop print adapters are implemented.
- Fractional scrolling and WPF/WinForms/MAUI GPU hosts are under the exact-head matrix.
- Production blockers remain: catalog completion, advanced lookup/arrays/LET/LAMBDA, drawings/charts/pivots, plugin isolation, security/fuzzing, localization/accessibility, packaging/recovery and release gates.

## Documentation/handoff gate

This documentation commit synchronizes README, roadmap, current status, feature matrix, financial/formula/SDK contracts, master schedule and F005 worklog. Public F005 completion requires the documentation exact-head hosted CI to be green.

## Next five — F006

1. `ODDLYIELD`.
2. `DATEDIF`.
3. `DAYS360`.
4. `ISOWEEKNUM`.
5. `WEEKNUM`.

F006 must reuse the F005 odd-last price state for `ODDLYIELD`, then lock date/week compatibility semantics without introducing host-specific calendar logic. PR remains Draft; do not merge while a newer exact-head run is red or unknown.
