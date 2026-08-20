# NeraSpreadSheet current implementation status

This file is the handoff source of truth for the current development branch. A capability is classified as implemented only when executable source, automated tests and the applicable runtime gate exist.

## Product rules

NeraSpreadSheet is an independent spreadsheet SDK.

- No runtime dependency on Microsoft Excel, LibreOffice or DevExpress.
- No UNO/Excel command identifiers in Nera public contracts.
- No UI control per cell.
- Workbook, formulas, editing, layout, scrolling and commands remain independent from WPF, WinForms and MAUI.
- Viewports use continuous `double` pixel offsets and may stop between row/column boundaries.
- Desktop and GPU hosts consume the same workbook, viewport and display-list semantics.
- Document-format dependencies stay inside adapter projects; OpenXml types do not enter Core public contracts.

## Implemented

### Core workbook, formulas and editing

- Excel-size sparse worksheets, multiple worksheets, values, formulas, direct styles, dimensions and native merged ranges.
- Immutable snapshots and bounded caches for viewport/rendering work.
- Selection, clipboard, reusable editor, commands, sort and data undo/redo.
- Structural insert/delete/reorder with overflow preflight, formula/reference mapping and atomic rollback.
- Formula tokenizer, parser and AST for arithmetic, comparison, concatenation, A1 references/ranges and basic cross-sheet references.
- `SUM`, `AVERAGE`, `MIN`, `MAX`, `COUNT` and `IF`.
- Dependency graph, circular-reference detection and affected-only recalculation.

### Sparse styles and model-safe axis transforms

- `CellStylePatch` stores property-level changes without materializing logical axes.
- Sparse row/column style spans compose by one worksheet-global chronological sequence.
- Direct-cell styles remain complete overrides; whole-axis changes preserve unrelated properties.
- Insert/delete/reorder map cells, dimensions, styles, merged ranges, formulas, selection and pane offsets through one transaction.
- Unsafe discontiguous range/merge/freeze transforms are rejected instead of silently corrupted.

Full contract: `docs/whole-axis-style-contract.md`.

### Continuous viewport, split panes and view history

- Fractional pixel scrolling, sparse metric indexes, freeze panes and one/two/four-pane topology.
- Independent per-pane continuous offsets, pane-local scrollbars and per-worksheet persistence.
- Split-aware headers, selection, editor, resize, reorder and dirty-region projection.
- Snapshot/tile caching and a view-history stack isolated from workbook/data undo/redo.

### Desktop and cross-platform rendering

- WPF DrawingContext and shared-texture D3DImage.
- WinForms GDI+, Direct2D/DirectWrite HWND and D3D11/DXGI `FlipDiscard`.
- Hardware preference, WARP fallback, text caching, recovery and diagnostics.
- Cross-platform Skia renderer for fill, line, text, nested lists, clip and translation.
- Repeated device/context recreation stress for desktop and loaded MAUI Windows hosts.
- MAUI Windows, Android, iOS and Mac Catalyst build against real workloads.

### Shared formulas

- Two-pass SpreadsheetML shared-formula import supports follower-before-anchor ordering.
- Relative, mixed and absolute A1 references, quoted sheet names and string literals use one Core translator.
- Export compacts only translation-equivalent continuous rectangles and assigns deterministic worksheet-order shared indexes.
- Gap, `#REF!`, structured/array markers and failed bidirectional proof fall back to independent formulas.
- Cached-value modes, structural behavior, schema validation and opaque repeated-save gates are implemented.

### Conditional formatting Core model

- `ConditionalFormattingRule` is platform/document independent and supports `CellIs` and `Expression`.
- Cell-is operators: equal, not-equal, greater/greater-or-equal, less/less-or-equal, between and not-between.
- Rules support one or more ranges, integer priority and `StopIfTrue`.
- `DifferentialStyleCatalog` deduplicates property-level patches for font, fill, border, alignment and number format.
- Worksheet snapshots deep-copy rules and differential styles for thread-safe renderer use.
- `ConditionalFormattingEvaluator` evaluates rules in priority order and composes non-conflicting lower-priority properties while letting higher-priority properties win.
- Expression formulas are translated from the rule anchor to each target cell through the shared Core A1 translator.
- Cell mutations conservatively expand `CellsChanged` dirty regions to all conditional target ranges, so a source cell may invalidate a different rendered area without materializing any target cells.

### Conditional formatting structural safety

- Rule ranges and formulas participate in structural state, insert/delete, undo/redo and rollback.
- The Core structural-reference rewriter is shared by editing operations and conditional rules; absolute/mixed references and formula ranges are rewritten consistently.
- Insert/delete removes rules whose complete target disappears and maps remaining ranges/formulas.
- Axis reorder requires every rule range to be one uniform translation. A range that remains contiguous but whose internal rows/columns use different deltas is rejected atomically.
- Production controller tests verify insert → undo → redo restores exact rule ID, priority, range and formula.

### Conditional formatting XLSX interoperability

