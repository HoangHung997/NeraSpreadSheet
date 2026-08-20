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

### Shared-formula import, export and round-trip

#### Import

- SpreadsheetML formulas with `t="shared"` are imported through a two-pass resolver.
- Formula-bearing anchors are collected before followers, so followers may appear earlier than their anchor in worksheet XML.
- `SharedIndex`, anchor range and cell addresses are validated before worksheet changes are applied.
- Every existing follower receives an independent Nera formula translated from the anchor by the same `FormulaReferenceTranslator` used by copy/paste and structural editing.
- Relative and mixed references are translated while `$A$1`, `$A1`, `A$1`, quoted sheet names and doubled-quote string literals retain their intended semantics.
- The declared shared range is never enumerated or materialized; only cells already present in `sheetData` are processed.
- Duplicate anchors, missing anchors/indexes, missing or reversed anchor ranges, followers outside the declared range and followers declaring their own range are rejected with `InvalidDataException`.
- Cached formula values are retained or discarded according to `LoadCachedFormulaValues` without dropping formulas.

#### Export grouping

- `OpenXmlSharedFormulaExportPlan` examines only existing used formula cells.
- A group is emitted only when cells form a continuous rectangle and every follower equals an exact translation of the candidate anchor.
- Safety is bidirectional: translating anchor → follower and follower → anchor must both reproduce the exact normalized formulas.
- Shared indexes are regenerated deterministically in worksheet row-major order.
- The anchor emits formula text plus `t="shared"`, `si` and `ref`; followers emit only `t="shared"` and `si`.
- At most `100,000` groups are emitted per worksheet and each group is bounded to `1,000,000` existing cells.
- Formulas containing unsupported/error/structured/array markers, formulas separated by gaps and ambiguous translation sets remain normal formulas.
- Cached results follow `WriteCachedFormulaValues` for both anchors and followers.

#### Structural and preservation gates

- Insert/delete that preserves translation equivalence is regrouped safely at the new export boundary.
- Row/column reorder is allowed to fall back to independent normal formulas when the reordered set is no longer translation-equivalent; exact logical formulas still round-trip.
- Import → export → re-import preserves independent Nera formulas and cached results.
- Two consecutive `PreserveUnknownParts=true` saves retain an opaque workbook part while regenerating schema-valid shared groups.
- Generated shared-formula packages pass `OpenXmlValidator(FileFormatVersions.Office2013)`.

### Unknown OpenXml package-part preservation

`NeraOpenXmlWorkbookSerializer` supports `PreserveUnknownParts=true` without placing `DocumentFormat.OpenXml` types in the Core workbook model.

#### Internal package envelope

- Load captures the original XLSX bytes into an internal `OpenXmlPackageEnvelope` associated with the loaded `Workbook` through a `ConditionalWeakTable`.
- Capture is bounded to 512 MiB.
- Worksheet object identity, relationship ID, order and part URI are retained and revalidated.
- Worksheet add/remove/reorder, duplicate binding, unsafe URI and unsupported chart/dialog-sheet topology fail explicitly.
- Saving with preservation disabled performs a full Nera rewrite and detaches the envelope.

#### Copy-and-patch save

- A preservation save first builds a complete Nera-supported package in memory.
- It clones the captured package and patches only regions Nera owns instead of reconstructing the opaque relationship graph.
- Workbook sheet names are updated in place while original worksheet relationship IDs and part URIs remain unchanged.
- Worksheet `cols`, `sheetData` and `mergeCells` are replaced from the generated package.
- Supported style-table children are replaced in schema order.
- Workbook, worksheet and stylesheet markup outside Nera-owned regions remains untouched.
- The Nera exact sparse style-state part is refreshed; other custom/extended parts are not rewritten.
- Successful saves refresh the envelope from the emitted bytes, so repeated saves continue from the latest package state.

#### Nested package-graph gate

A repeated-save fixture proves preservation of:

- package-root opaque `ExtendedPart` plus nested opaque child;
- package-root and nested external relationships;
- standard worksheet `DrawingsPart` and worksheet `<drawing r:id>` reference;
- a real PNG `ImagePart`, relationship ID, URI, content type and exact bytes;
- opaque nested relationship beneath the drawing part;
- non-Nera `CustomXmlPart`, `CustomXmlPropertiesPart` and opaque nested relationship;
- exact relationship IDs/types, part URIs, content types and binary/XML bytes;
- worksheet rename and Nera cell edits through two preservation saves.

Both outputs pass `OpenXmlValidator` after rename/edit and repeated save.

#### Package graph preflight

`OpenXmlPackageGraphValidator` traverses package root and nested containers before a preserved workbook is restored. It covers internal parts, external relationships, hyperlink relationships and data-part references.

The validator enforces:

- package-wide part-URI uniqueness across OpenXml parts and data parts;
- per-container relationship-ID uniqueness across internal and reference relationships;
- XML NCName relationship IDs;
- absolute relationship-type URIs;
- bounded part count and relationship count;
- bounded URI/type/target lengths;
- no literal or decoded `.`/`..` traversal segments;
- no encoded slash/backslash, empty URI segment, backslash, query, fragment or control character in part URIs;
- no literal or percent-decoded control character in relationship type/target text.

Preservation load validates captured bytes before workbook restoration. Preservation save creates and validates the final output envelope before the destination stream is truncated or written.

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

The loaded unpackaged Windows smoke uses the same public view and native `SKSwapChainPanel`/`GRContext` path as production. It performs pinch, pan, tap, workbook mutation, wheel animation to settle, alternating resize classes and same-view handler/context recreations.

Validated invariants include preserved session/selection/zoom/fractional offsets, fresh handler/native/context identity after recreation, balanced context generations and zero failed/abandoned/stale frame transitions.

