# NeraSpreadSheet current implementation status

This file is the handoff source of truth for the current development branch. A capability is listed as implemented only when executable source, automated tests and the applicable runtime gate exist.

## Product rules

NeraSpreadSheet is an independent spreadsheet SDK.

- No runtime dependency on Microsoft Excel, LibreOffice or DevExpress.
- No UNO/Excel command identifiers in Nera public contracts.
- No UI control per cell.
- Workbook, formula, editing, layout, scrolling and command projects remain independent from WPF, WinForms and MAUI.
- Viewports use continuous `double` pixel offsets and may stop between row/column boundaries.
- Desktop and GPU/fallback hosts consume shared workbook, viewport and display-list semantics.

## Implemented

### Core workbook, formula and editing

- Sparse worksheets over an Excel-size logical address space.
- Multiple worksheets, immutable snapshots, values, formulas, direct style IDs, sparse row/column dimensions and native merged ranges.
- Structural insert/delete for complete axes with overflow preflight, formula/reference mapping and atomic rollback.
- Formula tokenizer, parser and AST for arithmetic, comparison, concatenation, references, ranges and basic cross-sheet references.
- `SUM`, `AVERAGE`, `MIN`, `MAX`, `COUNT` and `IF`.
- Dependency graph, circular-reference detection and affected-only recalculation.
- Selection, clipboard, formatting, merge, sort, reusable editor, commands and data undo/redo.

### Sparse whole-axis style storage

- `CellStylePatch` stores property-level changes instead of materializing every addressed cell.
- Every worksheet owns non-overlapping sparse row-style and column-style span maps.
- Row/column properties compose by one worksheet-global chronological sequence.
- Explicit non-default cell styles remain complete direct overrides; whole-axis actions patch existing direct cells without losing unrelated properties.
- Whole-row, whole-column and whole-sheet formatting keeps blank cells implicit and bypasses finite-range materialization limits.
- Insert/delete/reorder map style spans through the same axis transforms as cells, dimensions and merged ranges.
- Structural state includes row spans, column spans and style sequence for exact rollback/history.
- `WorksheetSnapshot` deep-copies style spans and caches equivalent row/column compositions.
- Visible blank and populated cells render effective axis styles through the shared display-list composer.
- Style-only execute/undo/redo is calculation-neutral.

Full semantics: `docs/whole-axis-style-contract.md`.

### Model-safe row and column reordering

- `WorksheetAxisMove` is a fixed-length permutation of one contiguous axis interval.
- Sparse cells, dimensions, axis styles and merged ranges move without materializing the logical axis.
- Local and cross-sheet formulas follow logical cell identity while preserving `$`, quoted sheet names and string literals.
- Discontiguous formula images and unsafe merged/freeze transformations are rejected atomically.
- Selection and all split-pane offsets map through the same transaction with exact undo/redo.
- Split and unsplit WPF/WinForms header drag share one reorder model and geometry.
- Drag-edge auto-scroll remains fractional-pixel and targets only the active pane/control.

### Continuous viewport, freeze, split panes and cache

- Sparse metric indexes and fractional pixel scrolling without row/column snapping.
- Snapshot cache and bounded translated viewport tile cache.
- Freeze panes and one/two/four-pane topology.
- Independent per-pane continuous scroll state, active-pane fallback and per-worksheet persistence.
- Integrated and optional overlay pane scrollbars.
- Split-aware headers, selection, editor, resizing, header reorder and dirty-region projection.

### Standalone split-view undo/redo history

- Split-view history is separate from workbook/data `SpreadsheetSession.History`.
- Each worksheet owns an independent bounded view-history stack.
- Direct topology, split-separator, active-pane and pane-scroll changes can be undone/redone without changing cell data history.
- `View.Split.Undo` and `View.Split.Redo` are native Nera commands with state/description support.
- `SpreadsheetSplitViewHistoryTransaction` coalesces many low-level updates, such as animated/wheel pane scroll or separator drag, into one logical history entry.
- No-change transactions do not create history; cancel/dispose can restore the exact before-state.
- WinForms and WPF public split controllers expose `CanUndoViewChange`, `CanRedoViewChange`, descriptions and `UndoViewChange`/`RedoViewChange`.
- Desktop runtime smoke verifies exact topology/offset restoration, redo, WPF GPU rendering after redo and complete isolation from data undo history.

### Desktop rendering and GPU backends

- WPF DrawingContext and shared-texture D3DImage.
- WinForms GDI+, Direct2D/DirectWrite HWND and D3D11/DXGI `FlipDiscard`.
- Hardware preference, WARP fallback, text-layout caching, recovery and diagnostics.
- Partial invalidation for safe retained paths and explicit full-frame fallbacks where required.

