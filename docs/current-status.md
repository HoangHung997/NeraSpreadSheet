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

### Core workbook, formulas and editing

- Sparse worksheets over an Excel-size logical address space.
- Multiple worksheets, immutable snapshots, values, formulas, direct styles, sparse dimensions and native merged ranges.
- Structural insert/delete/reorder with overflow preflight, reference mapping and atomic rollback.
- Formula tokenizer, parser and AST for arithmetic, comparison, concatenation, references, ranges and basic cross-sheet references.
- `SUM`, `AVERAGE`, `MIN`, `MAX`, `COUNT` and `IF`.
- Dependency graph, circular-reference detection and affected-only recalculation.
- Selection, clipboard, formatting, merge, sort, reusable editor, commands and data undo/redo.

### Sparse whole-axis style storage

- `CellStylePatch` stores property-level changes without materializing every addressed cell.
- Each worksheet owns non-overlapping sparse row-style and column-style span maps.
- Row/column properties compose by one worksheet-global chronological sequence.
- Explicit direct-cell styles remain complete overrides; whole-axis changes patch existing direct cells without losing unrelated properties.
- Whole-row, whole-column and whole-sheet formatting keeps blank cells implicit.
- Insert/delete/reorder map style spans through the same axis transforms as cells, dimensions and merged ranges.
- Structural snapshots preserve row spans, column spans and style sequence for exact rollback/history.
- Visible blank and populated cells render effective axis styles through the shared display-list composer.

Full semantics: `docs/whole-axis-style-contract.md`.

### Model-safe row and column reordering

- `WorksheetAxisMove` is a fixed-length permutation of one contiguous axis interval.
- Sparse cells, dimensions, styles and merged ranges move without materializing the logical axis.
- Local and cross-sheet formulas follow logical cell identity while preserving `$`, quoted sheet names and string literals.
- Discontiguous formula images and unsafe merged/freeze transformations are rejected atomically.
- Selection and all split-pane offsets map through the same transaction with exact undo/redo.
- Split and unsplit WPF/WinForms header drag share one reorder model and geometry.
- Drag-edge auto-scroll remains fractional-pixel and targets only the active pane/control.

### Continuous viewport, freeze, split panes and view history

- Sparse metric indexes and fractional pixel scrolling without row/column snapping.
- Snapshot cache and bounded translated viewport tile cache.
- Freeze panes and one/two/four-pane topology.
- Independent per-pane continuous scroll state, active-pane fallback and per-worksheet persistence.
- Integrated and optional overlay pane scrollbars.
- Split-aware headers, selection, editor, resizing, header reorder and dirty-region projection.
- Split-view undo/redo is isolated per worksheet and from workbook/data history.
- Direct topology, separator, active-pane and pane-scroll changes support bounded exact history.
- Animated/wheel pane scroll and separator drag are coalesced into one logical view-history entry.

### Desktop rendering and recovery

- WPF DrawingContext and shared-texture D3DImage.
- WinForms GDI+, Direct2D/DirectWrite HWND and D3D11/DXGI `FlipDiscard`.
- Hardware preference, WARP fallback, text-layout caching, recovery and diagnostics.
- Partial invalidation for safe retained paths and explicit full-frame fallback where required.
- Runtime stress repeatedly recreates HWND Direct2D, DXGI device stacks and loaded WPF shared-texture rendering.
- Deterministic lifecycle reset gates verify resource recreation, resize, cached text reuse and post-recovery rendering.

### Cross-platform Skia display-list renderer

- Executes the complete current shared command surface: fill, line, text, nested lists, clip and translation.
- Nested lists retain reference semantics and explicitly balance clip/translation state.
- Text supports command clipping and basic wrapped layout through current SkiaSharp APIs.
- Typeface resources use a bounded LRU cache with hit/miss/eviction diagnostics and explicit ownership rules.
- Rendering supports logical-to-device DPI scaling and restores the caller canvas save depth after success or exception.
- Failed frames are rethrown, counted and prevented from corrupting the next frame.
- Linux raster tests and the full Windows suite verify pixels, transforms, clipping, cache reuse/eviction, DPI mapping and exception recovery.

### XLSX style fidelity and malformed-input hardening

