# Dynamic Arrays Foundation contract

This document defines the validated dynamic-array behavior of NeraSpreadSheet. Excel and LibreOffice are compatibility references only and are not runtime dependencies.

## 1. Architecture boundary

- `FormulaArrayValue` is the immutable rectangular row-major array result owned by Core.
- `NeraDynamicArrayFormulaEngine` evaluates supported array functions and returns shape, values, errors and dependencies.
- `DynamicArrayAwareFormulaEngine` exposes an owner's top-left value to scalar consumers.
- `DynamicArrayWorkbookCalculationEngine` coordinates scalar calculation, spill materialization, dependencies and bounded stabilization.
- Worksheet spill ownership is separate from sparse cell storage; children are derived output.
- WPF, WinForms and MAUI consume snapshots and do not implement spill semantics.
- OpenXml behavior remains inside `NeraSpreadSheet.OpenXml`.
- SDK array capabilities exist in metadata but are rejected by the default v1 registry until plugin-array spill integration exists.

The complete built-in subsystem currently contains **266 unique names**: 239 eager/versioned, 20 AST/reference-aware and 7 dynamic-array names.

## 2. Array value and safety limits

`FormulaArrayValue` requires a non-empty rectangular shape and stores values row-major.

- positive row/column counts;
- exact shape/value count;
- maximum 1,000,000 cells;
- immutable caller view;
- detached `ToArray()` copy;
- transpose returns a new array.

Invalid shapes fail before partial materialization.

## 3. Spill ownership

A spill has one top-left owner. `FormulaSpillRange` records owner, complete range, immutable values and shape. Worksheet/snapshot APIs resolve spill by owner, owner by cell, child status and current spill list/count.

Only the owner contains a formula. Children contain derived values and may retain direct styles.

## 4. Collision and `#SPILL!`

Preflight rejects:

- non-blank child targets;
- formulas;
- other spills;
- merged ranges;
- Tables;
- worksheet bounds.

Style-only cells do not block output. Child styles survive materialization, resize and clear. Blocked output leaves blockers unchanged and commits `#SPILL!` while retaining owner formula.

## 5. Atomic materialization

Replacement validates owner/shape, preflights target, clears obsolete children preserving styles, writes owner/children and replaces metadata as one logical transaction. Direct external child mutation invalidates ownership and clears unchanged stale siblings.

## 6. Calculation and dependencies

- Scalar arguments/source ranges enter the graph.
- Output changes trigger dependent-only recalculation.
- Committed spill values are frozen during that pass, including `#SPILL!`.
- Source edits may resize output.
- Blocked spills recover after blocker removal.
- Stabilization is bounded to eight passes.
- F008 CHOOSE spill bridge captures selector plus selected source only.
- CHOOSECOLS/CHOOSEROWS capture source and index ranges/arrays.

The `A1#` operator is not implemented.

## 7. Supported functions

- `SEQUENCE(rows,[columns],[start],[step])`: bounded row-major numeric array.
- `TRANSPOSE(array)`: swaps rows/columns for ranges, cells, scalars or supported nested arrays.
- `FILTER(array,include,[if_empty])`: row/column Boolean vector filtering; mismatch `#VALUE!`, no match fallback or `#CALC!`.
- `SORT(array,[sort_index],[sort_order],[by_col])`: one stable row/column key.
- `UNIQUE(array,[by_col],[exactly_once])`: first-occurrence row/column uniqueness.
- `CHOOSECOLS(array,col_num1,...)`: ordered/duplicate/negative column projection.
- `CHOOSEROWS(array,row_num1,...)`: ordered/duplicate/negative row projection.
- `CHOOSE(index_num,...)`: AST/reference-aware name with a dynamic-array bridge when the selected branch is a range/supported nested array.

## 8. Projection semantics

- Index values may come from scalars, ranges or supported nested arrays.
- Values are consumed row-major.
- Numeric values truncate toward zero.
- Positive indices start at 1; negative indices count from the end.
- Zero/out-of-range returns `#VALUE!`.
- Requested order and duplicates are preserved.
- Output over 1,000,000 cells returns `#NUM!`.

## 9. Editing and history

- Child value/formula edits are rejected.
- Partial clear is rejected.
- Clearing owner clears complete output.
- Undo/Redo rematerializes output.
- Source edits trigger affected recalculation.
- Ordinary non-spill editing is unchanged.

## 10. Structural operations

Derived children are removed from canonical structural state before row/column transforms. Owner formulas and ordinary data move/rewrite, then dynamic calculation regenerates output. Undo/Redo uses the same rule. Rejected preflight restores/rematerializes cleared output without false version advancement.

## 11. Clipboard

- Partial spill copy/cut is rejected.
- Complete copy stores owner formula once.
- Derived child values/formulas are omitted.
- Direct child styles may be blank style-only cells.
- Free-space paste creates one owner and regenerates children.
- Paste intersecting a spill is rejected before history.
- Complete cut and Undo are supported.

## 12. Snapshot and rendering boundary

`WorksheetSnapshot` captures immutable spill ownership with materialized values. Renderers display sparse cells while selection/hit-test/UI can identify ownership without mutable worksheet access. Dedicated spill-border/selection UX is pending.

## 13. XLSX document boundary

`NeraOpenXmlDocumentSerializer` retains owner formula, removes derived child values/formulas, retains child styles and supports load-then-recalculate rematerialization. Package graph validation and unknown-part preservation remain active.

Full Microsoft Office extension metadata and producer cached-spill conventions remain corpus work.

## 14. Function SDK boundary

The SDK can describe array arguments/returns and carry `FormulaArrayValue`, but default v1 rejects array-capable extensions until invocation, spill ownership, dependencies and package metadata are integrated end to end.

## 15. Deliberately pending

- `A1#` and `@`;
- array constants/vectorized operators;
- advanced helpers (`SORTBY`, `TAKE`, `DROP`, stack/wrap families);
- CHOOSE selector arrays;
- LET/LAMBDA and higher-order arrays;
- multi-key/locale sort and complete coercion;
- native spill UX;
- full Office metadata and external corpus;
- large-array hardware budgets/fuzzing;
- array-returning extensions.

## 16. Validation gates

Promotion requires array shape/limit, ownership/collision, dynamic function/dependency, projection order/index, stabilization, edit/history, structure, clipboard, snapshot, XLSX and SDK/Core/Windows/MAUI tests.

PR #1 remains Draft while a newer exact-head CI is red or unknown.
