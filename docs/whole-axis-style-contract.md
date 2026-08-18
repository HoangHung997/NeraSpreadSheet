# Sparse whole-axis style contract

This document locks the NeraSpreadSheet row, column and whole-sheet formatting model. The implementation must preserve spreadsheet-scale sparsity: formatting an entire logical row, column or worksheet must not create one `CellData` instance per addressed cell.

## 1. Scope

Whole-axis formatting covers:

- one or more complete rows;
- one or more complete columns;
- the complete logical worksheet;
- intersections between row and column formatting;
- explicit direct-cell styles;
- merged cells intersected by a whole-axis selection;
- structural insert/delete and axis reorder;
- exact undo/redo;
- immutable worksheet snapshots and desktop rendering.

Finite rectangular selections continue to use explicit cell styles, subject to the materialization safety limit.

## 2. Sparse representation

A worksheet owns two interval maps:

- row style spans over `[0, SpreadsheetLimits.MaxRows - 1]`;
- column style spans over `[0, SpreadsheetLimits.MaxColumns - 1]`.

Each non-overlapping span stores an ordered list of `WorksheetAxisStyleOperation` values. An operation contains:

- a worksheet-global monotonically increasing sequence;
- a `CellStylePatch` containing only the properties changed by the user action.

Adjacent spans with identical operation sequences are normalized into one span. Empty/default regions remain implicit.

Formatting a whole row, whole column or whole worksheet therefore changes interval metadata only. It does not materialize blank cells.

## 3. Effective style composition

For a cell without an explicit direct style:

1. start from `CellStyle.Default`;
2. obtain the row operations covering the cell;
3. obtain the column operations covering the cell;
4. merge both operation streams by global sequence;
5. apply each patch in sequence order.

Later operations override only the properties they explicitly change. This makes row/column precedence chronological rather than hard-coded.

Example:

```text
1. Row 5 fill = red
2. Column C fill = blue

C5 = blue
D5 = red

3. Row 5 fill = green

C5 = green
D5 = green
```

## 4. Direct-cell style precedence

A non-default `CellData.StyleId` is an explicit complete cell style and overrides inherited row/column style composition.

When a whole-axis formatting action covers a directly styled cell, the action patches that direct style so the user-visible formatting command is not hidden by the direct override. Unchanged direct properties are retained.

Undo restores the exact prior `StyleId`; redo restores the exact resulting `StyleId`.

## 5. Whole-sheet formatting

A whole-sheet selection is stored as one full row-axis span rather than duplicating the same operation in both row and column maps.

This keeps the representation minimal while preserving chronological composition with later row or column operations.

## 6. Finite-range safety

Finite rectangles continue to materialize explicit cell styles because they do not align with a complete logical axis.

`SpreadsheetStyleController` enforces `DefaultMaximumMaterializedCells`. A finite request above that limit is rejected before mutation.

Whole-row, whole-column and whole-sheet selections bypass that limit because they use sparse axis spans.

## 7. Merged ranges

A merged range renders through its top-left anchor.

If a whole-row or whole-column formatting action intersects a merged range but does not contain its anchor coordinate, Nera creates or updates exactly one explicit style at the anchor. This is the minimum state needed to make the merged range reflect the formatting action.

The anchor update:

- retains its value and formula;
- retains existing direct style properties not changed by the patch;
- applies every selected axis patch that intersects the merged rectangle;
- participates in exact undo/redo;
- does not split or otherwise alter the merge.

When the selected axis already contains the anchor, the sparse axis style is sufficient and no extra anchor cell is created unless an explicit direct style already exists.

## 8. Structural insertion

Inserted rows or columns inherit the sparse style operation sequence present at the insertion index before mutation.

Existing axis identities at and after the insertion index shift by `Count`. Shifted style spans are clipped at the fixed worksheet boundary; style metadata must never overflow the logical axis.

Consequences:

- inserting inside a styled span preserves a continuous styled region;
- inserting inside a fully styled worksheet keeps the full worksheet styled;
- inserting at an unstyled index creates unstyled axes;
- no cells are materialized.