- Basic values, formulas/cached values, multiple sheets, dimensions and merged ranges.
- The current Nera style model round-trips fonts, fills, borders, alignment, number formats and direct-cell style IDs through a deduplicated standard SpreadsheetML style table.
- Standard cell, row and column style indexes provide external XLSX interoperability.
- A versioned Nera custom XML part preserves exact sparse row/column style spans, chronological sequence and stable catalog identifiers without materializing blank cells.
- Generated packages pass OpenXml schema validation and huge-axis no-flattening gates.
- Duplicate/default-invalid catalogs, invalid sequence bounds, overlapping spans, empty patches and multiple exact style-state parts are rejected.
- XML, base64 and JSON failures are normalized to `InvalidDataException` before workbook restoration.
- Payload, catalog, worksheet and span counts are bounded against malformed-package allocation attacks.

### MAUI native GPU/touch host, production input and context lifecycle

- `NeraSpreadsheetView` is one public `SKGLView`; it never creates a native control per cell.
- The control binds a Nera `Workbook`, owns a `SpreadsheetSession`/viewport engine and consumes the same spreadsheet display-list composer as desktop hosts.
- `UseNeraSpreadSheet()` registers SkiaSharp's platform-owned `SKGLView` handler graph.
- Windows, Android, iOS and Mac Catalyst targets compile against their real MAUI workloads in CI.

#### GPU context and frame lifecycle

- `NeraGpuContextLifecycle` serializes context/frame transitions per view with monotonically increasing context generations.
- Every production paint starts a frame lease bound to the current `GRContext`; completion from a stale generation is rejected.
- Handler detach/replacement records context loss before the old native surface is released.
- Context replacement abandons any still-active old frame; successful, failed, abandoned and stale transitions are independently diagnosed.
- Dispose is idempotent and prevents new frame leases.
- Production `PaintSurface` observers run only after the lifecycle has completed the tracked frame, so diagnostics cannot report a leaked active frame.

#### Production pointer state machine

- `NeraSpreadsheetInputController` owns the touch/wheel state used directly by `NeraSpreadsheetView.OnTouch`.
- Platform events and deterministic tests enter through the same `Process(SKTouchEventArgs)` method; there is no second gesture model for tests.
- The controller implements continuous fractional-pixel pan, anchored pinch zoom, wheel scaling by current zoom, tap selection, cancellation and explicit gesture reset.
- Pointer identity and press order are stable without creating LINQ arrays on every move.
- When one pointer leaves a three-pointer gesture and two remain, pinch is rebased to the remaining pair instead of reusing a stale anchor.
- Duplicate/unknown pointer transitions are ignored and diagnosed rather than mutating active gesture state.
- Handler changes, workbook changes, worksheet changes and view reset clear in-progress gestures before the affected state changes.
- Public diagnostics expose event counts, pan/pinch/tap counts, ignored events, resets and current active-touch state.

#### Automated input tests

- Tap below movement threshold selects the release point.
- Pan preserves fractional offsets and suppresses tap.
- Pinch preserves its document anchor.
- Wheel delta is normalized by current zoom.
- Cancelled pinch transitions the remaining pointer into pan without a false tap.
- A remaining two-pointer gesture is rebased after one of three pointers is released.
- `CancelAll` and dispose clear state and reject later input as specified.
- The MAUI Windows test assembly currently contains 14 passing tests: two handler tests, five GPU lifecycle tests and seven input-controller tests.

#### Loaded native MAUI Windows runtime gate

A loaded unpackaged MAUI Windows application validates the public view, native surface, production input path, resize and same-view context recreation together.

The gate:

1. opens a real native `SKGLView`/SwapChain surface with a live `GRContext`;
2. sends deterministic `SKTouchEventArgs` through `NeraSpreadsheetView.ProcessTouchInput`, which is also the path used by `OnTouch`;
3. performs pinch to zoom `1.375`, pan to fractional offsets `17.25 / 31.75` and a corner tap selection;
4. mutates workbook data and renders the result;
5. resizes the native surface from `944 x 600` to `784 x 480`;
6. detaches the handler from the same `NeraSpreadsheetView`, verifies context loss/no active frame and reattaches that same control;
7. verifies a new handler, platform surface and `GRContext`, context generation `1 -> 2`, and the resized surface remains `784 x 480`;
8. verifies zoom, fractional offsets, workbook/session state and cleared pointer state survive recreation.

Validated runtime diagnostics:

- frames started/completed/abandoned: `9 / 9 / 0`;
- context created/lost/recreated: `2 / 1 / 1`;
- input press/move/release: `4 / 2 / 4`;
- input pan/pinch/tap updates: `1 / 1 / 1`;
- active touches after the sequence: `0`;
- stale GPU-frame transitions: `0`;
- cached typefaces: `1`.

