# NeraSpreadSheet current implementation status

This file is the handoff source of truth for the current development branch. It intentionally distinguishes implemented behavior from future architecture.

## Implemented

### Core and workbook
- Sparse worksheet storage over an Excel-size logical address space.
- Cell values, formulas, styles, worksheet dimensions and versioned snapshots.
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
- Fractional pixel scrolling with `double` offsets; no row/column snapping.
- Sparse row/column metric index.
- Pixel hit-testing and content extent.
- Merged-cell hit-test and editor bounds resolve to the merged region/top-left cell.
- Worksheet snapshot cache keyed by worksheet/dimension versions so pure scroll frames do not recopy all sparse cells.
- Translated viewport tile cache: 256-pixel scroll tiles, bounded LRU entries and double-precision translation inside a tile.
- Allocation regression test verifies cached fractional scrolling allocates less than full recomposition.
- BenchmarkDotNet viewport benchmark compares repeated full composition with cached tile scrolling.

### Rendering and desktop hosts
- Shared display-list composition; visible cells only; no UI control per cell.
- Grid, text, selection, fill, font and border rendering.
- Merged cells render as one visual cell and suppress internal grid lines.
- Display-list clip and translation stacks shared by WPF, GDI+ and Direct2D executors.
- WPF fallback display-list executor.
- WinForms GDI+ fallback display-list executor.
- Executable Windows Direct2D/DirectWrite HWND renderer using Vortice.
- WinForms selectable rendering backend: `GdiPlus` or `Direct2D`; GDI+ remains the conservative default while GPU runtime coverage grows.
- Direct2D brush and text-format caches.
- Bounded DirectWrite `IDWriteTextLayout` LRU cache with hit/miss/eviction diagnostics.
- Direct2D retained-content presentation and WinForms dirty-region repaint for cell changes; resize/scroll/dimension changes still request a full redraw when required.
- One-shot Direct2D recovery: native rendering failure recreates target-dependent resources and retries one frame; a second failure is surfaced.
- Renderer diagnostics expose surface size, text-layout cache metrics and recovery count.
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

The WinForms sample can toggle the Direct2D path. Both samples exercise formulas, style interning, merged cells, in-cell editing and XLSX open/save. They are included in the full Windows solution so Windows CI compiles them.

## Implemented but intentionally basic
- Number formatting uses the current .NET formatting bridge; it is not a complete Excel-format-code engine.
- Sort is an in-memory range sort with a materialization safety limit; merged ranges are rejected.
- TSV clipboard is an interoperability fallback. Native Nera clipboard remains the high-fidelity internal format.
- The HWND Direct2D backend is a real executable GPU-capable path, but it is not the final Windows composition architecture.
- WPF still uses its functional fallback executor rather than a dedicated GPU composition host.

## Next rendering architecture
The planned higher-end Windows path is a separate D3D11 + DXGI flip-model swap-chain backend feeding a Direct2D device context. It will reuse Nera's display-list semantics/caches instead of duplicating spreadsheet rendering logic. The existing HWND Direct2D renderer remains the stable fallback until that backend is compile-, test- and runtime-verified.

## Not implemented yet
- D3D11/DXGI flip-model Direct2D device-context backend and final WPF GPU composition integration.
- Actual Skia GPU surface and MAUI native handler/touch interaction.
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
- Performance-sensitive caches keep correctness/allocation regression tests and BenchmarkDotNet coverage where practical.
- The PR stays Draft and must not be merged while the latest-head CI is red or unknown.
- GPU/advanced XLSX features are not marked implemented until there is executable code plus CI validation; runtime-only claims require a real runtime smoke test or benchmark.

## Latest validation milestone
CI run #55 for commit `7c1619976448ea2707b89ffed2f0dccb470faab8` passed Core build/tests/architecture verification and the full Windows build/test job.

## Independence rule
NeraSpreadSheet is a native independent spreadsheet SDK. Excel, LibreOffice and DevExpress may be used as external behavior/coverage references only. Their command identifiers, public types and runtime engines are not part of Nera's Core contracts.
