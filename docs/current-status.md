# NeraSpreadSheet current implementation status

This file is the handoff source of truth for the current development branch. It distinguishes executable behavior from planned architecture. A feature is only listed as implemented when source, tests and the applicable runtime gate exist.

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
- Pixel hit-testing, content extent and merged-anchor resolution.
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

### Public WinForms split panes

- Split panes are enabled on an existing public `NeraSpreadsheetControl` through `EnableSplitPanes` and disabled through `DisableSplitPanes`.
- Public controller exposes split mode/coordinates, active pane, per-pane scroll state, targeted scroll input, hit testing and diagnostics.
- Existing single-pane control remains unchanged underneath; split mode uses a Nera-owned child surface that shares the same `SpreadsheetSession` and render contracts.
- Vertical/horizontal separators can be dragged, including the four-pane intersection.
- Mouse wheel and Shift+wheel target the pane under the pointer.
- Body, row-header and column-header interaction activate and select through the correct pane.
- One reusable editor is positioned/clipped inside the active split pane and its freeze subregion.
- GDI+, Direct2D HWND and D3D11/DXGI `FlipDiscard` paths render the same split display list.
- `RenderNow` explicitly performs layout, creates the child handle and invokes the selected renderer; it does not depend on nondeterministic WM_PAINT scheduling.
- A real off-screen STA WinForms runtime smoke creates the public control, enables four panes, applies fractional per-pane scroll, renders all three backends, resizes, hit-tests and shuts down cleanly.

### Public WPF split panes

- Split panes are enabled/disabled on the existing public WPF `NeraSpreadsheetControl` through extension APIs matching the WinForms lifecycle.
- A Nera-owned `Adorner` overlays the existing single-pane control; the host must provide an `AdornerLayer` (normally through `AdornerDecorator`).
- Public controller exposes session/backend forwarding, split topology/coordinates, active pane, per-pane scroll, hit testing and GPU diagnostics.
- The adorner routes wheel, Shift+wheel, body selection, whole-axis header selection, keyboard shortcuts and text editing through the active pane.
- Vertical/horizontal split separators are draggable.
- One reusable WPF `TextBox` editor is arranged and clipped within the active pane/freeze subregion.
- DrawingContext and Nera-owned D3D11 shared-texture/D3DImage backends consume the same split display list.
- A real off-screen STA WPF runtime smoke hosts the public control inside an `AdornerDecorator`, renders four panes in both backends, verifies DirectWrite layout reuse, resizes, hit-tests and closes cleanly.

### Desktop rendering backends

- WPF DrawingContext fallback.
- WinForms GDI+ fallback.
- Direct2D/DirectWrite HWND renderer.
- D3D11 + DXGI two-buffer `FlipDiscard` swap-chain renderer with optional VSync.
- Hardware adapter preference with hardware/default and Microsoft WARP fallback.
- Nera-owned WPF D3D11 shared texture, D3D9Ex bridge and D3DImage lifecycle; no child-HWND airspace.
- Shared Direct2D display-list executor, brush/text-format caches and bounded `IDWriteTextLayout` LRU.
- One-shot renderer/device recovery and frame-pacing diagnostics.
- Existing Windows runtime tests cover Direct2D HWND, DXGI swap chain and the public WPF shared-texture control independently from the split-control tests.

### Spreadsheet headers and desktop interaction

- Shared row/column headers and top-left select-all corner.
- Labels use A..Z, AA.. and one-based row numbers.
- Freeze-aware header clips preserve fractional movement for scrolling headers and fixed geometry for frozen headers.
- Active/whole-axis selections receive header highlighting.
- Single-pane WPF and WinForms controls support live row-height/column-width drag resizing.
- Split-pane public controls currently support header selection; split-aware dimension resize handles remain a separate follow-up.

### XLSX adapter

- Basic cell values and formulas/cached values.
- Multiple worksheets.
- Row heights, column widths and merged ranges.
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
- Split panes currently invalidate/recompose at control level after workbook/dimension changes; split-aware dirty-region optimization is not yet implemented.
- Split state and pane scroll offsets live in the public split controller, not yet in worksheet/view persistence or undo history.
- Per-pane scrollbars are not implemented; wheel/precision/programmatic scrolling is implemented.
- Split-aware row/column resize handles are not implemented; separator dragging and header selection are implemented.
- Runtime smoke validates initialization, render, resize, cache reuse and shutdown. Sustained FPS/input-latency/power behavior still requires target-hardware benchmarks.

## Next implementation work

1. Persist split topology, coordinates, active pane and pane scroll snapshots per worksheet.
2. Define undo/redo and structural row/column mapping semantics for persisted split state.
3. Add split-aware row/column resize handles and dirty-region invalidation.
4. Add optional per-pane scrollbars and expose split controls in both desktop samples.
5. Add longer-running injected device-loss/front-buffer-loss stress coverage.
6. Implement Skia GPU surface plus MAUI native handler/touch interaction.

## Not implemented yet

- Split-state workbook/XLSX persistence, undo/redo and structural mapping.
- Per-pane scrollbars and split-aware header resizing/reordering.
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
- PR #1 remains Draft and must not merge while latest-head CI is red or unknown.

## Latest validated implementation milestone

CI run #196 passed at implementation commit `2be6a45451628f600fe1647e2bc47b9e55901f99`:

- Core restore/build/tests and architecture verification passed on Ubuntu.
- Full Windows restore/build/test passed with zero blocking diagnostics.
- Mandatory Windows desktop GPU runtime smoke passed.
- Existing Direct2D HWND, DXGI and WPF shared-texture tests passed.
- Public WinForms split smoke passed across GDI+, Direct2D HWND and DXGI.
- Public WPF split smoke passed across DrawingContext and D3DImage, including DirectWrite cache reuse, resize, hit test and clean shutdown.

## Independence rule

Excel, LibreOffice and DevExpress may be used only as external behavior/coverage references. Their runtime engines, command IDs and public types are not NeraSpreadSheet dependencies.
