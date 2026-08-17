# NeraSpreadSheet current implementation status

This file is the handoff source of truth for the current development branch. It intentionally distinguishes implemented behavior from future architecture.

## Implemented

### Core and workbook
- Sparse worksheet storage over an Excel-size logical address space.
- Cell values, formulas, styles, worksheet dimensions and versioned snapshots.
- Multiple worksheets, rename/remove/add.
- Native merged-cell ranges with overlap protection.
- Workbook-owned immutable style interning (`StyleId`).
- Native structural row/column insert and delete over the full logical worksheet axes.
- Structural mutation preflights sparse cells, dimension overrides and merged ranges before committing, so overflow failures leave worksheet and dimension versions/state unchanged.
- Structural snapshots restore cells, dimensions and merged ranges for undo/redo.

### Formula and recalculation
- Tokenizer, parser and AST.
- Arithmetic, comparison, concatenation, references and ranges.
- Basic cross-sheet references.
- SUM, AVERAGE, MIN, MAX, COUNT and IF.
- Dependency graph, circular-reference detection and affected-only recalculation.
- Spreadsheet error literals such as `#REF!` are tokenized, parsed and evaluated as `CellValueKind.Error`, not plain text.
- Structural reference rewriting for local and cross-sheet formulas, including absolute markers, reversed ranges, quoted/escaped sheet qualifiers and string-literal exclusion.
- Insert expands or shifts references/ranges; delete shrinks partially intersected ranges and emits a standalone `#REF!` when the referenced cell/range is removed.
- A deleted qualified reference is normalized to `=#REF!`; invalid forms such as `=Sheet1!#REF!` are not retained.
- Workbook recalculation is rebuilt after structural operations and their undo/redo transitions.

### Editing, commands and view state
- Session-owned selection, history, calculation, clipboard, style, merge, sort, editor, view and structure controllers.
- Undo/redo for cell edits, paste, formatting, merge/unmerge, sort and structural row/column operations.
- Native Nera command IDs; no UNO/Excel command identifiers.
- Clear contents, recalculate, copy/cut/paste, bold/italic, merge/unmerge, sort ascending/descending and row/column insert/delete.
- Structural commands use whole-row/whole-column selection size when applicable and otherwise operate at the active cell.
- Relative/absolute formula translation for native paste.
- TSV/quoted-text clipboard import/export adapter independent of other spreadsheet products.
- Per-worksheet freeze-pane state with native `View.FreezePanes` / `View.UnfreezePanes` commands.
- Freeze boundaries reject merged ranges that would be split; new merges are also rejected when they would cross an active freeze boundary.
- Structural edits map selection active/anchor/multi-range state and freeze boundaries; undo restores their exact snapshots.
- Failed structural inserts do not enter undo history and do not mutate workbook formulas, selection or freeze state.
- Structural operations roll back their captured worksheet/formula/selection/freeze state if a later phase throws after worksheet mutation.

