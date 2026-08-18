# NeraSpreadSheet current implementation status

This file is the handoff source of truth for the current development branch. A capability is listed as implemented only when executable source, automated tests and the applicable desktop/runtime gate exist.

## Product rules

NeraSpreadSheet is an independent spreadsheet SDK.

- No runtime dependency on Microsoft Excel, LibreOffice or DevExpress.
- No UNO/Excel command identifiers in Nera public contracts.
- No UI control per cell.
- Workbook, formula, editing, layout, scrolling and command projects remain independent from WPF, WinForms and MAUI.
- Viewports use continuous `double` pixel offsets and may stop between row/column boundaries.
- WPF, WinForms and GPU/fallback hosts consume shared viewport/display-list semantics rather than implementing separate workbook engines.

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
- The destination is an insertion boundary expressed in the original axis coordinate system.
- Dropping inside the source interval or at either adjacent boundary is a no-op and does not enter history.
- Sparse cells, row-height/column-width overrides and merged ranges are transformed without inserting, deleting or materializing the logical axis.
- Cell identity is preserved: formulas on the moved worksheet and formulas on other worksheets are rewritten so references continue following the same logical cells.
- Absolute markers, quoted/escaped sheet names and string-literal exclusion are preserved.
- A formula range whose image would become discontiguous is rejected before mutation rather than silently changing meaning.
- A merged range that would split, reverse its anchor order or cross an active freeze boundary is rejected before mutation.
- Whole-row/whole-column selections and the active/anchor cells are mapped through the same permutation.
- Every split pane offset is mapped by preserving the identity of the top-left axis item plus its fractional local pixel offset, using exact sparse metrics before and after the move.
- `SpreadsheetAxisReorderController` owns atomic preflight, execution, rollback, full-workbook recalculation and exact undo/redo.
- Public programmatic entry points are `SpreadsheetSession.Reorder.MoveRows`, `MoveColumns` and `Move`.

### Continuous viewport, freeze and cache

- Sparse row/column metric index and fractional pixel scrolling without row/column snapping.
- Pixel hit testing, content extent and merged-anchor resolution.
- Worksheet snapshot cache and bounded translated viewport tile cache.
- Freeze panes compose through frozen corner, frozen rows, frozen columns and scrolling body.
- Pane-aware freeze cache replays a shared tile origin with axis-specific translation.
- Nested display lists retain immutable child references rather than flatten-copying command arrays.
- GDI+, WPF and Direct2D executors share clip/translation semantics.
- Allocation regression tests and BenchmarkDotNet coverage exist for normal and frozen scrolling.

### Split-pane foundation and per-worksheet state

- Platform-neutral one-pane, vertical, horizontal and four-pane topology.
- Validated/clamped split coordinates, separator thickness and minimum pane extent.
- Half-open pane/separator hit regions, including separator intersection.
- Every pane owns an independent `ContinuousScrollController` with `double` X/Y offsets and bounds.
- Precision, wheel, touch and programmatic input can target one pane without moving the others.
- Hidden panes retain scroll state; an unavailable active pane falls back to `TopLeft`.
- Pane-local hit testing resolves merged anchors and returns common body-coordinate cell bounds.
- Shared split chrome renders headers, selection, separator continuation and active-pane state.
- `SpreadsheetSplitViewState` stores topology, split X/Y, active pane and all four pane offsets per worksheet.
- Structural insert/delete and reorder operations participate in split-state transactions and exact undo/redo.

### Per-pane split scrollbars

- Shared geometry creates horizontal/vertical bars for every visible pane whose content exceeds its viewport.
- Track, proportional thumb, maximum offset and hit geometry use pane-local bounds and continuous offsets.
- Hit testing distinguishes arrow/button, thumb, track-before and track-after behavior.
- Thumb drag maps pointer position to a continuous offset without row/column snapping.
- A request targets exactly one pane and one axis while preserving the other axis and every other pane.
- Integrated split-frame scrollbars are controlled by `SpreadsheetRenderTheme.ShowSplitPaneScrollBars` and render through the shared display list.
- Public optional WinForms and WPF overlay controllers expose enable/disable, visibility, style, layout, count, hit testing and refresh.
- Scrollbar changes persist through `SpreadsheetSplitViewState`.
- WinForms runtime smoke uses real Windows mouse messages; WPF runtime smoke uses native OS pointer input and routed mouse capture.

### Split header drag reordering

- `SpreadsheetSplitHeaderReorderGeometry` is shared by WinForms and WPF.
- Row sources are supplied only by left-edge panes; column sources are supplied only by top-edge panes.
- Resize-handle tolerance, split separators and pane scrollbars take priority over reorder initiation.
- A drag candidate becomes active only after a shared movement threshold.
- If the pointer starts inside one selected contiguous whole-axis range, the whole range moves; otherwise one row or column moves.
- Drop position is selected by the nearest slot half and represented as an original-coordinate destination boundary.
- Shared preview geometry spans the relevant full control axis; a no-op target uses neutral styling and a valid target uses active styling.
- WinForms consumes `MK_LBUTTON` from the actual Windows message, uses pointer capture and renders preview through the shared display-list composer.
- WPF uses preview routed input, mouse capture and a lightweight `DrawingVisual` preview above both DrawingContext and D3DImage content.
- Rejected operations leave the workbook unchanged and do not enter undo history.

