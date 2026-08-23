# Dynamic Arrays Foundation milestone

## Validated implementation head

- Implementation commit: `705afb46f05e687a7ee13147e6ed106b82944c04`
- GitHub Actions: CI `#746`, run `32624762199`, success
- PR #1 remains Draft and has not been merged into `develop`.

## Implemented source surface

### Core

- `FormulaArrayValue`: immutable, rectangular, row-major arrays with a one-million-cell limit.
- `FormulaSpillRange`: owner, rectangular range, shape and immutable values.
- Worksheet spill store: owner/child resolution, atomic apply/replace/clear and `#SPILL!` assignment.
- Immutable spill metadata in `WorksheetSnapshot`.

### Formula engine

- `NeraDynamicArrayFormulaEngine`.
- `DynamicArrayAwareFormulaEngine` scalar top-left compatibility.
- `DynamicArrayWorkbookCalculationEngine` with affected dependencies and eight-pass stabilization.
- `SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT`, `UNIQUE`.
- Explicit `FormulaErrorCode.Spill`.

### Editing and history

- Dynamic-array calculation is the production `SpreadsheetSession` calculation path.
- Spill children cannot be directly edited or partially cleared.
- Clear owner, Undo and Redo rematerialize the complete result.
- Structural insert/delete transforms owner formulas rather than derived children.
- Rejected structural preflight restores/rematerializes output without false version advancement.

### Clipboard

- Complete-spill selection is required for copy/cut.
- Owner formula appears once in the package.
- Derived child values/formulas are omitted.
- Direct child styles are retained as blank style-only cells.
- Paste into an existing spill is rejected before history.
- Copy/paste and cut/Undo of complete spills are covered.

### XLSX

- Dynamic-array-aware document save removes derived child values/formulas.
- Owner formula and direct styles remain.
- Load plus recalculation regenerates spill output.
- Existing package graph/schema/preservation behavior remains validated.

## Automated tests added

- Array shape, transpose, equality, detached-copy and limit tests.
- Worksheet spill ownership, replacement, styles, collisions and invalidation tests.
- Snapshot immutability and stale-ownership tests.
- `SEQUENCE`/`TRANSPOSE` result and dependency tests.
- `FILTER`/`SORT`/`UNIQUE` row/column/nesting tests.
- Workbook materialization, blocker recovery, dependency propagation and resize tests.
- Session edit guards, clear, Undo/Redo and affected recalculation tests.
- Structural insert/delete/Undo/Redo/failure tests.
- Clipboard partial/full copy/cut/paste/history tests.
- XLSX owner/child/style/load/recalculate tests.

## Exact implementation validation

CI #746 passed:

1. Core restore/build/tests.
2. Architecture verification.
3. Full Windows restore/build/tests.
4. Windows desktop GPU runtime smoke.
5. MAUI Android build.
6. MAUI iOS build.
7. MAUI Mac Catalyst build.
8. MAUI Windows build and handler tests.
9. Loaded MAUI Windows Table-filter smoke.
10. Loaded MAUI Windows context/runtime smoke.
11. Loaded MAUI Windows scale/orientation smoke.

## Deliberately pending

- `A1#` and `@` syntax.
- Array constants and vectorized ordinary operators.
- Advanced array helper families.
- LET/LAMBDA/higher-order array functions.
- Native spill border/selection affordances.
- Full Microsoft Office extension metadata and external workbook corpus.
- One-million-cell hardware performance, memory budgets and fuzzing.
- Versioned function-extension SDK.

## Next implementation order

1. Versioned function identity and registry SDK.
2. Capability, deterministic/volatile and security metadata.
3. Conditional aggregate criteria engine and `SUMIF(S)`/`COUNTIF(S)`/`AVERAGEIF(S)`.
4. Statistical/financial/engineering function families.
5. Advanced arrays and native spill UX.
6. Drawings/charts, advanced data and release hardening.
