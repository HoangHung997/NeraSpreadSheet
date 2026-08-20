# NeraSpreadSheet current implementation status

This file is the handoff source of truth for the current development branch. A capability is classified as implemented only when executable source, automated tests and the applicable runtime gate exist.

## Product rules

NeraSpreadSheet is an independent spreadsheet SDK.

- No runtime dependency on Microsoft Excel, LibreOffice or DevExpress.
- No UNO/Excel command identifiers in Nera public contracts.
- No UI control per cell.
- Workbook, formulas, editing, layout, scrolling and command projects remain independent from WPF, WinForms and MAUI.
- Viewports use continuous `double` pixel offsets and may stop between row/column boundaries.
- Desktop and GPU hosts consume the same workbook, viewport and display-list semantics.

## Implemented

### Core workbook, formulas and editing

- Sparse worksheets over an Excel-size logical address space.
- Multiple worksheets, immutable snapshots, values, formulas, direct styles, sparse dimensions and native merged ranges.
- Structural insert/delete/reorder with overflow preflight, reference mapping and atomic rollback.
- Formula tokenizer, parser and AST for arithmetic, comparison, concatenation, references, ranges and basic cross-sheet references.
- `SUM`, `AVERAGE`, `MIN`, `MAX`, `COUNT` and `IF`.
- Dependency graph, circular-reference detection and affected-only recalculation.
- Selection, clipboard, formatting, merge, sort, reusable editor, commands and data undo/redo.

### Sparse whole-axis styles and model-safe reordering

- `CellStylePatch` stores property-level changes without materializing every addressed cell.
- Each worksheet owns non-overlapping sparse row-style and column-style span maps.
- Row/column properties compose by one worksheet-global chronological sequence.
- Direct-cell styles remain complete overrides; whole-axis changes patch existing direct cells without losing unrelated properties.
- Insert/delete/reorder map cells, dimensions, styles, merged ranges, formulas, selection and split-pane offsets through the same atomic transform.
- Discontiguous formula images and unsafe merged/freeze transformations are rejected rather than silently corrupted.
- Split and unsplit WPF/WinForms header drag share one reorder model and retain fractional-pixel edge auto-scroll.

Full style semantics: `docs/whole-axis-style-contract.md`.

### Continuous viewport, freeze, split panes and view history

- Sparse metric indexes and fractional pixel scrolling without row/column snapping.
- Snapshot cache and bounded translated viewport tile cache.
- Freeze panes and one/two/four-pane topology.
- Independent per-pane continuous scroll state, active-pane fallback and per-worksheet persistence.
- Integrated and optional overlay pane scrollbars.
- Split-aware headers, selection, editor, resizing, header reorder and dirty-region projection.
- Split-view undo/redo is bounded, per worksheet and isolated from workbook/data history.

### Desktop rendering and recovery

- WPF DrawingContext and shared-texture D3DImage.
- WinForms GDI+, Direct2D/DirectWrite HWND and D3D11/DXGI `FlipDiscard`.
- Hardware preference, WARP fallback, text-layout caching, recovery and diagnostics.
- Runtime stress repeatedly recreates HWND Direct2D, DXGI stacks and loaded WPF shared-texture rendering while validating resize, resource recreation and cached text reuse.

### Cross-platform Skia renderer

- Executes the shared fill, line, text, nested-list, clip and translation command surface.
- Nested lists preserve reference semantics and balance clip/translation state.
- Text supports command clipping and basic wrapping.
- Typeface resources use a bounded LRU cache with explicit ownership and diagnostics.
- Logical-to-device scaling restores caller canvas state after success or exception.
- Linux raster tests and the Windows suite verify pixels, transforms, clipping, cache reuse/eviction, DPI mapping and exception recovery.

### XLSX style fidelity and malformed-input hardening

