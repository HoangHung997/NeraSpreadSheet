# Dynamic Arrays Foundation contract

This document defines the validated first-generation dynamic-array behavior of NeraSpreadSheet. It is a Nera-owned contract. Excel and LibreOffice are compatibility references only and are not runtime dependencies.

## 1. Architecture boundary

- `FormulaArrayValue` is the immutable rectangular, row-major array result owned by Core.
- `NeraDynamicArrayFormulaEngine` evaluates supported array-producing functions and returns shape, values, errors and dependencies.
- `DynamicArrayAwareFormulaEngine` preserves scalar compatibility by exposing an array owner's top-left value to existing scalar consumers.
- `DynamicArrayWorkbookCalculationEngine` coordinates scalar calculation, spill materialization, dependency updates and bounded stabilization.
- Worksheet spill ownership is recorded separately from the sparse cell model; materialized children remain derived values, not independent formulas.
- WPF, WinForms and MAUI consume ordinary worksheet snapshots and must not implement spill semantics.
- OpenXml-specific behavior remains inside `NeraSpreadSheet.OpenXml`.

The formula subsystem now recognizes the 104 Formula Surface I names plus five dynamic-array names: `SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT` and `UNIQUE`.

## 2. Array value and safety limits

`FormulaArrayValue` requires a non-empty rectangular shape and stores values in row-major order.

- Row and column counts must be positive.
- Shape and supplied value count must match exactly.
- One array is limited to 1,000,000 cells.
- Values are immutable from the caller's perspective.
- `ToArray()` returns a detached copy.
- Transposition returns a new immutable array.

An invalid shape returns a function error or is rejected at the Core construction boundary; it is never partially materialized.

## 3. Spill ownership

A spill has exactly one owner: the top-left formula cell. `FormulaSpillRange` records:

- owner address;
- complete rectangular spill range;
- immutable array values;
- row and column counts.

Worksheet and immutable snapshot APIs can resolve:

- a spill by owner;
- the owner for any materialized cell;
- whether an address is a spill child;
- the current list/count of spills.

Only the owner contains the formula. Spill children contain derived values and may retain direct styles.

## 4. Collision and `#SPILL!` policy

Spill application performs complete preflight before mutation. A spill is blocked by:

- a non-blank value in a target child;
- another formula in a target child;
- another spill owner or child;
- any intersecting merged range;
- any intersecting Table;
- worksheet row/column bounds.

Direct style-only cells do not block a spill. Existing direct child styles are preserved when values are materialized, resized or cleared.

Blocked materialization leaves blocking cells unchanged and commits `#SPILL!` to the owner while retaining the owner formula. `FormulaErrorCode.Spill` maps to `#SPILL!`.

## 5. Atomic materialization and replacement

Spill replacement is one worksheet transaction:

1. validate owner and target shape;
2. preflight the complete target range;
3. clear obsolete derived children while preserving styles;
4. materialize the new owner value and children;
5. replace ownership metadata.

A failed preflight does not partially write the new array. Direct external mutation of a child invalidates ownership; unchanged derived siblings are cleared so stale output is not presented as live spill data.

## 6. Calculation and dependencies

Dynamic formulas participate in the existing dependency graph.

- Scalar arguments and referenced ranges are recorded as dependencies.
- Spill output changes trigger dependent-only recalculation.
- During that pass, committed spill values are frozen so dependents consume the owner/children already written, including `#SPILL!`, rather than recursively re-evaluating the owner as a scalar formula.
- Source edits may resize a spill and update formulas that depend on the spill range.
- Blocked spills can recover after a blocker is cleared.
- Stabilization is bounded to eight passes; failure to stabilize is reported rather than looping indefinitely.

The current graph tracks source dependencies and committed output ranges. The Excel spill-reference operator (`A1#`) is not yet implemented.

## 7. Supported functions

### `SEQUENCE`

`SEQUENCE(rows, [columns], [start], [step])` creates a row-major numeric array.

- `rows` and `columns` must be positive integers;
- defaults are one column, start `1`, step `1`;
- non-finite output or excessive shape returns an error;
- scalar arguments retain dependencies.

### `TRANSPOSE`

`TRANSPOSE(array)` swaps rows and columns. The source may be a range, a cell, a scalar or a supported nested dynamic-array function.

### `FILTER`

`FILTER(array, include, [if_empty])` accepts either:

- a Boolean column vector matching source rows; or
- a Boolean row vector matching source columns.

