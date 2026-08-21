# NeraSpreadSheet roadmap

`docs/current-status.md` is the implementation source of truth. A capability is complete only after executable source, automated tests and the applicable runtime gate pass.

## A. Independent spreadsheet engine

- [x] Excel-size sparse workbook/worksheet model.
- [x] Values, formulas, merges, dimensions and immutable snapshots.
- [x] Selection, clipboard, editor, commands and data/view Undo/Redo.
- [x] Atomic structural insert/delete/reorder with formula/rule/Table mapping.
- [x] Sparse whole-row/column styles.
- [x] Conditional Formatting and Data Validation Core models with structural history.
- [x] Table model with workbook-unique names, stable Table/column IDs and structural state/history.
- [x] Calculated-column metadata projection with bounded atomic rollback.
- [x] Totals-row label/formula projection and production metadata commands.
- [ ] Sparse manual hide/group/outline metadata and complete axis property model.

## B. Viewport and rendering

- [x] Continuous fractional-pixel scrolling.
- [x] Freeze/split panes and independent pane scrolling.
- [x] Shared headers, resize, selection, editor and drag reorder.
- [x] Snapshot/tile caching and split-aware dirty regions.
- [x] WPF and WinForms software/GPU backends.
- [x] Shared Skia renderer and native MAUI GPU host.
- [x] Conditional/validation overlays in the shared display list.
- [x] AutoFilter compressed hidden-row projection in layout, extent and hit test.
- [x] Shared Table filter-button identity/geometry for rendering, hit testing and native overlays.
- [x] Loaded device/context recreation and scale/orientation gates.
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
- [x] Standard TableDefinitionPart, worksheet table relationships, styles, formulas and AutoFilter round-trip.
- [x] Table malformed-input, schema and repeated `extLst` preservation gates.
- [ ] External compatibility corpus from Excel, LibreOffice and other XLSX generators.
- [ ] First-class drawings/images/charts model and editor.
- [ ] Print areas, page setup, page breaks, preview and PDF export.

## E. Data and analysis

- [x] Basic bounded in-memory sort.
- [x] Current complete Data Validation evaluator and editor gate.
- [x] Table AutoFilter value/blank/comparison predicates.
- [x] Table add/remove/rename/filter and calculated/totals metadata operations with Undo/Redo.
- [x] Filter-aware totals execution.
- [x] Platform-neutral Table manager and filter-menu snapshots.
- [x] Bounded distinct-value enumeration, search, truncation diagnostics and visible-selection commands.
- [x] Active-cell Table/column resolver and platform-neutral keyboard navigator.
- [x] Native WPF, WinForms and responsive MAUI Table-filter presenters.
- [x] Loaded desktop and MAUI Windows Apply/Undo/Redo/focus lifecycle gates.
- [ ] Complete Table design/resize/style manager UI.
- [ ] Virtualized/paged native distinct-value lists.
- [ ] Rich text/date/top/bottom/color/icon/custom-list filters and direct worksheet AutoFilter.
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

1. Rich filter predicates and direct worksheet AutoFilter.
2. Virtualized/paged filter-value lists and complete Table design/resize/style manager UI.
3. MAUI IME/virtual-keyboard lifecycle and broader accessibility/localization/theme hardening.
4. External XLSX Table/AutoFilter compatibility corpus and differential tests.
5. Formula/function surface, dynamic arrays and plugin SDK.
6. Advanced sort, grouping, virtualized data and outlines.
7. Printing/PDF, drawings/charts and pivot/slicers.
8. Packaging, fuzzing, performance budgets and release hardening.

## Weighted progress after native Table-filter presenters

- Engine/viewport/renderer foundation: approximately `89%`.
- Basic spreadsheet MVP: approximately `82–85%`.
- Complete professional roadmap: approximately `54%`.
- Production release readiness: approximately `30–33%`.

These are engineering-weighted estimates, not checkbox counts.

## Validation rule

The latest validated native Table-filter milestone is implementation commit `e3a814f5c0f6eb0fff75d30ee5ee217069139d71`, CI `#570`, run `32474664182`, completed successfully on August 21, 2026. PR #1 remains Draft and must not merge while a newer exact-head CI is red or unknown.