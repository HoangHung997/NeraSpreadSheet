# NeraSpreadSheet feature matrix

This is Nera's capability map. Excel, LibreOffice and DevExpress are behavior/coverage references only.

| Area | Current validated capability | Next implementation |
|---|---|---|
| Workbook / sparse cells | Excel-size sparse sheets, merges, dimensions, snapshots and atomic structure transforms | manual hide/group/outline metadata and names |
| Selection / editing | Multi-range selection, reusable editor, spill-aware clipboard, commands and data/view Undo/Redo | mobile IME lifecycle and richer command surfaces |
| Styles | Direct styles, sparse whole-axis patches and exact history | named/theme styles and complete format semantics |
| Formula syntax/dependencies | Parser, AST, shared/structured formulas, dependency graph, scalar and dynamic affected recalculation | spill-reference syntax, vectorized expressions and volatile scheduling |
| Formula surface | 92 registry + 12 special + 5 dynamic names across scalar/reference/array families | plugin SDK, conditional aggregate, statistical/financial and advanced arrays |
| Dynamic arrays | Immutable arrays, owner/child spills, `#SPILL!`, eight-pass stabilization, SEQUENCE/TRANSPOSE/FILTER/SORT/UNIQUE | A1#, @, multi-key/advanced helpers, LET/LAMBDA and host spill UX |
| Dynamic editing/history | Child edit/partial clear guards, Undo/Redo, structural rematerialization and immutable snapshot identity | native spill-range selection affordances and large-array budgets |
| Clipboard | Partial spill copy/cut and paste-over-spill rejected before history; complete spills copy owner formula once and preserve direct child styles | external clipboard metadata/interoperability corpus and fuzzing |
| Formula errors/coercion | Shared blank/Boolean/number/text/date coercion, `#NUM!`, `#SPILL!`, lazy fallbacks and aggregate propagation | complete Excel literal/reference and locale compatibility |
| Lookup/reference | Basic INDEX/MATCH/XLOOKUP/VLOOKUP/HLOOKUP with dependency capture | advanced match/search modes, wildcards and broader array returns |
| Conditional formatting | CellIs/Expression, differential styles, history, renderer and XLSX | color scales, data bars, icon sets and manager UI |
| Data validation | Current rule types/operators, editor gate, history, diagnostics, rendering and XLSX | named/cross-sheet lists and native presenters |
| Tables | Stable IDs, history, calculated/totals metadata, manager snapshots and standard XLSX | complete design/resize/style manager UI |
| AutoFilter | Table + direct worksheet predicates, compressed hidden rows and paged native foundations | rich XLSX filter markup, sort state and incremental publication |
| Rendering | Fractional scrolling and shared display lists across WPF, WinForms and MAUI GPU hosts; snapshots expose spill identity | spill border/selection UX and enforced hardware performance/accessibility baselines |
| Page setup/PDF | Deterministic pagination, virtualized preview, staged PDF and native print adapters | remaining XLSX semantics, font/visual corpus and real printers |
| XLSX | Cells, styles, panes, formulas, rules, Tables/filters, print settings, dynamic spill child cleanup and unknown-part preservation | full Office dynamic metadata, manual breaks, first/even headers, custom paper and external corpus |
| CSV/TSV | Streaming quotes/newlines, buffer boundaries, type policy, injection protection and staged output | encoding/delimiter detection, corpus and fuzzing |
| Data / analysis | Basic sort, validation, Tables, filters, totals and first-generation array FILTER/SORT/UNIQUE | advanced sort, grouping, virtual data, pivot and slicers |
| Product hardening | Multi-platform CI, atomic export limits, validation runner and Codex acceptance plan | packaging, API compatibility, large-array fuzzing, recovery and release gates |

## Latest validated Dynamic Arrays Foundation milestone

- Implementation commit: `705afb46f05e687a7ee13147e6ed106b82944c04`.
- GitHub Actions: CI `#746`, run `32624762199`, success.
- Core, architecture, Windows, desktop GPU, Android, iOS, Mac Catalyst and MAUI Windows loaded-runtime gates passed.

## Weighted progress

- Engine/viewport/renderer foundation: approximately **92%**.
- Basic spreadsheet MVP: approximately **92–94%**.
- Complete professional roadmap: approximately **64%**.
- Production release readiness: approximately **41–44%**.

These are engineering-weighted estimates, not checkbox counts.
