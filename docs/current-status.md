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

## Implemented

### Core, editing and rendering

- Excel-size sparse worksheets, values/formulas/styles/dimensions/merges, immutable snapshots and bounded caches.
- Selection, spill-aware clipboard, editor, commands, sort and Undo/Redo.
- Atomic structural operations with formula/rule/Table/filter/spill mapping.
- Sparse whole-axis styles, fractional scrolling, freeze/split panes and shared display-list rendering across WPF, WinForms and MAUI GPU hosts.

### Formula engine and SDK

- Parser/AST, A1/cross-sheet references, dependency graph, circular detection and affected-only recalculation.
- Shared/structured formulas and Table formula rewrite/projection.
- Function Extension SDK v1.0 with identity, implementation/API versions, aliases, side-by-side versions, capabilities, volatility/state, dependency policy, argument-count policy, conflict rules and legacy adapter.
- Built-in eager/versioned registry: **196 names**.
- AST/reference-aware built-ins: **18 names**.
- Dynamic-array built-ins: **5 names**.
- Complete built-in subsystem: **219 names**.

### Formula families

- Conditional aggregates with shared invariant criteria and bounded enumeration.
- Descriptive/order statistics, covariance/correlation/regression and 30 transformation/distribution functions.
- Nineteen engineering functions covering bit/shift/radix/comparison behavior.
- Twelve database criteria-table aggregate functions.

### Financial Functions Foundation

Twenty-three deterministic/pure SDK v1 functions are implemented:

- annuities and roots: `PV`, `FV`, `PMT`, `NPER`, `RATE`;
- periodic/irregular cash flows: `NPV`, `IRR`, `XNPV`, `XIRR`;
- payment decomposition/schedules: `IPMT`, `PPMT`, `CUMIPMT`, `CUMPRINC`, `ISPMT`;
- depreciation: `SLN`, `SYD`, `DB`, `DDB`, `VDB`;
- rate/growth helpers: `EFFECT`, `NOMINAL`, `RRI`, `PDURATION`.

Key contracts:

- `ISPMT` uses the equal-principal formula and a zero-based period coordinate in `0..nper`.
- `EFFECT` and `NOMINAL` truncate compounding periods toward zero and require at least one period.
- `RRI` requires positive periods/present/future values; equal present/future values return zero growth.
- `PDURATION` requires positive rate/present/future values; equal values return zero duration.
- `EFFECT`/`NOMINAL` and `RRI`/`PDURATION` are covered by forward/inverse round trips.
- Shared financial `log1p` evaluates a 64-term convergent series for `|x| <= 0.5`, avoiding cancellation at rates down to `1e-12` and below.
- Root, dated-cash-flow, cumulative-payment and depreciation contracts from earlier milestones remain unchanged.

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
- Financial basis/calendar, `YEARFRAC`, coupon-date helpers, AMOR, bond/treasury/price/yield/duration families remain pending.
- `ISPMT` currently follows the explicit zero-based period contract; broader external differential compatibility remains pending.
- External Excel/LibreOffice financial corpus, locale compatibility and fuzzing remain pending.
- Statistical hypothesis tests, confidence intervals, additional distributions and broader aliases remain pending.
- Advanced lookup/reference, special engineering, formula-expression database criteria and advanced arrays remain pending.
- Native spill UX, drawings/charts, real-printer validation, packaging/security/performance gates remain pending.

## Weighted progress estimate

- Engine/viewport/renderer foundation: approximately `92%`.
- Basic spreadsheet MVP: approximately `96–98%`.
- Complete professional roadmap: approximately `77%`.
- Production release readiness: approximately `54–57%`.

These are engineering-weighted estimates, not checkbox counts.

## Next implementation work

1. Financial calendar/day-count basis `0..4`.
2. `YEARFRAC` and coupon-date helpers: `COUPDAYBS`, `COUPDAYS`, `COUPDAYSNC`, `COUPNCD`, `COUPPCD`, `COUPNUM`.
3. Bond/treasury/price/yield/duration and AMOR families.
4. Statistical hypothesis tests and confidence intervals.
5. Advanced lookup/reference and dynamic-array helpers.
6. Plugin packaging/discovery, compatibility and isolation.
7. Native spill UX, drawings/charts, advanced data/pivot and release hardening.

## Validation policy

Core, architecture, full Windows, desktop GPU, Android, iOS, Mac Catalyst and MAUI Windows loaded-runtime gates are mandatory. Formula families additionally require result, descriptor, coercion/error, numerical-stability, inverse/reconciliation and resource-budget regressions.

## Latest validated implementation milestone

Implementation commit `e2d3bb4b296292ae83dc4c1a5e35a442f6574e4f` passed CI `#849`, run `32740594038`, including 185 formula tests and the complete hosted matrix. PR #1 remains Draft and must not merge while a newer exact-head run is red or unknown.
