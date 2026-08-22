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
| Totals / SUBTOTAL | Filter-aware Average/Count/COUNTA/Min/Max/Sum with Table filter-source dependencies | remaining aggregate codes, nested exclusion and manual hidden rows |
| Structured references | Current canonical grammar, evaluation, dependencies and rename rewrite | richer grammar and dynamic-array interaction |
| Rich filter predicates | Comparison, begins/ends/contains, blank/nonblank and relative date periods shared by Table/worksheet filters | top/bottom/color/icon and locale-aware date grouping |
| Direct worksheet AutoFilter | Range/criteria model, structural history, compressed visibility and production commands | shared header buttons and native presenters |
| Filter menu contract | Bounded values/counts/search plus cancellable generation-checked paged session foundation | native async binding, virtualization and incremental publication |
| Native Table filters | Shared button geometry plus WPF Popup, WinForms ToolStripDropDown and responsive MAUI sheet | paged value binding and full Table manager/column menus |
| Keyboard / focus | Alt+Down, Escape, list navigation, toggle, visible selection, open-focus and close restoration on loaded native hosts | complete IME, screen-reader, high-contrast and localization certification |
| Formula engine | Parser, dependencies, shared formulas, six base functions plus current SUBTOTAL | broad function surface, dynamic arrays and plugin SDK |
| Pixel scrolling / panes | Fractional offsets, freeze/split panes, independent scrolling and tile cache | enforced 60/120-Hz hardware budgets |
| Rendering | Shared display lists across WPF, WinForms and MAUI GPU hosts; filtered-row spans affect extent and hit test | direct worksheet filter-button geometry and production performance baselines |
| XLSX | Cells, styles, panes, shared formulas, CF, validation, Tables and worksheet AutoFilter with unknown-part preservation | top10/dynamic/date-group/sortState and external compatibility corpus |
| Data / analysis | Basic sort, validation, Tables, direct AutoFilter, native Table filters and filter-aware totals | advanced sort, grouping, virtual data, pivot and slicers |
| Product hardening | Deterministic multi-platform CI and loaded runtime gates | NuGet, API compatibility, fuzzing, support bundle and release gates |

## Latest validated milestone

- Implementation commit: `023835495a5c56aea19830aff299765808ab5598`.
- GitHub Actions: CI `#586`, run `32543422821`, `success` on August 22, 2026.
- Core, Windows desktop, Android, iOS, Mac Catalyst and MAUI Windows gates passed.
- New gates cover worksheet AutoFilter XLSX round-trip, malformed input, repeated opaque/extLst preservation and paged-session generations/cancellation.
- Loaded MAUI Windows Table-filter, context-recreation and scale/orientation smokes remained green.

## Weighted progress

- Engine/viewport/renderer foundation: approximately **90%**.
- Basic spreadsheet MVP: approximately **84–87%**.
- Complete professional roadmap: approximately **56%**.
- Production release readiness: approximately **32–35%**.

These are engineering-weighted estimates, not checkbox counts.