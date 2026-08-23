# Conditional aggregate functions contract

This document defines NeraSpreadSheet behavior for `COUNTIF`, `COUNTIFS`, `SUMIF`, `SUMIFS`, `AVERAGEIF` and `AVERAGEIFS`.

## 1. Architecture boundary

- `FormulaCriteria` owns criteria parsing and matching.
- `NeraFormulaEngine` retains range shape, evaluates criteria expressions and records dependencies.
- Conditional aggregates are reference-aware functions; they are intentionally not flattened through the eager scalar registry.
- Workbook recalculation uses the existing dependency graph and affected-only calculation.
- Platform hosts and OpenXml adapters do not implement criteria or aggregation semantics.

These six names extend the scalar/reference surface from 104 to 110 names. Together with five dynamic-array names, the complete formula subsystem recognizes 115 built-in names. User-registered SDK extensions are not included in this count.

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

Text criteria recognize these prefixes:

- no prefix or `=`: equal;
- `<>`: not equal;
- `<`: less than;
- `<=`: less than or equal;
- `>`: greater than;
- `>=`: greater than or equal.

The operand after the prefix is parsed invariantly as, in order:

1. known formula error code;
2. Boolean;
3. finite number;
4. invariant DateTime;
5. text.

A non-text criterion keeps its original typed `CellValue` and uses equality.

Criteria are limited to 1,024 characters.

## 4. Text and wildcard matching

Text comparison is ordinal and case-insensitive.

For equal/not-equal text criteria:

- `*` matches zero or more characters;
- `?` matches exactly one character;
- `~*`, `~?` and `~~` represent literal `*`, `?` and `~`.

Wildcard matching uses a bounded, culture-invariant, non-backtracking regular expression with a 100-ms timeout.

Wildcard criteria match text cells only. A numeric cell does not match a text wildcard merely because its display text happens to contain the same characters.

## 5. Blank, Boolean, number, date and error semantics

- `"="` matches blank cells.
- `"<>"` matches non-blank cells.
- Boolean criteria match Boolean cells, not text containing `TRUE`/`FALSE`.
- Numeric criteria match numeric cells and use numeric comparisons.
- Date criteria compare DateTime/OLE serial values.
- Error criteria such as `"#N/A"` match the same formula-error code.
- Not-equal criteria return true for a different value kind unless both values are directly comparable.

Core currently collapses empty text to blank; therefore zero-length text is not distinct from blank in this milestone.

## 6. Range and shape rules

A conditional aggregate range argument must be a cell or a canonical range AST. Scalar expressions, arbitrary array expressions and dynamic spill-reference syntax are not accepted as criteria/aggregate ranges in this milestone.

All positional ranges in one function must have identical row and column counts. Shape mismatch returns `#VALUE!` before enumeration.

Criteria and aggregate values are paired by row/column offset, not by absolute address equality. This allows same-shape ranges on different worksheets or locations.

## 7. Numeric aggregation

`SUMIF(S)` and `AVERAGEIF(S)` aggregate:

- finite number cells;
- DateTime cells converted to OLE serial values.

They ignore matched blank, text and Boolean aggregate cells.

A matched aggregate error propagates. An error at an unmatched position is not inspected and does not propagate.

- no match for sum returns `0`;
- no numeric match for average returns `#DIV/0!`;
- non-finite totals return `#NUM!`.

`COUNTIF(S)` counts matching positions regardless of the matched value kind.

## 8. Dependencies and recalculation

The engine records:

- every criteria range;
- the aggregate/sum/average range;
- every cell/range used to calculate a criterion expression.

Changing either a criteria cell or an aggregate value therefore triggers affected-only recalculation of the dependent conditional aggregate.

The criteria evaluator itself does not declare hidden external dependencies.

## 9. Work budget

Conditional aggregates are bounded to two million positional range passes per evaluation.

Examples:

- `COUNTIF` consumes one pass per position;
- `SUMIF`/`AVERAGEIF` consume two passes per position (criteria plus aggregate);
- `COUNTIFS` consumes one pass per criteria range;
- `SUMIFS`/`AVERAGEIFS` consume all criteria passes plus the aggregate pass.

The budget is validated before enumeration. Excessive work returns `#NUM!` rather than allocating or scanning the full request.

## 10. Error behavior

- invalid argument count: `#VALUE!`;
- non-range criteria/aggregate operand: `#VALUE!`;
- shape mismatch: `#VALUE!`;
- invalid/excessive criterion: `#VALUE!`;
- excessive scan budget: `#NUM!`;
- average without numeric match: `#DIV/0!`;
- matched aggregate error: propagate that error.

## 11. Deliberately pending

- locale-specific criteria parsing;
- complete Excel coercion differences between literal and referenced criteria;
- array-expression and `A1#` criteria ranges;
- external/3-D references;
- advanced wildcard/collation compatibility corpus;
- database functions and criteria tables;
- manual hidden-row exclusion semantics;
- criteria caching/indexes for very large repeated calculations;
- external Excel/LibreOffice/Google Sheets differential corpus and fuzzing.

## 12. Required validation

Promotion requires:

1. numeric/date/text/Boolean/error criteria tests;
2. wildcard and tilde-escape tests;
3. blank/non-blank tests;
4. all six function families;
5. shape and argument validation;
6. matched/unmatched error propagation;
7. dependency and affected-only recalculation tests;
8. scan-budget tests;
9. existing scalar, dynamic-array, workbook, Windows and MAUI regression matrix.

PR #1 remains Draft while a newer exact-head CI is red or unknown.
