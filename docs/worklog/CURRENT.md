# Current Work Handoff

- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` into `develop` — Draft, unmerged
- Formula Surface I implementation head: `497ebf3fbaca79e2f294475af861077d47400d3c`
- GitHub Actions: CI `#706`, run `32613991638`, success
- Source of truth: `docs/current-status.md`
- Formula contract: `docs/formula-surface-i-contract.md`
- Final acceptance: `docs/CODEX_FINAL_ACCEPTANCE.md`

## Batch completed: Formula Surface I

### Registry and evaluator

- Shared coercion and error mapping.
- 92 eager registry functions.
- 12 AST/reference-aware special functions.
- Total recognized names: 104.
- Lazy branches for `IF`, `IFERROR`, `IFNA`, `IFS`, `SWITCH` and `CHOOSE`.
- Range-aware `INDEX`, `MATCH`, `XLOOKUP`, `VLOOKUP`, `HLOOKUP` and existing `SUBTOTAL`.

### Function families

- Aggregate/information.
- Logical/error.
- Math, rounding, logarithmic and trigonometric.
- Text, search/replace, Unicode.
- Date/time and deterministic clock.
- Basic lookup/reference.

### Semantic hardening

- Numeric aggregates propagate formula errors.
- `SUM/MIN/MAX/PRODUCT/SUMSQ` return zero for numeric-empty argument sets.
- `AVERAGE` returns `#DIV/0!` for a numeric-empty argument set.
- Counting functions keep non-propagating counting behavior.
- Numeric domain failures map to `#NUM!`.
- Lookup ranges enter dependency tracking.
- Lazy functions avoid evaluating unselected result branches.

### CI

CI #706 confirms:

- Core build/tests and architecture verification.
- Formula surface, aggregate semantics, lookup/dependency, date/time and text/Unicode regressions.
- Existing workbook calculation, structured-reference, Table/filter, Data Validation, Conditional Formatting and XLSX regressions.
- Full Windows build/tests and desktop GPU runtime smoke.
- Android, iOS, Mac Catalyst and MAUI Windows build/runtime gates.

## Explicit limitations

- No dynamic arrays or spill ownership.
- No versioned plugin-function SDK.
- No conditional aggregate/statistical/financial families yet.
- Basic lookup only; no wildcard/binary/reverse modes.
- Empty text collapses to blank in Core.
- Date model does not emulate Excel's 1900 leap-year bug.
- `TODAY`/`NOW` do not yet schedule volatile recalculation automatically.
- External Excel/LibreOffice differential corpus remains final acceptance work.

## Progress

- Engine/viewport/renderer: about `92%`.
- Basic spreadsheet MVP: about `90–93%`.
- Complete professional roadmap: about `62%`.
- Production readiness: about `39–42%`.

## Next batch

1. Dynamic Arrays Foundation.
2. Spill collision/ownership and structural mapping.
3. Affected-only recalculation for spill dependencies.
4. Versioned plugin-function SDK.
5. Conditional aggregate/statistical/financial families.
6. Exact-head Core/Windows/MAUI CI.

PR remains Draft; do not merge while a newer exact-head CI is red or unknown.