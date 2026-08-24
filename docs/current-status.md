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
- Financial coupon/date semantics live in one shared layer so later PRICE/YIELD/DURATION families cannot diverge silently.

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
- Built-in eager/versioned registry: **203 names**.
- AST/reference-aware built-ins: **18 names**.
- Dynamic-array built-ins: **5 names**.
- Complete built-in subsystem: **226 names**.

### Formula families

- Conditional aggregates with shared invariant criteria and bounded enumeration.
- Descriptive/order statistics, covariance/correlation/regression and 30 transformation/distribution functions.
- Nineteen engineering functions covering bit/shift/radix/comparison behavior.
- Twelve database criteria-table aggregate functions.

### Financial Functions Foundation

Thirty deterministic/pure SDK v1 functions are implemented:

- annuities and roots: `PV`, `FV`, `PMT`, `NPER`, `RATE`;
- periodic/irregular cash flows: `NPV`, `IRR`, `XNPV`, `XIRR`;
- payment decomposition/schedules: `IPMT`, `PPMT`, `CUMIPMT`, `CUMPRINC`, `ISPMT`;
- depreciation: `SLN`, `SYD`, `DB`, `DDB`, `VDB`;
- rate/growth helpers: `EFFECT`, `NOMINAL`, `RRI`, `PDURATION`;
- calendar/day-count: `YEARFRAC`, `COUPDAYBS`, `COUPDAYS`, `COUPDAYSNC`, `COUPNCD`, `COUPPCD`, `COUPNUM`.

Key calendar/day-count contracts:

- Financial dates are normalized to whole dates before calculation.
- Basis values are `0` US NASD 30/360, `1` Actual/Actual, `2` Actual/360, `3` Actual/365 and `4` European 30/360.
- Basis and coupon frequency are truncated toward zero; valid frequencies are annual `1`, semiannual `2` and quarterly `4`.
- Coupon functions require settlement strictly earlier than maturity.
- Coupon schedules are generated from the maturity anchor rather than chaining prior results, preventing date drift.
- End-of-month maturity remains end-of-month across shorter months and leap-year February.
- `COUPPCD` may equal settlement when settlement is exactly a coupon date; `COUPNCD` remains strictly later.
- `COUPDAYS` uses actual coupon-period length for basis 1, `360/frequency` for bases 0/2/4 and `365/frequency` for basis 3.
- `YEARFRAC` supports signed intervals and an Actual/Actual denominator that handles leap-year and multi-year spans.
- Coupon search is bounded to 100.000 periods.

Earlier root, cash-flow, cumulative-payment, depreciation and scalar-rate contracts remain unchanged.

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
- Discount/maturity security functions, fixed-coupon PRICE/YIELD/DURATION, treasury and AMOR/odd-coupon families remain pending.
- Current coupon schedules are regular maturity-anchored schedules; odd-first/odd-last periods and business-day adjustment are pending.
- External Excel/LibreOffice financial corpus, locale compatibility and fuzzing remain pending.
- Statistical hypothesis tests, confidence intervals, additional distributions and broader aliases remain pending.
- Advanced lookup/reference, special engineering, formula-expression database criteria and advanced arrays remain pending.
- Native spill UX, drawings/charts, real-printer validation, packaging/security/performance gates remain pending.

## Weighted progress estimate

- Engine/viewport/renderer foundation: approximately `92%`.
- Basic spreadsheet MVP: approximately `96–98%`.
- Complete professional roadmap: approximately `78%`.
- Production release readiness: approximately `55–58%`.

These are engineering-weighted estimates, not checkbox counts.

## Next implementation work

1. Discount/maturity securities: `ACCRINTM`, `DISC`, `INTRATE`, `RECEIVED`, `PRICEDISC`, `YIELDDISC`.
2. `PRICEMAT` and `YIELDMAT` after the common equations are locked.
3. Fixed-coupon `PRICE`, `YIELD`, `DURATION`, `MDURATION`.
4. Treasury, AMOR and odd-first/odd-last coupon families.
5. Statistical hypothesis tests and confidence intervals.
6. Advanced lookup/reference and dynamic-array helpers.
7. Plugin packaging/discovery, compatibility and isolation.
8. Native spill UX, drawings/charts, advanced data/pivot and release hardening.

## Validation policy

Core, architecture, full Windows, desktop GPU, Android, iOS, Mac Catalyst and MAUI Windows loaded-runtime gates are mandatory. Financial calendar changes additionally require all basis variants, leap-year, February/end-of-month, exact-coupon-date, frequency, domain, scalar-capability and registry-count regressions.

## Latest validated implementation milestone

Implementation commit `eeb74ad4ee596f7cb56343b8459f2311538c8243` passed CI `#854`, run `32745296544`, including 192 formula tests and the complete hosted matrix. PR #1 remains Draft and must not merge while a newer exact-head run is red or unknown.
