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
- Structural insert/delete/reorder with overflow preflight, formula/reference/rule/Table mapping and atomic rollback.
- Formula tokenizer, parser and AST for arithmetic, comparison, concatenation, A1 references/ranges and basic cross-sheet references.
- `SUM`, `AVERAGE`, `MIN`, `MAX`, `COUNT` and `IF`.
- Dependency graph, circular-reference detection and affected-only recalculation.

### Sparse styles and model-safe axis transforms

- `CellStylePatch` stores property-level changes without materializing logical axes.
- Sparse row/column style spans compose by one worksheet-global chronological sequence.
- Direct-cell styles remain complete overrides; whole-axis changes preserve unrelated properties.
- Insert/delete/reorder map cells, dimensions, styles, merged ranges, formulas, rules, Tables, selection and pane offsets through coordinated transactions.
- Unsafe discontiguous range/merge/freeze/rule/Table transforms are rejected instead of silently corrupted.

Full style contract: `docs/whole-axis-style-contract.md`.

### Continuous viewport, split panes and view history

- Fractional pixel scrolling, sparse metric indexes, freeze panes and one/two/four-pane topology.
- Independent per-pane continuous offsets, pane-local scrollbars and per-worksheet persistence.
- Split-aware headers, selection, editor, resize, reorder and dirty-region projection.
- Snapshot/tile caching and a view-history stack isolated from workbook/data undo/redo.
- Table AutoFilter hides rows through compressed index spans rather than per-row cell/control materialization.
- Filtered rows consume no viewport extent, are absent from row slots and are skipped by hit-testing while original row sizes remain recoverable.
- Filter source changes invalidate row-visibility metrics and make rows reappear without flattening worksheet dimensions.

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

### Data Validation

- `DataValidationRule` is independent from UI and OpenXml and supports `Whole`, `Decimal`, `List`, `Date`, `Time`, `TextLength` and `Custom`.
- Numeric/date/time/text operators include between/not-between, equal/not-equal and the four ordered comparisons.
- Rules support sparse multiple ranges, stable IDs, formulas, `AllowBlank`, input-message metadata, error-alert metadata, Stop/Warning/Information style and list-dropdown visibility.
- One worksheet cell is owned by at most one validation rule; overlapping ranges within or across rules are rejected atomically.
- Custom formulas see the candidate being committed rather than the stale stored target value.
- Stop alerts reject editor commits; Warning/Information require explicit host acceptance; disabled alerts still leave invalid cells diagnosable.
- Bounded invalid-cell scans and shared display-list outlines work across WPF, WinForms and MAUI.
- Standard SpreadsheetML `dataValidations`, metadata and formulas round-trip with schema, malformed-input and opaque repeated-save gates.

### Table Core model and structural safety

- `SpreadsheetTable` and `SpreadsheetTableColumn` are platform/document independent and use stable `Guid` identities.
- Table names are unique across a workbook; column names and identities are unique inside each Table without regard to case.
- Tables on one worksheet cannot overlap.
- Header, data and totals ranges are derived from one canonical Table range without materializing cells.
- Table styles, header/totals flags, calculated-column formula metadata, totals formula/label metadata and AutoFilter metadata are immutable/copy-safe.
- Tables participate in worksheet snapshots and structural state.
- Insert/delete maps Table ranges and A1 references; axis reorder requires one uniform Table translation.
- Ambiguous deletion of header/totals semantics, implicit insertion of an unnamed Table column and internally permuted ranges are rejected before mutation.
- `SpreadsheetSession.Tables` provides add/remove, Table rename, column rename and set/clear AutoFilter operations with Undo/Redo.
- Failed duplicate rename restores Table/formula state and does not enter history.

Full contract: `docs/table-structured-reference-contract.md`.

### Structured references and recalculation

- Supported canonical references include `Table[Column]`, `Table[#Data]`, `Table[#All]`, `Table[#Headers]`, `Table[#Totals]`, `Table[#This Row]` and `[@Column]`.
- Structured references are expanded to absolute A1 references before the existing parser/evaluator runs; string literals are not rewritten.
- References to a Table on another worksheet receive a correctly quoted worksheet qualifier.
- `[@Column]` is valid only from a formula row inside the Table data range.
- Expanded A1 ranges enter the existing dependency graph, so affected-only recalculation responds to edits inside Table data columns.
- Table and column rename rewrite explicit references across the workbook and implicit `[@Column]` references only inside the owning Table.
- Table metadata and all rewritten formulas change in one history transaction; Undo/Redo restores both.

### AutoFilter and row projection

- Table AutoFilter supports explicit value sets, blank matching and one or two numeric/text comparison conditions combined with AND or OR.
- Multiple filtered columns combine by AND at the row level.
- Row visibility is evaluated against an immutable worksheet snapshot and bounded by explicit safety limits.
- Adjacent filtered rows are compressed into `FilteredRowSpan` values and then into `AxisIndexRange` metrics.
- Hidden spans preserve sparse size overrides, reduce total extent, skip layout slots and maintain continuous pixel scrolling.
- Filter changes participate in production history; Undo/Redo immediately updates row visibility.

### Standard XLSX Table interoperability