## 9. Structural deletion

Deleted axis identities and their style metadata are removed. Later spans shift upward or leftward by `Count`.

New logical rows/columns exposed at the fixed bottom/right boundary are implicit default style.

## 10. Axis reorder

`WorksheetAxisMove` maps style spans through the same fixed-length permutation used by cells, dimensions, merges, selections and split-pane offsets.

A source style span may become several target intervals. The mapped intervals retain the original ordered operation list and are normalized afterward.

No style-specific permutation implementation is allowed outside `WorksheetAxisStyleMap` and `WorksheetAxisMove`.

## 11. Structural snapshots and rollback

`WorksheetStructuralState` includes:

- row style spans;
- column style spans;
- the next global style sequence.

Insert, delete and reorder preflight/rollback therefore restore the exact style state together with cells, dimensions and merged ranges.

## 12. Immutable worksheet snapshots

`WorksheetSnapshot.Capture` deep-copies row and column style spans. Later worksheet mutations cannot alter an existing snapshot.

`WorksheetSnapshot.GetEffectiveStyle` uses the same direct-style and chronological patch semantics as the live worksheet.

Equivalent row/column operation combinations are cached inside the immutable snapshot, so rendering many visible cells in one styled row does not repeatedly compose identical `CellStyle` objects.

## 13. Rendering

`SpreadsheetDisplayListComposer` resolves the effective style for every visible cell address, including blank cells.

Therefore sparse axis formatting can render:

- fills;
- borders;
- font properties;
- alignment and wrapping;
- number-format bridges for populated cells;

without materializing the logical row or column.

All desktop backends consume the same display list and therefore the same style semantics.

## 14. Transaction and history

`SetWorksheetStylesOperation` owns one atomic formatting transaction containing:

- sparse row/column mutations;
- finite explicit-cell mutations;
- directly styled cells covered by an axis action;
- merged anchors requiring an explicit override.

The operation captures exact before/after axis and cell state. Any exception restores the complete before state and the failed operation does not enter history.

Undo/redo return the executed operation to `SpreadsheetSession`, allowing the session to respect calculation impact.

## 15. Calculation neutrality

Style-only operations expose `AffectsCalculation = false`.

Execution, undo and redo of a style-only operation do not invoke the formula calculation engine. Value, formula, dependency and cached result state remain unchanged.

Operations whose calculation impact is unknown remain conservative and trigger recalculation.

## 16. Performance invariants

The following are mandatory:

- whole-row/column/sheet formatting must keep `UsedCellCount` unchanged unless a merged anchor requires one explicit override;
- lookup is logarithmic in the number of style spans plus the short operation sequence for the matching span;
- snapshot style composition is cached by row/column operation identity;
- no loop may iterate all `1,048,576 × 16,384` logical cells;
- structural mapping operates on spans, not logical axis items;
- visible rendering remains bounded by viewport rows, columns and overscan.

BenchmarkDotNet coverage exists for sparse whole-row formatting and repeated snapshot style resolution.

## 17. XLSX boundary

The current basic XLSX serializer does not yet implement the full style table, direct cell style fidelity or row/column style round-trip. Sparse whole-axis style metadata is therefore an in-memory Nera contract in this milestone.

XLSX style serialization must be implemented together with the broader Open XML style-table milestone; it must not silently flatten a whole logical axis into millions of cells.

## 18. Required gates

This milestone is accepted only when all of the following pass:

- row/column chronological composition tests;
- no-materialization tests for whole row, column and sheet;
- direct-cell override and exact undo/redo tests;
- merged-anchor formatting tests;
- insert/delete/reorder mapping and rollback tests;
- fixed-axis boundary clipping/inheritance tests;
- snapshot immutability and style-cache reuse tests;
- renderer tests for blank styled cells and row/column intersections;
- finite-range safety-limit regression tests;
- undo/redo operation-identity and failure-safety tests;
- cross-platform Core build/tests and architecture verification;
- full Windows build/tests and mandatory GPU/runtime smoke.
