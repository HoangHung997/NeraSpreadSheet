# NeraSpreadSheet feature matrix

This is Nera's own capability map. Excel, LibreOffice and DevExpress are behavior/coverage references only.

| Area | Current validated capability | Next implementation |
|---|---|---|
| Workbook / sparse cells | Excel-size sparse sheets, bulk mutation, merges, dimensions, snapshots and atomic structure transforms | manual hide/group/outline metadata and names |
| Selection / editing | Multi-range selection, reusable editor, clipboard, commands and data/view Undo/Redo | mobile IME lifecycle and richer command surfaces |
| Styles | Direct styles, sparse whole-axis patches and exact history | named/theme styles and complete Excel format semantics |
| Conditional formatting | CellIs/Expression, differential styles, history, renderer and XLSX | color scales, data bars, icon sets and manager UI |
| Data validation | Current rule types/operators, editor gate, history, diagnostics, rendering and XLSX | named/cross-sheet lists and native presenters |
| Tables | Stable Table/column IDs, naming, structural history, calculated/totals metadata and standard XLSX | native manager and richer table UX |
| Calculated columns | Bounded propagation, A1 translation, structural refill, metadata recovery and exact Undo/Redo | metadata inference from interactive formula edits and virtual very-large columns |
| Totals / SUBTOTAL | Filter-aware Average/Count/COUNTA/Min/Max/Sum with filter-source dependencies | remaining aggregate codes, nested exclusion and manual hidden rows |
| Structured references | Current canonical grammar, evaluation, dependencies and rename rewrite | richer grammar and dynamic-array interaction |
| AutoFilter | Value/blank/custom comparisons and compressed hidden-row projection | native dropdown, rich text/date/top/custom-list filters and worksheet AutoFilter |
| Formula engine | Parser, dependencies, shared formulas, six base functions plus current SUBTOTAL | broad function surface, dynamic arrays and plugin SDK |
| Pixel scrolling / panes | Fractional offsets, freeze/split panes, independent scrolling and tile cache | enforced 60/120-Hz hardware budgets |
| Rendering | Shared display lists across WPF, WinForms and MAUI GPU hosts | accessibility semantics and production performance baselines |
| XLSX | Cells, styles, panes, shared formulas, CF, validation, Tables/filters and unknown-part preservation | external compatibility corpus, printing and drawings/charts |
| Data / analysis | Basic sort, validation, Tables, AutoFilter and filter-aware totals | advanced sort, grouping, virtual data, pivot and slicers |
| Product hardening | Deterministic multi-platform CI and loaded runtime gates | NuGet, API compatibility, fuzzing, support bundle and release gates |

## Weighted progress

- Engine/viewport/renderer foundation: approximately **89%**.
- Basic spreadsheet MVP: approximately **79–82%**.
- Complete professional roadmap: approximately **52%**.
- Production release readiness: approximately **27–31%**.

These are engineering-weighted estimates, not checkbox counts.