### Extended renderer recovery stress

- HWND Direct2D is repeatedly torn down/recreated, resized and rendered for 32 cycles while DirectWrite layout reuse remains valid.
- D3D11/DXGI swap-chain device stacks are recreated, resized and presented for 16 cycles with adapter/feature-level diagnostics retained.
- WPF shared-texture rendering is forced through 8 complete production `EndD3D -> StartD3D` device-stack restart cycles while the control remains loaded in a real Window.
- Every WPF restart must recreate a non-zero shared texture, render cached text and then demonstrate second-frame text-layout reuse.
- These gates intentionally use explicit/injected lifecycle resets rather than depending on nondeterministic physical driver failure on hosted CI.

### XLSX and samples

- Basic values, formulas/cached values, multiple sheets, row heights, column widths and merged ranges.
- Per-worksheet split state through standard SpreadsheetML pane metadata plus a Nera custom XML part.
- WPF and WinForms samples expose split modes, pane-scrollbar visibility, diagnostics and session Open/Save.

## Implemented but intentionally conservative

- Direct cell styles are complete overrides; Nera does not introduce a second partial-cell inheritance layer.
- Formula ranges that become discontiguous and merged ranges that split/reverse are rejected rather than converted into unions.
- Number formatting currently uses a .NET bridge rather than a complete Excel format-code engine.
- Sort is in-memory and uses a materialization limit.
- Basic XLSX does not yet round-trip the complete style table or sparse row/column style metadata.
- Structural/formula rewriting covers A1 syntax, not tables, structured references, shared formulas or dynamic arrays.
- Structural, metric, topology, theme and device-lifecycle changes use conservative full invalidation where retained correctness is not yet proven.
- Hosted CI can deterministically exercise explicit device-stack reset/restart, but cannot guarantee real hardware driver removal or OS-controlled D3DImage front-buffer loss.
- Sustained FPS, input latency and power behavior still require target-hardware benchmarks.

## Next implementation work

1. Production Skia display-list renderer plus MAUI native handler and touch/pan/pinch interaction.
2. Complete XLSX style-table and sparse row/column style round-trip without flattening logical axes.
3. Shared formulas, conditional formatting, validation, tables, drawings and unknown-part preservation.
4. Filters and advanced sorting.
5. Printing, page layout, preview and PDF export.
6. Charts, pivot/slicers, accessibility, packaging and production hardening.

## Not implemented yet

- Production Skia GPU/MAUI control and touch interaction.
- Full XLSX style fidelity and sparse axis-style round-trip.
- Shared formulas, conditional formatting, validation, tables, drawings, charts, macros and unknown-part preservation.
- Complete Excel-compatible function surface and dynamic arrays.
- AutoFilter/filter UI and advanced sort.
- Printing, page layout, preview and PDF export.
- Charts, pivot, slicers, collaboration and macro/query engine.

## Validation policy

- `NeraSpreadSheet.Core.slnx` must restore, build and test on cross-platform CI.
- `NeraSpreadSheet.slnx` must restore/build on Windows and all tests must pass.
- Architecture verification and Windows GPU/runtime smoke are mandatory.
- Whole-axis style requires no-materialization, chronological composition, direct override, merged anchor, structural mapping, exact history, snapshot cache and renderer tests.
- Split-view history must remain isolated per worksheet and from data history; desktop public-controller runtime smoke is mandatory.
- Recovery stress must exercise repeated HWND, DXGI and WPF device-stack lifecycle recreation without resource or rendering regression.
- PR #1 remains Draft and must not merge while exact-head CI is red or unknown.

## Latest validated implementation milestone

CI run #399 passed at implementation commit `8c796f9b39751ada69a77ada6a67bc21d984c363`:

- Core restore/build/tests and architecture verification passed.
- Full Windows restore/build/test and mandatory GPU/runtime smoke passed.
- Public WinForms/WPF split-view undo/redo runtime gates passed with data-history isolation.
- Existing cross-platform split-view transaction/command/history tests remained green.
- 32 HWND Direct2D resource-recreation cycles passed.
- 16 DXGI swap-chain device-stack recreation cycles passed.
- 8 forced WPF D3D device-stack restart cycles passed with texture recreation and text-layout reuse.
- Existing whole-axis styles, split panes, scrollbars, header reorder, edge auto-scroll and dirty-region gates remained green.

## Independence rule

Excel, LibreOffice and DevExpress may be used only as external behavior/coverage references. Their runtime engines, command IDs and public types are not NeraSpreadSheet dependencies.
