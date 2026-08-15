# NeraSpreadSheet feature matrix

This is Nera's own capability map. External spreadsheet products are comparison targets only.

| Area | Current milestone | Next implementation |
|---|---|---|
| Workbook / sparse cells | Sparse cells and bulk mutation | names, tables, structural row/column edits |
| Selection | Single, extended, multi-range, mouse/keyboard desktop interaction | row/column/full-sheet selection, drag selection |
| Undo / Redo | Generic stack plus undoable cell-edit session | grouped transactions and style/structural operations |
| Formula | Parser, arithmetic, comparisons, ranges, SUM/AVERAGE/MIN/MAX/COUNT/IF | more functions and richer reference syntax |
| Formula dependencies | Direct/transitive graph and affected-only recalc | spatial dependency index and scheduling |
| Pixel scrolling | Fractional offsets, precision input, wheel frame interpolation | inertia tuning and native precision-input adapters |
| Layout | Sparse row/column metric index | merged cells, freeze/split panes |
| Viewport | Shared composition, content extent and pixel hit-test | tile cache and dirty-region compositor |
| Rendering | Visible-cell display list; WPF/WinForms fallback executors | style resolution, DirectWrite text cache |
| Direct2D / DirectWrite | Contract only | device, surface, glyph cache, composition |
| Skia GPU | Contract only | GPU surface and MAUI handler |
| XLSX | Basic cells/formulas/sheets/row heights/column widths round-trip | styles, shared formulas, merged cells, drawings, preservation layer |
| Commands | Registry, state query, dispatcher, Undo/Redo/Clear/Recalculate | copy/paste, formatting, row/column and sheet commands |
| Ribbon / Bars / DataGrid | Schema foundations | platform presenters and design-time tooling |
