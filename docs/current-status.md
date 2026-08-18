# NeraSpreadSheet current implementation status

This file is the handoff source of truth for the current development branch. It distinguishes executable behavior from planned architecture. A feature is listed as implemented only when source, tests and the applicable runtime gate exist.

## Product rules

NeraSpreadSheet is an independent spreadsheet SDK.

- No runtime dependency on Microsoft Excel, LibreOffice or DevExpress.
- No UNO/Excel command identifiers in Nera public contracts.
- No UI element per cell.
- Workbook, formula, layout, scrolling and command projects remain independent from WPF, WinForms and MAUI.
- Viewports use continuous `double` offsets and may stop between row/column boundaries.

## Implemented

### Core workbook model

- Sparse worksheet storage over an Excel-size logical address space.
- Multiple worksheets with add, remove and rename operations.
- Cell values, formulas, style IDs, row/column dimensions and versioned snapshots.
- Workbook-owned immutable style interning.
- Native merged ranges with overlap protection.
- Structural insert/delete for complete logical row and column axes.
- Structural preflight prevents cells, dimension overrides or merged ranges from moving outside worksheet limits.
- Structural snapshots restore cells, dimensions and merged ranges for undo/redo.

### Formula and calculation engine

- Tokenizer, parser and AST.
- Arithmetic, comparison, concatenation, references, ranges and basic cross-sheet references.
- `SUM`, `AVERAGE`, `MIN`, `MAX`, `COUNT` and `IF`.
- Dependency graph, circular-reference detection and affected-only recalculation.
- Spreadsheet error literals such as `#REF!` remain `CellValueKind.Error`, not text.
- Structural reference rewriting supports local/cross-sheet references, absolute markers, reversed ranges, quoted/escaped sheet names and string-literal exclusion.
- Insert expands or shifts affected references/ranges.
- Delete shrinks partial ranges and emits standalone `=#REF!` when a referenced cell/range is removed.
- Recalculation state is rebuilt after structural execute, undo and redo.

### Editing, commands and session ownership

- Session-owned selection, history, calculation, clipboard, style, merge, sort, editor, view and structure controllers.
- Single, extended, multi-range, whole-row, whole-column and whole-sheet selection.
- Undo/redo for cell edits, paste, formatting, merge/unmerge, sort and structural operations.
- Native Nera command registry/dispatcher.
- Clear, recalculate, copy/cut/paste, bold/italic, merge/unmerge, sort and row/column insert/delete commands.
- Native clipboard package plus TSV/quoted-text interoperability fallback.
- Relative/absolute A1 reference translation during paste.
- One reusable in-cell editor per desktop host.
- Per-worksheet freeze-pane state with native freeze/unfreeze commands.
- Merge/freeze safety prevents a merge from crossing an active freeze boundary and prevents a freeze boundary from splitting a merge.
- Structural operations map selection and freeze boundaries, restore exact snapshots on undo and roll back atomically if a later phase fails.

### Continuous viewport, freeze panes and cache

- Sparse row/column metric index.
- Fractional pixel scrolling without row/column snapping.
- Pixel hit testing, content extent and merged-anchor resolution.
- Worksheet snapshot cache keyed by worksheet/dimension versions.
- Bounded translated viewport tile cache using 256-pixel scroll tiles.
- Freeze panes compose through four clipped regions: frozen corner, frozen rows, frozen columns and scrolling body.
- Pane-aware freeze cache replays one cached tile-origin body with axis-specific translation.
- Freeze separators are appended after cached replay so they never inherit tile translation.
- Display-list nesting stores immutable child references rather than flatten-copying command arrays.
- GDI+, WPF and Direct2D executors share clip/translation semantics and recursively traverse nested display lists.
- Allocation regression tests and BenchmarkDotNet coverage exist for normal and frozen scrolling caches.

### Split-pane foundation

- Platform-neutral split topology supports one pane, vertical split, horizontal split and four panes.
- Split coordinates, separator thickness and minimum pane extent are validated/clamped.
- Hit testing uses half-open bounds and distinguishes pane, vertical separator, horizontal separator and separator intersection.
- Each pane owns an independent `ContinuousScrollController` with `double` X/Y offsets and pane-specific bounds.
- Precision, wheel, touch and programmatic deltas can target one pane without changing the others.
- Hidden panes retain their scroll state; topology restoration reuses and re-clamps that state.
- Active pane falls back to `TopLeft` when a topology no longer contains the previous pane.
- Pane-local hit testing resolves merged anchors.
- Cell bounds are translated back into common body coordinates.
- A shared split-aware chrome compositor renders headers and separator continuation through header bands.
- Top-edge panes provide column headers; left-edge panes provide row headers.
- Split chrome rejects missing, duplicate or mismatched pane metadata.