- Worksheet `tableParts/tablePart` relationships and standard `TableDefinitionPart` XML are imported/exported.
- Table name/display name, range, header/totals state, columns, calculated/totals formula metadata, totals labels and `tableStyleInfo` round-trip.
- Nera-generated packages encode stable Table IDs in relationship IDs and stable column IDs in `tableColumn@uniqueName`.
- Foreign packages without Nera IDs receive deterministic fallback identities derived from package/Table metadata.
- AutoFilter value sets, blank-only filters and one/two custom comparison filters round-trip.
- Generated packages pass `OpenXmlValidator(FileFormatVersions.Office2013)`.
- Duplicate/missing relationships, unreferenced Table parts, count mismatch, invalid/reversed ranges, bad/duplicate column IDs, invalid filter indexes and unsupported Table/filter markup are rejected as `InvalidDataException`.
- `PreserveUnknownParts=true` refreshes owned Table parts/relationships and retains unowned worksheet markup plus Table `extLst` payloads across consecutive saves.

### XLSX style fidelity and package preservation

- Values, formulas/cached values, sheets, dimensions, merges, panes and current Nera style semantics round-trip.
- Standard cell/row/column style indexes and a Nera exact sparse-style state part preserve interoperability and no-flattening behavior.
- Unknown-part preservation uses an internal bounded package envelope and atomic copy-and-patch save.
- Nested opaque parts, standard drawing/image relationships, custom XML/properties and package-root relationships retain exact IDs, URIs, content types and bytes across repeated saves.
- Package graph preflight checks part/relationship counts, uniqueness, NCName IDs, absolute relationship types, unsafe URI segments and encoded control/traversal payloads before restoration or destination mutation.

## Implemented but intentionally conservative

- Calculated-column and totals formula metadata round-trip, but Nera does not yet auto-fill calculated formulas into every data row or execute totals metadata automatically.
- AutoFilter currently supports value/blank and comparison conditions; rich date/text/top/bottom/color/icon filters are not modeled.
- The Core exposes Table/filter operations, but native WPF/WinForms/MAUI Table manager and filter dropdown presenters are not implemented.
- Direct worksheet AutoFilter outside a Table is not a supported model yet.
- Foreign Table relationship IDs are deterministically mapped to Nera identities; exact original relationship IDs are not promised after a normal semantic rewrite.
- Validation rules do not overlap; Nera does not attempt ambiguous multi-rule-per-cell precedence.
- List validation supports quoted literal lists and same-sheet A1 ranges. Named ranges, external references and cross-sheet list/custom evaluation are not a supported contract yet.
- The Core exposes validation prompt/error/dropdown metadata, but native rule-manager, popup prompt and dropdown presenter controls are not implemented.
- Programmatic `Worksheet.SetValue/SetCells` intentionally bypass the interactive validation gate; invalid values remain visible through diagnostics and renderer highlighting.
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

- Engine/viewport/renderer foundation: approximately `88%`.
- Basic spreadsheet MVP: approximately `76–80%`.
- Complete professional roadmap: approximately `50–51%`.
- Production release readiness: approximately `26–30%`.

These are weighted engineering estimates, not checkbox counts.

## Next implementation work

1. Calculated-column propagation and totals-row execution.
2. Native Table manager and AutoFilter dropdown/predicate UX for desktop and MAUI.
3. External XLSX Table/AutoFilter compatibility corpus and differential tests.
4. Formula/function surface, dynamic arrays and plugin function SDK.
5. Advanced sorting, grouping and virtualized data.
6. Printing/page layout/PDF, first-class drawings/charts and pivot/slicers.
7. Accessibility, packaging, fuzzing, performance budgets and release hardening.

## Not implemented yet

- Automatic calculated-column fill, totals metadata execution and rich structured-reference grammar.
- Native Table/filter presenter UI and direct worksheet AutoFilter outside Tables.
- Native data-validation rule manager, popup prompt/error presenters, dropdown UI, named-range lists and cross-sheet validation evaluation.
- Color scales, data bars, icon sets and conditional-format rule-manager UI.
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
- Table work must prove stable identity, workbook-wide naming, structural/history atomicity, structured-reference dependencies, compressed row projection, schema-valid standard Table parts, malformed-input rejection and repeated preservation.
- Data validation must prove all supported types/operators, candidate-value semantics, blank/error-alert policy, editor history, diagnostics/rendering, structural rollback, schema-valid XLSX, malformed-input rejection and opaque repeated saves.
- Unknown-part preservation must retain opaque bytes, URI, relationship ID/type, content type and unowned markup across repeated saves.
- PR #1 remains Draft and must not merge while exact-head CI is red or unknown.

## Latest validated implementation milestone

CI run #503 (`32440549596`) passed at implementation commit `f1c899554343aa49dee072be10145554eb86e371` on August 21, 2026.

- Core restore/build/tests and architecture verification passed, including Table model, structural mapping, controller history, structured-reference dependency/recalculation and compressed filter-row projection.
- Standard Table parts, styles, calculated/totals metadata, value/blank/custom filters, malformed input, schema validation and Table `extLst` repeated-preservation tests passed.
- Existing Data Validation, Conditional Formatting, sparse styles, shared formulas, package graph hardening and unknown-part preservation remained green.
- Full Windows build/tests and desktop GPU runtime smoke passed.
- MAUI Android, iOS and Mac Catalyst real-target builds passed.
- MAUI Windows build/tests and both loaded runtime smokes passed.

The PR remains Draft and has not been merged into `develop`.

## Independence rule

Excel, LibreOffice and DevExpress may be used only as external behavior/coverage references. Their runtime engines, command IDs and public types are not NeraSpreadSheet dependencies.
