# Dynamic Arrays Foundation contract

This document defines the validated first-generation dynamic-array behavior of NeraSpreadSheet. Excel and LibreOffice are compatibility references only and are not runtime dependencies.

## 1. Architecture boundary

- `FormulaArrayValue` is the immutable rectangular row-major array result owned by Core.
- `NeraDynamicArrayFormulaEngine` evaluates supported array-producing functions and returns shape, values, errors and dependencies.
- `DynamicArrayAwareFormulaEngine` preserves scalar compatibility by exposing an array owner's top-left value.
- `DynamicArrayWorkbookCalculationEngine` coordinates scalar calculation, spill materialization, dependency updates and bounded stabilization.
- Worksheet spill ownership is recorded separately from the sparse cell model; children are derived output, not independent formulas.
- WPF, WinForms and MAUI consume ordinary worksheet snapshots and do not implement spill semantics.
- OpenXml-specific behavior remains inside `NeraSpreadSheet.OpenXml`.
- The versioned extension SDK currently supports scalar-return plugins only under its default policy; extension-array returns are not yet connected to spill ownership.

The built-in formula subsystem currently recognizes 110 scalar/reference names and five dynamic-array names, for 115 built-in names total. User-registered extensions are additional.

## 2. Array value and safety limits

`FormulaArrayValue` requires a non-empty rectangular shape and stores values row-major.

- Row and column counts are positive.
- Shape and supplied value count match exactly.
- One array is limited to 1,000,000 cells.
- Values are immutable from the caller's perspective.
- `ToArray()` returns a detached copy.
- Transposition returns a new immutable array.

Invalid shape is rejected or returned as a function error before partial materialization.

## 3. Spill ownership

A spill has one owner: its top-left formula cell. `FormulaSpillRange` records owner, complete range, immutable values and shape.

Worksheet and snapshot APIs resolve:

- spill by owner;
- owner for any materialized cell;
- child status;
- current spills/count.

Only the owner contains the formula. Children contain derived values and may retain direct styles.

## 4. Collision and `#SPILL!`

Spill application preflights the complete target. A spill is blocked by:

- a non-blank child target;
- another formula;
- another spill;
- merged ranges;
- Tables;
- worksheet bounds.

Style-only cells do not block a spill. Direct child styles survive materialization, resize and clear.

Blocked materialization leaves blockers unchanged and commits `#SPILL!` to the owner while retaining the formula. `FormulaErrorCode.Spill` maps to `#SPILL!`.

## 5. Atomic materialization

Replacement is one worksheet transaction:

1. validate owner and target shape;
2. preflight target range;
3. clear obsolete derived children while preserving styles;
4. materialize owner value and children;
5. replace ownership metadata.

Direct external child mutation invalidates ownership and clears unchanged stale siblings.

## 6. Calculation and dependencies

- Scalar arguments and source ranges enter the dependency graph.
- Spill output changes trigger dependent-only recalculation.
- During that pass, committed spill values are frozen so dependents read written owner/children, including `#SPILL!`.
- Source edits may resize output.
- Blocked spills recover after blocker removal.
- Stabilization is bounded to eight passes.

The Excel spill-reference operator `A1#` is not implemented.

## 7. Supported functions

### `SEQUENCE(rows, [columns], [start], [step])`

Creates a row-major numeric array. Dimensions are positive integers; defaults are one column, start `1`, step `1`. Excessive/non-finite output returns an error.

### `TRANSPOSE(array)`

Swaps rows and columns. Source may be a range, cell, scalar or supported nested dynamic function.

### `FILTER(array, include, [if_empty])`

Accepts a Boolean column vector matching source rows or Boolean row vector matching source columns. Shape mismatch returns `#VALUE!`; no match returns fallback or `#CALC!`.

### `SORT(array, [sort_index], [sort_order], [by_col])`

Performs one stable row/column key sort. Indexes are one-based and order is `1` or `-1`.

### `UNIQUE(array, [by_col], [exactly_once])`

Preserves first occurrence. Rows compare by default; `by_col=TRUE` compares columns; `exactly_once=TRUE` retains only sequences occurring once.

## 8. Editing and history

- Spill children cannot be edited directly as values/formulas.
- Clearing part of a spill is rejected.
- Clearing the owner clears complete derived output.
- Undo/Redo restores the owner formula and rematerializes output.
- Source edits trigger affected recalculation and resize.
- Ordinary non-spill editing is unchanged.

Rejected operations occur before history mutation.

## 9. Structural operations

Before row/column insert/delete/reorder, derived children are removed from canonical structural state. The transform moves/rewrites owner formulas and ordinary data, then dynamic calculation rematerializes output at mapped owners.

Undo/Redo uses the same rule. Rejected structural preflight restores/rematerializes any cleared spill without false worksheet-version advancement.

## 10. Clipboard

- Partial spill copy/cut is rejected.
- Complete spill copy stores the owner formula once.
- Derived child values/formulas are omitted.
- Direct child styles may be stored as blank style-only cells.
- Pasting into free space creates one new owner and regenerates children.
- Paste intersecting an existing spill is rejected before history.
- Complete spill cut clears output and Undo restores it.

## 11. Snapshot and rendering boundary

`WorksheetSnapshot` captures immutable spill ownership metadata with materialized values. Renderers display normal sparse cells while selection/hit-test/UI can identify owner/children without reading mutable worksheet state.

Dedicated spill-border and spill-range selection UX is not yet implemented on every host.

## 12. XLSX document boundary

`NeraOpenXmlDocumentSerializer` is the dynamic-array-aware save path.

- owner formula retained;
- derived child values/formulas removed;
- direct child styles retained;
- load followed by Nera recalculation rematerializes output;
- package graph validation and unknown-part preservation remain active.

Full Microsoft Office dynamic-array extension metadata and producer-specific cached-spill conventions remain corpus work.

## 13. Function SDK boundary

`FormulaFunctionCapabilities` includes array arguments/returns, and `FormulaFunctionArgument` can carry a `FormulaArrayValue`. However, the default v1 extension registry rejects array capabilities. Array-returning plugins will be enabled only after versioned invocation, spill ownership, dependency behavior and package metadata are integrated end to end.

Full SDK contract: `docs/function-extension-sdk-contract.md`.

## 14. Deliberately pending

- `A1#` and `@` syntax;
- array constants and vectorized ordinary operators;
- `SORTBY`, `TAKE`, `DROP`, `CHOOSECOLS`, `CHOOSEROWS`, `TOCOL`, `TOROW`, `HSTACK`, `VSTACK` and wrap helpers;
- `LET`, `LAMBDA`, `MAP`, `REDUCE`, `SCAN`, `MAKEARRAY`, `BYROW`, `BYCOL`;
- multiple-key sort, locale-specific comparison and complete Excel coercion;
- native spill-border/selection UX;
- full Office extension metadata and external workbook corpus;
- large-array target-hardware budgets and fuzzing;
- array-returning function extensions.

## 15. Validation gates

Promotion requires:

1. immutable array shape/limit tests;
2. ownership/replacement/collision/`#SPILL!` tests;
3. dynamic function and dependency tests;
4. affected recalculation/stabilization tests;
5. editing/Undo/Redo tests;
6. structural history tests;
7. clipboard ownership tests;
8. immutable snapshot tests;
9. XLSX save/load/recalculation tests;
10. function SDK compatibility and existing Core/Windows/MAUI matrix.

PR #1 remains Draft while a newer exact-head CI is red or unknown.