- Standard SpreadsheetML `dxfs`, `dxf`, `conditionalFormatting`, `cfRule` and `formula` are imported/exported.
- `cellIs`, `expression`, operator, `priority`, `stopIfTrue`, `dxfId` and multiple `sqref` ranges round-trip.
- Workbook-wide differential styles are deduplicated deterministically at save boundaries while Core keeps worksheet-local catalogs.
- Generated packages pass `OpenXmlValidator(FileFormatVersions.Office2013)`.
- Duplicate priorities, missing/invalid priority, invalid `sqref`, out-of-range `dxfId`, malformed formula counts and unsupported rule/style markup are rejected.
- `PreserveUnknownParts=true` owns and refreshes worksheet conditional-formatting elements plus stylesheet `dxfs` while retaining opaque package bytes and unowned markup across repeated saves.

### XLSX style fidelity and package preservation

- Values, formulas/cached values, sheets, dimensions, merges, panes and current Nera style semantics round-trip.
- Standard cell/row/column style indexes and a Nera exact sparse-style state part preserve interoperability and no-flattening behavior.
- Unknown-part preservation uses an internal bounded package envelope and copy-and-patch save.
- Nested opaque parts, standard drawing/image relationships, custom XML/properties and package-root relationships retain exact IDs, URIs, content types and bytes across repeated saves.
- Package graph preflight checks part/relationship counts, uniqueness, NCName IDs, absolute relationship types, unsafe URI segments and encoded control/traversal payloads before restoration or destination mutation.

## Implemented but intentionally conservative

- Conditional formatting currently supports `CellIs` and `Expression`; color scales, data bars, icon sets and specialized duplicate/top/average/time rules are not modeled.
- Imported differential colors currently require explicit RGB; theme/indexed colors are not semantically converted.
- Conditional expression evaluation currently resolves the active worksheet snapshot; cross-sheet conditional references are not a supported evaluation contract.
- Dependent conditional invalidation is conservative: any cell mutation expands to every conditional target range on that worksheet.
- Direct styles remain complete overrides; no second partial-cell inheritance layer is introduced.
- Number formatting uses a .NET bridge rather than a complete Excel format-code engine.
- Sort is in-memory and bounded.
- Unknown-part preservation requires the same worksheet objects and order; topology-changing merges are rejected before destination mutation.
- Package preservation is in-memory and bounded to 512 MiB.
- Hosted CI cannot guarantee physical driver removal, real monitor-to-monitor DPI transitions or every OS-controlled context-loss mode.

## Progress estimate

- Engine/viewport/renderer foundation: approximately `86%`.
- Basic spreadsheet MVP: approximately `70–74%`.
- Complete professional roadmap: approximately `46%`.
- Production release readiness: approximately `22–26%`.

These are weighted engineering estimates, not checkbox counts.

## Next implementation work

1. Data validation Core model, whole/decimal/date/time/text/list/custom rules and commit-time editor gate.
2. Validation input message, error alert, invalid-cell diagnostics and XLSX `dataValidations` round-trip.
3. Tables, structured references and AutoFilter integration.
4. Formula/function surface, dynamic arrays and plugin function SDK.
5. Advanced sorting, grouping and virtualized data.
6. Printing/page layout/PDF, first-class drawings/charts and pivot/slicers.
7. Accessibility, packaging, fuzzing, performance budgets and release hardening.

## Not implemented yet

- Color scales, data bars, icon sets and conditional-format rule-manager UI.
- Data validation and tables/structured references.
- Dynamic arrays and complete Excel-compatible function surface.
- External XLSX compatibility corpus from multiple spreadsheet generators.
- Topology-changing unknown-part preservation.
- Complete themes, named styles and Excel format-code semantics.
- AutoFilter/filter UI, advanced sort, printing, preview and PDF export.
- First-class charts, pivot, slicers, collaboration and macro/query engines.
- Full accessibility/designer/NuGet/security/performance/release gates.

## Validation policy

- `NeraSpreadSheet.Core.slnx` must restore, build and test on cross-platform CI.
- `NeraSpreadSheet.slnx` must restore/build on Windows and all tests must pass.
- Architecture verification and Windows desktop GPU/runtime smoke are mandatory.
- MAUI changes require real platform builds; production lifecycle/input/scale claims require loaded native Windows gates.
- Conditional formatting must prove evaluator priority/stop behavior, snapshot isolation, renderer output, dependent-range invalidation, structural history, uniform-reorder proof, schema-valid XLSX, malformed-input rejection and opaque repeated saves.
- Unknown-part preservation must retain opaque bytes, URI, relationship ID/type, content type and unowned markup across repeated saves.
- PR #1 remains Draft and must not merge while exact-head CI is red or unknown.

## Latest validated implementation milestone

CI run #476 (`32372708251`) passed at implementation commit `58ed4a1c440b22bc75f8b3add40a3ba988a50517` on August 20, 2026.

- Core restore/build/tests and architecture verification passed, including conditional model, evaluator, renderer, dirty invalidation, structural mapping and controller history gates.
- Standard `dxfs/cfRule/formula` round-trip, multiple ranges, malformed priority/`dxfId`, schema validation and opaque repeated-save tests passed.
- Existing sparse styles, shared formulas, package graph hardening and unknown-part preservation remained green.
- Full Windows build/tests and desktop GPU runtime smoke passed.
- MAUI Android, iOS and Mac Catalyst real-target builds passed.
- MAUI Windows build/tests and both loaded runtime smokes passed.

The PR remains Draft and has not been merged into `develop`.

## Independence rule

Excel, LibreOffice and DevExpress may be used only as external behavior/coverage references. Their runtime engines, command IDs and public types are not NeraSpreadSheet dependencies.
