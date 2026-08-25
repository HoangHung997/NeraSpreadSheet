# NeraSpreadSheet current implementation status

This file is the source of truth for the current development branch. A capability is implemented only when executable source, automated tests and the applicable build/runtime gate exist.

## Product rules

- Independent spreadsheet SDK; no Excel, LibreOffice or DevExpress runtime dependency.
- No native control per cell.
- Formula, dynamic-array, editing, layout, scrolling and printing semantics remain platform-neutral.
- OpenXml types stay inside adapter projects.
- Extension functions pass API, capability, state and resource validation before registration.
- Numerical solvers, schedule loops and special-function primitives are deterministic, bounded and fail closed.
- Built-ins use one authoritative aggregation path.
- Financial date/basis semantics live in `FinancialDateMath`; security functions reuse that service instead of duplicating conventions.

## Implemented

### Core, editing and rendering

- Excel-size sparse worksheets, values/formulas/styles/dimensions/merges, immutable snapshots and bounded caches.
- Selection, spill-aware clipboard, editor, commands, sort and Undo/Redo.
- Atomic structural operations with formula/rule/Table/filter/spill mapping.
- Sparse whole-axis styles, fractional scrolling, freeze/split panes and shared display-list rendering across WPF, WinForms and MAUI GPU hosts.

### Formula engine and SDK

- Parser/AST, A1/cross-sheet references, dependency graph, circular detection and affected-only recalculation.
- Shared/structured formulas and Table formula rewrite/projection.
- Function Extension SDK v1.0 with identity/version/API/capability/state/dependency/conflict contracts.
- Built-in eager/versioned registry: **208 names**.
- AST/reference-aware built-ins: **18 names**.
- Dynamic-array built-ins: **5 names**.
- Complete built-in subsystem: **231 names**.

### Formula families

- Conditional aggregates with shared invariant criteria and bounded enumeration.
- Descriptive/order statistics, covariance/correlation/regression and 30 transformation/distribution functions.
- Nineteen engineering functions covering bit/shift/radix/comparison behavior.
- Twelve database criteria-table aggregate functions.

### Financial Functions Foundation

Thirty-five deterministic/pure SDK v1 functions are implemented:

- annuities and roots: `PV`, `FV`, `PMT`, `NPER`, `RATE`;
- periodic/irregular cash flows: `NPV`, `IRR`, `XNPV`, `XIRR`;
- payment decomposition/schedules: `IPMT`, `PPMT`, `CUMIPMT`, `CUMPRINC`, `ISPMT`;
- depreciation: `SLN`, `SYD`, `DB`, `DDB`, `VDB`;
- rate/growth helpers: `EFFECT`, `NOMINAL`, `RRI`, `PDURATION`;
- calendar/day-count: `YEARFRAC`, `COUPDAYBS`, `COUPDAYS`, `COUPDAYSNC`, `COUPNCD`, `COUPPCD`, `COUPNUM`;
- maturity securities: `ACCRINTM`, `DISC`, `INTRATE`, `RECEIVED`, `PRICEDISC`.

Maturity-security contracts:

- Dates are normalized to whole dates; issue/settlement or settlement/maturity ordering is validated before arithmetic.
- Basis is truncated toward zero and must be in `0..4`.
- `ACCRINTM` computes `par × rate × YEARFRAC(issue, settlement, basis)`, with default par 1000 and default basis 0.
- `DISC`, `INTRATE`, `RECEIVED` and `PRICEDISC` share one year fraction and fail closed on zero/nonpositive denominators or nonpositive resulting price.
- `DISC(PRICEDISC(...))` round-trip regression locks the shared discount equation.
- Unsupported range arguments or failed scalar coercion return `#VALUE!`; invalid financial domains return `#NUM!`.

Earlier root, cash-flow, cumulative-payment, depreciation, scalar-rate and coupon-calendar contracts remain unchanged.

Full contract: `docs/financial-functions-foundation-contract.md`.

### Dynamic arrays, rules and data

- Immutable row-major arrays, spill ownership, collision preflight, `#SPILL!`, atomic replacement and stabilization.
- `SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT`, `UNIQUE`.
- Conditional Formatting, Data Validation, Tables and worksheet/Table AutoFilter.
- XLSX round-trip for current cells/formulas/styles/panes/rules/Tables/filters/printing.
- Deterministic pagination, virtualized preview, staged PDF and streaming CSV/TSV.

## Conservative limitations

- Formula surface is not complete Excel compatibility.
- SDK v1 does not yet load signed plugin packages or isolate third-party code.
- `YIELDDISC`, maturity-interest price/yield, full accrued-interest schedules, fixed-coupon PRICE/YIELD/DURATION, treasury and AMOR/odd-coupon families remain pending.
- Current coupon schedules are regular maturity-anchored schedules; odd-first/odd-last periods and business-day adjustment are pending.
- External Excel/LibreOffice financial corpus, locale compatibility and fuzzing remain pending.
- Statistical hypothesis tests, advanced lookup/reference, special engineering, advanced arrays, plugin isolation and release hardening remain pending.

## Weighted progress estimate

- Engine/viewport/renderer foundation: approximately `92%`.
- Basic spreadsheet MVP: approximately `96–98%`.
- Complete professional roadmap: approximately `79%`.
- Production release readiness: approximately `56–59%`.

These are engineering-weighted estimates, not checkbox counts.

## Next implementation work

1. F002: `YIELDDISC`, `PRICEMAT`, `YIELDMAT`, `ACCRINT`, `FVSCHEDULE`.
2. Fixed-coupon `PRICE`, `YIELD`, `DURATION`, `MDURATION`, then treasury/AMOR/odd-coupon families.
3. Continue through the five-function queue in `docs/formula-completion-master-schedule.md`.

## Validation policy

Core, architecture, full Windows, desktop GPU, Android, iOS, Mac Catalyst and MAUI Windows loaded-runtime gates are mandatory. Maturity-security changes additionally require official reference values, basis/date/domain/scalar-capability tests, equation round trips and registry-count regressions.

## Validation state

Implementation commit `3ea2ae2d576e40b72e91c02ab493f1e244ffe0bd` passed Core build, architecture and **198/198 formula tests**. CI #859's Apple job failed before checkout because its runner could not resolve `github.com`; an exact-head rerun is required before the public F001 milestone is reported complete. PR #1 remains Draft and unmerged.
