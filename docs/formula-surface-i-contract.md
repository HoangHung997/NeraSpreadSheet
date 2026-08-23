# Formula Surface I contract

This document defines the validated Formula Surface I behavior of NeraSpreadSheet. Excel and LibreOffice are compatibility references only and are not runtime dependencies.

## 1. Architecture boundary

- `FormulaParser` and the AST own syntax.
- `NeraFormulaEngine` owns evaluation order, lazy branches, reference-aware functions, conditional aggregates, dependency capture and error mapping.
- `BuiltInFormulaFunctionRegistry` owns eager scalar/range functions and exposes versioned SDK metadata.
- `FormulaValueCoercion` owns shared conversion of blank, number, Boolean, text and `DateTime`, and is public for extension functions.
- `VersionedFormulaFunctionRegistry` owns extension identity/version/capability validation.
- Workbook calculation continues to use the existing dependency graph and affected-only recalculation.
- Dynamic-array values and spill ownership are specified in `docs/dynamic-arrays-contract.md`.
- Platform hosts must not implement function semantics.

Full extension contract: `docs/function-extension-sdk-contract.md`.

## 2. Error model

`FormulaErrorCode` maps:

- `DivisionByZero` → `#DIV/0!`;
- `InvalidReference` → `#REF!`;
- `InvalidName` → `#NAME?`;
- `InvalidValue` → `#VALUE!`;
- `CircularReference` → `#CIRC!`;
- `NotAvailable` → `#N/A`;
- numeric/domain failures → `#NUM!` value;
- `Spill` → `#SPILL!`.

Formula errors propagate through ordinary scalar and numeric aggregate functions. Information/counting functions with explicit non-propagating contracts inspect or count errors instead.

`IFERROR` and `IFNA` evaluate the fallback only when required. `IF`, `IFS`, `SWITCH` and `CHOOSE` evaluate only selected result branches.

## 3. Shared coercion

The public coercion layer currently follows these rules:

- finite numbers remain numbers;
- Boolean converts to `1`/`0` where numeric coercion is allowed;
- blank converts to `0` in scalar numeric coercion;
- `DateTime` converts through OLE Automation serial representation;
- text converts only where the function explicitly enables invariant parsing;
- non-finite numeric output becomes `#NUM!`;
- text output is bounded to 32,767 characters.

Core currently collapses empty text to blank.

## 4. Eager built-in functions

The eager built-in registry contains **92 names**, described as namespace `NERA.BUILTIN`, implementation version `1.0.0`, host API `1.0`.

### Aggregate, logical and information

`SUM`, `AVERAGE`, `MIN`, `MAX`, `COUNT`, `COUNTA`, `COUNTBLANK`, `PRODUCT`, `SUMSQ`, `AND`, `OR`, `XOR`, `NOT`, `TRUE`, `FALSE`, `NA`, `ISBLANK`, `ISNUMBER`, `ISTEXT`, `ISLOGICAL`, `ISERROR`, `ISERR`, `ISNA`, `N`, `T`.

Numeric aggregates propagate errors. With no numeric input:

- `SUM`, `MIN`, `MAX`, `PRODUCT`, `SUMSQ` return `0`;
- `AVERAGE` returns `#DIV/0!`.

### Math, rounding and trigonometry

`ABS`, `SIGN`, `INT`, `TRUNC`, `ROUND`, `ROUNDDOWN`, `ROUNDUP`, `MOD`, `POWER`, `SQRT`, `QUOTIENT`, `EVEN`, `ODD`, `CEILING.MATH`, `FLOOR.MATH`, `PI`, `EXP`, `LN`, `LOG10`, `LOG`, `SIN`, `COS`, `TAN`, `ASIN`, `ACOS`, `ATAN`, `ATAN2`, `DEGREES`, `RADIANS`.

### Text and Unicode

`LEN`, `LOWER`, `UPPER`, `PROPER`, `TRIM`, `CLEAN`, `LEFT`, `RIGHT`, `MID`, `REPT`, `EXACT`, `CONCAT`, `TEXTJOIN`, `FIND`, `SEARCH`, `REPLACE`, `SUBSTITUTE`, `VALUE`, `CHAR`, `CODE`, `UNICHAR`, `UNICODE`.

### Date and time

