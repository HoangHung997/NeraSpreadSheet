# Formula Surface I handoff

## Validated implementation

- Branch: `feature/bootstrap-architecture-v0.1`
- PR: `#1` into `develop`, Draft and unmerged
- Initial implementation commit: `306d50566a51bc3317969cfa67dda9292dd50e5c`
- Analyzer hardening commit: `cbe4277761ab46730b19c99ecbeb25962050bf17`
- Aggregate semantic hardening: `cdf657f67ec0006db0cae8cc02e6fced984f60f1`
- Aggregate regression head: `497ebf3fbaca79e2f294475af861077d47400d3c`
- Full implementation CI: `#706`, run `32613991638` — success

## Implemented source surface

- Shared formula coercion and `#NUM!` error mapping.
- Deterministic clock context for `TODAY` and `NOW`.
- 92 registry functions across aggregate/logical, math, text/Unicode and date/time categories.
- Lazy evaluator functions: `IF`, `IFERROR`, `IFNA`, `IFS`, `SWITCH`, `CHOOSE`.
- Reference-aware functions: `INDEX`, `MATCH`, `XLOOKUP`, `VLOOKUP`, `HLOOKUP`.
- Existing filter-aware `SUBTOTAL` retained.
- Date arithmetic between `DateTime` values and numeric day offsets.
- Lookup range dependency capture.
- Error propagation through numeric aggregates.
- Function-specific empty numeric set behavior.

## Tests

- Logical and information coercion.
- Numeric domain errors and rounding of positive/negative values.
- Registry surface count.
- Text search, replacement, joining, Unicode and text-length limit.
- Date construction, month shifting, weekday, arithmetic and deterministic clock.
- Lookup/reference results, missing values, invalid references and dependencies.
- Formula-error literals.
- Aggregate error propagation, empty numeric sets and counting-function behavior.
- Existing workbook calculation, structured reference, Conditional Formatting, Data Validation and filter-aware subtotal regressions.

## Deliberately pending

- Dynamic arrays and spill ownership.
- Complete plugin-function SDK.
- Conditional aggregates.
- Statistical/financial/engineering/database functions.
- Advanced lookup modes and wildcard matching.
- Locale-aware text/number/date formatting.
- Volatile recalculation scheduling.
- External differential corpus and fuzzing.

Full contract: `docs/formula-surface-i-contract.md`.