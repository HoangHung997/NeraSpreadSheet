# NeraSpreadSheet feature matrix

This is Nera's own capability map. Excel, LibreOffice and DevExpress are external behavior/coverage references only.

| Area | Current validated capability | Next implementation |
|---|---|---|
| Workbook / sparse cells | Excel-size sparse sheets, bulk mutation, merges, dimensions, snapshots, insert/delete/reorder | manual hide/group/outline metadata and richer axis properties |
| Tables | Stable Table/column IDs, workbook-wide naming, header/data/totals ranges, structural state and edit history | calculated-column propagation, totals execution and native Table manager |
| Selection / editing | Multi-range selection, reusable editor, clipboard, commands and data undo/redo | mobile IME/editor lifecycle and richer command surfaces |
| Styles | Direct styles, sparse whole-axis patches, chronological row/column composition and exact history | named/theme styles and complete Excel format semantics |
| Conditional formatting | `CellIs`/`Expression`, priority, `StopIfTrue`, differential styles, structural history and renderer integration | color scales, data bars, icon sets, duplicate/top/average/time rules and rule-manager UI |
| Data validation | Whole/decimal/date/time/text/list/custom rules, blank/error policies, editor gate, rule/cell history, diagnostics and shared rendering | named/cross-sheet lists, native rule manager, prompt/error presenters and dropdown UI |
| Formula | Parser, arithmetic/comparison/ranges, basic cross-sheet references, six built-ins and shared formulas | large function surface, dynamic arrays and plugin SDK |
| Structured references | Canonical Table references expand to A1, participate in dependency/affected recalc and rewrite atomically on rename | richer grammar, calculated-column fill and totals-aware semantics |
| Formula dependencies | Direct/transitive graph, affected-only recalc, circular policy and expanded Table-range dependencies | spatial dependency index and scheduling |
| Pixel scrolling / panes | Fractional offsets, freeze/split panes, independent scrolling and tile cache | target-hardware performance budgets |
| AutoFilter / row visibility | Value/blank and one/two comparison filters; compressed hidden spans drive extent, slots and hit-test | rich predicates, direct worksheet filters and native dropdown presenter |
| Rendering | Shared display lists; WPF/WinForms fallback and GPU backends; Skia/MAUI GPU host; invalid-cell overlay | accessibility semantics and sustained 60/120-Hz validation |
| XLSX | Cells, styles, panes, merges, shared formulas, conditional formatting, validation, standard Table parts/styles/filters and unknown-part preservation | external compatibility corpus, printing and drawings/charts |
| Package hardening | Nested relationship graph preservation, URI/relationship preflight, Table `extLst` coexistence and atomic copy-and-patch save | fuzzing and streaming preservation above 512 MiB |
| Data / analysis | Basic bounded sort, validation engine and current Table AutoFilter projection | advanced sort, subtotals, grouping, virtual data, pivot and slicers |
| Cross-platform controls | Public WPF/WinForms hosts and MAUI Windows/Android/iOS/Mac Catalyst builds | production Ribbon/Bars/DataGrid presenters, validation/Table/filter UX, localization and designer support |
| Product hardening | Deterministic CI, desktop GPU and loaded MAUI runtime gates | NuGet, API compatibility, security review, support bundle and release gates |

## Weighted progress after Table/Structured References/AutoFilter foundation

- Engine/viewport/renderer foundation: approximately **88%**.
- Basic spreadsheet MVP: approximately **76–80%**.
- Complete professional roadmap: approximately **50–51%**.
- Production release readiness: approximately **26–30%**.

These are engineering-weighted estimates, not checkbox counts.
