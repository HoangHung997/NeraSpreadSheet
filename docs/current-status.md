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
- Multiple worksheets, versioned snapshots, values, formulas, style IDs, row/column dimensions and native merged ranges.
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
- The destination is an insertion boundary expressed in original axis coordinates.
- Dropping inside the source interval or at either adjacent boundary is a no-op and creates no history entry.
- Sparse cells, row-height/column-width overrides and merged ranges move without materializing the logical axis.
- Formulas on the moved worksheet and formulas on other worksheets are rewritten so references follow the same logical cells.
- Absolute markers, quoted/escaped sheet names and string-literal exclusion are preserved.
- A formula range whose image would become discontiguous is rejected before mutation rather than silently changing meaning.
- A merged range that would split, reverse anchor order or cross an active freeze boundary is rejected before mutation.
- Active cell, anchor and whole-axis/multi-range selection map through the same permutation.
- Every split-pane offset is mapped by preserving the identity of its top-left row/column plus the fractional local pixel offset, using exact sparse metrics before and after the move.
- `SpreadsheetAxisReorderController` owns atomic preflight, execution, rollback, full-workbook recalculation and exact undo/redo.
- Programmatic entry points are `SpreadsheetSession.Reorder.MoveRows`, `MoveColumns` and `Move`.

### Continuous viewport, freeze, split panes and cache

- Sparse row/column metric index and fractional pixel scrolling without row/column snapping.
- Pixel hit testing, content extent and merged-anchor resolution.
- Worksheet snapshot cache and bounded translated viewport tile cache.
- Freeze panes compose through frozen corner, frozen rows, frozen columns and scrolling body.
- Platform-neutral one-pane, vertical, horizontal and four-pane topology.
- Every pane owns an independent `ContinuousScrollController` with `double` X/Y offsets and bounds.
- Hidden panes retain state; unavailable active panes fall back to `TopLeft`.
- Pane-local hit testing and common-coordinate cell bounds.
- Shared split chrome renders headers, selection, separator continuation, active pane and integrated pane scrollbars.
- `SpreadsheetSplitViewState` stores topology, split X/Y, active pane and all four pane offsets per worksheet.
- Structural insert/delete and axis reorder participate in split-state transactions and exact undo/redo.
- Nested display lists retain immutable child references instead of flatten-copying command arrays.

### Per-pane scrollbars and split-aware dirty regions

- Shared pane-local scrollbar geometry supports buttons, track, proportional thumb, continuous offset, line/page input and targeted pane/axis routing.
- Integrated scrollbars are controlled by `SpreadsheetRenderTheme.ShowSplitPaneScrollBars`.
- Public optional WinForms/WPF scrollbar overlay controllers expose lifecycle, style, layout, hit testing and refresh.
- Scrollbar changes persist through `SpreadsheetSplitViewState`.
- Changed ranges project into every visible pane, expand through merged cells and split at freeze boundaries.
- WinForms GDI+ and Direct2D HWND use partial invalidation; DXGI `FlipDiscard` uses explicit full-frame fallback.
- WPF D3DImage presents multiple dirty rectangles; DrawingContext uses full visual invalidation.

### Native header drag reordering in public split hosts

- `SpreadsheetSplitHeaderReorderGeometry` is shared by WinForms and WPF.
- Row sources come from left-edge panes; column sources come from top-edge panes.
- Pane scrollbars, split separators and dimension resize handles take priority over reorder.
- A shared movement threshold separates click-selection from drag-reorder.
- A selected contiguous whole-row/whole-column range moves as one block; otherwise one row/column moves.
- Drop position uses the nearest slot half and produces an original-coordinate destination boundary.
- Shared preview geometry spans the perpendicular control extent and distinguishes valid/no-op drops.
- WinForms reads `MK_LBUTTON` from the actual Windows message, uses pointer capture and renders preview through the shared display list.
- WPF production routed handlers use preview input, optional mouse capture and a lightweight `DrawingVisual` preview over both DrawingContext and D3DImage.
- WPF does not discard a valid transaction merely because capture is unavailable, and only attempts capture while the physical left button is pressed.
- Rejected operations leave the workbook unchanged and do not enter undo history.

