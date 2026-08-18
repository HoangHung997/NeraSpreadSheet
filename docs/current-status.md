# NeraSpreadSheet current implementation status

This file is the handoff source of truth for the current development branch. A capability is listed as implemented only when executable source, automated tests and the applicable runtime gate exist.

## Product rules

NeraSpreadSheet is an independent spreadsheet SDK.

- No runtime dependency on Microsoft Excel, LibreOffice or DevExpress.
- No UNO/Excel command identifiers in Nera public contracts.
- No UI control per cell.
- Workbook, formula, editing, layout, scrolling and command projects remain independent from WPF, WinForms and MAUI.
- Viewports use continuous `double` pixel offsets and may stop between row/column boundaries.
- WPF, WinForms and GPU/fallback hosts consume shared workbook, viewport and display-list semantics.

## Implemented

### Core workbook, formula and editing

- Sparse worksheets over an Excel-size logical address space.
- Multiple worksheets, versioned snapshots, values, formulas, style IDs, sparse row/column dimensions and native merged ranges.
- Structural insert/delete for complete row/column axes with overflow preflight, formula/reference mapping and atomic rollback.
- Formula tokenizer, parser and AST for arithmetic, comparison, concatenation, references, ranges and basic cross-sheet references.
- `SUM`, `AVERAGE`, `MIN`, `MAX`, `COUNT` and `IF`.
- Dependency graph, circular-reference detection and affected-only recalculation.
- Session-owned selection, undo/redo, calculation, clipboard, formatting, merge, sort, editor, view, structure and axis-reorder controllers.
- Single, extended, multi-range, whole-row, whole-column and whole-sheet selection.
- Native clipboard package plus TSV interoperability and relative/absolute A1 translation during paste.
- One reusable in-cell editor per desktop host.
- Per-worksheet freeze-pane state; merge/freeze boundaries cannot split a merged range.

### Model-safe row and column reordering

- `WorksheetAxisMove` represents a fixed-length permutation of one contiguous row or column interval.
- Destination is an insertion boundary in original axis coordinates; dropping inside or adjacent to the source is a no-op.
- Sparse cells, row-height/column-width overrides and merged ranges move without materializing the logical axis.
- Local and cross-sheet formulas follow logical cell identity while retaining `$` markers, quoted sheet names and string literals.
- Formula ranges whose image becomes discontiguous are rejected atomically rather than silently changing meaning.
- Merged ranges that would split, reverse anchor order or cross a freeze boundary are rejected before mutation.
- Active cell, anchor, whole-axis/multi-range selection and all split-pane offsets map through the same transaction.
- Pane offsets preserve the identity of the top-left row/column plus its fractional local pixel offset using exact sparse metrics.
- `SpreadsheetAxisReorderController` owns preflight, execution, rollback, full-workbook recalculation and exact undo/redo.

### Continuous viewport, freeze, split panes and cache

- Sparse row/column metric index and fractional pixel scrolling without row/column snapping.
- Pixel hit testing, content extent and merged-anchor resolution.
- Worksheet snapshot cache and bounded translated viewport tile cache.
- Freeze panes compose through frozen corner, frozen rows, frozen columns and scrolling body.
- Platform-neutral one-pane, vertical, horizontal and four-pane topology.
- Every pane owns an independent `ContinuousScrollController` with `double` X/Y offsets and bounds.
- Hidden panes retain state; unavailable active panes fall back to `TopLeft`.
- Shared split chrome renders headers, selection, separator continuation, active pane and integrated pane scrollbars.
- `SpreadsheetSplitViewState` stores topology, split X/Y, active pane and all four pane offsets per worksheet.
- Structural insert/delete and axis reorder participate in split-state transactions and exact undo/redo.
- Nested display lists retain immutable child references instead of flatten-copying command arrays.

### Pane-local scrollbars and split-aware dirty regions

- Shared pane-local scrollbar geometry supports buttons, track, proportional thumb, continuous offset, line/page input and targeted pane/axis routing.
- Integrated scrollbars are controlled by `SpreadsheetRenderTheme.ShowSplitPaneScrollBars`.
- Public optional WinForms/WPF scrollbar overlay controllers expose lifecycle, style, layout, hit testing and refresh.
- Changed ranges project into every visible pane, expand through merged cells and split at freeze boundaries.
- WinForms GDI+ and Direct2D HWND use partial invalidation; DXGI `FlipDiscard` uses explicit full-frame fallback.
- WPF D3DImage presents multiple dirty rectangles; DrawingContext uses full visual invalidation.

### Header drag reordering in split and unsplit desktop hosts

- `SpreadsheetSplitHeaderReorderGeometry` is the shared source/drop/threshold/preview contract for WinForms and WPF.
- Split row sources come from left-edge panes; split column sources come from top-edge panes.
- Pane scrollbars, split separators and dimension resize handles take priority over reorder.
- A selected contiguous whole-row/whole-column range moves as one block; otherwise one row/column moves.
- Drop position uses the nearest slot half and produces an original-coordinate destination boundary.
- Split WinForms uses the actual child-HWND message path, pointer capture and shared display-list preview.
- Split WPF uses preview routed input, optional mouse capture and a lightweight `DrawingVisual` above DrawingContext/D3DImage.
- Public unsplit WinForms and WPF controls now expose optional `EnableHeaderReordering`, `TryGetHeaderReorderController` and `DisableHeaderReordering` lifecycle APIs.
- The unsplit WinForms controller uses one lightweight hit-transparent preview child and the existing public control/session/viewport contracts.
- The unsplit WPF controller uses one lightweight preview adorner and requires an `AdornerLayer` only for visual preview; the model transaction remains host-independent.
- Rejected/no-op drops leave the workbook unchanged and create no undo entry.

