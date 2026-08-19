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
- `SpreadsheetSplitViewHistoryTransaction` coalesces animated/wheel pane scroll and separator drag into one logical history entry.
- No-change transactions do not create history; cancel/dispose can restore the exact before-state.
- WinForms and WPF public split controllers expose view-history state and exact undo/redo.
- Desktop runtime smoke verifies topology/offset restoration, redo, post-redo WPF GPU rendering and isolation from data history.

### Desktop rendering and GPU backends

- WPF DrawingContext and shared-texture D3DImage.
- WinForms GDI+, Direct2D/DirectWrite HWND and D3D11/DXGI `FlipDiscard`.
- Hardware preference, WARP fallback, text-layout caching, recovery and diagnostics.
- Partial invalidation for safe retained paths and explicit full-frame fallbacks where required.

### Extended renderer recovery stress

- HWND Direct2D is repeatedly torn down/recreated, resized and rendered for 32 cycles while DirectWrite layout reuse remains valid.
- D3D11/DXGI swap-chain device stacks are recreated, resized and presented for 16 cycles with adapter/feature-level diagnostics retained.
- WPF shared-texture rendering is forced through 8 complete production `EndD3D -> StartD3D` device-stack restart cycles while the control remains loaded in a real Window.
- Every WPF restart must recreate a non-zero shared texture, render cached text and demonstrate second-frame text-layout reuse.
- These gates intentionally use explicit/injected lifecycle resets rather than nondeterministic physical driver failure on hosted CI.

### Cross-platform Skia display-list renderer

- `SkiaDisplayListRenderer` executes the complete current shared display-list surface: fill, line, text, nested display lists, clip and translation.
- Nested display lists retain reference semantics; clip/translation state is explicitly balanced across recursion.
- Text supports command clipping and basic wrapped layout through current SkiaSharp APIs.
- Typeface resources use a bounded LRU cache with hit/miss/eviction diagnostics and explicit native ownership rules.
- Default typeface fallbacks are cached without disposing Skia-owned global resources.
- Rendering supports logical-to-device DPI scaling and restores the caller's canvas save depth after success or exception.
- Failed frames are rethrown, counted and prevented from corrupting the next frame.
- Linux raster tests and the full Windows suite verify pixels, nested transforms, clipping, text reuse, cache eviction, DPI mapping and exception-state recovery.

### XLSX and desktop samples

- Basic values, formulas/cached values, multiple sheets, row heights, column widths and merged ranges.
- Per-worksheet split state through standard SpreadsheetML pane metadata plus a Nera custom XML part.
- WPF and WinForms samples expose split modes, pane-scrollbar visibility, diagnostics and session Open/Save.

## Source-complete baseline awaiting runtime validation

### MAUI native GPU/touch host

- `NeraSpreadsheetView` is a single `SKGLView`; it does not create one native control per cell.
- The control binds a Nera `Workbook`, owns a `SpreadsheetSession`/viewport engine and consumes the same spreadsheet display-list composer as desktop hosts.
- It provides continuous pan/wheel scrolling, anchored pinch zoom, tap cell selection, hit testing, configurable overscan/theme and renderer diagnostics.
- `UseNeraSpreadSheet()` registers SkiaSharp and the platform-owned `SKGLView` handler.
- The Windows MAUI target restores and compiles in CI with the real MAUI workload and package graph.
- A loaded native Window/device runtime smoke, GL-context recreation gate and Android/iOS/Mac Catalyst build/lifecycle matrix are still required before this host is classified as production-validated.

## Implemented but intentionally conservative

- Direct cell styles are complete overrides; Nera does not introduce a second partial-cell inheritance layer.
- Formula ranges that become discontiguous and merged ranges that split/reverse are rejected rather than converted into unions.
- Number formatting currently uses a .NET bridge rather than a complete Excel format-code engine.
- Sort is in-memory and uses a materialization limit.
- Basic XLSX does not yet round-trip the complete style table or sparse row/column style metadata.
- Structural/formula rewriting covers A1 syntax, not tables, structured references, shared formulas or dynamic arrays.
- Structural, metric, topology, theme and device-lifecycle changes use conservative full invalidation where retained correctness is not yet proven.
- The Skia renderer is caller-owned-canvas and thread-affine; GPU context ownership/recovery belongs to each platform host.
- Hosted CI can deterministically exercise desktop device-stack reset/restart, but cannot guarantee real hardware driver removal or OS-controlled front-buffer/context loss.
- Sustained FPS, input latency and power behavior still require target-hardware benchmarks.

## Next implementation work

1. Complete XLSX style-table and exact sparse row/column style round-trip without flattening logical axes or losing chronological composition.
2. Add a loaded MAUI Windows runtime smoke and Android/iOS/Mac Catalyst build/lifecycle gates, including first frame, resize, pan/pinch/tap and GL-context recreation.
3. Shared formulas, conditional formatting, validation, tables, drawings and unknown-part preservation.
4. Filters and advanced sorting.
5. Printing, page layout, preview and PDF export.
6. Charts, pivot/slicers, accessibility, packaging and production hardening.

## Not implemented yet

- Complete XLSX style fidelity and exact sparse axis-style round-trip.
- MAUI native runtime/device lifecycle validation across supported platforms.
- Shared formulas, conditional formatting, validation, tables, drawings, charts, macros and unknown-part preservation.
- Complete Excel-compatible function surface and dynamic arrays.
- AutoFilter/filter UI and advanced sort.
- Printing, page layout, preview and PDF export.
- Charts, pivot, slicers, collaboration and macro/query engine.

## Validation policy

- `NeraSpreadSheet.Core.slnx` must restore, build and test on cross-platform CI.
- `NeraSpreadSheet.slnx` must restore/build on Windows and all tests must pass.
- Architecture verification and Windows desktop GPU/runtime smoke are mandatory.
- Skia rendering requires cross-platform raster tests for every shared command plus bounded-resource, DPI and failure-recovery gates.
- MAUI changes must at minimum compile the real Windows target; production validation additionally requires loaded native runtime and platform lifecycle gates.
- Whole-axis style requires no-materialization, chronological composition, direct override, merged anchor, structural mapping, exact history, snapshot cache and renderer tests.
- Split-view history must remain isolated per worksheet and from data history; desktop public-controller runtime smoke is mandatory.
- Recovery stress must exercise repeated HWND, DXGI and WPF device-stack lifecycle recreation without resource or rendering regression.
- PR #1 remains Draft and must not merge while exact-head CI is red or unknown.

## Latest validated implementation milestone

CI run #406 (`32230805548`) passed at implementation commit `5ce73280c2a7cbb4aee4563b7c1597781fc1cdc5`:

- Core restore/build/tests and architecture verification passed.
- Full Windows restore/build/test and mandatory desktop GPU/runtime smoke passed.
- The real MAUI Windows target compiled after removing the restore-time TFM override that had corrupted referenced project assets.
- Skia raster tests passed on Linux and Windows, including nested transforms/clips, text, bounded cache reuse/eviction, DPI mapping and exception-state restoration.
- Existing whole-axis styles, split panes/history, header reorder, edge auto-scroll, dirty-region and desktop recovery gates remained green.

The current branch also contains preparatory OpenXml internal access for exact sparse style serialization. That preparatory commit is not counted as completed XLSX fidelity until serializer, reader, round-trip tests and exact-head CI are present.

## Independence rule

Excel, LibreOffice and DevExpress may be used only as external behavior/coverage references. Their runtime engines, command IDs and public types are not NeraSpreadSheet dependencies.
