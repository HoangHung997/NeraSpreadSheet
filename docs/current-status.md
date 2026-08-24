# NeraSpreadSheet current implementation status

This file is the source of truth for the current development branch. A capability is implemented only when executable source, automated tests and the applicable build/runtime gate exist.

## Product rules

- Independent spreadsheet SDK; no Excel, LibreOffice or DevExpress runtime dependency.
- No native control per cell.
- Formula, dynamic-array, editing, layout, scrolling and printing semantics remain platform-neutral.
- OpenXml types stay inside adapter projects.
- Spill children are derived output owned by one top-left formula.
- Extension functions pass API, capability, state and resource validation before registration.
- Numerical solvers and schedule loops are deterministic, bounded and fail closed.
- Built-ins are registered through one authoritative aggregation path; duplicate parallel implementations are rejected.

## Implemented

### Core, editing and rendering

- Excel-size sparse worksheets, values/formulas/styles/dimensions/merges, immutable snapshots and bounded caches.
- Selection, spill-aware clipboard, editor, commands, sort and data/view Undo/Redo.
- Atomic structural operations with formula/rule/Table/filter/spill mapping.
- Sparse whole-axis styles, fractional scrolling, freeze/split panes and shared display-list rendering across WPF, WinForms and MAUI GPU hosts.

### Formula engine and SDK

- Parser/AST, A1/cross-sheet references, dependency graph, circular detection and affected-only recalculation.
- Shared/structured formulas and Table formula rewrite/projection.
- Function Extension SDK v1.0 with identity, implementation/API versions, aliases, side-by-side versions, capabilities, volatility/state, dependency policy, argument-count policy, registration conflict rules and legacy adapter.
- Built-in eager/versioned registry: **191 names**.
- AST/reference-aware built-ins: **18 names**.
- Dynamic-array built-ins: **5 names**.
- Complete built-in subsystem: **214 names**.

### Conditional aggregates

`COUNTIF`, `COUNTIFS`, `SUMIF`, `SUMIFS`, `AVERAGEIF`, `AVERAGEIFS` use a shared invariant criteria parser, wildcard/tilde escaping, positional ranges, dependency capture and a two-million-pass budget.

### Statistical foundations

- Median/mode/percentile/quartile, variance/deviation and order statistics.
- Covariance/correlation/regression/forecast.
- Normal, log-normal, exponential, binomial, Poisson, Weibull, beta, gamma, chi-square, Student-t and F distribution families.
- Bounded stable moments, regularized beta/gamma and inverse searches with fail-closed non-convergence.

Full contract: `docs/advanced-statistical-functions-foundation-contract.md`.

### Financial Functions Foundation

Eighteen deterministic/pure SDK v1 functions are implemented:

- annuities: `PV`, `FV`, `PMT`, `NPER`, `RATE`;
- periodic/irregular cash flows: `NPV`, `IRR`, `XNPV`, `XIRR`;
- payment decomposition and cumulative schedules: `IPMT`, `PPMT`, `CUMIPMT`, `CUMPRINC`;
- depreciation: `SLN`, `SYD`, `DB`, `DDB`, `VDB`.

Key contracts:

- `RATE`, hardened `IRR` and `XIRR` use bounded root solvers and nearest-guess selection.
- `XNPV`/`XIRR` preserve positional value/date schedules, numeric-date truncation, a 365-day basis, dependencies and resource budgets.
- `CUMIPMT`/`CUMPRINC` reuse PMT/IPMT sign and beginning/end timing semantics, iterate inclusive whole-period ranges and reconcile to cumulative payments.
- `DB` rounds its fixed declining rate to three decimals and supports the optional first-year month plus final stub period.
- `DDB` caps each declining-factor charge at the remaining depreciable basis.
- `VDB` supports fractional start/end periods and switches once to straight-line when it becomes larger unless `no_switch` is true.
- Cumulative/depreciation schedules are limited to 2.000.000 periods.

Full contract: `docs/financial-functions-foundation-contract.md`.

### Engineering Functions Foundation

Nineteen deterministic/pure SDK v1 functions cover bit/shift, radix conversions, `DELTA` and `GESTEP`.

Full contract: `docs/engineering-functions-foundation-contract.md`.

### Database Functions Foundation

Twelve deterministic/pure SDK v1 database aggregate functions support rectangular databases, field selectors, AND/OR criteria tables, wildcard escaping, stable sums/variance, dependencies and explicit budgets.

Full contract: `docs/database-functions-foundation-contract.md`.

### Dynamic arrays, rules and data

- Immutable row-major arrays, spill ownership, collision preflight, `#SPILL!`, atomic replacement and stabilization.
- `SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT`, `UNIQUE`.
- Conditional Formatting, Data Validation, Tables and worksheet/Table AutoFilter.
- XLSX round-trip for current cells/formulas/styles/panes/rules/Tables/filters/printing.
- Deterministic pagination, virtualized preview, staged PDF and streaming CSV/TSV.

## Conservative limitations

- Formula surface is not complete Excel compatibility.
- SDK v1 does not yet load signed plugin packages or isolate third-party code.
- `ISPMT`, `EFFECT`, `NOMINAL`, `RRI`, `PDURATION`, AMOR/date-basis, bond/coupon/treasury/price/yield/duration families remain pending.
- Current `DB` contract requires whole `life`, `period` and `month`; current `DDB` target period is whole while `VDB` handles partial periods.
- External Excel/LibreOffice financial differential corpus, locale compatibility and financial fuzzing remain pending.
- Statistical hypothesis tests, confidence intervals, additional distributions and broader aliases remain pending.
- Engineering complex-number, unit conversion, Bessel and error-function families remain pending.
- Database formula-expression criteria and indexed execution remain pending.
- Spill reference `A1#`, implicit intersection `@`, advanced arrays and LET/LAMBDA remain pending.
- Native spill UX, drawings/charts, real-printer validation, packaging/security/performance gates remain pending.

## Weighted progress estimate

- Engine/viewport/renderer foundation: approximately `92%`.
- Basic spreadsheet MVP: approximately `96–98%`.
- Complete professional roadmap: approximately `76%`.
- Production release readiness: approximately `53–56%`.

These are engineering-weighted estimates, not checkbox counts.

## Next implementation work

1. Scalar financial helpers: `ISPMT`, `EFFECT`, `NOMINAL`, `RRI`, `PDURATION`.
2. AMOR/date-basis and bond/coupon/treasury/price/yield/duration families.
3. Statistical hypothesis tests and confidence intervals.
4. Advanced lookup/reference and dynamic-array helpers.
5. Plugin packaging/discovery, compatibility and isolation.
6. Native spill UX, drawings/images/charts and print/PDF pagination.
7. Advanced data, grouping/outlines, virtual data, pivot tables and slicers.
8. Remaining XLSX/PDF/font/external corpora, accessibility/IME and release hardening.

## Validation policy

Core, architecture, full Windows, desktop GPU, Android, iOS, Mac Catalyst and MAUI Windows loaded-runtime gates are mandatory. Formula families additionally require result, descriptor, coercion/error, numerical-stability/convergence, reconciliation and resource-budget regressions.

## Latest validated implementation milestone

Implementation commit `ea61fe227919358539355b814d4c2baf5f05b538` passed CI `#844`, run `32734262232`, including 179 formula tests and the complete hosted matrix. PR #1 remains Draft and must not merge while a newer exact-head run is red or unknown.