### Per-worksheet split view state

- `SpreadsheetSplitViewState` stores topology, split X/Y coordinates, active pane and all four pane scroll offsets.
- State is owned by `SpreadsheetViewController` and stored independently for each worksheet.
- Hidden-pane offsets remain stored while their topology is absent.
- Source-tagged split change events prevent WinForms/WPF hosts from feeding their own state changes back recursively.
- The outgoing worksheet state is captured before `ActiveWorksheet` changes, and the incoming worksheet state is restored afterward.
- Disabling and re-enabling a public split overlay restores the worksheet's previous split state rather than replacing it with defaults.
- Direct view changes are not yet standalone undo-history commands; structural operations do include split-state snapshots in their undo/redo transaction.

### Structural mapping of split state

- Row insertion/deletion maps every pane's Y offset using the exact pre-mutation row metrics.
- Column insertion/deletion maps every pane's X offset using the exact pre-mutation column metrics.
- Insertion shifts offsets at or beyond the inserted interval by the inserted physical extent.
- Deletion collapses offsets inside the removed interval to its leading edge and subtracts the exact removed extent from later offsets.
- The unaffected axis, split topology, split coordinates and active pane remain unchanged.
- Structural undo/redo restores the exact prior/mapped split state.
- Failed structural preflight or rollback leaves split state unchanged and does not enter undo history.

### Public WinForms split panes

- Split panes are enabled on an existing public `NeraSpreadsheetControl` through `EnableSplitPanes` and disabled through `DisableSplitPanes`.
- Public controller exposes split mode/coordinates, active pane, per-pane scroll state, targeted scroll input, hit testing and diagnostics.
- Existing single-pane control remains unchanged underneath; split mode uses a Nera-owned child surface that shares the same `SpreadsheetSession` and render contracts.
- Vertical/horizontal separators can be dragged, including the four-pane intersection.
- Mouse wheel and Shift+wheel target the pane under the pointer.
- Body, row-header and column-header interaction activate and select through the correct pane.
- One reusable editor is positioned/clipped inside the active split pane and its freeze subregion.
- Split-aware row-height and column-width resize handles work through the public child surface.
- Row resize handles are supplied by left-edge panes; column resize handles are supplied by top-edge panes.
- Split separator hit regions take priority over dimension resize handles.
- Live dimension dragging updates shared sparse worksheet metrics, so every pane reflects the new size immediately.
- GDI+, Direct2D HWND and D3D11/DXGI `FlipDiscard` paths render the same split display list.
- `RenderNow` explicitly performs layout, creates the child handle and invokes the selected renderer; it does not depend on nondeterministic WM_PAINT scheduling.
- Real off-screen STA WinForms smoke tests cover four-pane render, fractional per-pane scroll, all three backends, hit testing, lifecycle and actual mouse-message row/column resizing.

### Public WPF split panes

- Split panes are enabled/disabled on the existing public WPF `NeraSpreadsheetControl` through extension APIs matching the WinForms lifecycle.
- A Nera-owned `Adorner` overlays the existing single-pane control; the host must provide an `AdornerLayer` (normally through `AdornerDecorator`).
- Public controller exposes session/backend forwarding, split topology/coordinates, active pane, per-pane scroll, hit testing and GPU diagnostics.
- The adorner routes wheel, Shift+wheel, body selection, whole-axis header selection, keyboard shortcuts and text editing through the active pane.
- Vertical/horizontal split separators are draggable.
- One reusable WPF `TextBox` editor is arranged and clipped within the active pane/freeze subregion.
- Split-aware row-height and column-width resize geometry is shared with WinForms and updates the same sparse worksheet dimensions.
- DrawingContext and Nera-owned D3D11 shared-texture/D3DImage backends consume the same split display list.
- Real off-screen STA WPF smoke tests cover four-pane render in both backends, DirectWrite layout reuse, hit testing, lifecycle, host resize application and post-resize D3DImage rendering.

### Desktop rendering backends

- WPF DrawingContext fallback.
- WinForms GDI+ fallback.
- Direct2D/DirectWrite HWND renderer.
- D3D11 + DXGI two-buffer `FlipDiscard` swap-chain renderer with optional VSync.
- Hardware adapter preference with hardware/default and Microsoft WARP fallback.
- Nera-owned WPF D3D11 shared texture, D3D9Ex bridge and D3DImage lifecycle; no child-HWND airspace.
- Shared Direct2D display-list executor, brush/text-format caches and bounded `IDWriteTextLayout` LRU.
- One-shot renderer/device recovery and frame-pacing diagnostics.
- Existing Windows runtime tests cover Direct2D HWND, DXGI swap chain and the public WPF shared-texture control independently from split-control tests.

