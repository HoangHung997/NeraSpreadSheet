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

### Conditional formatting

- Platform-independent `CellIs` and `Expression` rules support priority, `StopIfTrue`, multiple ranges and differential styles.
- Rules evaluate against immutable snapshots and compose through the shared display-list renderer.
- Rule ranges/formulas participate in structural state, insert/delete/reorder, undo/redo and rollback.
- Standard SpreadsheetML `dxfs`, `dxf`, `conditionalFormatting`, `cfRule` and `formula` round-trip with malformed-input and opaque repeated-save gates.

### Data Validation Core model

- `DataValidationRule` is independent from UI and OpenXml and supports `Whole`, `Decimal`, `List`, `Date`, `Time`, `TextLength` and `Custom`.
- Numeric/date/time/text operators include between/not-between, equal/not-equal and the four ordered comparisons.
- Rules support sparse multiple ranges, stable IDs, formulas, `AllowBlank`, input-message metadata, error-alert metadata, Stop/Warning/Information style and list-dropdown visibility.
- One worksheet cell is owned by at most one validation rule; overlapping ranges within or across rules are rejected atomically.
- Worksheet snapshots deep-copy validation rules; structural state includes them for exact history and rollback.
- Package-controlled rule/range/formula/title/message counts and lengths are bounded.

### Data Validation evaluation and editor gate

- Whole, decimal, date, time and text-length candidates use operator-aware formula thresholds.
- Literal comma-separated lists and same-sheet A1 range lists are supported without materializing the logical worksheet.
- Custom formulas and formula-backed limits use the shared Core A1 translator and Nera formula engine.
- Candidate-value substitution ensures a custom formula sees the value being committed at its target address rather than the previously stored value.
- `AllowBlank` is authoritative: blank is accepted only when enabled, independent of numeric zero coercion.
- Stop alerts reject editor commits; Warning/Information require explicit host acceptance; disabled error alerts permit the commit while leaving the cell diagnosable.
- Input title/message are exposed through the editor controller; validation failure events carry style/title/message metadata.
- Add/remove validation-rule operations and accepted cell edits participate in production undo/redo.

### Data Validation diagnostics and rendering

- Bounded diagnostic scans return invalid cells without storing a materialized invalid-cell index.
- `SpreadsheetDisplayListComposer` outlines invalid visible cells through one shared theme contract; WPF, WinForms and MAUI consume the same result.
- Hosts can disable validation highlighting through `ShowValidationErrors` without disabling the underlying rules.
- Cell or rule mutation conservatively expands dirty regions to validation target ranges, so formula/list dependencies repaint correctly.

### Data Validation structural safety

- Insert/delete maps target ranges and rewrites relative, mixed, absolute and range references through the shared structural rewriter.
- A rule is removed when all of its targets are deleted; undo restores the exact ID, ranges, formulas and metadata.
- Row/column reorder requires each validation range to be one uniform translation. A contiguous but internally permuted image is rejected before any worksheet mutation.
- Transformed rule sets are prevalidated for overlaps before cells, dimensions, merges or styles are replaced.

### Data Validation XLSX interoperability

- Standard SpreadsheetML `dataValidations`, `dataValidation`, `formula1` and `formula2` are imported/exported.
- Type, operator, `sqref`, `allowBlank`, inverse `showDropDown`, input/error visibility, prompt/error text and Stop/Warning/Information style round-trip.
- Generated packages pass `OpenXmlValidator(FileFormatVersions.Office2013)`.
- Duplicate collections, count mismatch, unsupported types/operators/children, invalid `sqref`, bad formula counts and overlapping targets are rejected as `InvalidDataException`.
- `PreserveUnknownParts=true` patches generated `dataValidations` into the preserved worksheet package after normal copy-and-patch, retaining opaque parts and relationship bytes across repeated saves.

### XLSX style fidelity and package preservation

- Values, formulas/cached values, sheets, dimensions, merges, panes and current Nera style semantics round-trip.
- Standard cell/row/column style indexes and a Nera exact sparse-style state part preserve interoperability and no-flattening behavior.
- Unknown-part preservation uses an internal bounded package envelope and atomic copy-and-patch save.
- Nested opaque parts, standard drawing/image relationships, custom XML/properties and package-root relationships retain exact IDs, URIs, content types and bytes across repeated saves.
- Package graph preflight checks part/relationship counts, uniqueness, NCName IDs, absolute relationship types, unsafe URI segments and encoded control/traversal payloads before restoration or destination mutation.

## Implemented but intentionally conservative

