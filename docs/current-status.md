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
- Document-format dependencies stay inside adapter projects; Microsoft/OpenXml types do not enter public Core contracts.

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

### Unknown OpenXml package-part preservation

`NeraOpenXmlWorkbookSerializer` now supports `PreserveUnknownParts=true` without placing `DocumentFormat.OpenXml` types in the Core workbook model.

#### Internal package envelope

- Load captures the original XLSX bytes into an internal `OpenXmlPackageEnvelope` associated with the loaded `Workbook` through a `ConditionalWeakTable`.
- The capture is bounded to 512 MiB and validates that every logical worksheet maps one-to-one to a `WorksheetPart`.
- Worksheet relationship IDs and part URIs are captured and revalidated before every preservation save.
- Unsafe worksheet part URIs, duplicate worksheet relationship IDs, duplicate worksheet part URIs and unsupported chart/dialog sheet topology are rejected.
- The envelope disappears with the workbook and is detached when the caller explicitly saves with preservation disabled.

#### Copy-and-patch save model

- A preservation save first builds a complete Nera-supported package in memory.
- It then clones the captured package and patches only regions Nera owns instead of reconstructing the opaque relationship graph.
- Workbook sheet names are updated in place while original worksheet relationship IDs and part URIs remain unchanged.
- Worksheet `cols`, `sheetData` and `mergeCells` are replaced from the generated Nera package.
- The supported style-table children (`numFmts`, `fonts`, `fills`, `borders`, `cellStyleXfs`, `cellXfs`, `cellStyles`) are replaced in schema order.
- Other workbook, worksheet and stylesheet markup remains untouched, including `extLst` payloads and future/extension markup outside Nera-owned regions.
- The Nera exact sparse style-state part is refreshed as Nera-owned metadata; other custom/extended parts are not rewritten.
- The complete output package is assembled before the destination stream is changed, giving failure atomicity for validation and merge failures.
- A successful save refreshes the envelope from the emitted bytes, so repeated preservation saves operate from the latest package state.
- A new Nera workbook can also be saved with preservation enabled; its first generated package becomes the baseline envelope.

#### Validated opaque invariants

Automated round-trip tests prove that these survive rename/edit and repeated saves:

- workbook-level and worksheet-level opaque `ExtendedPart` relationships;
- exact relationship IDs;
- exact part URIs;
- relationship types and content types;
- arbitrary binary/XML bytes;
- worksheet external relationships and target URI;
- workbook, worksheet and stylesheet extension markup outside Nera-owned regions;
- newly edited Nera cell data and worksheet rename.

A separate failure-atomicity test adds a worksheet after a preservation load, verifies that topology validation throws, and proves that a pre-populated destination stream is unchanged.

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

#### Surface scale and viewport classes

- `NeraSurfaceMetrics` separates logical MAUI viewport units, renderer canvas units and raw backing pixels.
- It records canvas/viewport, raw/viewport and raw/canvas scale on a completed production frame.
- Orientation is derived only from logical viewport dimensions: Portrait, Landscape or Square.
- Width class is derived only from logical width: Compact `<600`, Medium `600..<840`, Expanded `>=840`.
- Raw pixel dimensions and monitor DPI never alter the logical width class.
- `IgnorePixelScaling=true` keeps approximately one canvas unit per logical viewport unit; raw/canvas scale follows native display scale.
- `IgnorePixelScaling=false` maps canvas dimensions to raw backing pixels; canvas/viewport scale follows native display scale.

Full contract: `docs/maui-surface-scale-contract.md`.

## Implemented but intentionally conservative

- Direct cell styles are complete overrides; Nera does not add a second partial-cell inheritance layer.
- Unsafe formula/merge transforms are rejected rather than converted into ambiguous unions.
- Number formatting uses a .NET bridge rather than a complete Excel format-code engine.
- Sort is in-memory and materialization-bounded.
- Structural rewriting covers A1 syntax, not tables, structured references, shared formulas or dynamic arrays.
- Unknown-part preservation requires the same worksheet objects in the same order; add/remove/reorder is rejected before destination mutation.
- Preservation mode currently supports ordinary worksheet topology only; chart sheets and dialog sheets are rejected.
- Nera replaces supported worksheet/style regions conservatively; semantic rewrites of unknown formulas, defined names, tables, drawings or vendor extensions are not attempted.
- The package envelope is in-memory and bounded; streaming preservation for packages above 512 MiB is not implemented.
- Nested drawing/media graphs are retained by copying the original package, but a dedicated fixture gate for every standard nested graph remains future hardening.
- Themes, named styles, differential styles, conditional formats and full Excel format-code semantics remain outside the current XLSX milestone.
- Hosted CI cannot guarantee physical driver removal, a real monitor-to-monitor DPI transition or every OS-controlled context-loss mode.
- Sustained FPS, input latency, physical touch behavior and power use still require target-hardware benchmarks.

## Next implementation work

1. Harden unknown-part preservation with nested drawing/media/custom-XML fixtures, package-level relationships, hostile/conflicting relationship cases and schema validation after repeated saves.
2. Implement shared-formula import/export and reference translation without flattening sparse worksheets.
3. Add conditional formatting, validation, tables and drawings as first-class supported models.
4. Add device/emulator MAUI execution and global native pointer injection where infrastructure is reliable.
5. Add filters, advanced sorting, printing, page layout, preview and PDF export.
6. Add charts, pivot/slicers, accessibility, packaging and sustained-performance hardening.

## Not implemented yet

- First-class themes, named/differential/conditional styles and complete Excel format-code semantics.
- Shared formulas, validation, tables, drawings, charts, macros and complete dynamic arrays.
- Topology-changing preservation merge for worksheet add/remove/reorder and chart/dialog sheets.
- Dedicated nested drawing/media and package-root opaque-relationship fixtures.
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
- Unknown-part preservation must retain opaque bytes, URI, relationship ID/type, content type, external relationships and unowned markup across repeated saves.
- Preservation topology conflicts must fail before mutating the destination stream.
- PR #1 remains Draft and must not merge while exact-head CI is red or unknown.

## Latest validated implementation milestone

CI run #449 (`32330382258`) passed at implementation commit `75b8292f060eccaaa7caff1fbed88f650f68ea7f` on August 20, 2026.

- Core restore/build/tests and architecture verification passed, including the new unknown-part preservation and failure-atomicity tests.
- Full Windows restore/build/test and desktop GPU runtime smoke passed.
- MAUI Android, iOS and Mac Catalyst real-target builds passed.
- MAUI Windows build, handler/lifecycle/input/surface-metrics tests and both loaded runtime smokes passed.
- Exact sparse XLSX style fidelity and malformed-input hardening remained green.

The PR remains Draft and has not been merged into `develop`.

## Independence rule

Excel, LibreOffice and DevExpress may be used only as external behavior/coverage references. Their runtime engines, command IDs and public types are not NeraSpreadSheet dependencies.
