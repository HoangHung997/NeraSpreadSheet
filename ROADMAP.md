# NeraSpreadSheet roadmap

`docs/current-status.md` is the implementation source of truth. A capability is complete only after executable source, automated tests and the applicable runtime gate pass.

## A. Independent spreadsheet engine

- [x] Excel-size sparse workbook/worksheet model.
- [x] Values, formulas, merges, dimensions and immutable snapshots.
- [x] Selection, clipboard, editor, commands and data/view Undo/Redo.
- [x] Atomic structural insert/delete/reorder with formula/rule/Table/worksheet-filter mapping.
- [x] Sparse whole-row/column styles.
- [x] Conditional Formatting and Data Validation Core models with structural history.
- [x] Table model with workbook-unique names, stable Table/column IDs and structural history.
- [x] Calculated-column metadata projection with bounded atomic rollback.
- [x] Totals-row label/formula projection and production metadata commands.
- [x] Direct worksheet AutoFilter range/criteria model with structural history.
- [ ] Sparse manual hide/group/outline metadata and complete axis property model.

## B. Viewport and rendering

- [x] Continuous fractional-pixel scrolling.
- [x] Freeze/split panes and independent pane scrolling.
- [x] Shared headers, resize, selection, editor and drag reorder.
- [x] Snapshot/tile caching and split-aware dirty regions.
- [x] WPF and WinForms software/GPU backends.
- [x] Shared Skia renderer and native MAUI GPU host.
- [x] Conditional/validation overlays in the shared display list.
- [x] Table and direct worksheet AutoFilter compressed hidden-row projection in layout, extent and hit test.
- [x] Shared Table filter-button identity/geometry for rendering, hit testing and native overlays.
- [x] Loaded device/context recreation and scale/orientation gates.
- [ ] Shared direct worksheet AutoFilter header-button geometry and native overlay entry points.
- [ ] Sustained 60/120 Hz, 4K target-hardware latency, memory and power budgets.

## C. Formula engine

- [x] Tokenizer, parser, AST, dependency graph and circular-reference policy.
- [x] Arithmetic, comparison, concatenation, A1 references/ranges and basic cross-sheet references.
- [x] `SUM`, `AVERAGE`, `MIN`, `MAX`, `COUNT` and `IF`.
- [x] Shared-formula import/export and mixed/absolute translation.
- [x] Shared structural-reference rewriter.
- [x] Structured-reference translation/evaluation and affected-only dependencies.
- [x] Atomic Table/column rename rewrite for cell formulas and Table metadata.
- [x] Calculated-column formula propagation across data rows.
- [x] Filter-aware `SUBTOTAL` for Average, Count Numbers, Count Nonblank, Maximum, Minimum and Sum.
- [x] Filter-source dependency tracking for affected-only totals recalculation.
- [ ] Remaining `SUBTOTAL`/`AGGREGATE` functions, nested subtotal exclusion and manual hidden-row semantics.
- [ ] Richer structured-reference grammar.
- [ ] Dynamic arrays and spill contracts.
- [ ] Complete math, text, date/time, lookup, statistical and financial function surface.
- [ ] Plugin function SDK for estimating/domain workloads.

## D. XLSX, printing and interoperability

- [x] Values, cached formulas, sheets, dimensions and merged ranges.
- [x] Pane metadata and Nera multi-pane state.
- [x] Cell/row/column styles and exact sparse style state.
- [x] Unknown package-part copy-and-patch preservation.
- [x] Shared formulas, Conditional Formatting and Data Validation standard round-trip.
- [x] Standard TableDefinitionPart, worksheet table relationships, styles, formulas and Table AutoFilter round-trip.
- [x] Standard worksheet-level AutoFilter value/custom/wildcard round-trip.
- [x] Worksheet AutoFilter malformed-input, schema and repeated opaque/extLst preservation gates.
- [ ] First-class `top10`, dynamic/date-group/color/icon filters and `sortState`.
- [ ] External compatibility corpus from Excel, LibreOffice and other XLSX generators.
- [ ] First-class drawings/images/charts model and editor.
- [ ] Print areas, page setup, page breaks, preview and PDF export.