- Values, formulas/cached values, multiple sheets, dimensions and merged ranges.
- The current Nera style model round-trips fonts, fills, borders, alignment, number formats and direct-cell style IDs through a deduplicated standard SpreadsheetML style table.
- Standard cell, row and column style indexes provide external interoperability.
- A versioned Nera custom XML part preserves exact sparse row/column spans, chronological sequence and stable catalog IDs without materializing blank cells.
- Generated packages pass OpenXml schema validation and huge-axis no-flattening gates.
- Duplicate/default-invalid catalogs, invalid sequence bounds, overlapping spans, empty patches and duplicate exact-state parts are rejected.
- XML, base64 and JSON failures are normalized to `InvalidDataException`; package-controlled counts and payload sizes are bounded.

### MAUI native GPU host

`NeraSpreadsheetView` is one public `SKGLView`. It binds a Nera `Workbook`, owns a `SpreadsheetSession`/viewport engine and consumes the same spreadsheet display-list composer as desktop hosts. Windows, Android, iOS and Mac Catalyst compile against real MAUI workloads.

#### GPU context lifecycle

- Every production paint opens a frame lease bound to the current `GRContext` generation.
- Handler detach/replacement records context loss before the old native surface is released.
- Completion from a stale generation is rejected; completed, failed, abandoned and stale transitions are diagnosed independently.
- Dispose is idempotent and prevents new frame leases.
- Public `PaintSurface` observers run only after the tracked frame is closed.

#### Production pointer state machine

- `NeraSpreadsheetInputController` is the single touch/wheel state machine used directly by `NeraSpreadsheetView.OnTouch`.
- Deterministic tests and loaded smokes call the same production path; no test-only gesture model exists.
- Fractional pan, anchored pinch, zoom-normalized wheel, tap selection, cancellation, topology rebasing and explicit reset are implemented.
- Handler, workbook, worksheet and view reset boundaries clear active gesture state.
- Public diagnostics expose event/update counts, ignored transitions, resets and active touch topology.

#### Repeated loaded Windows stress

The loaded unpackaged Windows smoke uses the same public view and native `SKSwapChainPanel`/`GRContext` path as production. It performs pinch, pan, tap, workbook mutation, wheel animation to settle, three alternating resize classes and three same-view handler/context recreations.

Validated invariants:

- zoom and fractional current/target offsets survive every recreation;
- the same `SpreadsheetSession`, workbook state, selection ranges and selection version survive;
- each recreation creates a handler, native platform surface and `GRContext` not used earlier;
- context generation advances once per recreation;
- all started frames finish exactly once with no failed, abandoned or stale transition;
- pointer state is empty at every lifecycle boundary.

Exact run diagnostics are recorded in `docs/worklog/CURRENT.md`.

#### Surface scale and viewport classes

- `NeraSurfaceMetrics` separates logical MAUI viewport units, renderer canvas units and raw backing pixels.
- It records canvas/viewport, raw/viewport and raw/canvas scale on a completed production frame.
- Orientation is derived only from logical viewport dimensions: Portrait, Landscape or Square.
- Width class is derived only from logical width: Compact `<600`, Medium `600..<840`, Expanded `>=840`.
- Raw pixel dimensions and monitor DPI never alter the logical width class.
- `IgnorePixelScaling=true` keeps approximately one canvas unit per logical viewport unit; raw/canvas scale follows native display scale.
- `IgnorePixelScaling=false` maps canvas dimensions to raw backing pixels; canvas/viewport scale follows native display scale.

Full contract: `docs/maui-surface-scale-contract.md`.

The dedicated loaded Windows scale smoke does not assume 100% DPI. It reads native `SKSwapChainPanel.ContentsScale`, verifies raw backing dimensions against it, and runs these scenarios on the same public view with handler/context recreation after each:

1. physical-canvas Portrait/Compact;
2. logical-canvas Landscape/Expanded;
3. logical-canvas Square/Medium.

Across all scenarios it preserves the same session, workbook, exact selection, zoom and fractional scroll state while keeping GPU accounting balanced.

