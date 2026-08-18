# Header reordering contract

This document locks the first native NeraSpreadSheet row/column reorder semantics. The operation is a fixed-length permutation, not an insert/delete pair and not a clipboard move.

## 1. Scope and terminology

A reorder request is represented by `WorksheetAxisMove`:

- `Axis`: row or column.
- `SourceIndex`: first source row/column in the original worksheet coordinates.
- `Count`: length of the contiguous source interval.
- `DestinationBoundary`: insertion boundary in the original coordinate system, from `0` through the logical axis length.

The source interval is inclusive: `[SourceIndex, SourceIndex + Count - 1]`.

A destination inside `[SourceIndex, SourceEnd + 1]` is a no-op. It neither mutates the worksheet nor creates undo history.

## 2. Permutation semantics

A reorder keeps the logical axis length unchanged.

- No logical row or column is created or destroyed.
- The source interval preserves internal order.
- The intervening interval shifts by exactly `Count` positions.
- Every valid source index maps to exactly one target index.
- Every target index has exactly one source index.
- `InsertionIndex` is the target start after source removal; it equals `DestinationBoundary` for upward/leftward moves and `DestinationBoundary - Count` for downward/rightward moves.

`WorksheetAxisMove.MapIndex`, `MapAddress` and `MapInterval` are the shared mapping source. Hosts and higher layers must not reimplement the permutation independently.

## 3. Sparse workbook mutation

`Worksheet.ApplyAxisMove` transforms only stored state:

- sparse used cells;
- row-height or column-width overrides on the moved axis;
- merged ranges.

Default-sized empty rows/columns remain implicit. A move must not materialize the full logical worksheet.

The operation publishes one affected full-axis band from the minimum source/target index through the maximum source/target index.

## 4. Formula identity

Reordering follows logical cell identity.

- A reference to a moved cell follows that cell to its new address.
- A reference to an intervening shifted cell follows that shifted cell.
- Formulas located on the moved worksheet move with their containing cells and are rewritten from their mapped addresses.
- Formulas on other worksheets keep their own addresses but rewrite references to the moved worksheet.
- `$` absolute markers are formatting/translation markers and remain present; they do not prevent identity mapping during structural reorder.
- Quoted and escaped worksheet names are preserved.
- Text inside string literals is never parsed as a cell reference.

A rectangular A1 range may be rewritten only when its image remains one contiguous interval on the moved axis. If its image would be a union/discontiguous set, preflight rejects the complete reorder with `InvalidOperationException`.

The initial implementation does not synthesize union expressions or rewrite structured/table/shared/dynamic-array references.

## 5. Merged cells and freeze boundaries

Merged ranges must remain one contiguous rectangle and retain normal top-left anchor ordering.

A reorder is rejected before mutation when it would:

- split one merged range into multiple axis intervals;
- reverse the mapped order of a merged range endpoint/anchor;
- move a merged range so it crosses the current frozen-row boundary;
- move a merged range so it crosses the current frozen-column boundary.

Moving a complete merged block is valid. Its anchor cell, stored value/style and dimensions move through the same permutation.

## 6. Selection mapping

The selection is part of the edit transaction.

- Active cell and anchor cell map through `MapAddress`.
- Each selected range maps through `MapInterval` on the moved axis.
- If one selected rectangle maps into multiple disjoint rectangles, all resulting ranges are retained as a multi-range selection.
- A contiguous whole-row or whole-column selection remains an axis selection when the selected block itself moves.
- Undo restores the exact previous selection snapshot; redo restores the exact mapped snapshot.

## 7. Split-pane scroll mapping

`SpreadsheetSplitViewState` remains per worksheet and participates in the reorder transaction.

For every pane, the moved axis offset is mapped by:

1. Resolving the source top-left row/column containing the current pixel offset using exact pre-move sparse metrics.
2. Preserving the local fractional pixel offset inside that row/column.
3. Mapping the row/column identity through `WorksheetAxisMove`.
4. Computing its post-move pixel start using exact mapped sparse metrics.
5. Adding the preserved local offset.

The unaffected axis, split topology, split coordinates and active pane remain unchanged.

This is intentionally different from mapping an offset by a fixed pixel delta; rows and columns may have non-uniform sizes.

## 8. Transaction, rollback and history

