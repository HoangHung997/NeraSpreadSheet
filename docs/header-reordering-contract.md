# Header reordering contract

This document locks NeraSpreadSheet row/column reorder semantics. The operation is a fixed-length permutation, not an insert/delete pair and not a clipboard move.

## 1. Request model

`WorksheetAxisMove` contains:

- `Axis`: row or column.
- `SourceIndex`: first source item in original coordinates.
- `Count`: length of the contiguous source interval.
- `DestinationBoundary`: insertion boundary in original coordinates, from `0` through the logical axis length.

The source interval is inclusive. A destination inside the source interval or at either adjacent boundary is a no-op and creates no history entry.

## 2. Permutation semantics

- Logical axis length does not change.
- No row/column is created or deleted.
- The source interval preserves internal order.
- The intervening interval shifts by exactly `Count` positions.
- Every valid source index maps to exactly one target index and vice versa.
- `WorksheetAxisMove.MapIndex`, `MapAddress` and `MapInterval` are the shared mapping source; hosts must not reimplement them.

## 3. Sparse model mutation

`Worksheet.ApplyAxisMove` transforms only stored state:

- sparse used cells;
- row-height/column-width overrides on the moved axis;
- merged ranges.

Default-sized empty axes remain implicit. The operation publishes one affected full-axis band spanning source and target.

## 4. Formula identity

References follow logical cell identity.

- A reference to a moved cell follows that cell.
- A reference to an intervening shifted cell follows that cell.
- Formulas located on the moved worksheet move with their cells and rewrite from mapped addresses.
- Formulas on other worksheets keep their own addresses but rewrite references to the moved worksheet.
- `$` markers and quoted/escaped sheet names are preserved.
- String literals are never parsed as references.

A rectangular A1 range may be rewritten only if its image remains one contiguous interval on the moved axis. Otherwise preflight rejects the reorder atomically. The implementation does not synthesize union expressions or structured/shared/dynamic-array reference forms.

## 5. Merged cells and freeze boundaries

A reorder is rejected before mutation if it would:

- split one merged range;
- reverse mapped merge endpoint/anchor order;
- move a merged range across the active frozen-row boundary;
- move a merged range across the active frozen-column boundary.

Moving a complete merged block is valid and preserves its anchor value/style and dimensions.

## 6. Selection and split-view mapping

- Active and anchor cells map through `MapAddress`.
- Selection ranges map through `MapInterval`; disjoint images remain a multi-range selection.
- A selected contiguous whole-axis block remains selected after moving.
- Every split-pane offset preserves the identity of its top-left row/column plus the fractional local pixel offset.
- Offset mapping uses exact sparse metrics before and after the move, not a fixed delta.
- The unaffected axis, topology, split coordinates and active pane remain unchanged.
- Undo/redo restores exact pre/post selection and split snapshots.

## 7. Transaction, rollback and history

`SpreadsheetAxisReorderController` owns:

1. worksheet structural snapshot;
2. external formula snapshot;
3. full-workbook formula rewrite preflight;
4. merged/freeze validation;
5. selection mapping;
6. split-offset mapping;
7. atomic execution/rollback;
8. full-workbook recalculation;
9. exact undo/redo.

Any failure after mutation begins restores captured state. A failed operation never enters history.

## 8. Shared header drag geometry

`SpreadsheetSplitHeaderReorderGeometry` owns source/drop/threshold behavior for both split and unsplit hosts.

- Left-edge split panes supply row headers.
- Top-edge split panes supply column headers.
- An unsplit host supplies one synthetic `TopLeft` pane covering its body viewport.
- Resize tolerance excludes edge hits from reorder.
- Pane scrollbars and split separators have higher priority.
- Drag activates only after a shared Euclidean threshold.
- Drop before/after is selected from the nearest half of the target slot.
- Drop carries the original-coordinate `DestinationBoundary` and complete `WorksheetAxisMove`.
- Preview spans the full perpendicular control extent.
- Valid targets use active styling; no-op targets use neutral styling.
- If pointer-down belongs to the sole selected contiguous whole-axis range, the whole range moves; otherwise one row/column moves.

## 9. Desktop input priority

For split hosts:

1. pane scrollbar;
2. split separator;
3. dimension resize;
4. header reorder;
5. ordinary header selection.