An incompatible include shape returns `#VALUE!`. No match returns the optional fallback, otherwise `#CALC!`.

### `SORT`

`SORT(array, [sort_index], [sort_order], [by_col])` performs one stable key sort.

- indexes are one-based;
- sort order is `1` or `-1`;
- rows are sorted by default;
- `by_col=TRUE` sorts columns by a selected row.

### `UNIQUE`

`UNIQUE(array, [by_col], [exactly_once])` preserves first-occurrence order.

- rows are compared by default;
- `by_col=TRUE` compares columns;
- `exactly_once=TRUE` retains only sequences occurring once;
- comparison uses Nera `CellValue` sequence equality.

## 8. Editing and history

`SpreadsheetSession` uses dynamic-array-aware calculation.

- A spill child cannot be edited directly as a value or formula.
- Clearing only part of a spill is rejected.
- Clearing the owner clears the complete derived range.
- Undo/Redo restores the owner formula and rematerializes the spill.
- Source edits trigger affected-only recalculation and spill resize.
- Ordinary non-spill editing remains unchanged.

Rejected operations occur before history mutation.

## 9. Structural operations

Before row/column insert/delete/reorder work, derived spill children are removed from the canonical structural state. The transform moves/rewrites owner formulas and ordinary cells, then dynamic calculation rematerializes output at the mapped owner.

Undo/Redo follows the same rule. Derived children are never moved or captured as independent user data. A rejected structural preflight restores/rematerializes any cleared spill without incorrectly advancing the worksheet version.

## 10. Clipboard contract

Copy/cut/paste treats a spill as one owned formula result.

- Copy or cut of a partial spill is rejected.
- Copying a complete spill stores the owner formula once.
- Derived child values/formulas are omitted from the clipboard package.
- Direct child styles may be stored as style-only blank cells.
- Pasting the package into free space creates one new owner and lets calculation regenerate its children.
- Any paste range intersecting an existing spill is rejected before mutation/history.
- Cutting a complete spill clears the owner/output and Undo restores it.
- Ordinary clipboard behavior outside spills is unchanged.

## 11. Snapshot and rendering boundary

`WorksheetSnapshot` captures immutable spill ownership metadata together with materialized cell values. Renderers therefore display normal sparse cells while selection, hit testing and host UI can identify owner/child relationships without consulting mutable worksheet state.

This milestone does not yet add a dedicated spill-border visual or spill-range selection affordance to every host.

## 12. XLSX document boundary

`NeraOpenXmlDocumentSerializer` is the canonical dynamic-array-aware save path.

- the owner formula is retained;
- derived child values/formulas are removed from worksheet XML;
- direct child styles are retained;
- loading followed by Nera recalculation rematerializes the spill;
- package graph validation and existing unknown-part preservation remain in force.

This is a conservative Nera round-trip boundary. Full Microsoft Office dynamic-array extension metadata, cross-producer compatibility and external cached spill conventions remain corpus work.

## 13. Deliberately pending

- Spill-reference syntax such as `A1#`.
- Implicit-intersection `@` semantics.
- Array constants and arbitrary vectorized binary expressions.
- `SORTBY`, `TAKE`, `DROP`, `CHOOSECOLS`, `CHOOSEROWS`, `TOCOL`, `TOROW`, `WRAPROWS`, `WRAPCOLS`, `HSTACK` and `VSTACK`.
- `LET`, `LAMBDA`, `MAP`, `REDUCE`, `SCAN`, `MAKEARRAY`, `BYROW` and `BYCOL`.
- Multiple-key sort, locale-specific comparison and complete Excel coercion rules.
- Dedicated spill-border/selection UX on every host.
- Full Excel/LibreOffice dynamic-array XLSX metadata compatibility.
- Large-array target-hardware memory/latency budgets and fuzzing.

## 14. Validation gates

Promotion requires the exact head to pass:

1. immutable array shape/limit tests;
2. spill ownership, replacement, collision and `#SPILL!` tests;
3. dynamic function result/dependency tests;
4. affected recalculation and stabilization tests;
5. session editing and Undo/Redo tests;
6. structural insert/delete/reorder history tests;
7. clipboard copy/cut/paste ownership tests;
8. immutable snapshot tests;
9. XLSX document save/load/recalculation tests;
10. existing Core, architecture, Windows, Android, iOS, Mac Catalyst and MAUI Windows matrix.

PR #1 remains Draft while a newer exact-head CI is red or unknown.
