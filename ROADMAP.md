# NeraSpreadSheet roadmap

`docs/current-status.md` is the implementation source of truth. A capability is complete only after executable source, automated tests and the applicable build/runtime gate pass.

## A. Independent spreadsheet engine

- [x] Excel-size sparse workbook/worksheet model.
- [x] Values, formulas, merges, dimensions and immutable snapshots.
- [x] Selection, spill-aware clipboard, editor, commands and data/view Undo/Redo.
- [x] Atomic structural insert/delete/reorder with formula/rule/Table/filter/spill mapping.
- [x] Sparse whole-row/column styles.
- [x] Conditional Formatting and Data Validation Core models with structural history.
- [x] Table model with stable IDs, structural state/history and calculated/totals metadata.
- [x] Worksheet-associated page setup and print area.
- [x] Immutable formula arrays and worksheet spill owner/child contracts.
- [ ] Sparse manual hide/group/outline metadata and complete axis property model.
- [ ] Print settings in structural history and complete named-range integration.

## B. Viewport, rendering and preview

- [x] Continuous fractional-pixel scrolling.
- [x] Freeze/split panes and independent pane scrolling.
- [x] Shared headers, resize, selection, editor and drag reorder.
- [x] Snapshot/tile caching and split-aware dirty regions.
- [x] WPF and WinForms software/GPU backends.
- [x] Shared Skia renderer and native MAUI GPU host.
- [x] Conditional/validation overlays and compressed filter spans.
- [x] Shared Table/worksheet filter-button geometry.
- [x] Shared print display-list composition.
- [x] Virtualized print-preview layout/session and native host foundations.
- [x] Immutable spill identity in worksheet snapshots.
- [x] Loaded spreadsheet context recreation and scale/orientation gates.
- [ ] Dedicated spill-border/selection UX on every native host.
- [ ] Dedicated loaded interaction gates for every new filter/preview/spill path.
- [ ] Enforced 60/120-Hz, 4K and large-array hardware budgets.

## C. Formula engine

- [x] Tokenizer, parser, AST, dependency graph and circular-reference policy.
- [x] Arithmetic, comparison, concatenation and A1 references/ranges.
- [x] Shared formulas and structured references.
- [x] Atomic Table/column formula rewrite.
- [x] Calculated-column propagation and current filter-aware `SUBTOTAL`.
- [x] Shared coercion/error layer including `#NUM!` and `#SPILL!`.
- [x] Logical/error and lazy-control functions.
- [x] Aggregate/information functions.
- [x] Scalar math, rounding, logarithmic and trigonometric functions.
- [x] Text, search/replace and Unicode functions.
- [x] Date/time functions and deterministic clock context.
- [x] Basic `INDEX`, `MATCH`, `XLOOKUP`, `VLOOKUP` and `HLOOKUP`.
- [x] Immutable dynamic-array values and spill ownership/collision contracts.
- [x] Dynamic-array source dependencies and bounded affected-only stabilization.
- [x] `SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT` and `UNIQUE`.
- [x] Spill-aware editing, Undo/Redo, structural mapping, clipboard and snapshot contracts.
- [ ] Spill-reference operator (`A1#`) and implicit-intersection (`@`).
- [ ] Array constants and arbitrary vectorized expressions.
- [ ] Advanced dynamic arrays: `SORTBY`, `TAKE`, `DROP`, `CHOOSECOLS`, `CHOOSEROWS`, `TOCOL`, `TOROW`, `HSTACK`, `VSTACK` and related helpers.
- [ ] `LET`, `LAMBDA`, `MAP`, `REDUCE`, `SCAN`, `MAKEARRAY`, `BYROW` and `BYCOL`.
- [ ] Versioned plugin-function SDK.
- [ ] Conditional aggregate families.
- [ ] Statistical, financial, engineering and database functions.
- [ ] Advanced lookup/reference modes and locale-aware formatting.
- [ ] Complete Excel coercion and volatile scheduling compatibility.

## D. XLSX, page layout and PDF

- [x] Values, formulas, sheets, dimensions, merges, panes and styles.
- [x] Unknown package-part copy-and-patch preservation.
- [x] Shared formulas, Conditional Formatting and Data Validation round-trip.
- [x] Standard Table parts and current AutoFilter round-trip.
- [x] Print margins, paper code, orientation, scale, fit, print options and odd header/footer.
- [x] `_xlnm.Print_Area` and `_xlnm.Print_Titles`.
- [x] Deterministic pagination and merged-cell protection.
- [x] Staged PDF for one worksheet, selected worksheets and print tickets.
- [x] WPF paginator and WinForms `PrintDocument`.
- [x] Dynamic-array-aware document save retains owner formulas, removes derived children and preserves direct child styles.
- [ ] Full Microsoft Office dynamic-array extension metadata and external producer corpus.
- [ ] XLSX manual breaks, first/even headers and arbitrary custom paper.
- [ ] Independent PDF validator/raster visual-diff corpus and font policy.
- [ ] First-class drawings/images/charts and print/PDF pagination.
- [ ] Physical printer capability and hard-margin negotiation.

