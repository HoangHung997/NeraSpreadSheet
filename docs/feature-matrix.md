# NeraSpreadSheet feature matrix

This is Nera's capability map. Excel, LibreOffice and DevExpress are behavior/coverage references only.

| Area | Current validated capability | Next implementation |
|---|---|---|
| Workbook / sparse cells | Excel-size sparse sheets, merges, dimensions, snapshots and atomic structure transforms | manual hide/group/outline metadata and names |
| Selection / editing | Multi-range selection, reusable editor, clipboard, commands and data/view Undo/Redo | mobile IME lifecycle and richer command surfaces |
| Styles | Direct styles, sparse whole-axis patches and exact history | named/theme styles and complete format semantics |
| Formula syntax/dependencies | Parser, AST, shared/structured formulas, dependency graph and affected recalculation | array/spill dependency ownership |
| Formula surface | 92 registry + 12 special names across logical, aggregate, math, text, date/time and lookup | dynamic arrays, plugin SDK, conditional aggregate, statistical/financial |
| Formula errors/coercion | Shared blank/Boolean/number/text/date coercion, `#NUM!`, lazy error fallbacks and aggregate propagation | complete Excel literal/reference and locale compatibility |
| Lookup/reference | Basic INDEX/MATCH/XLOOKUP/VLOOKUP/HLOOKUP with dependency capture | advanced match/search modes, wildcards and array returns |
| Conditional formatting | CellIs/Expression, differential styles, history, renderer and XLSX | color scales, data bars, icon sets and manager UI |
| Data validation | Current rule types/operators, editor gate, history, diagnostics, rendering and XLSX | named/cross-sheet lists and native presenters |
| Tables | Stable IDs, history, calculated/totals metadata, manager snapshots and standard XLSX | complete design/resize/style manager UI |
| AutoFilter | Table + direct worksheet predicates, compressed hidden rows and paged native foundations | rich XLSX filter markup, sort state and incremental publication |
| Rendering | Fractional scrolling and shared display lists across WPF, WinForms and MAUI GPU hosts | enforced hardware performance/accessibility baselines |
| Page setup/PDF | Deterministic pagination, virtualized preview, staged PDF and native print adapters | remaining XLSX semantics, font/visual corpus and real printers |
| XLSX | Cells, styles, panes, formulas, rules, Tables/filters, print settings and unknown-part preservation | manual breaks, first/even headers, custom paper and external corpus |
| CSV/TSV | Streaming quotes/newlines, buffer boundaries, type policy, injection protection and staged output | encoding/delimiter detection, corpus and fuzzing |
| Data / analysis | Basic sort, validation, Tables, filters and filter-aware totals | advanced sort, grouping, virtual data, pivot and slicers |
| Product hardening | Multi-platform CI, atomic export limits, validation runner and Codex acceptance plan | packaging, API compatibility, fuzzing, recovery and release gates |

## Latest validated Formula Surface I milestone

- Implementation commit: `497ebf3fbaca79e2f294475af861077d47400d3c`.
- GitHub Actions: CI `#706`, run `32613991638`, success.
- Core, Windows, Android, iOS, Mac Catalyst and MAUI Windows gates passed.

## Weighted progress

- Engine/viewport/renderer foundation: approximately **92%**.
- Basic spreadsheet MVP: approximately **90–93%**.
- Complete professional roadmap: approximately **62%**.
- Production release readiness: approximately **39–42%**.

These are engineering-weighted estimates, not checkbox counts.