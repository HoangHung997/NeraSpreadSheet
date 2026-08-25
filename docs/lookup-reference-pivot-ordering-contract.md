# F011 — Lookup, reference, pivot and ordering contract

F011 adds exactly ten public formula names:

`LOOKUP`, `OFFSET`, `PERCENTOF`, `PIVOTBY`, `ROW`, `ROWS`, `SHEET`, `SHEETS`, `SORTBY`, `TAKE`.

## Architecture

- `LOOKUP` and `PERCENTOF` use the versioned eager registry.
- `OFFSET`, `ROW`, `ROWS`, `SHEET` and `SHEETS` remain AST/reference-aware so reference identity and current workbook metadata are not flattened.
- `PIVOTBY`, `SORTBY` and `TAKE` use the dynamic-array engine and existing spill ownership.
- `IFormulaWorkbookMetadataEvaluationContext` exposes deterministic worksheet count/order without placing formula semantics in WPF, WinForms or MAUI.
- All array outputs remain capped at 1,000,000 cells.

## Behavior

- `LOOKUP` supports vector and array forms with approximate matching and exact range dependencies.
- `OFFSET` supports nested static/CHOOSE/INDIRECT/OFFSET references, truncated offsets, optional height/width, range-aware invocation and dynamic spill.
- `PERCENTOF` is `SUM(subset) / SUM(all)` and returns `#DIV/0!` for a zero total.
- `PIVOTBY` supports row/column grouping, SUM/AVERAGE/COUNT/COUNTA/MIN/MAX/PERCENTOF, headers, grand totals, scalar sorting, filtering and `relative_to` 0–4.
- `ROW` and `ROWS` support current-cell context, static references and dynamic-array shape.
- `SHEET` and `SHEETS` use workbook metadata; `SHEETS` also counts distinct worksheets in a parenthesized reference union.
- `SORTBY` supports stable multi-key ascending/descending row or column ordering.
- `TAKE` supports leading/trailing rows and columns, omitted dimensions and `#CALC!` for a requested zero dimension.

## Conservative limits

- `PIVOTBY` currently accepts one values column, grand-total depths `-1/0/1`, and scalar sort orders `-1/0/1`; multi-level subtotals, vector lambdas and vector sort orders remain pending.
- `SHEETS` does not yet parse Excel 3-D syntax such as `Sheet1:Sheet3!A1`; distinct reference-union sheets are supported.
- `OFFSET` volatility metadata is pending a general engine-owned volatility contract; exact selector/offset/target dependencies are captured.
- Broader Excel/LibreOffice/ODS differential corpora remain a release gate.
