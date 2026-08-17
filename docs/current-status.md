# NeraSpreadSheet current implementation status

This file is the handoff source of truth for the current development branch. It intentionally distinguishes implemented behavior from future architecture.

## Implemented

### Core and workbook
- Sparse worksheet storage over an Excel-size logical address space.
- Cell values, formulas, styles, worksheet dimensions and versioned snapshots.
- Multiple worksheets, rename/remove/add.
- Native merged-cell ranges with overlap protection.
- Workbook-owned immutable style interning (`StyleId`).

### Formula and recalculation
- Tokenizer, parser and AST.
- Arithmetic, comparison, concatenation, references and ranges.
- Basic cross-sheet references.
- SUM, AVERAGE, MIN, MAX, COUNT and IF.
- Dependency graph, circular-reference detection and affected-only recalculation.

### Editing, commands and view state
- Session-owned selection, history, calculation, clipboard, style, merge, sort, editor and view controllers.
- Undo/redo for cell edits, paste, formatting, merge/unmerge and sort.
- Native Nera command IDs; no UNO/Excel command identifiers.
- Clear contents, recalculate, copy/cut/paste, bold/italic, merge/unmerge, sort ascending/descending.
- Relative/absolute formula translation for native paste.
- TSV/quoted-text clipboard import/export adapter independent of other spreadsheet products.
- Per-worksheet freeze-pane state with native `View.FreezePanes` / `View.UnfreezePanes` commands.
- Freeze boundaries reject merged ranges that would be split; new merges are also rejected when they would cross an active freeze boundary.

### Selection and viewport
- Single, extended and multi-range selection.
- Native whole-row, whole-column and whole-sheet selection primitives.
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

### Rendering and desktop hosts
- Shared display-list composition; visible cells only; no UI control per cell.
- Grid, text, selection, fill, font and border rendering.
- Merged cells render as one visual cell and suppress internal grid lines.
- Display-list clip and translation stacks shared by WPF, GDI+ and Direct2D executors.
- Frozen rendering is split into four independently clipped panes plus freeze separator lines.
- Shared spreadsheet chrome compositor draws row/column headers and the top-left select-all corner outside the body viewport.
- Header geometry is centralized and shared by WPF/WinForms; body coordinates remain local to the spreadsheet viewport.
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
- WPF selectable rendering backend: `DrawingContext` or Direct2D on a shared D3D11 texture presented through `D3DImage`/`Vortice.Wpf.DrawingSurface`, avoiding child-HWND airspace.
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

### XLSX adapter
- Basic cell values and formulas/cached values.
- Multiple worksheets.
- Row heights and column widths.
- Merged-cell import/export.
- Unknown-part preservation is explicitly unsupported rather than silently claimed.

### Samples
- `samples/NeraSpreadSheet.Wpf.Sample`
- `samples/NeraSpreadSheet.WinForms.Sample`

Both samples exercise formulas, style interning, merged cells, in-cell editing and XLSX open/save. They expose rendering-backend switching, live FPS/p95 diagnostics and Freeze/Unfreeze controls. Row/column headers are enabled by default, so whole-row/whole-column/corner selection and drag-resizing can be smoke-tested directly. The WinForms sample compares GDI+, HWND Direct2D and D3D11/DXGI flip-model; the WPF sample compares DrawingContext with the shared-texture Direct2D GPU path. Both samples are included in the full Windows solution so Windows CI compiles them.

## Implemented but intentionally basic
- Number formatting uses the current .NET formatting bridge; it is not a complete Excel-format-code engine.
- Sort is an in-memory range sort with a materialization safety limit; merged ranges are rejected.
- TSV clipboard is an interoperability fallback. Native Nera clipboard remains the high-fidelity internal format.
- Full-row/full-column range operations are still subject to existing materialization safety limits; sparse whole-axis style storage is not implemented yet.
- Header drag-reordering is not implemented yet; live row/column resizing is implemented.
- Pane-aware cache correctness/allocation are CI-gated; real GPU FPS still depends on target hardware and should be measured with the sample diagnostics.

## Next implementation work
- Structural row/column insert/delete with sparse-cell/dimension/merge transforms and complete formula/reference rewriting.
- Additional runtime smoke/benchmark coverage for HWND Direct2D, DXGI flip-model and WPF shared-texture GPU paths on real Windows hardware.
- Skia GPU surface + MAUI native handler/touch interaction.

## Not implemented yet
- Split panes independent from freeze panes.
- Header drag-reordering and sparse whole-axis styles.
- Structural row/column insert/delete with complete formula/reference rewriting.
- Full XLSX styles, shared formulas, conditional formatting, validation, tables, drawings, charts, macros and unknown-part preservation.
- Complete Excel-compatible formula/function surface and dynamic arrays.
- AutoFilter/filter UI and advanced sort.
- Printing, page layout, preview and PDF export.
- Charts, pivot, slicers, collaboration, macro/query engine.

## Validation policy
- `NeraSpreadSheet.Core.slnx` must restore, build and test on the cross-platform CI job.
- `NeraSpreadSheet.slnx` must restore/build on the Windows CI job and all test projects must pass.
- Architecture verification must remain green.
- Performance-sensitive caches keep correctness/allocation regression tests and BenchmarkDotNet coverage where practical.
- The PR stays Draft and must not be merged while the latest-head CI is red or unknown.
- GPU/advanced XLSX features are not marked implemented until there is executable code plus CI validation; runtime-only claims require a real runtime smoke test or benchmark.

## Latest validation milestone
CI run #116 for commit `1548a9eac40fff4b88f5957c0aeba060680248f4` passed Core restore/build/tests/architecture verification and the full Windows restore/build/test job. This milestone includes GPU backends, freeze panes, row/column headers, pane-aware caching, nested display lists, allocation/benchmark gates and live WPF/WinForms row/column header resizing.

## Independence rule
NeraSpreadSheet is a native independent spreadsheet SDK. Excel, LibreOffice and DevExpress may be used as external behavior/coverage references only. Their command identifiers, public types and runtime engines are not part of Nera's Core contracts.