For unsplit hosts, dimension resize remains ahead of reorder. A candidate does not capture immediately; capture starts only after threshold crossing.

## 10. WinForms split host

- Reads `MK_LBUTTON` from the actual `wParam`.
- Uses the Nera-owned child HWND and pointer capture.
- Renders preview through `SpreadsheetHeaderReorderPreviewDisplayListComposer`, shared by GDI+, Direct2D HWND and DXGI.
- Commits via `SpreadsheetSession.Reorder` on release.
- Clears state on cancellation or `WM_CAPTURECHANGED`.
- Runtime tests dispatch the actual surface message path for both row and column moves and verify formula identity, selection and undo.

## 11. WPF split host

- Starts candidates from preview routed input before ordinary header selection completes.
- Uses one lightweight `DrawingVisual` preview above DrawingContext/D3DImage content.
- Attempts mouse capture only while the physical left button is pressed.
- A valid transaction is retained if capture is unavailable; ordinary routed moves/releases can still finish while the pointer remains in the host.
- Lost capture while the button is still pressed cancels safely; release-time capture loss is not mistaken for a stolen drag.
- Commits via `SpreadsheetSession.Reorder` on release.

The hosted Windows runner cannot reliably inject global WPF pointer state. Stable CI opens a real WPF `Window` with the public control/controller and drives the production state machine deterministically. Production routed handlers remain the caller of that state machine.

## 12. WinForms unsplit host

The optional public controller is enabled through `EnableHeaderReordering` and removed through `DisableHeaderReordering`.

- It subscribes to the existing public control input events without creating another workbook/viewport model.
- It composes a one-pane layout from the current session and continuous scroll state.
- It uses one hit-transparent preview child, not one control per cell.
- It never starts while a split controller owns the control surface.
- It commits through the same `SpreadsheetSession.Reorder` transaction and preserves selection/formula/undo behavior.

## 13. WPF unsplit host

The optional public controller mirrors the WinForms lifecycle.

- It uses preview routed input and a lightweight preview adorner.
- The preview attaches when an `AdornerLayer` is available; the model operation itself remains independent of visual attachment.
- It never starts while a split controller owns the public control.
- Completion, cancellation, unload and disposal detach preview, capture and frame subscriptions.
- Post-move runtime coverage requires successful rendering through the existing D3DImage path.

## 14. Drag-edge auto-scroll

`SpreadsheetHeaderReorderAutoScroll` is the shared velocity contract.

- It calculates velocity only on the moved axis.
- Velocity is zero outside the configured activation zone.
- Velocity increases quadratically toward the edge and is clamped to a maximum when the pointer leaves the viewport.
- Elapsed time converts velocity to a fractional pixel delta; no row/column snapping is allowed.
- Unsplit hosts use their complete body viewport.
- Split hosts use the current source/target pane bounds and scroll exactly one pane.
- Every scroll step recomposes visible slots and recalculates the drop boundary at the stationary pointer coordinate.
- WinForms uses a drag-owned timer; WPF uses `CompositionTarget.Rendering`.
- Completion, cancellation, unload and disposal must stop the timer/render subscription.

## 15. Conservative exclusions

- union-expression generation for discontiguous formula ranges;
- special visible-only behavior for filtered/hidden/outlined axes;
- structured/table/shared/dynamic-array reference rewriting;
- standalone command arguments or keyboard-only reorder UI.

Programmatic reorder through `SpreadsheetSession.Reorder` is host-independent.

## 16. Required gates

- permutation/index/interval mapping tests;
- sparse cell/dimension and merge mutation tests;
- local/cross-sheet formula identity tests;
- atomic discontiguous-range and merged/freeze rejection tests;
- selection, split-offset, rollback, undo/redo and recalculation tests;
- shared source/drop/threshold/preview geometry tests;
- WinForms real-message split row and column drag runtime smoke;
- WPF loaded-window split state-machine and post-move D3DImage smoke;
- public unsplit WinForms/WPF controller lifecycle, preview, commit and undo smoke;
- platform-neutral auto-scroll velocity/boundary tests;
- unsplit WinForms/WPF edge auto-scroll runtime smoke;
- split WinForms/WPF targeted-pane auto-scroll runtime smoke;
- full Windows build/tests/GPU runtime gate;
- cross-platform Core build/tests and architecture verification.