`DATE`, `TIME`, `YEAR`, `MONTH`, `DAY`, `HOUR`, `MINUTE`, `SECOND`, `DAYS`, `EDATE`, `EOMONTH`, `WEEKDAY`, `DATEVALUE`, `TIMEVALUE`, `TODAY`, `NOW`.

`TODAY` and `NOW` use `IFormulaClockEvaluationContext` when supplied. Their descriptors are volatile/context-read-only; automatic volatile scheduling is pending.

## 5. Reference/AST-aware functions

The evaluator recognizes **18 additional scalar/reference names**:

- lazy/error: `IF`, `IFERROR`, `IFNA`, `IFS`, `SWITCH`, `CHOOSE`;
- lookup/reference: `INDEX`, `MATCH`, `XLOOKUP`, `VLOOKUP`, `HLOOKUP`;
- visibility aggregate: `SUBTOTAL`;
- conditional aggregate: `COUNTIF`, `COUNTIFS`, `SUMIF`, `SUMIFS`, `AVERAGEIF`, `AVERAGEIFS`.

The scalar/reference surface therefore contains **110 built-in names**.

The dynamic-array engine recognizes five further names — `SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT`, `UNIQUE` — so the complete built-in subsystem recognizes **115 names**. Registered extension functions are additional and are not included in this count.

## 6. Conditional aggregates

The conditional aggregate families share `FormulaCriteria`:

- comparison operators `=`, `<>`, `<`, `<=`, `>`, `>=`;
- invariant Boolean/number/date/error/text parsing;
- case-insensitive ordinal text;
- `*`/`?` wildcards and tilde escaping;
- blank/non-blank and error matching.

Ranges must be canonical cell/range references with identical shapes. Multiple criteria combine by AND. Aggregate errors propagate only at matched positions. Work is bounded to two million positional range passes.

Full contract: `docs/conditional-aggregates-contract.md`.

## 7. Lookup/reference semantics

- `INDEX` uses one-based row/column indexes and returns `#REF!` outside the range.
- `MATCH` supports exact `0` and basic approximate `1`/`-1`.
- `XLOOKUP` supports exact matching and optional fallback.
- `VLOOKUP`/`HLOOKUP` support exact and conservative approximate mode.
- Lookup ranges enter the dependency graph.
- Approximate lookup assumes correctly sorted input.
- Wildcard/binary/reverse XLOOKUP modes remain pending.

## 8. Range and argument compatibility

Versioned invocation preserves logical range identity and values for extension functions.

Built-ins and legacy functions explicitly use `FlattenedValues` argument counting. This preserves historical behavior such as:

- `SUM(A1:A2,5)` flattening the range;
- `DATE(A1:A3)` supplying three values;
- `ABS(A1:A2)` failing fixed arity rather than silently using the first value.

New SDK extensions default to logical-argument counting.

## 9. Function Extension SDK

The validated SDK provides:

- stable namespace/name identity;
- semantic implementation versions;
- host API compatibility;
- capabilities and state classifications;
- alias/conflict rules;
- logical/range invocation metadata;
- engine or extension dependency policy;
- shared coercion helpers;
- legacy registration compatibility.

The default registry rejects external-state and array-capable extensions. Formula-text version pinning, assembly discovery, signing and isolation remain pending.

## 10. Date representation

Nera stores dates as `.NET DateTime` and converts to/from OLE Automation serial values for numeric date arithmetic.

The engine does not emulate Excel's fictitious 1900-02-29. Locale-specific parsing/formatting and the `TEXT` formatting language remain pending.

## 11. Deliberately pending

- statistical, financial, engineering, database and cube function families;
- `OFFSET`, `INDIRECT`, `ADDRESS`, `ROW(S)`, `COLUMN(S)`;
- advanced lookup modes and locale-aware `TEXT`;
- complete literal/reference coercion compatibility;
- volatile recalculation scheduling;
- advanced dynamic arrays and higher-order functions;
- extension package manifests, signatures, loading and isolation;
- external Excel/LibreOffice differential corpus and fuzzing.

## 12. Validation gates

Promotion requires:

1. Core restore/build/tests and architecture verification;
2. logical/error/lazy regressions;
3. aggregate/math/text/date/lookup regressions;
4. function SDK API/version/capability/conflict/dependency compatibility tests;
5. criteria and all six conditional aggregate tests;
6. affected-only dependency and scan-budget tests;
7. dynamic-array, Table, filter, XLSX, Windows and MAUI regression matrix.

PR #1 remains Draft while a newer exact-head CI is red or unknown.