### Selection, viewport and split-pane foundation
- Single, extended and multi-range selection.
- Native whole-row, whole-column and whole-sheet selection primitives.
- Selection snapshot restore with change suppression when the target snapshot already matches current state.
- Shift-extension for row/column header selection preserves the original axis anchor; Ctrl can add whole-axis ranges to a multi-range selection.
- Fractional pixel scrolling with `double` offsets; no row/column snapping.
- Sparse row/column metric index.
- Pixel hit-testing and content extent.
- Freeze-aware row-only and column-only hit testing shared by desktop header UI.
- Merged-cell hit-test and editor bounds resolve to the merged region/top-left cell.
- Worksheet snapshot cache keyed by worksheet/dimension versions so pure scroll frames do not recopy all sparse cells.
- Translated viewport tile cache: 256-pixel scroll tiles, bounded LRU entries and double-precision translation inside a tile.
- Pane-aware freeze caching reuses one cached tile-origin body and reprojects it through four clips: frozen corner, frozen rows (X translation only), frozen columns (Y translation only) and scrolling body (XY translation).
- Freeze separator lines are appended fresh after cached pane replay so they never inherit tile translation.
- Cache identity includes frozen-row/frozen-column configuration, worksheet/dimension/selection versions, viewport geometry and render theme.
- Display-list nesting is reference based: `Append`/`DrawDisplayList` store a single immutable child-list reference instead of flatten-copying command arrays.
- GDI+, WPF and Direct2D executors recursively traverse nested display lists while preserving one shared clip/translation stack; Direct2D keeps a single BeginDraw/EndDraw frame.
- Allocation regression tests verify both normal cached scrolling and frozen pane cached scrolling allocate less than fresh composition.
- BenchmarkDotNet coverage exists for normal viewport caching and frozen pane-aware caching.
- Freeze panes preserve fractional scroll in the scrollable body while frozen rows/columns remain fixed.
- Frozen hit-testing, cell/editor bounds and dirty-region calculations use pane-aware coordinates.
- Platform-neutral split geometry supports unsplit, vertical split, horizontal split and four-pane layouts with configurable separator thickness and minimum pane extent.
- Split separators use half-open hit-test regions so separator/intersection hits never leak into an adjacent pane.
- Every visible split pane owns an independent `ContinuousScrollController`; X/Y offsets remain `double` and are clamped against that pane's own viewport bounds.
- Hidden pane scroll state is retained when a split is temporarily removed and is restored if the same topology returns.
- The active split pane falls back to `TopLeft` when a topology change removes the prior active pane.
- Split viewport composition clips/translates one viewport frame per pane, routes pane-specific hit testing and returns cell bounds in shared body coordinates.
- Split viewport tests cover independent fractional scrolling, targeted precision deltas, pane-local hit testing, translated cell bounds and topology fallback/persistence.

### Rendering and desktop hosts
- Shared display-list composition; visible cells only; no UI control per cell.
- Grid, text, selection, fill, font and border rendering.
- Merged cells render as one visual cell and suppress internal grid lines.
- Display-list clip and translation stacks shared by WPF, GDI+ and Direct2D executors.
- Frozen rendering is split into four independently clipped panes plus freeze separator lines.
- Shared spreadsheet chrome compositor draws row/column headers and the top-left select-all corner outside the body viewport.
- Header geometry is centralized and shared by WPF/WinForms; body coordinates remain local to the spreadsheet viewport.
- Single-pane and split-pane chrome now share the same internal header renderer for labels, selection highlighting, borders and freeze separators.
- Split-aware chrome takes column labels from panes touching the top edge and row labels from panes touching the left edge.
- Vertical and horizontal split separators continue through the corresponding column-header and row-header bands.
- Split chrome validates that every pane has exactly one matching viewport layout and rejects missing, duplicate or mismatched pane metadata.
- Column labels use the native A..Z, AA.. sequence and row labels use one-based row numbers.
- Header rendering uses freeze-aware `AxisSlot` geometry, so frozen headers remain fixed while scrolling headers move fractionally with the body.
- Active row/column headers and whole-axis selections receive distinct header highlighting.
- Desktop controls enable headers by default; theme settings can disable headers or customize header geometry/colors/font/strokes.
- Clicking the row header selects the entire logical row, clicking the column header selects the entire logical column, and clicking the corner selects the whole logical worksheet.
- Shared freeze-aware header resize geometry detects row/column boundaries from rendered `AxisSlot` edges, including fractional scroll and frozen panes.
- WinForms and WPF support live row-height/column-width dragging with resize cursors and pointer/mouse capture; dimensions remain sparse and existing dimension-change invalidation updates viewport/cache/editor geometry during the drag.
- WPF `DrawingContext` fallback display-list executor.
- WinForms GDI+ fallback display-list executor.
- Executable Windows Direct2D/DirectWrite HWND renderer using Vortice.
- Executable D3D11 + DXGI two-buffer `FlipDiscard` swap-chain backend feeding a Direct2D device context; `Present(1)`/VSync is the default.
- D3D11 adapter selection prefers a high-performance hardware adapter and falls back to hardware/default then Microsoft WARP.
- WinForms selectable rendering backend: `GdiPlus`, `Direct2D` or `Direct2DSwapChain`; GDI+ remains the conservative default.
- WPF selectable rendering backend: `DrawingContext` or Direct2D on a Nera-owned shared D3D11 texture/D3DImage bridge, avoiding child-HWND airspace.
- The WPF GPU host no longer depends at runtime on `Vortice.Wpf.DrawingSurface`; Nera owns the D3D11 device, D3D9Ex bridge, D3DImage back buffer, render subscription and unload/close lifecycle.
- WPF GPU cleanup is idempotent across both `Unloaded` and `Window.Closed`; reload can create a fresh device/surface without double-disposal.
- The Nera-owned WPF surface clears its dirty flag after a rendered frame, so `AlwaysRefresh=false` no longer causes continuous redundant rendering.
- Direct2D display-list execution and DirectWrite caches are shared by HWND, DXGI swap-chain and WPF GPU surfaces rather than duplicated.
- Direct2D brush and text-format caches.
- Bounded DirectWrite `IDWriteTextLayout` LRU cache with hit/miss/eviction diagnostics.
- Direct2D retained-content presentation and WinForms dirty-region repaint for cell changes; freeze-crossing dirty ranges deliberately fall back to full invalidation.
- One-shot Direct2D/DXGI resource recovery: native rendering failure recreates target-dependent resources/device stack and retries one frame; a second failure is surfaced.
- Renderer diagnostics expose surface size, text-layout cache metrics, adapter/feature-level/VSync data and recovery counts where applicable.
- Rolling frame-pacing diagnostics expose FPS, average/p95/max frame intervals.
- One reusable in-cell text editor overlay per host.
- Editor overlays are clipped to the correct frozen/scrollable pane and translated by header chrome so a partially obscured scrolling cell cannot paint over frozen panes or headers.
- F2, double-click and direct typing edit entry; Enter/Tab commit; Esc cancel.
- Desktop shortcuts include Ctrl+Z/Y/C/X/V/B/I.
- Both hosts subscribe to view changes, so freeze/unfreeze repaints immediately without application code manually invalidating the control.
- `tests/NeraSpreadSheet.Windows.Rendering.Tests` is a Windows-only runtime smoke project, not a compile-only descriptor test.
- CI creates real off-screen STA WinForms HWNDs for the Direct2D HWND and D3D11/DXGI flip-model renderers.
- CI also creates a real off-screen WPF `Window`, hosts the public `NeraSpreadsheetControl` with `Direct2DD3DImage`, waits for texture/text rendering, verifies DirectWrite cache reuse, resizes and closes the window.
- The three runtime tests cover nested display-list execution, text-layout reuse, native surface resize and clean shutdown/unload.
- The Windows CI job has a mandatory `Windows desktop GPU runtime smoke` step; a backend that only compiles but cannot initialize/render/present/resize/close causes CI failure.

