# NeraSpreadSheet feature matrix

This is Nera's capability map. Excel, LibreOffice and DevExpress are behavior/coverage references only.

| Area | Current validated capability | Next implementation |
|---|---|---|
| Workbook / sparse cells | Excel-size sparse sheets, merges, dimensions, snapshots and atomic structure transforms | manual hide/group/outline metadata and names |
| Selection / editing | Multi-range selection, editor, spill-aware clipboard, commands and data/view Undo/Redo | mobile IME lifecycle and richer command surfaces |
| Styles | Direct styles, sparse whole-axis patches and exact history | named/theme styles and complete format semantics |
| Formula syntax/dependencies | Parser, AST, shared/structured formulas, dependency graph and affected recalculation | spill-reference syntax, vectorized expressions and volatile scheduler |
| Built-in formula surface | 113 eager + 18 reference-aware + 5 dynamic = 136 names | engineering/database, remaining finance, advanced statistics/lookup/array families |
| Function Extension SDK | API 1.0, identity/version, capabilities, volatility/state, dependencies, aliases/conflicts and legacy compatibility | manifests, version pinning, loading/signatures/isolation and array-return plugins |
| Conditional aggregates | Criteria engine plus COUNTIF(S), SUMIF(S), AVERAGEIF(S), shapes/dependencies/errors/budget | locale criteria, indexes, database criteria tables and external corpus |
| Statistical functions | MEDIAN, MODE.SNGL, inclusive percentile/quartile, VAR/STDEV P/S, RANK.EQ, LARGE, SMALL; logical args and two-million-value budget | exclusive percentiles, MODE.MULT, RANK.AVG, distributions, covariance/correlation/regression |
| Financial functions | PV/FV/PMT/NPER, NPV/IRR, IPMT/PPMT, SLN/SYD; zero-rate/timing/sign/dependency/budget and bounded nearest-guess IRR | RATE, XNPV/XIRR, cumulative payment, bonds/coupons/day-count and accelerated depreciation |
| Dynamic arrays | Immutable arrays, spill owners, `#SPILL!`, stabilization, SEQUENCE/TRANSPOSE/FILTER/SORT/UNIQUE | A1#, @, advanced helpers, LET/LAMBDA and host spill UX |
| Formula errors/coercion | Shared public conversion, lazy fallbacks, error propagation and range/scalar coercion boundaries | distinct numeric enum identity, complete literal/reference, locale and tie compatibility |
| Lookup/reference | Basic INDEX/MATCH/XLOOKUP/VLOOKUP/HLOOKUP with dependency capture | advanced search modes, wildcards and broader array returns |
| Conditional formatting | CellIs/Expression, differential styles, history, renderer and XLSX | color scales, data bars, icon sets and manager UI |
| Data validation | Current rules/operators, editor gate, history, diagnostics, rendering and XLSX | named/cross-sheet lists and native presenters |
| Tables | Stable IDs, history, calculated/totals metadata, manager snapshots and standard XLSX | complete design/resize/style manager UI |
| AutoFilter | Table + worksheet predicates, compressed hidden rows and paged native foundations | rich XLSX filter markup, sort state and incremental publication |
| Rendering | Fractional scrolling and shared display lists across WPF/WinForms/MAUI | spill UX and enforced hardware performance/accessibility baselines |
| Page setup/PDF | Deterministic pagination, virtualized preview, staged PDF and native print adapters | remaining XLSX semantics, font/visual corpus and real printers |
| XLSX | Cells/styles/panes/formulas/rules/Tables/filters/print settings/spill cleanup/unknown parts | Office dynamic metadata, manual breaks, first/even headers, custom paper and corpus |
| CSV/TSV | Streaming quotes/newlines, buffer boundaries, type policy, injection protection and staged output | encoding/delimiter detection, corpus and fuzzing |
| Data / analysis | Basic sort, filters, totals, dynamic arrays, conditional aggregates, scalar statistics and finance | engineering/database criteria, advanced sort, grouping, virtual data, pivot and slicers |
| Product hardening | Multi-platform CI, export limits, SDK/criteria/statistics/finance gates, validation runner and final acceptance | NuGet/API compatibility, plugin trust/isolation, fuzzing, recovery and release gates |

## Latest validated implementation milestone

- Implementation commit: `e8c349d0b969fa8c9734452573bf7e9bcfa4df28`.
- GitHub Actions: CI `#809`, run `32644745950`.
- Documentation promotes this milestone only after all Core, architecture, full Windows, desktop GPU, Android, iOS, Mac Catalyst and MAUI Windows loaded-runtime jobs conclude successfully.

## Weighted progress

- Engine/viewport/renderer foundation: approximately **92%**.
- Basic spreadsheet MVP: approximately **96–98%**.
- Complete professional roadmap: approximately **70%**.
- Production release readiness: approximately **47–50%**.

These are engineering-weighted estimates, not checkbox counts.