#### Surface scale and viewport classes

- `NeraSurfaceMetrics` separates logical MAUI viewport units, renderer canvas units and raw backing pixels.
- Orientation and width class are derived only from logical viewport dimensions.
- Raw pixel dimensions and monitor DPI never alter logical Compact/Medium/Expanded classification.
- Loaded Windows gates read native `SKSwapChainPanel.ContentsScale`, switch logical/physical modes and preserve state through context recreation.

Full contract: `docs/maui-surface-scale-contract.md`.

## Implemented but intentionally conservative

- Direct cell styles are complete overrides; Nera does not add a second partial-cell inheritance layer.
- Unsafe formula/merge transforms are rejected rather than converted into ambiguous unions.
- Number formatting uses a .NET bridge rather than a complete Excel format-code engine.
- Sort is in-memory and materialization-bounded.
- Structural rewriting covers A1 syntax, not tables, structured references or dynamic arrays.
- Shared-formula grouping is conservative and regenerated on every save; it does not preserve source `si` identity.
- Shared formulas with gaps, `#REF!`, structured/array markers or failed bidirectional proof fall back to normal formulas.
- Unknown-part preservation requires the same worksheet objects in the same order; add/remove/reorder is rejected before destination mutation.
- Preservation mode supports ordinary worksheet topology only; chart sheets and dialog sheets are rejected.
- Unknown formulas, defined names, tables, drawings and vendor extensions are retained but not semantically rewritten.
- The package envelope is in-memory and bounded; streaming preservation above 512 MiB is not implemented.
- Themes, named styles, differential styles, conditional formats and full Excel format-code semantics remain outside the current XLSX milestone.
- Hosted CI cannot guarantee physical driver removal, a real monitor-to-monitor DPI transition or every OS-controlled context-loss mode.
- Sustained FPS, input latency, physical touch behavior and power use still require target-hardware benchmarks.

## Progress estimate

- Engine/viewport/renderer foundation: approximately `85%`.
- Basic spreadsheet MVP: approximately `68–72%`.
- Complete professional roadmap: approximately `45%`.
- Production release readiness: approximately `21–25%`.

These are weighted engineering estimates, not checkbox counts.

## Next implementation work

1. Add conditional formatting model, differential styles, renderer integration and XLSX round-trip.
2. Add data validation model, list/custom rules and desktop validation behavior.
3. Add tables, structured references and AutoFilter integration.
4. Expand formula functions, dynamic arrays and plugin function SDK.
5. Add advanced sorting, grouping and virtualized data.
6. Add printing/page layout/PDF, first-class drawings/charts and pivot/slicers.
7. Add accessibility, packaging, fuzzing, performance budgets and release hardening.

## Not implemented yet

- External shared-formula compatibility corpus from multiple spreadsheet generators.
- Dynamic arrays, structured references and complete Excel-compatible function surface.
- First-class conditional formatting, validation, tables, drawings and charts.
- Topology-changing preservation merge for worksheet add/remove/reorder and chart/dialog sheets.
- Complete themes, named/differential styles and Excel format-code semantics.
- AutoFilter/filter UI, advanced sort, printing, preview and PDF export.
- Pivot, slicers, collaboration and macro/query engines.
- Full accessibility/designer/NuGet/security/performance/release gates.

## Validation policy

- `NeraSpreadSheet.Core.slnx` must restore, build and test on cross-platform CI.
- `NeraSpreadSheet.slnx` must restore/build on Windows and all tests must pass.
- Architecture verification and Windows desktop GPU/runtime smoke are mandatory.
- Skia rendering requires raster, bounded-resource, DPI and failure-recovery gates.
- MAUI changes require real platform builds; production lifecycle/input/scale claims additionally require loaded native runtime gates.
- Every started MAUI GPU frame must finish exactly once as completed, failed or abandoned; stale transitions may not mutate the active generation.
- Whole-axis styles require no-materialization, chronological composition, structural mapping, exact history and renderer tests.
- XLSX style state must pass schema validation, direct-style round-trip, sparse no-flattening and malformed-input rejection gates.
- Shared-formula import/export must prove mixed/absolute translation, quoted-sheet/string preservation, continuous rectangle proof, deterministic indexes, cached-value modes, structural safety, normal-formula fallback and preservation repeated-save without range materialization.
- Unknown-part preservation must retain opaque bytes, URI, relationship ID/type, content type, nested/external relationships and unowned markup across repeated saves.
- Package graph must be preflighted before workbook restoration and before destination mutation.
- PR #1 remains Draft and must not merge while exact-head CI is red or unknown.

## Latest validated implementation milestone

CI run #469 (`32347684027`) passed at implementation commit `d6808102298920ae868b86713341f2ccc1970594` on August 20, 2026.

- Core restore/build/tests and architecture verification passed, including shared-formula export grouping and all prior OpenXml regressions.
- Continuous rectangular groups, stable worksheet-order indexes, cached-value modes and schema-valid anchor/follower output passed.
- Gaps and unsupported tokens fell back to normal formulas.
- Insert/delete regrouped safely; reorder preserved exact logical formulas through fallback and round-trip.
- Two preservation saves retained opaque package bytes while regenerating valid shared groups.
- Full Windows restore/build/test and desktop GPU runtime smoke passed.
- MAUI Android, iOS and Mac Catalyst real-target builds passed.
- MAUI Windows build/tests and both loaded runtime smokes passed.
- Exact sparse XLSX style fidelity, package graph hardening and unknown-part preservation remained green.

The PR remains Draft and has not been merged into `develop`.

## Independence rule

Excel, LibreOffice and DevExpress may be used only as external behavior/coverage references. Their runtime engines, command IDs and public types are not NeraSpreadSheet dependencies.