## Implemented but intentionally conservative

- Direct cell styles are complete overrides; Nera does not introduce a second partial-cell inheritance layer.
- Formula ranges that become discontiguous and merged ranges that split/reverse are rejected rather than converted into unions.
- Number formatting currently uses a .NET bridge rather than a complete Excel format-code engine.
- Sort is in-memory and uses a materialization limit.
- XLSX style fidelity covers the current Nera style model; themes, named styles, differential styles, conditional formats and complete Excel format-code semantics remain outside this milestone.
- Structural/formula rewriting covers A1 syntax, not tables, structured references, shared formulas or dynamic arrays.
- Conservative full invalidation remains where retained correctness is not yet proven.
- The Skia renderer is caller-owned-canvas and thread-affine; each platform host owns its GPU context lifecycle.
- Hosted CI deterministically exercises context/device recreation, but cannot guarantee physical driver removal or every OS-controlled loss mode.
- Sustained FPS, input latency, physical touch-device behavior and power use still require target-hardware benchmarks.
- The loaded Windows smoke uses the real production input state machine and native rendering, but it is not global OS pointer injection.

## Next implementation work

1. Extend the loaded MAUI gate with wheel animation completion and repeated resize/context-recreation cycles.
2. Add DPI/display-scale and orientation/size-class transitions around context recreation.
3. Add real device/emulator execution and native pointer injection where hosted infrastructure is reliable.
4. Preserve unknown OpenXml parts and extend XLSX support for shared formulas, conditional formatting, validation, tables and drawings.
5. Add filters, advanced sorting, printing, page layout, preview and PDF export.
6. Add charts, pivot/slicers, accessibility, packaging and sustained-performance hardening.

## Not implemented yet

- Complete Excel themes, named styles, differential/conditional styles and unknown-part preservation.
- Global native MAUI pointer injection/device execution across all supported platforms.
- Repeated MAUI resize/DPI/orientation stress across multiple context generations.
- Shared formulas, validation, tables, drawings, charts, macros and complete dynamic arrays.
- Complete Excel-compatible function and format-code surfaces.
- AutoFilter/filter UI, advanced sort, printing, page layout, preview and PDF export.
- Charts, pivot, slicers, collaboration and macro/query engines.

## Validation policy

- `NeraSpreadSheet.Core.slnx` must restore, build and test on cross-platform CI.
- `NeraSpreadSheet.slnx` must restore/build on Windows and all tests must pass.
- Architecture verification and Windows desktop GPU/runtime smoke are mandatory.
- Skia rendering requires cross-platform raster, bounded-resource, DPI and failure-recovery gates.
- MAUI changes require real platform builds; production lifecycle/input claims additionally require the loaded native runtime gate.
- MAUI context diagnostics must finish every started frame exactly once as completed, failed or abandoned, and stale transitions must never mutate the active generation.
- Production pointer tests must call the same controller used by `OnTouch`; test-only gesture implementations are prohibited.
- Loaded input gates must finish with zero active touches and no stale pinch/tap state before handler recreation.
- Whole-axis style requires no-materialization, chronological composition, direct override, structural mapping, exact history, snapshot cache and renderer tests.
- Split-view history must remain isolated per worksheet and from data history.
- XLSX style-state must pass schema validation, direct-style round-trip, sparse no-flattening and malformed-input rejection gates.
- PR #1 remains Draft and must not merge while exact-head CI is red or unknown.

## Latest validated implementation milestone

CI run #438 (`32270133783`) passed at implementation commit `a4174b54acea452cf312a2741680947a38a60139` on August 19, 2026:

- Core restore/build/tests and architecture verification passed.
- Full Windows restore/build/test and mandatory desktop GPU/runtime smoke passed.
- MAUI Android, iOS and Mac Catalyst real-target builds passed.
- MAUI Windows real-target build and all 14 MAUI tests passed.
- The loaded native MAUI Windows runtime gate passed production pinch, pan, tap, resize and same-view handler/`GRContext` recreation.
- Exact sparse XLSX style fidelity and malformed-input hardening remained green.

The PR remains Draft and has not been merged into `develop`.

## Independence rule

Excel, LibreOffice and DevExpress may be used only as external behavior/coverage references. Their runtime engines, command IDs and public types are not NeraSpreadSheet dependencies.
