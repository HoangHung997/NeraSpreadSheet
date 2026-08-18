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
- Selection, clipboard, formatting, merge, sort, reusable editor, commands and undo/redo.

### Sparse whole-axis style storage

- `CellStylePatch` stores property-level changes instead of materializing every addressed cell.
- Every worksheet owns non-overlapping sparse row-style and column-style span maps.
- Span operations share one worksheet-global sequence, so row and column properties compose in chronological order rather than through fixed precedence.
- Explicit non-default cell styles remain complete direct overrides. A whole-axis action patches covered direct styles so the action remains visible without losing unrelated direct properties.
- Whole-row, whole-column and whole-sheet formatting leaves blank cells implicit and bypasses the finite-range materialization limit.
- Whole-sheet formatting uses one full row-axis span.
- Finite rectangles still materialize explicit styles and are rejected before mutation when they exceed the configured safety limit.
- Whole-axis formatting that intersects a merged range outside its anchor creates or updates only the top-left anchor while retaining merge, value, formula and unrelated style properties.
- Inserted axes inherit the sparse style sequence present at the insertion index. Shifted spans are clipped at the fixed worksheet boundary.
- Delete and `WorksheetAxisMove` map style spans through the same identity transform as cells, dimensions and merges.
- Structural state includes row spans, column spans and the next global style sequence for exact rollback and history.
- `WorksheetSnapshot` deep-copies style spans and caches equivalent row/column compositions.
- `SpreadsheetDisplayListComposer` renders effective styles for visible blank and populated cells.
- Style-only execute/undo/redo declares `AffectsCalculation = false`; the formula engine is not invoked.
- `UndoRedoManager.TryUndo` and `TryRedo` return the executed operation and restore their source stack when an operation throws.
- BenchmarkDotNet coverage exists for sparse whole-row formatting and repeated snapshot lookup.

The full semantics and gates are locked in `docs/whole-axis-style-contract.md`.

### Model-safe row and column reordering

- `WorksheetAxisMove` is a fixed-length permutation of one contiguous axis interval.
- Sparse cells, dimensions, axis styles and merged ranges move without materializing the logical axis.
- Local and cross-sheet formulas follow logical cell identity while preserving `$`, quoted sheet names and string literals.
- Discontiguous formula images and unsafe merged/freeze transformations are rejected atomically.
- Selection and all split-pane offsets map through the same transaction with exact undo/redo.

### Continuous viewport, freeze, split panes and cache

- Sparse metric indexes and fractional pixel scrolling without row/column snapping.
- Snapshot cache and bounded translated viewport tile cache.
- Freeze panes and one/two/four-pane topology.
- Independent per-pane continuous scroll state, active-pane fallback and per-worksheet persistence.
- Integrated and optional overlay pane scrollbars.
- Split-aware headers, selection, editor, resizing and dirty-region projection.

### Header reorder and edge auto-scroll

- Shared row/column drag source, threshold, drop and preview geometry.
- Native split and opt-in unsplit controllers for WPF and WinForms.
- Input priority preserves scrollbar, separator and resize interactions before reorder.
- Selected contiguous whole-axis ranges move as one block.
- Quadratic edge auto-scroll targets only the active control or pane, retains fractional offsets and recomputes the drop boundary every frame.
- WinForms real-message and WPF loaded-window production-state-machine gates cover commit, selection, undo and post-move GPU rendering.

### Desktop rendering and GPU backends

- WPF DrawingContext and shared-texture D3DImage.
- WinForms GDI+, Direct2D/DirectWrite HWND and D3D11/DXGI `FlipDiscard`.
- Hardware preference, WARP fallback, text-layout caching, recovery and diagnostics.
- Partial invalidation for safe retained paths and explicit full-frame fallbacks where required.

### XLSX and samples

- Basic values, formulas/cached values, multiple sheets, row heights, column widths and merged ranges.
- Per-worksheet split state through standard SpreadsheetML pane metadata plus a Nera custom XML part.
- WPF and WinForms samples expose split modes, pane-scrollbar visibility, diagnostics and session Open/Save.

## Implemented but intentionally conservative

- Direct cell styles are complete overrides; Nera does not introduce a second partial-cell inheritance layer.
- A merged anchor created only for off-anchor whole-axis formatting is one explicit styled cell, which is the minimum state needed for correct merged rendering.
- Formula ranges that become discontiguous and merged ranges that split/reverse are rejected rather than converted into unions.
- Number formatting currently uses a .NET bridge rather than a complete Excel format-code engine.
- Sort is in-memory and uses a materialization limit.
- Basic XLSX does not yet round-trip the complete style table or sparse row/column style metadata. Whole-axis styles are currently an in-memory Nera contract.
- Structural/formula rewriting covers A1 syntax, not tables, structured references, shared formulas or dynamic arrays.
- Structural, metric, topology, theme and device-lifecycle changes use conservative full invalidation where retained correctness is not yet proven.
- Sustained FPS, input-latency and power behavior still require target-hardware benchmarks.

## Next implementation work

1. Standalone undo/redo commands for direct split-view changes.
2. Longer-running injected device-loss/front-buffer-loss stress coverage.
3. Production Skia GPU plus MAUI native handler and touch interaction.
4. Complete XLSX style-table and sparse row/column style round-trip without flattening logical axes.
5. Shared formulas, conditional formatting, validation, tables, drawings and unknown-part preservation.
6. Filters, printing/PDF, charts, pivot, accessibility, packaging and production hardening.

## Not implemented yet

- Standalone undo/redo commands for direct split-view changes.
- Full XLSX style fidelity and sparse axis-style round-trip.
- Shared formulas, conditional formatting, validation, tables, drawings, charts, macros and unknown-part preservation.
- Complete Excel-compatible function surface and dynamic arrays.
- AutoFilter/filter UI and advanced sort.
- Printing, page layout, preview and PDF export.
- Charts, pivot, slicers, collaboration and macro/query engine.
- Production Skia GPU/MAUI control.

## Validation policy

- `NeraSpreadSheet.Core.slnx` must restore, build and test on cross-platform CI.
- `NeraSpreadSheet.slnx` must restore/build on Windows and all tests must pass.
- Architecture verification and Windows GPU/runtime smoke are mandatory.
- Whole-axis style requires no-materialization, chronological composition, direct override, merged anchor, structural mapping, exact history, snapshot cache and renderer tests.
- Style-only history must remain calculation-neutral.
- PR #1 remains Draft and must not merge while exact-head CI is red or unknown.

## Latest validated implementation milestone

CI run #377 passed at implementation commit `56e232ec928cf36db8a9497a78e2986b0b65a818`:

- Core restore/build/tests and architecture verification passed.
- Full Windows restore/build/test and mandatory GPU/runtime smoke passed.
- Whole-row, whole-column and whole-sheet formatting remained sparse.
- Direct override, merged anchor, insertion inheritance, delete/reorder mapping and exact undo/redo tests passed.
- Snapshot immutability/cache and blank-cell renderer tests passed.
- Finite materialization guard and whole-axis bypass tests passed.
- Undo/redo operation identity and failure-safety tests passed.
- Existing split, header reorder, dirty-region and GPU lifecycle gates remained green.

## Independence rule

Excel, LibreOffice and DevExpress may be used only as external behavior/coverage references. Their runtime engines, command IDs and public types are not NeraSpreadSheet dependencies.