## E. Data and analysis

- [x] Basic bounded in-memory sort.
- [x] Current complete Data Validation evaluator and editor gate.
- [x] Shared rich filter predicates: comparison, text, blank and relative date periods.
- [x] Table AutoFilter value/blank/custom predicates.
- [x] Direct worksheet AutoFilter production commands and Undo/Redo.
- [x] Table add/remove/rename/filter and calculated/totals metadata operations with Undo/Redo.
- [x] Filter-aware totals execution.
- [x] Platform-neutral Table manager and filter-menu snapshots.
- [x] Bounded distinct-value enumeration, search, truncation diagnostics and visible-selection commands.
- [x] Active-cell Table/column resolver and platform-neutral keyboard navigator.
- [x] Native WPF, WinForms and responsive MAUI Table-filter presenters.
- [x] Cancellable generation-checked paged Table filter-session foundation.
- [x] Loaded desktop and MAUI Windows Apply/Undo/Redo/focus lifecycle gates.
- [ ] Bind native WPF/WinForms/MAUI value lists to asynchronous paging and virtualization.
- [ ] Complete Table design/resize/style manager UI.
- [ ] Direct worksheet native filter buttons and presenters.
- [ ] Advanced multi-key sort and custom lists.
- [ ] Grouping, outlines and general subtotals.
- [ ] Pivot tables, slicers and calculated fields.
- [ ] External/virtualized data and incremental loading.

## F. Cross-platform controls

- [x] Platform-neutral command, Ribbon Core, Bars Core and DataGrid Core contracts.
- [x] Public WPF/WinForms spreadsheet hosts.
- [x] MAUI handler, touch state machine and pinch zoom.
- [x] Loaded Windows lifecycle/input/scale gates.
- [x] Native Table filter buttons, popup/dropdown/sheet and cross-platform keyboard navigation.
- [x] Search focus acquisition, close-time focus release/restoration and stable MAUI Automation IDs.
- [ ] Asynchronous paged/virtualized value-list bindings.
- [ ] Direct worksheet AutoFilter native entry points.
- [ ] Full Table manager and general column/context menus.
- [ ] Native validation manager/dropdown/prompt/error presenters.
- [ ] MAUI virtual keyboard and IME lifecycle.
- [ ] Responsive Ribbon/toolbar/menu/context-menu presenters.
- [ ] Production standalone DataGrid presenter.
- [ ] Complete theme, localization, high-contrast, accessibility and designer support.

## G. Product hardening

- [ ] API compatibility and package-version checks.
- [ ] NuGet packaging, symbols and source link.
- [ ] Crash recovery, safe mode and support bundle.
- [ ] Security review and fuzzing for formulas/XLSX/clipboard.
- [ ] Performance budgets enforced in CI.
- [ ] Alpha → Beta → RC → Production release gates.

## Immediate execution order

1. Native asynchronous paging/virtualization for WPF, WinForms and MAUI filter values.
2. Direct worksheet AutoFilter header-button geometry, target resolution and native presenters.
3. First-class XLSX `top10`, dynamic/date-group filters and `sortState`.
4. Complete Table design/resize/style manager UI.
5. MAUI IME/virtual-keyboard lifecycle and broader accessibility/localization/theme hardening.
6. External XLSX Table/AutoFilter compatibility corpus and differential tests.
7. Formula/function surface, dynamic arrays and plugin SDK.
8. Advanced sort, grouping, virtualized data, printing/PDF, drawings/charts and pivot/slicers.
9. Packaging, fuzzing, performance budgets and release hardening.

## Weighted progress after worksheet AutoFilter preservation and paged sessions

- Engine/viewport/renderer foundation: approximately `90%`.
- Basic spreadsheet MVP: approximately `84–87%`.
- Complete professional roadmap: approximately `56%`.
- Production release readiness: approximately `32–35%`.

These are engineering-weighted estimates, not checkbox counts.

## Validation rule

The latest validated implementation milestone is commit `023835495a5c56aea19830aff299765808ab5598`, CI `#586`, run `32543422821`, completed successfully on August 22, 2026. PR #1 remains Draft and must not merge while a newer exact-head CI is red or unknown.