# F009 — Reference Introspection and Array Shaping contract

## Scope

Exactly five public names:

1. `COLUMN`
2. `COLUMNS`
3. `DROP`
4. `EXPAND`
5. `FORMULATEXT`

## Architecture

- `IFormulaReferenceIntrospectionContext` exposes current worksheet/cell and formula metadata.
- Scalar engine owns static reference geometry, lazy `CHOOSE` resolution and `FORMULATEXT`.
- Dynamic engine owns multi-column `COLUMN`, dynamic `COLUMNS`, `DROP` and `EXPAND`.
- Workbook contexts implement metadata access; WPF/WinForms/MAUI do not implement formula semantics.

## Semantics

### COLUMN

- No argument returns current formula-cell column, one-based.
- Static cell/range returns leftmost column.
- Multi-column reference returns a horizontal spill vector.
- Geometry-only static reference creates no value dependency.

### COLUMNS

- Static reference returns its column count.
- Scalar input returns 1.
- Supported nested dynamic array returns its shape.
- Lazy `CHOOSE` evaluates selector and selected branch only.

### DROP

- Positive row/column count removes from start.
- Negative count removes from end.
- Missing column count preserves columns.
- Explicit zero or removing the entire dimension returns `#CALC!`.

### EXPAND

- Target dimensions must be at least source dimensions.
- Missing/blank target dimension keeps source size.
- Default padding is `#N/A`; custom scalar padding is supported.
- Output over 1.000.000 cells returns `#NUM!`.

### FORMULATEXT

- Reads formula text from the top-left referenced cell.
- Selected reference through `CHOOSE` stays lazy.
- Exact target dependency is captured.
- Self-reference is metadata-safe.
- No formula, unavailable context or text over 8.192 characters returns `#N/A`.

## Validation

Implementation head `bb332e65291776fea05e52ce8433db9e6b1ac810`; CI #882; build zero warnings/errors; **239/239 formula tests**; complete hosted matrix.

PR #1 remains Draft and unmerged.