### XLSX adapter
- Basic cell values and formulas/cached values.
- Multiple worksheets.
- Row heights and column widths.
- Merged-cell import/export.
- Unknown-part preservation is explicitly unsupported rather than silently claimed.

### Samples
- `samples/NeraSpreadSheet.Wpf.Sample`
- `samples/NeraSpreadSheet.WinForms.Sample`

Both samples exercise formulas, style interning, merged cells, in-cell editing and XLSX open/save. They expose rendering-backend switching, live FPS/p95 diagnostics, Freeze/Unfreeze controls and Insert/Delete Row/Column controls routed through native command dispatch. Row/column headers are enabled by default, so whole-row/whole-column/corner selection, drag-resizing and structural command selection-count behavior can be smoke-tested directly. The WinForms sample compares GDI+, HWND Direct2D and D3D11/DXGI flip-model; the WPF sample compares DrawingContext with the shared-texture Direct2D GPU path. Both samples are included in the full Windows solution so Windows CI compiles them.

## Implemented but intentionally basic
- Number formatting uses the current .NET formatting bridge; it is not a complete Excel-format-code engine.
- Sort is an in-memory range sort with a materialization safety limit; merged ranges are rejected.
- TSV clipboard is an interoperability fallback. Native Nera clipboard remains the high-fidelity internal format.
- Full-row/full-column range operations are still subject to existing materialization safety limits; sparse whole-axis style storage is not implemented yet.
- Header drag-reordering is not implemented yet; live row/column resizing is implemented.
- Structural formula rewriting covers current A1 cell/range syntax; complete Excel table/structured-reference, shared-formula and dynamic-array semantics are not implemented yet.
- Split geometry, per-pane scrolling, body composition, hit testing, cell bounds and shared header composition are platform-neutral and tested, but the public WPF/WinForms controls do not yet expose end-user split creation, separator dragging, pane activation, pane-specific wheel routing, editor routing or scrollbars.
- Split state currently belongs to the split viewport controller; it is not yet persisted in workbook/view snapshots or included in undo/redo.
- Pane-aware cache correctness/allocation and all three Windows desktop GPU paths are CI-gated for initialization/render/resize/shutdown.
- Sustained FPS, input latency, power use and hardware-specific behavior still depend on target machines and should be measured with sample diagnostics/benchmarks.
- WPF device-loss/front-buffer-loss recovery has lifecycle hooks but still needs dedicated injected-failure and long-running stress coverage.

