# Database Functions Foundation contract

This document defines the validated first-generation database-function behavior of NeraSpreadSheet. Excel and LibreOffice are compatibility references only and are not runtime dependencies.

## 1. Architecture boundary

- Database functions are deterministic/pure SDK v1 functions.
- Each invocation receives exactly three logical arguments: database range, field selector and criteria range.
- Database/criteria arguments retain canonical range identity and row-major values.
- Shared `FormulaCriterion` owns comparisons, wildcard and tilde escaping.
- The formula engine records database, field-expression and criteria dependencies before invocation.
- Evaluation is a bounded row scan and does not mutate source data or build worksheet indexes.

## 2. Supported functions

- `DSUM`, `DCOUNT`, `DCOUNTA`, `DAVERAGE`;
- `DMAX`, `DMIN`, `DPRODUCT`, `DGET`;
- `DSTDEV`, `DSTDEVP`, `DVAR`, `DVARP`.

Descriptors use namespace `NERA.BUILTIN`, version `1.0.0`, host API `1.0`, logical argument counting, scalar/range arguments and scalar return.

## 3. Database range and field

The database argument is one rectangular range.

- First row contains headers.
- Headers are trimmed text, nonblank and unique under ordinal case-insensitive comparison.
- Remaining rows are records and retain original `CellValue` kinds.

The field selector may be a header name or one-based integer index. Text is matched as a header first, then parsed as an invariant integer. Missing/invalid fields return `#VALUE!`.

## 4. Criteria table

The criteria argument is a rectangular range with at least two rows.

- First row contains field names resolved against database headers.
- Duplicate criteria headers are allowed for multiple conditions on one field.
- Populated cells within one criteria row combine by AND.
- Criteria rows combine by OR.
- Blank criteria cells are ignored.
- A completely blank criteria row matches all records.

Criteria use the shared parser:

- `=`, `<>`, `<`, `<=`, `>` and `>=`;
- invariant numbers, Booleans and dates;
- error values;
- ordinal case-insensitive text;
- `*` and `?` wildcards;
- tilde escaping for literal wildcard characters.

Criteria cells are values; formula expressions stored inside criteria cells are not executed in this milestone.

## 5. Aggregation semantics

Numeric database functions include matching values only when kind is Number or DateTime. Matching text, Boolean and blank values are ignored. A matching error in the selected field propagates; errors in nonmatching records are not inspected.

- `DCOUNT`: matching Number/DateTime values.
- `DCOUNTA`: matching nonblank values.
- `DSUM`, `DMAX`, `DMIN`, `DPRODUCT`: `0` on an empty numeric set.
- `DAVERAGE`: `#DIV/0!` on an empty numeric set.
- Sample variance/deviation requires at least two numeric values.
- Population variance/deviation requires at least one.
- `DGET`: exactly one record returns the selected value; zero records returns `#VALUE!`; multiple records returns `#NUM!`.

## 6. Numerical behavior

- `DSUM` and `DAVERAGE` use compensated summation.
- Variance/deviation use stable online accumulation.
- Sample divides by `n-1`; population divides by `n`.
- Tiny negative variance caused by cancellation is clamped only within a narrow tolerance.
- Non-finite intermediate/output values return `#NUM!`.

## 7. Dependencies and affected recalculation

Dependencies include:

- complete database range;
- field-selector expression dependency, when referenced;
- complete criteria range.

Changes to data, headers, field selector or criteria trigger affected-only recalculation through the shared graph.

## 8. Resource budgets

- Maximum database range: `2,000,000` cells.
- Maximum criteria range: `100,000` cells.
- Maximum record-condition comparisons: `10,000,000`.

Shape and budget checks occur before row enumeration. Excessive work returns `#NUM!`; malformed range identity/shape returns `#VALUE!`.

## 9. Deliberately pending

- Formula-expression criteria.
- Locale-specific criteria parsing.
- Criteria indexes and incremental query maintenance.
- Named database fields beyond ordinary references.
- Cube/external-database functions.
- Complete literal/reference coercion differences.
- Large external compatibility corpus and fuzzing.

## 10. Validation gates

Promotion requires descriptor tests; field/header/index tests; AND/OR, duplicate-header, blank-row, wildcard/escape tests; all twelve result/error tests; DGET cardinality tests; stable variance tests; dependency and affected recalculation tests; budget/malformed-input tests; and the complete hosted platform matrix.

PR #1 remains Draft while a newer exact-head CI is red or unknown.
