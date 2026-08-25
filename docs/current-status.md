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
- Built-in eager/versioned registry: **223 names**.
- AST/reference-aware built-ins: **18 names**.
- Dynamic-array built-ins: **5 names**.
- Complete built-in subsystem: **246 names**.
- Formula test registry count uses one shared test constant rather than repeated literals.

### Formula families

- Conditional aggregates, descriptive/order statistics, covariance/regression and advanced distributions.
- Nineteen engineering functions and twelve database aggregate functions.
- Dynamic arrays: `SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT`, `UNIQUE`.

### Financial Functions Foundation

Fifty deterministic/pure SDK v1 financial functions are implemented.

F003 adds:

- `PRICE` and `YIELD` over one maturity-anchored fixed-coupon cash-flow state;
- `DURATION` and `MDURATION` over the same coupon timing and present-value weights;
- `MIRR` with range dependency capture, positional cash-flow timing and bounded input size.

F004 adds:

- `TBILLEQ`, `TBILLPRICE`, `TBILLYIELD` with actual settlement-to-maturity days and a one-calendar-year boundary;
- `DOLLARDE`, `DOLLARFR` with truncated positive denominator, decimal-place scale and signed round-trip behavior.

Key contracts:

- `PRICE` subtracts accrued coupon interest from discounted cash flows; `YIELD` is a bounded inverse solver over the same equation.
- `DURATION` uses Macaulay present-value weights; `MDURATION = DURATION / (1 + yld/frequency)`.
- `MIRR` requires at least one positive and one negative participating cash flow and accepts at most 2,000,000 positions.
- Treasury-bill functions reject invalid date order, maturity beyond one calendar year, nonpositive discount/price and non-finite denominators.
- DOLLAR denominator is truncated toward zero; a negative input denominator returns `#NUM!`, while a truncated value below one returns `#DIV/0!`.
- Unsupported argument kinds/coercion return `#VALUE!`; invalid financial domains, non-finite results and exhausted budgets return `#NUM!`.

Earlier annuity/root, cash-flow, payment, depreciation, scalar-rate, calendar, F001 and F002 contracts remain unchanged.

Full contract: `docs/financial-functions-foundation-contract.md`.

## Conservative limitations

- Formula surface is not complete Excel compatibility.
- AMOR, odd-first/odd-last coupon, business-day and holiday families remain pending.
- Current regular coupon schedules are maturity anchored; odd-period quasi-coupon engines still require separate contracts.
- External Excel/LibreOffice differential corpora, locale compatibility and financial fuzzing remain pending.
- Statistical hypothesis tests, advanced lookup/reference, advanced arrays, special engineering, provider isolation and release hardening remain pending.

## Weighted progress estimate

- Engine/viewport/renderer foundation: approximately `92%`.
- Basic spreadsheet MVP: approximately `96–98%`.
- Complete professional roadmap: approximately `80–81%`.
- Production release readiness: approximately `58–61%`.

These are engineering-weighted estimates, not checkbox counts.

## Next implementation work

1. F005: `AMORLINC`, `AMORDEGRC`, `ODDFPRICE`, `ODDFYIELD`, `ODDLPRICE`.
2. Continue automatically through `docs/formula-completion-master-schedule.md` in exact five-function milestones.

## Validation state

F003 exact implementation head `48012398a3a020bfb12829bee46cfa88bc1c7fed` passed CI #866. F004 exact implementation head and current validation are recorded in `docs/worklog/CURRENT.md`. The formula suite contains **214 passing tests** at the F004 implementation gate. PR #1 remains Draft and unmerged.
