# Current Work Handoff

- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` into `develop` — Draft, unmerged
- Dynamic Arrays Foundation implementation head: `705afb46f05e687a7ee13147e6ed106b82944c04`
- GitHub Actions: CI `#746`, run `32624762199`, success
- Source of truth: `docs/current-status.md`
- Formula Surface I: `docs/formula-surface-i-contract.md`
- Dynamic Arrays contract: `docs/dynamic-arrays-contract.md`
- Final acceptance: `docs/CODEX_FINAL_ACCEPTANCE.md`

## Batch completed: Dynamic Arrays Foundation

### Core array and spill model

- Immutable rectangular `FormulaArrayValue`, row-major, maximum one million cells.
- One top-left formula owner per spill.
- Worksheet and snapshot owner/child resolution.
- Derived children remain ordinary sparse values plus optional direct styles.
- Atomic spill replacement and obsolete-child cleanup.

### Collision and errors

- Target preflight rejects non-blank values, formulas, other spills, merged ranges, Tables and worksheet bounds.
- Direct style-only cells do not block materialization.
- Blocked output commits `#SPILL!` while retaining owner formula and blocker state.
- `FormulaErrorCode.Spill` is explicit.

### Dynamic functions

- `SEQUENCE`.
- `TRANSPOSE`.
- `FILTER` with row/column include vectors and fallback.
- `SORT` with one stable row/column key.
- `UNIQUE` with row/column comparison and exactly-once mode.
- Formula subsystem total: 109 recognized names.

### Dependency and calculation

- Dynamic source/range dependencies enter the existing graph.
- Scalar compatibility exposes top-left value.
- Spill output changes trigger dependent-only recalculation.
- Committed owner/child values are frozen during dependent calculation.
- Source edits resize output and blockers can recover.
- Stabilization is bounded to eight passes.

### Editing and history

- Direct child value/formula edit rejected.
- Partial spill clear rejected.
- Clearing owner clears complete output.
- Undo/Redo restores owner formula and rematerializes children.
- Row/column structural operations canonicalize away derived children, transform owner formulas and regenerate output.
- Rejected structural preflight does not corrupt Version or leave output missing.

### Clipboard

- Partial spill copy/cut rejected before clipboard/history mutation.
- Complete spill copy stores owner formula once.
- Derived child values/formulas are omitted.
- Direct child styles may be copied as style-only blank cells.
- Paste into any existing spill is rejected before mutation/history.
- Pasting a complete spill into free space creates a new owner and regenerated output.
- Complete spill cut and Undo are covered.

### XLSX boundary

- `NeraOpenXmlDocumentSerializer` retains owner formulas.
- Derived spill-child values/formulas are removed from worksheet XML.
- Direct child styles remain.
- Load followed by Nera recalculation rematerializes output.
- Existing package graph, schema and unknown-part preservation gates remain intact.

### CI #746

- Core restore/build/tests: success.
- Architecture verification: success.
- Windows full build/tests: success.
- Windows desktop GPU smoke: success.
- Android: success.
- iOS and Mac Catalyst: success.
- MAUI Windows build/handler: success.
- Loaded Table-filter, runtime context and scale/orientation smokes: success.

## Explicit limitations

- No `A1#` spill-reference syntax or `@` implicit-intersection semantics.
- No array constants or arbitrary vectorized binary expressions.
- No advanced helpers such as SORTBY/TAKE/DROP/HSTACK/VSTACK.
- No LET/LAMBDA/MAP/REDUCE families.
- SORT currently has one key and conservative Nera value ordering.
- No dedicated spill-border/selection affordance on every host.
- No full Microsoft Office dynamic-array extension metadata compatibility.
- One-million-cell target-hardware performance and fuzzing remain final acceptance work.
- Versioned plugin-function SDK remains pending.

## Progress

- Engine/viewport/renderer: about `92%`.
- Basic spreadsheet MVP: about `92–94%`.
- Complete professional roadmap: about `64%`.
- Production readiness: about `41–44%`.

## Next batch

1. Versioned Function Extension SDK.
2. Function identity/capabilities/deterministic-volatility contracts.
3. `SUMIF(S)`, `COUNTIF(S)`, `AVERAGEIF(S)` and criteria evaluator.
4. Statistical/financial/engineering functions.
5. Advanced dynamic arrays and spill UX.
6. Exact-head Core/Windows/MAUI CI.

PR remains Draft; do not merge while a newer exact-head CI is red or unknown.