### Spreadsheet headers and desktop interaction

- Shared row/column headers and top-left select-all corner.
- Labels use A..Z, AA.. and one-based row numbers.
- Freeze-aware header clips preserve fractional movement for scrolling headers and fixed geometry for frozen headers.
- Active/whole-axis selections receive header highlighting.
- Single-pane and split-pane WPF/WinForms paths support live row-height/column-width resizing.
- Header drag reordering is not implemented.

### XLSX adapter and split view metadata

- Basic cell values and formulas/cached values.
- Multiple worksheets.
- Row heights, column widths and merged ranges.
- `NeraOpenXmlSpreadsheetSessionSerializer` round-trips per-worksheet split state.
- Standard SpreadsheetML `SheetView/Pane` metadata is written for compatible split topology, coordinates, active pane and top-left-cell behavior.
- A Nera custom XML part preserves the full four-pane scroll state that standard SpreadsheetML cannot represent exactly.
- If native metadata is absent, compatible standard split-pane metadata is imported into a Nera split state.
- A default unsplit session emits neither native split metadata nor a standard split pane.
- Unknown-part preservation is explicitly unsupported rather than silently claimed.

### Samples

- `samples/NeraSpreadSheet.Wpf.Sample`
- `samples/NeraSpreadSheet.WinForms.Sample`

The samples exercise formulas, style interning, merged cells, editing, XLSX open/save, backend switching, FPS diagnostics, freeze panes and structural commands. Public split controllers are runtime-tested; sample toolbar exposure for split modes is still planned.

## Implemented but intentionally basic

- Number formatting uses the current .NET formatting bridge, not a complete Excel format-code engine.
- Sort is in-memory, rejects merged ranges and uses a materialization safety limit.
- Full-row/full-column operations remain subject to existing materialization limits; sparse whole-axis style storage is not implemented.
- Structural rewriting currently covers A1 cell/range syntax, not tables, structured references, shared formulas or dynamic arrays.
- Public split hosts conservatively invalidate/recompose the overlay after workbook/dimension changes; split-aware dirty-region optimization is not implemented.
- Direct split topology/scroll changes are view state, not standalone undo/redo commands.
- Per-pane scrollbars are not implemented; wheel, precision and programmatic scrolling are implemented.
- Runtime smoke validates initialization, render, resize, cache reuse and shutdown. Sustained FPS/input-latency/power behavior still requires target-hardware benchmarks.

## Next implementation work

1. Add split-aware dirty-region projection and partial invalidation for cell/range changes.
2. Add optional per-pane scrollbars and expose split controls in both desktop samples.
3. Add header drag reordering and sparse whole-axis style storage.
4. Add longer-running injected device-loss/front-buffer-loss stress coverage.
5. Implement Skia GPU surface plus MAUI native handler/touch interaction.
6. Expand XLSX styles, shared formulas, conditional formatting, validation, tables, drawings and unknown-part preservation.

## Not implemented yet

- Split-aware dirty-region optimization.
- Per-pane scrollbars and header drag reordering.
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
- Windows runtime smoke is mandatory; compile-only GPU/split implementations are not accepted.
- Public WinForms split smoke must render GDI+, Direct2D HWND and DXGI.
- Public WPF split smoke must render DrawingContext and D3DImage and verify DirectWrite layout reuse.
- Split-aware resize must have shared geometry tests plus public-host runtime smoke.
- PR #1 remains Draft and must not merge while latest-head CI is red or unknown.

## Latest validated implementation milestone

CI run #230 passed at implementation commit `0862a9c297f0024c40563cd0f3ae40b2f5c32d9c`:

- Core restore/build/tests and architecture verification passed on Ubuntu.
- Full Windows restore/build/test passed.
- Mandatory Windows desktop GPU runtime smoke passed.
- Per-worksheet split persistence, structural mapping and XLSX split-state round-trip tests passed.
- Shared split-header resize geometry tests passed.
- Public WinForms split resize smoke changed real row/column dimensions through mouse messages.
- Public WPF split resize smoke changed dimensions and rendered the resized result through D3DImage.

## Independence rule

Excel, LibreOffice and DevExpress may be used only as external behavior/coverage references. Their runtime engines, command IDs and public types are not NeraSpreadSheet dependencies.
