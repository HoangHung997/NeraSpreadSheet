# Statistical Functions Foundation contract

This document defines the first validated statistical-function family owned by NeraSpreadSheet. Excel and LibreOffice are compatibility references only; they are not runtime dependencies.

## 1. Architecture boundary

- The functions are registered through Function Extension SDK API `1.0`.
- Every function uses namespace `NERA.BUILTIN`, implementation version `1.0.0` and logical-argument counting.
- `NeraFormulaEngine` preserves range identity and dependencies before invoking the function.
- `FormulaValueCoercion` is the shared scalar conversion surface.
- Statistical semantics remain independent from WPF, WinForms, MAUI, OpenXml and rendering.
- The family returns scalar values only; dynamic-array statistical results are not part of this milestone.

## 2. Registered functions

The eager registry adds eleven names:

- `MEDIAN`;
- `MODE.SNGL`;
- `PERCENTILE.INC`;
- `QUARTILE.INC`;
- `VAR.P`;
- `VAR.S`;
- `STDEV.P`;
- `STDEV.S`;
- `RANK.EQ`;
- `LARGE`;
- `SMALL`.

The eager registry therefore contains 103 names. Together with 18 AST/reference-aware functions and five dynamic-array functions, the built-in formula subsystem recognizes 126 names.

## 3. Argument and coercion policy

Each source expression remains one logical argument even when it is a range.

Range arguments:

- include finite `Number` and `DateTime` values;
- ignore blank, text and Boolean cells;
- propagate formula-error values before invocation through the SDK descriptor.

Scalar arguments:

- include finite numbers and dates;
- coerce Boolean to `1` or `0`;
- permit invariant numeric text;
- reject nonnumeric text with `#VALUE!`.

The distinction between scalar and range coercion is deliberate and covered by tests. It must not be removed by flattening logical range boundaries.

## 4. Safety budget

One invocation may collect at most 2,000,000 statistical values. Exceeding the limit returns `#NUM!` before an unbounded allocation or sort is allowed.

Functions that sort (`MEDIAN`, `MODE.SNGL`, percentiles, quartiles, `LARGE`, `SMALL`) materialize only the bounded numeric vector. Variance and standard deviation use an online numerically stable accumulation.

## 5. Function semantics

### `MEDIAN`

Sorts the numeric input. An odd-sized set returns its middle value; an even-sized set returns the mean of the two middle values. No numeric value returns `#NUM!`.

### `MODE.SNGL`

Returns the smallest numeric value among values tied for the highest repeated frequency. If no value occurs at least twice, it returns `#N/A`.

### `PERCENTILE.INC`

Uses inclusive linear interpolation over a sorted vector. The percentile must be finite and within `[0,1]`; otherwise the result is `#NUM!`. No numeric source value returns `#NUM!`.

### `QUARTILE.INC`

Maps quartiles `0` through `4` to inclusive percentiles `0`, `0.25`, `0.5`, `0.75` and `1`. Other quartile indexes return `#NUM!`.

### `VAR.P` and `STDEV.P`

Use the population denominator `N`. At least one numeric value is required; otherwise the result is `#DIV/0!`. `STDEV.P` returns the square root of population variance.

### `VAR.S` and `STDEV.S`

Use the sample denominator `N-1`. At least two numeric values are required; otherwise the result is `#DIV/0!`. `STDEV.S` returns the square root of sample variance.

### `RANK.EQ`

Returns a one-based equal rank. Omitted or zero order ranks descending; nonzero order ranks ascending. Duplicate values share the same rank. The ranked number is not required to appear in the reference vector.

### `LARGE` and `SMALL`

Return the `k`-th largest or smallest numeric value. `k` must be a positive integer not exceeding the numeric source count; otherwise the result is `#NUM!`.

## 6. Error and dependency behavior

- Argument errors propagate through the SDK's automatic error policy.
- Invalid scalar coercion returns `#VALUE!`.
- Domain/index failures return a `#NUM!` value.
- Insufficient sample/population sizes return `#DIV/0!` where documented.
- No mode returns `#N/A`.
- Every referenced range and scalar reference enters the dependency graph.
- Affected-only recalculation responds when a value inside a referenced statistical range changes.

## 7. Versioned SDK metadata

All eleven descriptors declare:

- identity namespace `NERA.BUILTIN`;
- implementation version `1.0.0`;
- minimum host API `1.0`;
- scalar and range arguments;
- scalar return;
- logical-argument counting;
- deterministic volatility;
- pure state classification;
- engine-captured dependencies.

This keeps the functions on the same extension surface available to future domain/plugin functions rather than adding a parallel evaluator path.

## 8. Deliberately pending

- `PERCENTILE.EXC` and `QUARTILE.EXC`;
- `MODE.MULT` and `RANK.AVG`;
- `PERCENTRANK`, covariance, correlation and regression functions;
- probability distributions, confidence intervals and hypothesis-test functions;
- complete Excel literal-versus-reference coercion compatibility;
- locale-aware numeric parsing and collation;
- approximate floating-point tie grouping for mode/rank;
- streaming/select algorithms that avoid sorting large bounded vectors;
- external Excel/LibreOffice differential corpus and statistical fuzzing.

## 9. Validation gates

Promotion requires:

1. odd/even median tests;
2. mode/tie/no-mode tests;
3. inclusive interpolation and quartile-boundary tests;
4. population/sample variance and standard-deviation tests;
5. rank/large/small duplicate and index tests;
6. scalar/range coercion and error propagation tests;
7. dependency and affected-only recalculation tests;
8. versioned descriptor metadata tests;
9. the complete Core, architecture, Windows, Android, iOS, Mac Catalyst and MAUI Windows matrix.

PR #1 remains Draft while a newer exact-head CI is red or unknown.
