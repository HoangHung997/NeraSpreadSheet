# NeraSpreadSheet current implementation status

This file is the handoff source of truth for the current development branch. It intentionally distinguishes implemented behavior from future architecture.

## Implemented

### Core and workbook
- Sparse worksheet storage over the Excel-size logical address space.
- Cell values, formulas, styles, worksheet dimensions and snapshots.
- Multiple worksheets, rename/remove/add.
- Native merged-cell ranges with overlap protection.
- Workbook-owned immutable style interning (`StyleId`).

### Formula and recalculation
- Tokenizer, parser and AST.
- Arithmetic, comparison, concatenation, references and ranges.
- Basic cross-sheet references.
- SUM, AVERAGE, MIN, MAX, COUNT and IF.
- Dependency graph, circular-reference detection and affected-only recalculation.

### Editing and commands
- Session-owned selection, history, calculation, clipboard, style, merge, sort and editor controllers.
- Undo/redo for cell edits, paste, formatting, merge/unmerge and sort.
- Native Nera command IDs; no UNO/Excel command identifiers.
- Clear contents, recalculate, copy/cut/paste, bold/italic, merge/unmerge, sort ascending/descending.
- Relative/absolute formula translation for native paste.
- TSV/quoted-text clipboard import/export adapter independent of other spreadsheet products.

### Selection and viewport
- Single, extended and multi-range selection.
- Fractional pixel scrolling with `double` offsets.
- Sparse row/column metric index.
- Pixel hit-testing and content extent.
- Merged-cell hit-test and editor bounds resolve to the merged region/top-left cell.

### Rendering and desktop hosts
- Shared display-list composition.
- Visible cells only; no UI control per cell.
- Grid, text, selection, fill, font and border rendering.
- Merged cells render as one visual cell and suppress internal grid lines through merged-area repaint.
- WPF fallback display-list executor.
- WinForms fallback display-list executor.
- One reusable in-cell text editor overlay per host.
- F2, double-click and direct typing edit entry; Enter/Tab commit; Esc cancel.
- Desktop shortcuts include Ctrl+Z/Y/C/X/V/B/I.

### XLSX adapter
- Basic cell values and formulas/cached values.
- Multiple worksheets.
- Row heights and column widths.
- Merged-cell import/export.
- Unknown-part preservation is explicitly unsupported rather than silently claimed.

### Samples
- `samples/NeraSpreadSheet.Wpf.Sample`
- `samples/NeraSpreadSheet.WinForms.Sample`

Both samples exercise formulas, style interning, merged cells, in-cell editing and XLSX open/save. They are included in the full Windows solution so Windows CI compiles them.

## Implemented but still intentionally basic
- Number formatting uses the current .NET formatting bridge; it is not a complete Excel-format-code engine.
- Sort is an in-memory range sort with a materialization safety limit; merged ranges are rejected.
- TSV clipboard is an interoperability fallback. Native Nera clipboard remains the high-fidelity internal format.
- WPF/WinForms renderers are functional fallback renderers, not the final GPU path.

## Not implemented yet
- Actual Direct2D device/surface/composition backend and DirectWrite glyph/text cache.
- Tile cache and dirty-region compositor.
- Actual Skia GPU surface and MAUI handler/touch interaction.
- Freeze/split panes.
- Row/column headers and full-row/full-column selection UI.
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
- The PR stays Draft and must not be merged while the latest-head CI is red or unknown.
- Direct2D/Skia/advanced XLSX features must not be marked implemented until there is executable code plus tests/benchmarks.

## Independence rule
NeraSpreadSheet is a native independent spreadsheet SDK. Excel, LibreOffice and DevExpress may be used as external behavior/coverage references only. Their command identifiers, public types and runtime engines are not part of Nera's Core contracts.
