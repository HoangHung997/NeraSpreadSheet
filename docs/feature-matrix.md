# NeraSpreadSheet feature matrix

This is Nera's capability map. Excel, LibreOffice and DevExpress are behavior/coverage references only.

| Area | Current validated capability | Next implementation |
|---|---|---|
| Workbook / sparse cells | Excel-size sparse sheets, merges, dimensions, snapshots and atomic structure transforms | manual hide/group/outline metadata and names |
| Selection / editing | Multi-range selection, editor, spill-aware clipboard, commands and data/view Undo/Redo | mobile IME lifecycle and richer command surfaces |
| Styles | Direct styles, sparse whole-axis patches and exact history | named/theme styles and complete format semantics |
| Formula syntax/dependencies | Parser, AST, shared/structured formulas, dependency graph, scalar/dynamic affected recalculation | spill-reference syntax, vectorized expressions and volatile scheduler |
| Built-in formula surface | 92 eager + 18 reference-aware + 5 dynamic = 115 names | statistical, financial, engineering/database and advanced lookup/array families |
| Function Extension SDK | API 1.0, stable identity/version, capabilities, volatility/state, dependency policy, aliases/conflicts and legacy compatibility | manifests, version pinning, package loading/signatures/isolation and array-return plugins |
| Conditional aggregates | Criteria engine plus COUNTIF(S), SUMIF(S), AVERAGEIF(S), shapes/dependencies/errors/budget | locale criteria, criteria indexes, database criteria tables and external corpus |
| Dynamic arrays | Immutable arrays, spill owners, `#SPILL!`, stabilization, SEQUENCE/TRANSPOSE/FILTER/SORT/UNIQUE | A1#, @, advanced helpers, LET/LAMBDA and host spill UX |
| Dynamic editing/history | Child guards, Undo/Redo, structural rematerialization, snapshots and XLSX boundary | native spill selection affordances and large-array budgets |
| Formula errors/coercion | Shared public conversion helpers, `#NUM!`, `#SPILL!`, lazy fallbacks and aggregate propagation | complete literal/reference and locale compatibility |
| Lookup/reference | Basic INDEX/MATCH/XLOOKUP/VLOOKUP/HLOOKUP with dependency capture | advanced search modes, wildcards and broader array returns |
| Conditional formatting | CellIs/Expression, differential styles, history, renderer and XLSX | color scales, data bars, icon sets and manager UI |
| Data validation | Current rule types/operators, editor gate, history, diagnostics, rendering and XLSX | named/cross-sheet lists and native presenters |
| Tables | Stable IDs, history, calculated/totals metadata, manager snapshots and standard XLSX | complete design/resize/style manager UI |
| AutoFilter | Table + worksheet predicates, compressed hidden rows and paged native foundations | rich XLSX filter markup, sort state and incremental publication |
| Rendering | Fractional scrolling and shared display lists across WPF/WinForms/MAUI; snapshots expose spill identity | spill UX and enforced hardware performance/accessibility baselines |
| Page setup/PDF | Deterministic pagination, virtualized preview, staged PDF and native print adapters | remaining XLSX semantics, font/visual corpus and real printers |
| XLSX | Cells/styles/panes/formulas/rules/Tables/filters/print settings/spill cleanup/unknown parts | Office dynamic metadata, manual breaks, first/even headers, custom paper and corpus |
| CSV/TSV | Streaming quotes/newlines, buffer boundaries, type policy, injection protection and staged output | encoding/delimiter detection, corpus and fuzzing |
| Data / analysis | Basic sort, filters, totals, array FILTER/SORT/UNIQUE and conditional aggregates | advanced sort, grouping, virtual data, pivot and slicers |
| Product hardening | Multi-platform CI, export limits, SDK/criteria gates, validation runner and final acceptance plan | NuGet/API compatibility, plugin trust/isolation, fuzzing, recovery and release gates |

## Latest validated implementation milestone

- Implementation commit: `19e749473ce68f0b67b110ba70b37339a4c7e155`.
- GitHub Actions: CI `#772`, run `32633548509`, success.
- Core, architecture, full Windows, desktop GPU, Android, iOS, Mac Catalyst and MAUI Windows loaded-runtime gates passed.

## Weighted progress

- Engine/viewport/renderer foundation: approximately **92%**.
- Basic spreadsheet MVP: approximately **94–96%**.
- Complete professional roadmap: approximately **66%**.
- Production release readiness: approximately **43–46%**.

These are engineering-weighted estimates, not checkbox counts.
