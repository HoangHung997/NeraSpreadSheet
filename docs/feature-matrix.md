# NeraSpreadSheet feature matrix

This is Nera's own capability map. Excel, LibreOffice and DevExpress are behavior/coverage references only.

| Area | Current validated capability | Next implementation |
|---|---|---|
| Workbook / sparse cells | Excel-size sparse sheets, bulk mutation, merges, dimensions, snapshots and atomic structure transforms | manual hide/group/outline metadata and names |
| Selection / editing | Multi-range selection, reusable editor, clipboard, commands and data/view Undo/Redo | mobile IME lifecycle and richer command surfaces |
| Styles | Direct styles, sparse whole-axis patches and exact history | named/theme styles and complete Excel format semantics |
| Conditional formatting | CellIs/Expression, differential styles, history, renderer and XLSX | color scales, data bars, icon sets and manager UI |
| Data validation | Current rule types/operators, editor gate, history, diagnostics, rendering and XLSX | named/cross-sheet lists and native presenters |
| Tables | Stable Table/column IDs, naming, structural history, calculated/totals metadata, manager snapshots and standard XLSX | complete design/resize/style manager UI |
| Calculated columns | Bounded propagation, A1 translation, structural refill, metadata recovery and exact Undo/Redo | metadata inference from interactive formula edits and virtual very-large columns |
| Totals / SUBTOTAL | Filter-aware Average/Count/COUNTA/Min/Max/Sum with filter-source dependencies | remaining aggregate codes, nested exclusion and manual hidden rows |
| Structured references | Current canonical grammar, evaluation, dependencies and rename rewrite | richer grammar and dynamic-array interaction |
| AutoFilter model | Value/blank/custom comparisons and compressed hidden-row projection | rich text/date/top/bottom/color/icon/custom-list filters and worksheet AutoFilter |
| Filter menu contract | Bounded value enumeration, counts, search, truncation flags, stable selection and production history | asynchronous/virtualized/paged enumeration for very large sources |
| Native Table filters | Shared button geometry plus WPF Popup, WinForms ToolStripDropDown and responsive MAUI sheet | full Table manager/column menus and broader mobile interaction |
| Keyboard / focus | Alt+Down, Escape, list navigation, toggle, visible selection, open-focus and close restoration on loaded native hosts | complete IME, screen-reader, high-contrast and localization certification |
| Formula engine | Parser, dependencies, shared formulas, six base functions plus current SUBTOTAL | broad function surface, dynamic arrays and plugin SDK |
| Pixel scrolling / panes | Fractional offsets, freeze/split panes, independent scrolling and tile cache | enforced 60/120-Hz hardware budgets |
| Rendering | Shared display lists across WPF, WinForms and MAUI GPU hosts | production performance and accessibility baselines |
| XLSX | Cells, styles, panes, shared formulas, CF, validation, Tables/filters and unknown-part preservation | external compatibility corpus, printing and drawings/charts |
| Data / analysis | Basic sort, validation, Tables, native Table filters and filter-aware totals | advanced sort, grouping, virtual data, pivot and slicers |
| Product hardening | Deterministic multi-platform CI and loaded runtime gates | NuGet, API compatibility, fuzzing, support bundle and release gates |

## Latest validated native-presenter milestone

- Implementation commit: `e3a814f5c0f6eb0fff75d30ee5ee217069139d71`.
- GitHub Actions: CI `#570`, run `32474664182`, `success` on August 21, 2026.
- Core, Windows desktop, Android, iOS, Mac Catalyst and MAUI Windows gates all passed.
- Loaded MAUI Windows Table-filter focus, Apply, Undo, Redo, reopen and close lifecycle passed.

## Weighted progress

- Engine/viewport/renderer foundation: approximately **89%**.
- Basic spreadsheet MVP: approximately **82–85%**.
- Complete professional roadmap: approximately **54%**.
- Production release readiness: approximately **30–33%**.

These are engineering-weighted estimates, not checkbox counts.