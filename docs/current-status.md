# NeraSpreadSheet current implementation status

This file is the source of truth for the current development branch. A capability is implemented only when executable source, automated tests and the applicable build/runtime gate exist.

## Product rules

- Independent spreadsheet SDK; no Excel, LibreOffice or DevExpress runtime dependency.
- No native control per cell.
- Formula, dynamic-array, editing, layout, scrolling and printing semantics remain platform-neutral.
- OpenXml types stay inside adapter projects.
- Spill children are derived output owned by one top-left formula.
- Extension functions must pass API, capability, state and resource validation before registration.

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
- Built-in eager/versioned registry: **144 names**.
- AST/reference-aware built-ins: **18 names**.
- Dynamic-array built-ins: **5 names**.
- Complete built-in subsystem: **167 names**.

### Conditional aggregates

`COUNTIF`, `COUNTIFS`, `SUMIF`, `SUMIFS`, `AVERAGEIF`, `AVERAGEIFS` use a shared invariant criteria parser with comparison operators, wildcard/tilde escaping, same-shape positional ranges, dependency capture and a two-million-pass budget.

### Statistical Foundation

`MEDIAN`, `MODE.SNGL`, `PERCENTILE.INC`, `QUARTILE.INC`, `VAR.P`, `VAR.S`, `STDEV.P`, `STDEV.S`, `RANK.EQ`, `LARGE`, `SMALL` with bounded value retention, stable variance and affected recalculation.

### Financial Foundation

`PV`, `FV`, `PMT`, `NPER`, `NPV`, `IRR`, `IPMT`, `PPMT`, `SLN`, `SYD` with shared sign/timing rules, zero-rate paths, bounded cash-flow retention and deterministic nearest-guess IRR hardening.

### Engineering Functions Foundation

Nineteen deterministic/pure SDK v1 functions:

- `DELTA`, `GESTEP`;
- `BITAND`, `BITOR`, `BITXOR`, `BITLSHIFT`, `BITRSHIFT`;
- `DEC2BIN`, `DEC2OCT`, `DEC2HEX`;
- `BIN2DEC`, `OCT2DEC`, `HEX2DEC`;
- `BIN2OCT`, `BIN2HEX`, `OCT2BIN`, `OCT2HEX`, `HEX2BIN`, `HEX2OCT`.

Contracts include 48-bit bounded bit operations, signed shift direction, fixed-width two's-complement negatives, optional zero-padding, target-range validation, invariant parsing and explicit error behavior.

Full contract: `docs/engineering-functions-foundation-contract.md`.

### Database Functions Foundation

Twelve deterministic/pure SDK v1 functions:

- `DSUM`, `DCOUNT`, `DCOUNTA`, `DAVERAGE`;
- `DMAX`, `DMIN`, `DPRODUCT`, `DGET`;
- `DSTDEV`, `DSTDEVP`, `DVAR`, `DVARP`.

Database behavior includes rectangular header/data ranges, field selection by header or one-based index, criteria tables, AND within one criteria row, OR across rows, duplicate criteria headers, shared wildcard/tilde parser, compensated sums, stable sample/population variance, exact-one-record `DGET`, dependency capture, affected recalculation and explicit database/criteria/comparison budgets.

Full contract: `docs/database-functions-foundation-contract.md`.

### Dynamic arrays

- Immutable row-major arrays and spill owner/child identity.
- Collision preflight, direct-style preservation, `#SPILL!`, atomic replacement and eight-pass stabilization.
- `SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT`, `UNIQUE`.
- Spill-aware editing, clear, clipboard, Undo/Redo, structural mapping, snapshots and XLSX save boundary.

### Rules, Tables and filtering

- Conditional Formatting and Data Validation models, history, rendering and XLSX round-trip.
- Stable Table/column identities, calculated columns, structured references and filter-aware totals.
- Table/direct worksheet AutoFilter with compressed hidden rows, generation-guarded paging and native presenter foundations.

### XLSX, CSV/TSV, page layout and PDF

- XLSX cells/formulas/styles/panes/rules/Tables/filters/print settings and unknown-part preservation.
- Deterministic pagination, virtualized preview, staged PDF and WPF/WinForms print adapters.
- Streaming CSV/TSV with quote-boundary handling, explicit type/formula policy, injection protection and staged limits.

## Conservative limitations

- Formula surface is not complete Excel compatibility.
- SDK v1 does not yet load signed plugin packages or isolate third-party code.
- Engineering complex-number, unit conversion, Bessel and error-function families are pending.
- Database criteria cells do not execute formula expressions; processing is a bounded scan and headers must be unique.
- Advanced statistics, distributions, `RATE`, `XNPV`, `XIRR`, bond/coupon/day-count and accelerated depreciation are pending.
- Spill-reference `A1#`, implicit intersection `@`, advanced arrays and LET/LAMBDA are pending.
- Native spill UX, full external dynamic-array metadata, drawings/charts pagination, real-printer validation, independent PDF/font corpus and final packaging/security/performance gates are pending.

## Weighted progress estimate

- Engine/viewport/renderer foundation: approximately `92%`.
- Basic spreadsheet MVP: approximately `96–98%`.
- Complete professional roadmap: approximately `72%`.
- Production release readiness: approximately `49–52%`.

These are engineering-weighted estimates, not checkbox counts.

## Next implementation work

1. Advanced Statistical Functions Foundation: covariance, correlation, regression and distributions.
2. Remaining finance: `RATE`, `XNPV`, `XIRR`, cumulative payment, bond/coupon and accelerated depreciation.
3. Advanced lookup/reference and dynamic-array helpers.
4. Plugin packaging/discovery, compatibility and isolation.
5. Native spill UX, drawings/images/charts and print/PDF pagination.
6. Advanced data, grouping/outlines, virtual data, pivot tables and slicers.
7. Remaining XLSX/PDF/font/external formula corpora, MAUI accessibility/IME and release hardening.

## Validation policy

Core, architecture, full Windows, desktop GPU, Android, iOS, Mac Catalyst and MAUI Windows loaded-runtime gates are mandatory. Formula families additionally require result, descriptor, coercion/error, dependency, affected-recalculation and resource-budget regressions.

## Latest validated implementation milestone

Implementation commit `ba7d0ce079c451f6390f5aafcb0cf861ccad0caa` passed CI `#819`, run `32651011596`, across the full hosted matrix.
