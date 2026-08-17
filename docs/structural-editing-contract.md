# Structural row and column editing contract

This document defines the correctness contract for native row/column insertion and deletion in NeraSpreadSheet. It is an implementation and QA companion to `docs/current-status.md`.

## Scope

The structural controller exposes four native operations:

- Insert rows.
- Delete rows.
- Insert columns.
- Delete columns.

Operations apply to the active worksheet and are executed through the shared edit-history model so they participate in undo/redo. Header-driven commands may select the axis from a whole-column or whole-row selection, while direct APIs accept an explicit zero-based index and positive count.

## Atomicity

A structural operation is all-or-nothing.

Before mutation, the operation captures:

- Sparse cells and formulas on the changed worksheet.
- Row-height and column-width overrides.
- Merged ranges.
- Formula cells on other worksheets that may be rewritten.
- Active cell, anchor and all selection ranges.
- Frozen-row and frozen-column boundaries.

If any stage fails, including an insertion that would move data beyond the logical worksheet limit, the captured state is restored before the exception is surfaced. A failed command must not leave partially shifted cells, dimensions, merges, formulas, selection or freeze state.

## Cell and formula movement

Cells at or after an insertion boundary move forward by the inserted count. Cells inside a deleted interval are removed and cells after it move backward.

Formula cells move with their owning cells. References are then rewritten across the workbook:

- Relative and absolute A1 references both follow structural movement; `$` affects copy translation, not structural movement.
- Same-sheet references are rewritten when the formula belongs to the changed worksheet.
- Qualified references on other worksheets are rewritten when they target the changed worksheet.
- Ranges expand, shrink or move according to the changed interval.
- A reference or range deleted in full becomes `#REF!`.
- String literals are never rewritten.
- Quoted worksheet qualifiers retain their original spelling and escaping.

The formula tokenizer/parser accepts spreadsheet error literals such as `#REF!`, so recalculation after a valid structural deletion must not fail merely because a deleted reference is represented as an error literal.

## Merged ranges

Merged ranges are transformed with the same structural interval:

- Insertion before a merge moves it.
- Insertion inside a merge expands it.
- Deletion before a merge moves it.
- Partial deletion shrinks it when a valid multi-cell merge remains.
- Full deletion removes it.
- The result must never contain overlapping or one-cell pseudo-merges.

## Dimensions

Sparse row-height and column-width overrides move with their corresponding logical rows or columns. Overrides inside a deleted interval are discarded. Default sizes remain unchanged.

## Selection and active cell

The active cell, anchor and every selection range are mapped through the structural change. Whole-row, whole-column and whole-sheet selections preserve their whole-axis semantics instead of being materialized into individual cells.

When deletion removes the active or anchor coordinate, it moves to the nearest surviving coordinate at the deletion boundary. Selection restoration publishes a normal selection-change event so desktop hosts repaint immediately.

## Freeze panes

Freeze boundaries are mapped as logical boundaries:

- Insertion before a boundary increases the frozen count.
- Deletion before a boundary decreases it.
- Deletion crossing a boundary clamps it to the surviving boundary.
- Changes on one axis do not alter the other axis.

After mapping, normal merged-boundary validation still applies.

## Undo and redo

Undo restores the exact captured worksheet, external formula cells, selection and freeze state. Redo reapplies the same structural change from the restored state. Recalculation runs after successful structural execution and after session-level undo/redo.

## Logical limits and validation

Rows and columns remain bounded by `SpreadsheetLimits.MaxRows` and `SpreadsheetLimits.MaxColumns`. Index and count must describe a non-empty interval inside the selected axis. Insertions that would push any used cell, dimension override or merged range beyond the logical limit are rejected atomically.

## Required regression coverage

CI must cover at least:

- Row and column insertion/deletion with sparse cells.
- Dimension override movement.
- Merge move/expand/shrink/removal.
- Same-sheet and cross-sheet formula rewriting.
- Absolute references and ranges.
- `#REF!` generation and recalculation.
- Selection and freeze-boundary mapping.
- Undo and redo.
- Overflow rejection with complete rollback.

## Current limitations

This contract does not claim complete Excel structural semantics for tables, conditional formatting, validation, drawings, charts, named ranges, shared formulas, external links or unknown XLSX parts. Those features require dedicated model support and round-trip tests before structural edits may rewrite them.