- Validation rules do not overlap; Nera does not attempt ambiguous multi-rule-per-cell precedence.
- List validation supports quoted literal lists and same-sheet A1 ranges. Named ranges, external references and cross-sheet list/custom evaluation are not a supported contract yet.
- The Core exposes prompt/error/dropdown metadata, but native rule-manager, popup prompt and dropdown presenter controls are not implemented.
- Programmatic `Worksheet.SetValue/SetCells` intentionally bypass the interactive editor gate; invalid values remain visible through diagnostics and renderer highlighting.
- Data-validation date/time numeric semantics use .NET/OA serial bridging, not the complete Excel date-system compatibility surface.
- Conditional formatting currently supports `CellIs` and `Expression`; color scales, data bars, icon sets and specialized rules are not modeled.
- Imported differential colors require explicit RGB; theme/indexed colors are not semantically converted.
- Conditional and validation formula evaluation currently resolves the active worksheet snapshot; cross-sheet contracts remain unsupported.
- Dependent invalidation is conservative and expands to all conditional/validation target ranges on that worksheet.
- Direct styles remain complete overrides; no second partial-cell inheritance layer is introduced.
- Number formatting uses a .NET bridge rather than a complete Excel format-code engine.
- Sort is in-memory and bounded.
- Unknown-part preservation requires the same worksheet objects and order; topology-changing merges are rejected before destination mutation.
- Package preservation is in-memory and bounded to 512 MiB.
- Hosted CI cannot guarantee physical driver removal, real monitor-to-monitor DPI transitions or every OS-controlled context-loss mode.

## Progress estimate

- Engine/viewport/renderer foundation: approximately `87%`.
- Basic spreadsheet MVP: approximately `73–77%`.
- Complete professional roadmap: approximately `48%`.
- Production release readiness: approximately `24–28%`.

These are weighted engineering estimates, not checkbox counts.

## Next implementation work

1. Tables Core model, stable table/column identity and structural transactions.
2. Structured-reference parsing, dependency tracking and rewrite integration.
3. Standard XLSX table parts, table styles and unknown-part coexistence.
4. AutoFilter model, filter predicates and desktop filter UI.
5. Formula/function surface, dynamic arrays and plugin function SDK.
6. Advanced sorting, grouping and virtualized data.
7. Printing/page layout/PDF, first-class drawings/charts and pivot/slicers.
8. Accessibility, packaging, fuzzing, performance budgets and release hardening.

## Not implemented yet

- Native data-validation rule manager, popup prompt/error presenters, dropdown UI, named-range lists and cross-sheet validation evaluation.
- Color scales, data bars, icon sets and conditional-format rule-manager UI.
- Tables, structured references and AutoFilter/filter UI.
- Dynamic arrays and complete Excel-compatible function surface.
- External XLSX compatibility corpus from multiple spreadsheet generators.
- Topology-changing unknown-part preservation.
- Complete themes, named styles and Excel format-code semantics.
- Advanced sort, printing, preview and PDF export.
- First-class charts, pivot, slicers, collaboration and macro/query engines.
- Full accessibility/designer/NuGet/security/performance/release gates.

## Validation policy

- `NeraSpreadSheet.Core.slnx` must restore, build and test on cross-platform CI.
- `NeraSpreadSheet.slnx` must restore/build on Windows and all tests must pass.
- Architecture verification and Windows desktop GPU/runtime smoke are mandatory.
- MAUI changes require real platform builds; production lifecycle/input/scale claims require loaded native Windows gates.
- Data validation must prove all supported types/operators, candidate-value semantics, blank/error-alert policy, editor history, diagnostics/rendering, structural rollback, schema-valid XLSX, malformed-input rejection and opaque repeated saves.
- Unknown-part preservation must retain opaque bytes, URI, relationship ID/type, content type and unowned markup across repeated saves.
- PR #1 remains Draft and must not merge while exact-head CI is red or unknown.

## Latest validated implementation milestone

CI run #486 (`32392801690`) passed at implementation commit `64682b9d633bfae699832dee5b73ef5646271bad` on August 20, 2026.

- Core restore/build/tests and architecture verification passed, including validation model, evaluator, candidate/blank policy, editor alerts, rule/cell undo, diagnostics, renderer and structural gates.
- Standard `dataValidations/dataValidation/formula1/formula2` round-trip, metadata, malformed input, schema validation and opaque repeated-save tests passed.
- Existing conditional formatting, sparse styles, shared formulas, package graph hardening and unknown-part preservation remained green.
- Full Windows build/tests and desktop GPU runtime smoke passed.
- MAUI Android, iOS and Mac Catalyst real-target builds passed.
- MAUI Windows build/tests and both loaded runtime smokes passed.

The PR remains Draft and has not been merged into `develop`.

## Independence rule

Excel, LibreOffice and DevExpress may be used only as external behavior/coverage references. Their runtime engines, command IDs and public types are not NeraSpreadSheet dependencies.