### Header drag edge auto-scroll

- `SpreadsheetHeaderReorderAutoScroll` calculates platform-neutral quadratic edge velocity and elapsed-time pixel deltas.
- Velocity is axis-specific, zero outside the activation zone and clamped to a maximum speed when the pointer leaves the viewport.
- Unsplit WinForms uses a drag-owned timer; unsplit WPF uses `CompositionTarget.Rendering`.
- Split WinForms and split WPF calculate velocity against the currently targeted pane bounds and scroll only that pane.
- Every scroll step recomposes the visible layout and recalculates the drop boundary at the stationary pointer coordinate.
- Fractional offsets remain continuous; auto-scroll never snaps to row or column boundaries.
- Timer/render subscriptions, pointer capture and preview state are detached on completion, cancellation, unload and disposal.

### Public desktop hosts and GPU backends

- WinForms uses a Nera-owned child surface over the existing public control; WPF uses a Nera-owned `Adorner` under `AdornerLayer`/`AdornerDecorator`.
- GDI+, WPF DrawingContext, Direct2D/DirectWrite HWND, D3D11/DXGI `FlipDiscard` and WPF shared-texture/D3DImage consume shared display-list semantics.
- Hardware preference, WARP fallback, bounded DirectWrite layout caching, recovery and frame diagnostics are implemented.
- Runtime tests cover repeated WPF unload/reload and explicit second-frame text-layout reuse.

### XLSX and desktop samples

- Basic values, formulas/cached values, multiple sheets, row heights, column widths and merged ranges.
- `NeraOpenXmlSpreadsheetSessionSerializer` round-trips per-worksheet split state.
- Standard SpreadsheetML pane metadata plus a Nera custom XML part preserve compatible and full four-pane state.
- WPF and WinForms samples expose split modes, pane-scrollbar visibility, diagnostics and XLSX session Open/Save.

## Implemented but intentionally conservative

- Header reorder behavior is opt-in on unsplit controls and is automatically routed through the dedicated split host while split panes are enabled.
- WPF runtime gates use a real loaded `Window`, public control/controller and production state machines. Deterministic state-machine invocation is used where hosted Windows runners cannot reliably inject a global physical pointer.
- Formula ranges that become discontiguous and merged ranges that split/reverse are rejected rather than converted into unions.
- Number formatting uses the current .NET bridge rather than a complete Excel format-code engine.
- Sort is in-memory and uses a materialization safety limit.
- Sparse whole-axis style storage is not implemented yet.
- Structural/formula rewriting covers A1 cell/range syntax, not tables, structured references, shared formulas or dynamic arrays.
- Structural, metric, topology, theme and device-lifecycle changes remain conservative full invalidations.
- Sustained FPS/input-latency/power behavior still requires target-hardware benchmarks.

## Next implementation work

1. Add sparse whole-axis style storage and effective style composition without materializing every selected cell.
2. Add standalone undo/redo commands for direct split-view changes.
3. Add longer-running injected device-loss/front-buffer-loss stress coverage.
4. Implement production Skia GPU plus MAUI native handler/touch interaction.
5. Expand XLSX styles, shared formulas, conditional formatting, validation, tables, drawings and unknown-part preservation.
6. Continue toward filters, printing/PDF, charts, pivot, accessibility, packaging and production hardening.

## Not implemented yet

- Sparse whole-axis styles.
- Standalone undo/redo commands for direct split-view changes.
- Full XLSX styles, shared formulas, conditional formatting, validation, tables, drawings, charts, macros and unknown-part preservation.
- Complete Excel-compatible function surface and dynamic arrays.
- AutoFilter/filter UI and advanced sort.
- Printing, page layout, preview and PDF export.
- Charts, pivot, slicers, collaboration and macro/query engine.
- Production Skia GPU/MAUI control.

## Validation policy

- `NeraSpreadSheet.Core.slnx` must restore, build and test on cross-platform CI.
- `NeraSpreadSheet.slnx` must restore/build on Windows and all tests must pass.
- Architecture verification must remain green.
- Windows runtime smoke is mandatory; compile-only desktop implementations are not accepted.
- Axis reorder requires permutation/model, formula identity, transaction/rollback/undo and shared geometry tests.
- Unsplit header reorder requires public-controller lifecycle, preview, commit/undo and WPF D3DImage runtime coverage.
- Header edge auto-scroll requires platform-neutral velocity tests plus runtime verification that only the targeted control/pane moves.
- PR #1 remains Draft and must not merge while exact-head CI is red or unknown.

## Latest validated implementation milestone

CI run #338 passed at exact head `d498d6ed7c9eab04fd2a0d8edc6ceae9f62e59b9`:

- Core restore/build/tests and architecture verification passed.
- Full Windows restore/build/test passed.
- Mandatory Windows desktop GPU/runtime smoke passed.
- Existing split row/column reorder, formula identity, rollback and undo/redo gates remained green.
- Public unsplit WinForms/WPF reorder controllers passed commit, selection, undo and post-move D3DImage coverage.
- Shared auto-scroll boundary/velocity tests passed.
- Unsplit WinForms/WPF edge auto-scroll runtime smoke passed.
- Split WinForms/WPF auto-scroll runtime smoke verified that only the source/target pane moved.

## Independence rule

Excel, LibreOffice and DevExpress may be used only as external behavior/coverage references. Their runtime engines, command IDs and public types are not NeraSpreadSheet dependencies.
