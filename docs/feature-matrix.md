# NeraSpreadSheet feature matrix

This is Nera's capability map. Excel, LibreOffice and DevExpress are behavior/coverage references only.

| Area | Current validated capability | Next implementation |
|---|---|---|
| Workbook / sparse cells | Excel-size sparse sheets, merges, dimensions, snapshots and atomic structure transforms | manual hide/group/outline metadata and names |
| Selection / editing | Multi-range selection, reusable editor, spill-aware clipboard, commands and data/view Undo/Redo | mobile IME lifecycle and richer command surfaces |
| Styles | Direct styles, sparse whole-axis patches and exact history | named/theme styles and complete format semantics |
| Formula syntax/dependencies | Parser, AST, shared/structured formulas, dependency graph and affected recalculation | volatile scheduling, spill references and vectorized expressions |
| Function SDK | API 1.0 identity/version/aliases/capabilities/state/dependency/conflict contracts with legacy adapter | package discovery, publisher trust, compatibility tooling and isolation |
| Formula surface | 183 eager/versioned + 18 special + 5 dynamic built-ins = 206 names | remaining finance, hypothesis tests, advanced lookup/arrays and special engineering |
| Conditional aggregates | COUNTIF(S), SUMIF(S), AVERAGEIF(S), shared criteria and positional dependencies | locale-specific criteria and broader coercion compatibility |
| Statistics | Median/mode/percentile/rank plus covariance, correlation, regression, forecast and 30 transformation/distribution functions | hypothesis tests, confidence intervals, additional distributions, aliases and extreme-tail corpus |
| Finance | PV/FV/PMT/NPER, NPV/IRR, IPMT/PPMT, SLN/SYD | RATE, XNPV/XIRR, cumulative payment, bond/coupon/day-count and accelerated depreciation |
| Engineering | 19 deterministic bit/shift/radix/comparison functions with fixed-width signed conversion | complex numbers, CONVERT/unit catalog, Bessel/error functions and corpus fuzzing |
| Database functions | 12 criteria-table aggregates with AND/OR criteria, wildcard escape, dependencies and budgets | formula-expression criteria, locale parsing, indexing and cube/external data |
| Dynamic arrays | Immutable arrays, spills, #SPILL!, affected calculation and SEQUENCE/TRANSPOSE/FILTER/SORT/UNIQUE | A1#, @, advanced helpers, LET/LAMBDA and native spill UX |
| Conditional formatting | CellIs/Expression, differential styles, history, renderer and XLSX | color scales, data bars, icon sets and manager UI |
| Data validation | Current rule types/operators, editor gate, history, diagnostics, rendering and XLSX | named/cross-sheet lists and native presenters |
| Tables | Stable IDs, history, calculated/totals metadata, manager snapshots and standard XLSX | complete design/resize/style manager UI |
| AutoFilter | Table + direct worksheet predicates, compressed hidden rows and paged native foundations | rich XLSX markup, sort state and incremental publication |
| Rendering | Fractional scrolling and shared display lists across WPF, WinForms and MAUI GPU hosts | spill UX and enforced hardware performance/accessibility baselines |
| Page setup/PDF | Deterministic pagination, virtualized preview, staged PDF and native print adapters | remaining XLSX semantics, font/visual corpus and real printers |
| XLSX | Cells, styles, panes, formulas, rules, Tables/filters, printing, spill cleanup and unknown-part preservation | Office dynamic metadata, manual breaks, first/even headers, custom paper and external corpus |
| CSV/TSV | Streaming quotes/newlines, buffer boundaries, type policy, injection protection and staged output | encoding/delimiter detection, corpus and fuzzing |
| Data / analysis | Basic sort, validation, Tables, filters, totals, dynamic FILTER/SORT/UNIQUE and database/statistical analysis foundations | advanced sort, grouping, virtual data, pivot and slicers |
| Product hardening | Multi-platform CI, atomic export limits, validation runner and Codex acceptance plan | packaging, API compatibility, security/fuzzing, recovery and release gates |

## Latest validated implementation milestone

- Implementation commit: `e713182d460f5c280e2c29e5642769eedf190d2f`.
- GitHub Actions: CI `#835`, run `32720631933`, success.
- Core, architecture, full Windows, desktop GPU, Android, iOS, Mac Catalyst and MAUI Windows loaded-runtime gates passed.

## Weighted progress

- Engine/viewport/renderer foundation: approximately **92%**.
- Basic spreadsheet MVP: approximately **96–98%**.
- Complete professional roadmap: approximately **74%**.
- Production release readiness: approximately **51–54%**.

These are engineering-weighted estimates, not checkbox counts.