### Public desktop hosts and GPU backends

- WinForms uses a Nera-owned child surface over the existing public control; WPF uses a Nera-owned `Adorner` under `AdornerLayer`/`AdornerDecorator`.
- Both expose split topology, active pane, targeted scrolling, hit testing, reusable editor, live dimension resizing, pane scrollbars and header reorder interaction.
- GDI+, WPF DrawingContext, Direct2D/DirectWrite HWND, D3D11/DXGI `FlipDiscard` and WPF shared-texture/D3DImage paths consume shared display-list semantics.
- Hardware preference, WARP fallback, bounded DirectWrite layout caching, recovery and frame diagnostics are implemented.
- Runtime tests cover repeated WPF unload/reload and explicit second-frame text-layout reuse.

### XLSX and desktop samples

- Basic values, formulas/cached values, multiple sheets, row heights, column widths and merged ranges.
- `NeraOpenXmlSpreadsheetSessionSerializer` round-trips per-worksheet split state.
- Standard SpreadsheetML pane metadata plus a Nera custom XML part preserve compatible and full four-pane state.
- WPF and WinForms samples expose split modes, pane-scrollbar visibility, diagnostics and XLSX session Open/Save.

## Implemented but intentionally conservative

- Header drag reordering is wired into public split hosts; unsplit public-control drag interaction is still pending.
- Drag-edge auto-scroll is not implemented.
- WPF CI uses a real loaded `Window`, public control/controller and the production drag state machine/preview/commit path. It invokes that state machine deterministically because global OS pointer injection is unreliable on hosted Windows runners; real routed mouse behavior remains implemented in the host source.
- Formula ranges that would become discontiguous and merged ranges that would split/reverse are rejected rather than converted into unions.
- Number formatting uses the current .NET bridge rather than a complete Excel format-code engine.
- Sort is in-memory and uses a materialization safety limit.
- Sparse whole-axis style storage is not implemented.
- Structural/formula rewriting covers A1 cell/range syntax, not tables, structured references, shared formulas or dynamic arrays.
- Structural, metric, topology, theme and device-lifecycle changes remain conservative full invalidations.
- Sustained FPS/input-latency/power behavior still requires target-hardware benchmarks.

## Next implementation work

1. Bring the same header drag-reorder interaction to unsplit public WPF/WinForms controls and add drag-edge auto-scroll.
2. Add sparse whole-axis style storage.
3. Add standalone undo/redo commands for direct split-view changes.
4. Add longer-running injected device-loss/front-buffer-loss stress coverage.
5. Implement production Skia GPU plus MAUI native handler/touch interaction.
6. Expand XLSX styles, shared formulas, conditional formatting, validation, tables, drawings and unknown-part preservation.

## Not implemented yet

- Unsplit-control header drag UI and drag-edge auto-scroll.
- Standalone undo/redo commands for direct split-view changes.
- Sparse whole-axis styles.
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
- WinForms reorder smoke must use the actual surface message path and verify row and column movement.
- WPF reorder smoke must use a loaded public split host, production state machine, preview lifecycle, commit/undo and post-move D3DImage rendering.
- PR #1 remains Draft and must not merge while exact-head CI is red or unknown.

## Latest validated implementation milestone

CI run #322 passed at implementation commit `16e034189328385b16e7c6b567c4b4b2a094c974`:

- Core restore/build/tests and architecture verification passed.
- Full Windows restore/build/test passed.
- Mandatory Windows desktop GPU/runtime smoke passed.
- Fixed-length row/column permutation, sparse cell/dimension mapping and merged-range safety tests passed.
- Local/cross-sheet formula identity rewriting and discontiguous-range rejection tests passed.
- Transaction tests passed for selection, split offsets, exact undo/redo, rollback and recalculation.
- Shared header source/drop/threshold/preview geometry tests passed.
- WinForms real-message row and column drag smoke passed, including formula identity and undo.
- WPF loaded-window state-machine smoke passed, including preview lifecycle, mapped selection, commit/undo and post-move D3DImage presentation.

## Independence rule

Excel, LibreOffice and DevExpress may be used only as external behavior/coverage references. Their runtime engines, command IDs and public types are not NeraSpreadSheet dependencies.
