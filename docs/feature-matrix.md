# NeraSpreadSheet feature matrix

This is Nera's own capability map. Excel, LibreOffice and DevExpress are external behavior/coverage references only.

| Area | Current validated capability | Next implementation |
|---|---|---|
| Workbook / sparse cells | Excel-size sparse sheets, bulk mutation, merges, dimensions, snapshots, insert/delete/reorder | hide/group/outline metadata, names and tables |
| Selection / editing | Multi-range selection, reusable editor, clipboard, commands and data undo/redo | mobile IME/editor lifecycle and richer command surfaces |
| Styles | Direct styles, sparse whole-axis patches, chronological row/column composition and exact history | named/theme styles and complete Excel format semantics |
| Conditional formatting | `CellIs`/`Expression`, priority, `StopIfTrue`, differential styles, structural history and renderer integration | color scales, data bars, icon sets, duplicate/top/average/time rules and rule-manager UI |
| Formula | Parser, arithmetic/comparison/ranges, basic cross-sheet references, six built-ins, shared-formula import/export | large function surface, dynamic arrays, structured references and plugin SDK |
| Formula dependencies | Direct/transitive graph, affected-only recalc and circular policy | spatial dependency index and scheduling |
| Pixel scrolling / panes | Fractional offsets, precision input, freeze/split panes, independent pane scrolling and tile cache | target-hardware performance budgets |
| Rendering | Shared display lists; WPF/WinForms fallback and GPU backends; Skia/MAUI GPU host | accessibility semantics and sustained 60/120-Hz validation |
| XLSX | Cells, styles, sparse style state, panes, merges, shared formulas, conditional formatting and unknown-part preservation | validation, tables, compatibility corpus, printing and drawings/charts |
| Package hardening | Nested relationship graph preservation, URI/relationship preflight and atomic copy-and-patch save | fuzzing and streaming preservation above 512 MiB |
| Data / analysis | Basic bounded in-memory sort | validation, AutoFilter, advanced sort, grouping, virtual data, pivot and slicers |
| Cross-platform controls | Public WPF/WinForms hosts and MAUI Windows/Android/iOS/Mac Catalyst builds | production Ribbon/Bars/DataGrid presenters, localization and designer support |
| Product hardening | Deterministic CI, desktop GPU and loaded MAUI runtime gates | NuGet, API compatibility, security review, support bundle and release gates |

## Weighted progress after conditional-formatting milestone

- Engine/viewport/renderer foundation: approximately **86%**.
- Basic spreadsheet MVP: approximately **70–74%**.
- Complete professional roadmap: approximately **46%**.
- Production release readiness: approximately **22–26%**.

These are engineering-weighted estimates, not checkbox counts.