## Next implementation work
- Integrate `SpreadsheetSplitViewportEngine` and split-aware chrome into the public WinForms control, then WPF, without regressing the existing single-pane path.
- Add desktop separator drag/capture UX, pane activation, pane-specific wheel/precision scrolling, editor clipping, header interaction, dirty invalidation and per-pane scrollbar plumbing.
- Decide and implement worksheet/view persistence plus structural-edit mapping rules for split positions and pane scroll snapshots.
- Add desktop runtime smoke coverage for real split controls on GDI+/Direct2D/DXGI and DrawingContext/D3DImage paths.
- Header drag-reordering and sparse whole-axis style storage.
- Add longer-running frame/device-recovery stress coverage for HWND Direct2D, DXGI swap-chain and WPF shared-texture paths.
- Skia GPU surface + MAUI native handler/touch interaction.

## Not implemented yet
- End-user split-pane UX in the public WPF/WinForms controls, including draggable separators and per-pane scrollbars.
- Split-pane persistence/undo semantics and structural mapping of split positions.
- Header drag-reordering and sparse whole-axis styles.
- Full XLSX styles, shared formulas, conditional formatting, validation, tables, drawings, charts, macros and unknown-part preservation.
- Complete Excel-compatible formula/function surface and dynamic arrays.
- AutoFilter/filter UI and advanced sort.
- Printing, page layout, preview and PDF export.
- Charts, pivot, slicers, collaboration, macro/query engine.

## Validation policy
- `NeraSpreadSheet.Core.slnx` must restore, build and test on the cross-platform CI job.
- `NeraSpreadSheet.slnx` must restore/build on the Windows CI job and all test projects must pass.
- Split-pane foundation changes must keep layout, per-pane viewport/scroll and split-chrome regression tests green.
- `NeraSpreadSheet.Windows.Rendering.Tests` must execute on the Windows runner after the full build; compile success alone is insufficient for HWND Direct2D, DXGI swap-chain or WPF shared-texture implementation claims.
- WPF runtime validation must include a real public control hosted in a `Window` and a clean `Window.Close()` lifecycle.
- Architecture verification must remain green.
- Performance-sensitive caches keep correctness/allocation regression tests and BenchmarkDotNet coverage where practical.
- The PR stays Draft and must not be merged while the latest-head CI is red or unknown.
- GPU/advanced XLSX features are not marked implemented until there is executable code plus CI validation; runtime-only claims require a real runtime smoke test or benchmark.

## Latest validation milestone
CI run #187 for commit `8808b2f6ee718eb1f3e59aabd57483254b69f30c` passed Core restore/build/tests/architecture verification, the full Windows restore/build/test job and the mandatory Windows desktop GPU runtime smoke gate. The run includes the structural `#REF!` semantics fixes, independent continuous per-pane split scrolling, pane-local hit testing/bounds and split-aware shared header/chrome regression coverage.

## Detailed contracts
- `docs/structural-editing-contract.md` locks structural insert/delete, formula rewrite and rollback semantics.
- `docs/split-pane-contract.md` locks the platform-neutral split layout, per-pane scroll, hit-test and shared chrome semantics, and lists the desktop integration work that remains.

## Independence rule
NeraSpreadSheet is a native independent spreadsheet SDK. Excel, LibreOffice and DevExpress may be used as external behavior/coverage references only. Their command identifiers, public types and runtime engines are not part of Nera's Core contracts.