## E. Data and analysis

- [x] Basic bounded in-memory sort.
- [x] Current Data Validation evaluator and editor gate.
- [x] Table and direct worksheet AutoFilter predicates.
- [x] Table operations and filter-aware totals with Undo/Redo.
- [x] Platform-neutral Table manager/filter snapshots.
- [x] Generation-guarded paged Table/worksheet filter sessions.
- [x] Random-access page cache and cancellable search.
- [x] Native WPF/WinForms/MAUI paged-filter foundations.
- [x] Streaming CSV/TSV and staged atomic output.
- [x] One-key row/column dynamic-array `SORT` and first-occurrence `UNIQUE`.
- [ ] Complete Table design/resize/style manager UI.
- [ ] Incremental distinct-value publication.
- [ ] Rich XLSX AutoFilter markup and `sortState`.
- [ ] Advanced multi-key sort, grouping, outlines and subtotals.
- [ ] Pivot tables, slicers and calculated fields.
- [ ] External/virtualized data and incremental loading.

## F. Cross-platform controls

- [x] Platform-neutral command, Ribbon Core, Bars Core and DataGrid Core contracts.
- [x] Public WPF/WinForms spreadsheet hosts.
- [x] MAUI handler, touch state machine and pinch zoom.
- [x] Native Table/worksheet filter and print-preview foundations.
- [x] WPF paginator and WinForms print adapter.
- [x] Spill child edit/clear/copy/cut/paste protection through shared session/controllers.
- [ ] Dedicated native spill-range border, selection and error affordances.
- [ ] Dedicated loaded smokes for every new native control path.
- [ ] Full Table manager and general column/context menus.
- [ ] Native validation presenters.
- [ ] MAUI virtual keyboard and IME lifecycle.
- [ ] Responsive Ribbon/toolbar/menu/context-menu presenters.
- [ ] Production standalone DataGrid presenter.
- [ ] Complete theme, localization, high-contrast, accessibility and designer support.

## G. Product hardening

- [x] Broad exact-head multi-platform CI matrix.
- [x] Staged atomic PDF and delimited-text output limits.
- [x] Repository-wide validation runner and Codex final-acceptance plan.
- [x] Dynamic-array shape/collision/history/structure/clipboard/XLSX automated gates.
- [ ] Versioned API compatibility and package-version checks.
- [ ] NuGet packaging, symbols and source link.
- [ ] Crash recovery, safe mode and support bundle.
- [ ] Security review and fuzzing for formulas, arrays, XLSX, CSV and clipboard.
- [ ] Performance budgets enforced in CI, including one-million-cell arrays.
- [ ] Target printer/device/DPI/accessibility compatibility matrix.
- [ ] Alpha → Beta → RC → Production release gates.

## Immediate execution order

1. Versioned function-extension SDK with stable identity, capabilities and compatibility policy.
2. `SUMIF(S)`, `COUNTIF(S)`, `AVERAGEIF(S)` and criteria parsing.
3. Statistical, financial, engineering and database function families.
4. Advanced dynamic-array syntax/functions and native spill UX.
5. Drawings/images/charts plus print/PDF pagination.
6. Advanced data analysis, grouping/outlines, virtual data, pivot and slicers.
7. Remaining print/XLSX semantics and PDF/font/dynamic-array compatibility corpus.
8. Complete Table design manager and rich filter markup.
9. Accessibility/IME/localization/theme and release hardening.
10. Execute final Codex acceptance before PR promotion.

## Weighted progress after Dynamic Arrays Foundation

- Engine/viewport/renderer foundation: approximately `92%`.
- Basic spreadsheet MVP: approximately `92–94%`.
- Complete professional roadmap: approximately `64%`.
- Production release readiness: approximately `41–44%`.

These are engineering-weighted estimates, not checkbox counts.

## Validation rule

Implementation commit `705afb46f05e687a7ee13147e6ed106b82944c04` passed CI `#746`, run `32624762199`. PR #1 remains Draft and must not merge while a newer exact-head run is red or unknown.