### Split-aware dirty regions

- Changed ranges are projected into every visible pane.
- Projection expands across intersecting merged cells and splits at freeze-row/freeze-column boundaries.
- Each rectangle is clipped to the correct frozen or scrolling pane subregion.
- Missing frame data or unsafe projection requests conservative full invalidation.
- WinForms GDI+ and Direct2D HWND use partial invalidation; `FlipDiscard` intentionally falls back to a full frame.
- WPF D3DImage presents multiple dirty rectangles; DrawingContext intentionally falls back to full visual invalidation.

### Public desktop split hosts and GPU backends

- WinForms uses a Nera-owned child surface over the existing public control; WPF uses a Nera-owned `Adorner` under an `AdornerLayer`/`AdornerDecorator`.
- Both expose split topology, active pane, targeted scrolling, hit testing, reusable editor, live dimension resizing, pane scrollbars and native header reorder interaction.
- GDI+, WPF DrawingContext, Direct2D/DirectWrite HWND, D3D11/DXGI `FlipDiscard` and WPF shared-texture/D3DImage paths consume shared display-list semantics.
- Hardware adapter preference, WARP fallback, bounded DirectWrite layout caching, recovery and frame diagnostics are implemented.
- Runtime tests cover repeated WPF unload/reload and explicit second-frame text-layout reuse.

### XLSX and split view metadata

- Basic values, formulas/cached values, multiple sheets, row heights, column widths and merged ranges.
- `NeraOpenXmlSpreadsheetSessionSerializer` round-trips per-worksheet split state.
- Compatible topology, coordinates, active pane and top-left-cell behavior are written to standard SpreadsheetML `SheetView/Pane` metadata.
- A Nera custom XML part preserves the four independent pane offsets that standard SpreadsheetML cannot represent exactly.
- Compatible standard pane metadata is imported when native metadata is absent.
- Unknown-part preservation remains explicitly unsupported.

### Desktop samples

- `samples/NeraSpreadSheet.Wpf.Sample`
- `samples/NeraSpreadSheet.WinForms.Sample`

Both samples expose split modes, integrated pane-scrollbar visibility, diagnostics and XLSX session Open/Save. A sparse demonstration extent permits horizontal and vertical pane scrolling without materializing a dense sheet.

## Implemented but intentionally conservative

- Header drag reordering is currently wired into the public split hosts; the unsplit public-control interaction path is still separate and does not yet expose the same drag behavior.
- Drag auto-scroll at the viewport edge is not implemented.
- Formula ranges that would become discontiguous and merged ranges that would split/reverse are rejected instead of being converted into unions or rewritten into a more complex expression.
- Number formatting uses the current .NET bridge rather than a complete Excel format-code engine.
- Sort is in-memory, rejects merged ranges and uses a materialization safety limit.
- Sparse whole-axis style storage is not implemented.
- Structural/formula rewriting covers A1 cell/range syntax, not tables, structured references, shared formulas or dynamic arrays.
- Structural, metric, topology, theme and device-lifecycle changes remain conservative full invalidations.
- Sustained FPS/input-latency/power behavior still requires target-hardware benchmarks.

## Next implementation work

1. Bring the same header drag-reorder interaction to the unsplit public WPF and WinForms controls and add edge auto-scroll.
2. Add sparse whole-axis style storage so whole-row/whole-column formatting does not materialize every cell.
3. Add standalone undo/redo commands for direct split-view changes.
4. Add longer-running injected device-loss/front-buffer-loss stress coverage.
5. Implement a production Skia GPU surface plus MAUI native handler/touch interaction.
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

- `NeraSpreadSheet.Core.slnx` must restore, build and test on the cross-platform CI job.
- `NeraSpreadSheet.slnx` must restore/build on Windows and all tests must pass.
- Architecture verification must remain green.
- Windows runtime smoke is mandatory; compile-only desktop implementations are not accepted.
- Axis reorder requires permutation/model tests, formula identity tests, transaction/rollback/undo tests and shared drag/drop geometry tests.
- Public WinForms reorder smoke must use the actual surface message path and verify row and column movement.
- Public WPF reorder smoke must use native OS pointer input, routed hit testing/mouse capture and post-move D3DImage rendering.
- PR #1 remains Draft and must not merge while latest-head CI is red or unknown.

## Latest validated implementation milestone

CI run #311 passed at implementation commit `44a5f37368dcf41dd89f5c33ba05bb15108d54dc`:

- Core restore/build/tests and architecture verification passed on Ubuntu.
- Full Windows restore/build/test passed.
- Mandatory Windows desktop GPU/runtime smoke passed.
- Fixed-length row/column permutation, sparse cell/dimension mapping and merged-range safety tests passed.
- Local/cross-sheet formula identity rewriting and discontiguous-range rejection tests passed.
- Session transaction tests passed for selection, split offsets, exact undo/redo, rollback and recalculation.
- Shared header source/drop/preview geometry tests passed.
- WinForms real-message row and column drag smoke passed, including formula identity and undo.
- WPF native-pointer row drag smoke passed and rendered the moved workbook through D3DImage.

## Independence rule

Excel, LibreOffice and DevExpress may be used only as external behavior/coverage references. Their runtime engines, command IDs and public types are not NeraSpreadSheet dependencies.