`SpreadsheetAxisReorderController` owns the edit transaction.

Preflight captures and validates:

- target worksheet structural state;
- formula cells on other worksheets;
- formula rewrites for the entire workbook;
- merged/freeze safety;
- selection snapshot;
- split-view snapshot and mapped target state.

Only after every preflight step succeeds may mutation start.

Execution order:

1. Apply worksheet axis permutation.
2. Apply local and external formula rewrites.
3. Restore unchanged freeze coordinates after merged-range validation.
4. Publish mapped split state.
5. Restore mapped selection.
6. Recalculate the workbook.

Any exception after mutation begins restores the captured state. A failed operation does not enter undo history.

Undo/redo restore exact pre/post snapshots and trigger workbook recalculation.

## 9. Shared header drag geometry

`SpreadsheetSplitHeaderReorderGeometry` owns platform-neutral source/drop semantics.

- Left-edge panes supply row headers.
- Top-edge panes supply column headers.
- A pointer within resize tolerance of a header edge is not a reorder source.
- Drag activation requires the shared Euclidean movement threshold.
- Drop before/after is selected from the nearest half of the target axis slot.
- Drop target carries the original-coordinate `DestinationBoundary` and the complete `WorksheetAxisMove`.
- A preview line spans the full perpendicular control extent.
- Valid targets use active styling; no-op targets use neutral header-border styling.
- Split separator gaps resolve to the nearest eligible edge pane instead of producing an arbitrary worksheet index.

If the pointer-down header belongs to the sole selected contiguous whole-axis range, the whole range is the source. Otherwise, only the hit row/column is the source.

## 10. Input priority

Desktop hosts apply this priority order:

1. Pane scrollbar interaction.
2. Split separator drag.
3. Row-height/column-width resize handle.
4. Header reorder candidate.
5. Ordinary row/column selection.

A reorder candidate does not capture input immediately. Capture begins only after the drag threshold is crossed, allowing a normal click to remain a selection action.

## 11. WinForms split host

The public WinForms split surface:

- reads `MK_LBUTTON` from the actual Windows message `wParam`;
- uses the existing child HWND and pointer capture;
- keeps resize/scrollbar/separator priority;
- appends preview geometry through `SpreadsheetHeaderReorderPreviewDisplayListComposer`, so GDI+, Direct2D HWND and DXGI share preview semantics;
- commits through `SpreadsheetSession.Reorder` on button release;
- clears capture/preview on cancellation or `WM_CAPTURECHANGED`.

Runtime tests dispatch the actual surface message path for both row and column movement, verify selection/formula identity and exercise undo.

## 12. WPF split host

The public WPF split adorner:

- starts candidates in preview routed input before the ordinary header selection handler;
- captures the mouse only after the shared threshold;
- uses a lightweight `DrawingVisual` preview above both DrawingContext and shared-texture D3DImage content;
- clears state on lost capture;
- commits through `SpreadsheetSession.Reorder` on mouse release.

Runtime coverage uses native OS cursor/button input, verifies the target row and selection/history, switches to D3DImage after the move and requires successful GPU presentation.

## 13. Conservative exclusions

The following are not part of this milestone:

- automatic scrolling while dragging near a viewport edge;
- direct drag integration in the unsplit public WinForms/WPF control paths;
- union-expression generation for discontiguous formula ranges;
- reordering filtered/hidden/outlined axes with special visible-only semantics;
- table/structured-reference, shared-formula or dynamic-array structural rewriting;
- standalone public command arguments or keyboard-only reorder UI.

Programmatic reorder through `SpreadsheetSession.Reorder` is host-independent even while the unsplit drag UI remains pending.

## 14. Required gates

A reorder milestone is accepted only when all of the following pass:

- permutation/index/interval mapping tests;
- sparse cell/dimension and merged-range mutation tests;
- local/cross-sheet formula identity and string-literal tests;
- discontiguous formula-range and merged/freeze atomic rejection tests;
- selection, split-offset, rollback, undo/redo and recalculation tests;
- shared source/drop/threshold/preview geometry tests;
- WinForms real-message row drag runtime smoke;
- WinForms real-message column drag runtime smoke;
- WPF native-pointer row drag and post-move D3DImage runtime smoke;
- full Windows build/tests/GPU gate;
- cross-platform Core build/tests and architecture verification.
