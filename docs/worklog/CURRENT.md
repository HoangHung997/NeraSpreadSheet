# Current Work Handoff

- Repository: `HoangHung997/NeraSpreadSheet`
- Branch: `feature/bootstrap-architecture-v0.1`
- Pull request: `#1` into `develop` — Draft, unmerged
- Financial implementation head: `e8c349d0b969fa8c9734452573bf7e9bcfa4df28`
- GitHub Actions: CI `#809`, run `32644745950`
- Source of truth: `docs/current-status.md`
- SDK contract: `docs/function-extension-sdk-contract.md`
- Statistical contract: `docs/statistical-functions-foundation-contract.md`
- Financial contract: `docs/financial-functions-foundation-contract.md`
- Final acceptance: `docs/CODEX_FINAL_ACCEPTANCE.md`

## Previously completed foundations

- Sparse workbook/worksheet, structural history, fractional viewport and multi-host rendering.
- Conditional Formatting, Data Validation, Tables, AutoFilter and native paged presenters.
- Page layout, print preview, PDF, native print adapters, XLSX preservation and CSV/TSV.
- Formula Surface I, Dynamic Arrays Foundation, Function Extension SDK v1.0, Conditional Aggregates and Statistical Functions Foundation.

## Batch completed: Financial Functions Foundation

### Ten financial functions

- `PV`.
- `FV`.
- `PMT`.
- `NPER`.
- `NPV`.
- `IRR`.
- `IPMT`.
- `PPMT`.
- `SLN`.
- `SYD`.

### Time-value and sign conventions

- `PV`, `FV`, `PMT` and `NPER` share one annuity sign/timing model.
- Payment type is integer `0` or `1`.
- Zero-rate paths use explicit linear formulas.
- Invalid rate/domain and non-finite output return `#NUM!`; invalid timing/shape returns `#VALUE!`.
- Zero-rate `NPER` with zero payment returns `#DIV/0!`.

### Cash-flow functions

- `NPV` preserves logical argument order and row-major range order.
- Range numeric/date values participate; range blank/text/Boolean values are ignored.
- Scalar Boolean and invariant numeric text may coerce.
- `NPV` retains at most 2,000,000 values and uses compensated summation.
- `IRR` retains at most 100,000 values and requires positive and negative cash flows.

### Hardened IRR solver

- Bounded Newton iteration remains the fast candidate.
- A transformed-rate log-domain sampler and bisection path supplies an independent candidate.
- Each solver phase is bounded to 100 iterations; bracket sampling uses 64 intervals.
- When both candidates converge, the root nearest the caller's `guess` is selected.
- A regression cash-flow vector with roots near `-0.8368694674` and `1.7426259408` proves Newton can cross to the farther basin and that the bracket candidate restores nearest-guess behavior.
- Repeated evaluation is deterministic.

### Payment decomposition and depreciation

- `IPMT` and `PPMT` use one-based periods.
- Beginning-of-period payment one has zero interest.
- Tests require `IPMT + PPMT == PMT` within tolerance.
- `SLN` implements straight-line depreciation.
- `SYD` implements sum-of-years-digits depreciation with period/domain validation.

### SDK and dependency integration

- Namespace `NERA.BUILTIN`, version `1.0.0`, host API `1.0`.
- Logical argument counting and scalar return.
- Deterministic/pure classification.
- `NPV`/`IRR` declare scalar and range arguments; remaining names are scalar-only.
- Cash-flow ranges enter the shared dependency graph.
- Affected-only recalculation responds to source edits.
- Eager registry increases from 103 to 113 names; complete built-in formula count increases from 126 to 136.

### Cleanup and compatibility fixes

- Removed one duplicate SDK implementation and one duplicate SDK test surface.
- Removed obsolete connector/tool-discovery marker files.
- Corrected criteria wildcard escaping so `~*` and `~?` match literal characters across all conditional aggregates.

## Implementation CI #809

The implementation commit is accepted only after all jobs conclude successfully:

- Core restore/build/tests and architecture verification;
- all financial, statistical, conditional aggregate, SDK and dynamic-array regressions;
- full Windows build/tests and desktop GPU smoke;
- Android;
- iOS and Mac Catalyst;
- MAUI Windows build/handler;
- loaded Table-filter, runtime-context and scale/orientation smokes.

## Explicit limitations

- No `RATE`, `XNPV`, `XIRR`, `CUMIPMT`, `CUMPRINC` or `ISPMT`.
- No bond/coupon, treasury, price/yield or day-count families.
- No DB/DDB/VDB or amortization-specific depreciation methods.
- IRR root discovery is bounded and does not claim every pathological external-producer case.
- Currency/locale/date-basis compatibility remains external corpus work.
- Numeric `#NUM!` values still share the existing invalid-value enum path.
- Financial fuzzing and very-large cash-flow performance remain final acceptance work.

## Progress after exact-head documentation validation

- Engine/viewport/renderer: about `92%`.
- Basic spreadsheet MVP: about `96–98%`.
- Professional roadmap: about `70%`.
- Production readiness: about `47–50%`.

## Next batch

1. Engineering and Database Functions Foundation.
2. Database criteria-table evaluator and dependency/budget contracts.
3. Advanced statistics, covariance/correlation/regression and distributions.
4. Remaining financial families.
5. Advanced lookup/reference and dynamic-array helpers.
6. Plugin packaging/discovery/isolation and API compatibility.
7. Native spill UX, drawings/charts and advanced data.
8. Release hardening and final Codex acceptance.

PR remains Draft; do not merge while a newer exact-head CI is red or unknown.
