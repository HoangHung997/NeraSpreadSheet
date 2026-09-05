# Conditional aggregate functions contract

This document defines NeraSpreadSheet behavior for `COUNTIF`, `COUNTIFS`, `SUMIF`, `SUMIFS`, `AVERAGEIF` and `AVERAGEIFS`.

## 1. Architecture boundary

- `FormulaCriteria` owns criteria parsing and matching.
- `NeraFormulaEngine` retains range shape, evaluates criteria expressions and records dependencies.
- Conditional aggregates are reference-aware functions; they are intentionally not flattened through the eager scalar registry.
- Workbook recalculation uses the existing dependency graph and affected-only calculation.
- Platform hosts and OpenXml adapters do not implement criteria or aggregation semantics.

These six names are part of the 18 AST/reference-aware names. With 103 eager registry functions, the scalar/reference surface contains 121 names. Together with five dynamic-array names, the complete built-in subsystem recognizes 126 names. User-registered SDK extensions are additional.

## 2. Supported functions

### `COUNTIF(range, criteria)`

Counts positions in one range that match one criterion.

### `COUNTIFS(criteria_range1, criteria1, ...)`

Counts positions where all criteria match. Every criteria range must have the same shape.

### `SUMIF(criteria_range, criteria, [sum_range])`

Sums matched numeric/DateTime values. If `sum_range` is omitted, the criteria range is also the aggregate range. An explicit sum range must have the same shape.

### `SUMIFS(sum_range, criteria_range1, criteria1, ...)`

Sums positions where all criteria match. Every criteria range must match the aggregate range shape.

### `AVERAGEIF(criteria_range, criteria, [average_range])`

Averages matched numeric/DateTime values. Non-numeric matched values are ignored. No numeric match returns `#DIV/0!`.

### `AVERAGEIFS(average_range, criteria_range1, criteria1, ...)`

Averages positions where all criteria match, with the same shape rules as `SUMIFS`.

## 3. Criteria operators

Text criteria recognize:

- no prefix or `=`: equal;
- `<>`: not equal;
- `<`: less than;
- `<=`: less than or equal;
- `>`: greater than;
- `>=`: greater than or equal.

The operand is parsed invariantly as known error, Boolean, finite number, DateTime or text. A non-text criterion keeps its typed `CellValue`. Criteria are limited to 1,024 characters.

## 4. Text and wildcard matching

Text comparison is ordinal and case-insensitive. For equal/not-equal text criteria:

- `*` matches zero or more characters;
- `?` matches one character;
- `~*`, `~?`, `~~` escape wildcard/tilde characters.

Wildcard matching is culture-invariant, non-backtracking and time-bounded. Wildcards match text cells only.

## 5. Blank, Boolean, number, date and error semantics

- `"="` matches blank.
- `"<>"` matches non-blank.
- Boolean criteria match Boolean cells.
- Numeric criteria use numeric comparison.
- Date criteria compare DateTime/OLE serial values.
- Error criteria match the same formula-error code.
- Not-equal returns true for a different value kind unless values are directly comparable.

Core currently collapses empty text to blank.

## 6. Range and shape rules

A range operand must be a cell or canonical range AST. Arbitrary scalar/array expressions and `A1#` are not accepted as criteria/aggregate ranges.

All positional ranges must have identical row/column counts. Shape mismatch returns `#VALUE!` before enumeration. Values pair by row/column offset, so same-shape ranges may be at different locations or worksheets.

## 7. Numeric aggregation

`SUMIF(S)` and `AVERAGEIF(S)` aggregate finite number and DateTime cells. They ignore matched blank, text and Boolean aggregate cells.

A matched aggregate error propagates; an unmatched error is not inspected.

- no sum match returns `0`;
- no numeric average match returns `#DIV/0!`;
- non-finite totals return `#NUM!`.

`COUNTIF(S)` counts matching positions regardless of value kind.

## 8. Dependencies and recalculation

The engine records every criteria range, aggregate range and cell/range used to compute a criterion expression. Changes to criteria or aggregate values trigger affected-only recalculation.

## 9. Work budget

Conditional aggregates are bounded to two million positional range passes per evaluation. The budget is validated before enumeration; excessive work returns `#NUM!`.

## 10. Error behavior

- invalid argument count, non-range operand, shape mismatch or invalid criterion: `#VALUE!`;
- excessive budget: `#NUM!`;
- average without numeric match: `#DIV/0!`;
- matched aggregate error: propagate it.

## 11. Deliberately pending

- locale-specific criteria parsing;
- complete literal/reference criteria coercion;
- array-expression and `A1#` ranges;
- external/3-D references;
- advanced wildcard/collation corpus;
- database functions and criteria tables;
- hidden-row exclusion semantics;
- criteria indexes/caches;
- external differential corpus and fuzzing.

## 12. Required validation

Promotion requires criteria-kind/wildcard/blank tests, all six families, shapes/arguments, matched/unmatched errors, dependency/affected recalculation, scan budgets and the existing scalar/dynamic/workbook/Windows/MAUI matrix.

PR #1 remains Draft while a newer exact-head CI is red or unknown.
