# Current Work Handoff

- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` into `develop` — Draft, unmerged
- Statistical implementation head: `6aa9b1a05f7a370d393d3222b533b3bee0088c9a`
- GitHub Actions: CI `#779`, run `32636739544`, success
- Source of truth: `docs/current-status.md`
- SDK contract: `docs/function-extension-sdk-contract.md`
- Statistical contract: `docs/statistical-functions-foundation-contract.md`
- Final acceptance: `docs/CODEX_FINAL_ACCEPTANCE.md`

## Previously completed foundations

- Sparse workbook/worksheet, structural history, fractional viewport and multi-host rendering.
- Conditional Formatting, Data Validation, Tables, AutoFilter and native paged presenters.
- Page layout, print preview, PDF, native print adapters, XLSX preservation and CSV/TSV.
- Formula Surface I, Dynamic Arrays Foundation, Function Extension SDK v1.0 and Conditional Aggregates.

## Batch completed: Statistical Functions Foundation

### Eleven new functions

- `MEDIAN`.
- `MODE.SNGL`.
- `PERCENTILE.INC`.
- `QUARTILE.INC`.
- `VAR.P`, `VAR.S`.
- `STDEV.P`, `STDEV.S`.
- `RANK.EQ`.
- `LARGE`, `SMALL`.

### SDK integration

- All functions use namespace `NERA.BUILTIN`.
- Implementation version `1.0.0`, host API `1.0`.
- Scalar/range arguments, scalar return.
- Logical-argument count policy.
- Deterministic and pure classification.
- Engine-captured dependencies.
- Automatic argument-error propagation.

### Coercion and safety

- Numeric/date range cells participate.
- Range blank/text/Boolean cells are ignored.
- Scalar Boolean becomes `1`/`0`.
- Scalar invariant numeric text may coerce; invalid text returns `#VALUE!`.
- One invocation is limited to 2,000,000 collected values.
- Percentiles/quartiles validate domains before indexing.
- Sample/population insufficient data returns documented `#DIV/0!`.

### Numerical behavior

- Median handles odd/even sets.
- MODE.SNGL returns the lowest tied mode and `#N/A` when no value repeats.
- Inclusive percentile/quartile use linear interpolation.
- Variance/standard deviation use stable online accumulation.
- RANK.EQ supports ascending/descending equal ranks.
- LARGE/SMALL validate one-based `k`.

### Dependency and tests

- Statistical ranges enter the shared dependency graph.
- Affected-only recalculation responds to edits inside a referenced range.
- Tests cover results, ties, boundaries, scalar/range coercion, errors, dependencies and SDK descriptors.
- Registry count increased from 92 to 103 eager built-ins.
- Complete built-in formula count increased from 115 to 126 names.

## CI #779

Passed:

- Core restore/build/tests;
- architecture verification;
- statistical tests and all previous formula/SDK/criteria/dynamic-array regressions;
- full Windows build/tests and desktop GPU smoke;
- Android;
- iOS and Mac Catalyst;
- MAUI Windows build/handler;
- loaded Table-filter, runtime-context and scale/orientation smokes.

## Explicit limitations

- No `PERCENTILE.EXC`, `QUARTILE.EXC`, `MODE.MULT` or `RANK.AVG`.
- No covariance, correlation, regression or probability distributions.
- Exact floating-point equality is used for mode ties.
- Complete Excel literal/reference coercion and locale compatibility are pending.
- No external differential statistical corpus or large-range fuzzing yet.
- Financial, engineering, database and cube families remain pending.

## Progress after exact-head documentation validation

- Engine/viewport/renderer: about `92%`.
- Basic spreadsheet MVP: about `95–97%`.
- Professional roadmap: about `68%`.
- Production readiness: about `45–48%`.

## Next batch

1. Financial Functions Foundation.
2. Engineering/database functions and criteria tables.
3. Advanced statistics and distributions.
4. Advanced lookup/reference and dynamic-array helpers.
5. Plugin packaging/discovery/isolation and API compatibility.
6. Native spill UX, drawings/charts and advanced data.
7. Release hardening and final Codex acceptance.

PR remains Draft; do not merge while a newer exact-head CI is red or unknown.
