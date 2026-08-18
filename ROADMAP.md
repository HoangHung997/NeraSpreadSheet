# NeraSpreadSheet roadmap

`docs/current-status.md` is the implementation source of truth. This roadmap orders the remaining work; a checked item means source, automated tests and applicable runtime gates exist.

## A. Independent spreadsheet engine

- [x] Sparse workbook/worksheet over Excel-size logical dimensions.
- [x] Values, formulas, merges, dimensions and immutable snapshots.
- [x] Selection, clipboard, reusable editor, commands and undo/redo.
- [x] Structural insert/delete with formula mapping and atomic rollback.
- [x] Model-safe row/column reorder with formula identity.
- [x] Sparse whole-row/column/sheet style storage and effective composition.
- [ ] Direct split-view changes as standalone undoable command operations.
- [ ] Sparse hide/group/outline metadata and complete axis property model.

## B. Viewport and desktop rendering

- [x] Continuous pixel scrolling with `double` offsets.
- [x] Freeze panes, split panes and independent pane scrolling.
- [x] Integrated/optional pane-local scrollbars.
- [x] Shared headers, resize, selection, editor and drag reorder.
- [x] Drag-edge auto-scroll for split and unsplit hosts.
- [x] Snapshot/tile caching and split-aware dirty-region projection.
- [x] WPF DrawingContext and D3DImage shared-texture backend.
- [x] WinForms GDI+, Direct2D/DirectWrite HWND and D3D11/DXGI backend.
- [ ] Long-running injected device-loss/front-buffer-loss stress suite.
- [ ] 60/120 Hz 4K target-hardware latency, FPS, memory and power baselines.

## C. Formula engine

- [x] Tokenizer, parser, AST, dependency graph and circular-reference policy.
- [x] Arithmetic, comparison, concatenation, A1 references/ranges and basic cross-sheet references.
- [x] `SUM`, `AVERAGE`, `MIN`, `MAX`, `COUNT` and `IF`.
- [ ] Complete math, text, date/time, lookup, statistical and financial function surface.
- [ ] Dynamic arrays, spill ranges and shared formula contracts.
- [ ] Tables/structured references and formula rewrite integration.
- [ ] Plugin function SDK for domain-specific estimating workflows.

## D. XLSX, printing and interoperability

- [x] Basic values, cached formulas, multiple sheets, dimensions and merged ranges.
- [x] Standard pane metadata plus Nera custom state for four independent pane offsets.
- [ ] Complete style table and direct-cell style round-trip.
- [ ] Sparse row/column style round-trip without logical-axis flattening.
- [ ] Shared formulas, validation, conditional formatting and tables.
- [ ] Drawings, images, charts and unknown-part preservation.
- [ ] Print areas, page setup, page breaks, preview and PDF export.
- [ ] Compatibility corpus and round-trip differential tests.

## E. Data and analysis

- [x] Basic in-memory sort with safety limits.
- [ ] AutoFilter model and desktop filter UI.
- [ ] Advanced/multi-key sort, custom lists and stable large-data path.
- [ ] Tables, subtotals, grouping and outlines.
- [ ] Pivot tables, slicers and calculated fields.
- [ ] External/virtualized data sources and incremental loading.

## F. Cross-platform control suite

- [x] Platform-neutral command, Ribbon Core, Bars Core and DataGrid Core contracts.
- [x] Public WPF and WinForms spreadsheet hosts and samples.
- [ ] Production Skia GPU display-list executor.
- [ ] MAUI native handler, touch selection, pinch zoom and virtual keyboard.
- [ ] Responsive command surface for mobile/tablet.
- [ ] Production Ribbon, toolbar, menu and context-menu controls.
- [ ] Standalone DataGrid control sharing infrastructure but not workbook semantics.
- [ ] Theme, localization, accessibility and designer support.

## G. Product hardening

- [ ] API compatibility and package-version checks.
- [ ] NuGet packaging, symbols and source link.
- [ ] Crash recovery, safe-mode startup and support bundle.
- [ ] Security review and fuzzing for formulas/XLSX/clipboard.
- [ ] Performance budgets enforced in CI.
- [ ] Alpha → Beta → RC → Production release gates.

## Immediate execution order

1. Direct split-view undo/redo commands.
2. Device/front-buffer loss stress coverage.
3. Production Skia GPU and MAUI handler.
4. XLSX style table plus sparse axis-style persistence.
5. Formula/function and data-feature expansion.
6. Printing/PDF, charts/pivot and production hardening.