## Implemented but intentionally conservative

- Direct cell styles are complete overrides; Nera does not add a second partial-cell inheritance layer.
- Unsafe formula/merge transforms are rejected rather than converted into ambiguous unions.
- Number formatting uses a .NET bridge rather than a complete Excel format-code engine.
- Sort is in-memory and materialization-bounded.
- Themes, named styles, differential styles, conditional formats and full Excel format-code semantics remain outside the current XLSX milestone.
- Structural rewriting covers A1 syntax, not tables, structured references, shared formulas or dynamic arrays.
- Conservative full invalidation remains where retained correctness is not yet proven.
- Hosted CI cannot guarantee physical driver removal, a real monitor-to-monitor DPI transition or every OS-controlled context-loss mode.
- Sustained FPS, input latency, physical touch behavior and power use still require target-hardware benchmarks.

## Next implementation work

1. Preserve unknown OpenXml parts across load/save without exposing Microsoft types in Nera public contracts.
2. Add shared formulas, conditional formatting, validation, tables and drawings.
3. Add device/emulator MAUI execution and global native pointer injection where infrastructure is reliable.
4. Add filters, advanced sorting, printing, page layout, preview and PDF export.
5. Add charts, pivot/slicers, accessibility, packaging and sustained-performance hardening.

## Not implemented yet

- Unknown OpenXml part preservation, themes, named/differential/conditional styles and complete Excel format-code semantics.
- Global OS-level pointer injection and device/emulator runtime coverage across all MAUI platforms.
- Shared formulas, validation, tables, drawings, charts, macros and complete dynamic arrays.
- Complete Excel-compatible function surface.
- AutoFilter/filter UI, advanced sort, printing, page layout, preview and PDF export.
- Charts, pivot, slicers, collaboration and macro/query engines.

## Validation policy

- `NeraSpreadSheet.Core.slnx` must restore, build and test on cross-platform CI.
- `NeraSpreadSheet.slnx` must restore/build on Windows and all tests must pass.
- Architecture verification and Windows desktop GPU/runtime smoke are mandatory.
- Skia rendering requires raster, bounded-resource, DPI and failure-recovery gates.
- MAUI changes require real platform builds; production lifecycle/input/scale claims additionally require loaded native runtime gates.
- Every started MAUI GPU frame must finish exactly once as completed, failed or abandoned; stale transitions may not mutate the active generation.
- Pointer tests must call the production controller used by `OnTouch` and finish with no active touch/pinch/tap state.
- Surface-scale classification must use logical viewport dimensions; raw pixels may only describe backing scale.
- Whole-axis styles require no-materialization, chronological composition, structural mapping, exact history and renderer tests.
- Split-view history must remain per worksheet and isolated from data history.
- XLSX style state must pass schema validation, direct-style round-trip, sparse no-flattening and malformed-input rejection gates.
- PR #1 remains Draft and must not merge while exact-head CI is red or unknown.

## Latest validated implementation milestone

CI run #445 (`32323479652`) passed at exact-head commit `5ccbf90dacf3c4c4395939ce26d78a7945ac60e3` on August 20, 2026.

- Core restore/build/tests and architecture verification passed.
- Full Windows restore/build/test and desktop GPU runtime smoke passed.
- MAUI Android, iOS and Mac Catalyst real-target builds passed.
- MAUI Windows build and all 18 handler/lifecycle/input/surface-metrics tests passed.
- The repeated loaded Windows input/resize/context-recreation smoke passed.
- The loaded Windows scale/orientation/width-class smoke passed.
- Exact sparse XLSX style fidelity and malformed-input hardening remained green.

The PR remains Draft and has not been merged into `develop`.

## Independence rule

Excel, LibreOffice and DevExpress may be used only as external behavior/coverage references. Their runtime engines, command IDs and public types are not NeraSpreadSheet dependencies.
