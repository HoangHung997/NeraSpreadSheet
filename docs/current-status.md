# NeraSpreadSheet current implementation status

This file is the source of truth for the current development branch. A capability is implemented only when executable source, automated tests and the applicable build/runtime gate exist.

## Product rules

- Independent spreadsheet SDK; no Excel, LibreOffice or DevExpress runtime dependency.
- Formula, dynamic-array, editing, layout, scrolling and printing semantics remain platform-neutral.
- Extension functions pass API, capability, state and resource validation before registration.
- Numerical solvers, schedule loops and special-function primitives are deterministic, bounded and fail closed.
- Built-ins use one authoritative aggregation path.
- Financial date/basis semantics live in `FinancialDateMath`; securities reuse that service.

## Implemented

### Core, editing and rendering

- Excel-size sparse worksheets, values/formulas/styles/dimensions/merges, immutable snapshots and bounded caches.
- Selection, spill-aware clipboard, editor, commands, sort and Undo/Redo.
- Atomic structural operations with formula/rule/Table/filter/spill mapping.
- Fractional scrolling, freeze/split panes and shared WPF/WinForms/MAUI GPU display-list rendering.

### Formula engine and SDK

- Parser/AST, A1/cross-sheet references, dependency graph, circular detection and affected-only recalculation.
- Shared/structured formulas and Table formula rewrite/projection.
- Function Extension SDK v1.0 with identity/version/API/capability/state/dependency/conflict contracts.
- Built-in eager/versioned registry: **213 names**.
- AST/reference-aware built-ins: **18 names**.
- Dynamic-array built-ins: **5 names**.
- Complete built-in subsystem: **236 names**.
- Formula test registry count now uses one shared test constant rather than repeated literals.

### Formula families

- Conditional aggregates, descriptive/order statistics, covariance/regression and advanced distributions.
- Nineteen engineering functions and twelve database aggregate functions.
- Dynamic arrays: `SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT`, `UNIQUE`.

### Financial Functions Foundation

Forty deterministic/pure SDK v1 financial functions are implemented. F002 adds:

- `YIELDDISC` — yield of a discounted security;
- `PRICEMAT` — price per 100 face value for interest paid at maturity;
- `YIELDMAT` — inverse yield for that maturity-interest price;
- `ACCRINT` — periodic accrued interest with frequency, basis and calculation-method rules;
- `FVSCHEDULE` — future value under an ordered scalar/range schedule of rates.

Key F002 contracts:

- `YIELDDISC` uses `(redemption-price)/(price×YEARFRAC)` and accepts basis `0..4` after truncation.
- `PRICEMAT` and `YIELDMAT` share issue→settlement, settlement→maturity and issue→maturity fractions and are round-trip tested.
- `ACCRINT` builds bounded quasi-coupon periods anchored to `first_interest`, preserves end-of-month behavior and supports annual/semiannual/quarterly frequency.
- `calc_method=FALSE` accrues from `first_interest` only when settlement is later than that date; pre-first-coupon references continue to accrue from issue.
- `FVSCHEDULE` accepts a scalar or range, treats blanks as zero rates, rejects nonnumeric schedule cells, preserves dependencies and is capped at 2,000,000 values.
- Unsupported argument kinds/coercion return `#VALUE!`; invalid financial domains, non-finite results and exhausted budgets return `#NUM!`.

Earlier annuity/root, cash-flow, payment, depreciation, scalar-rate, calendar and F001 maturity-security contracts remain unchanged.

Full contract: `docs/financial-functions-foundation-contract.md`.

## Conservative limitations

- Formula surface is not complete Excel compatibility.
- Fixed-coupon `PRICE`, `YIELD`, `DURATION`, `MDURATION`, treasury, AMOR and odd-coupon families remain pending.
- Current coupon schedules are regular maturity-anchored schedules; business-day/holiday adjustment remains pending.
- External Excel/LibreOffice differential corpora, locale compatibility and financial fuzzing remain pending.
- Statistical hypothesis tests, advanced lookup/reference, advanced arrays, special engineering, provider isolation and release hardening remain pending.

## Weighted progress estimate

- Engine/viewport/renderer foundation: approximately `92%`.
- Basic spreadsheet MVP: approximately `96–98%`.
- Complete professional roadmap: approximately `80%`.
- Production release readiness: approximately `57–60%`.

These are engineering-weighted estimates, not checkbox counts.

## Next implementation work

1. F003: `PRICE`, `YIELD`, `DURATION`, `MDURATION`, `MIRR`.
2. Continue automatically through `docs/formula-completion-master-schedule.md` in exact five-function milestones.

## Validation state

F002 implementation commit `70051299a1531016ce82df981a49753f09d1d8a6` passed Core build, architecture and **204/204 formula tests** in CI #861. The public F002 milestone requires the documentation/handoff exact-head hosted matrix to be fully green. PR #1 remains Draft and unmerged.
