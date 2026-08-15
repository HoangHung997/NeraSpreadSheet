# NeraSpreadSheet feature matrix

This is Nera's own capability map. External spreadsheet products are comparison targets only.

| Area | Current milestone | Next implementation |
|---|---|---|
| Workbook / sparse cells | Implemented foundation | bulk mutation, names, tables |
| Selection | Single, extended, multi-range | row/column/full-sheet selection, keyboard navigation |
| Undo / Redo | Generic operation stack | workbook edit operations and transactions |
| Formula | Parser, arithmetic, comparisons, ranges, SUM/AVERAGE/MIN/MAX/COUNT/IF | more functions, optimized dirty recalculation |
| Formula dependencies | Range dependency graph | spatial index and affected-only recalc |
| Pixel scrolling | Implemented foundation | inertia tuning, frame scheduler integration |
| Layout | Sparse row/column metric index | merged cells, freeze/split panes |
| Rendering | Spreadsheet display-list composer | style resolution, tile cache, dirty compositor |
| Direct2D / DirectWrite | Contract only | device, surface, glyph cache, composition |
| Skia GPU | Contract only | GPU surface and MAUI handler |
| XLSX | Contract only | round-trip import/export and preservation layer |
| Commands | Registry, state query, dispatcher | built-in spreadsheet command handlers |
| Ribbon / Bars / DataGrid | Schema foundations | platform presenters and design-time tooling |
