# Formula Surface I contract

This document defines the validated scalar/reference formula behavior of NeraSpreadSheet. Excel and LibreOffice are compatibility references only and are not runtime dependencies.

## 1. Architecture boundary

- `FormulaParser` and the AST own syntax.
- `NeraFormulaEngine` owns evaluation order, lazy branches, reference-aware functions, conditional aggregates, dependency capture and error mapping.
- `BuiltInFormulaFunctionRegistry` owns eager scalar/range functions and exposes versioned SDK metadata.
- `FormulaValueCoercion` owns shared conversion and is public for extension functions.
- `VersionedFormulaFunctionRegistry` owns extension identity/version/capability validation.
- Workbook calculation uses the shared dependency graph and affected-only recalculation.
- Dynamic arrays are specified in `docs/dynamic-arrays-contract.md`.
- Platform hosts do not implement function semantics.

Related contracts:

- `docs/function-extension-sdk-contract.md`;
- `docs/conditional-aggregates-contract.md`;
- `docs/statistical-functions-foundation-contract.md`;
- `docs/financial-functions-foundation-contract.md`.

## 2. Error model

`FormulaErrorCode` maps division, reference, name, value, circular, unavailable and spill errors. Numeric/domain failures currently carry a `#NUM!` cell value through the existing invalid-value code path.

Ordinary functions propagate argument errors. Information/counting functions with explicit non-propagating contracts inspect or count them instead. Lazy functions evaluate only the selected branch or fallback.

## 3. Shared coercion

- finite numbers remain numbers;
- Boolean may convert to `1`/`0` where enabled;
- blank converts to `0` in scalar numeric coercion;
- `DateTime` converts through OLE Automation serial representation;
- text converts only where a function explicitly enables invariant parsing;
- non-finite output becomes `#NUM!`;
- text output is bounded to 32,767 characters.

Core currently collapses empty text to blank.

## 4. Eager built-in registry

The eager registry contains **113 names**.

### Original 92 flattened-value functions

Aggregate/logical/information:

`SUM`, `AVERAGE`, `MIN`, `MAX`, `COUNT`, `COUNTA`, `COUNTBLANK`, `PRODUCT`, `SUMSQ`, `AND`, `OR`, `XOR`, `NOT`, `TRUE`, `FALSE`, `NA`, `ISBLANK`, `ISNUMBER`, `ISTEXT`, `ISLOGICAL`, `ISERROR`, `ISERR`, `ISNA`, `N`, `T`.

Math/rounding/trigonometry:

`ABS`, `SIGN`, `INT`, `TRUNC`, `ROUND`, `ROUNDDOWN`, `ROUNDUP`, `MOD`, `POWER`, `SQRT`, `QUOTIENT`, `EVEN`, `ODD`, `CEILING.MATH`, `FLOOR.MATH`, `PI`, `EXP`, `LN`, `LOG10`, `LOG`, `SIN`, `COS`, `TAN`, `ASIN`, `ACOS`, `ATAN`, `ATAN2`, `DEGREES`, `RADIANS`.

Text/Unicode:

`LEN`, `LOWER`, `UPPER`, `PROPER`, `TRIM`, `CLEAN`, `LEFT`, `RIGHT`, `MID`, `REPT`, `EXACT`, `CONCAT`, `TEXTJOIN`, `FIND`, `SEARCH`, `REPLACE`, `SUBSTITUTE`, `VALUE`, `CHAR`, `CODE`, `UNICHAR`, `UNICODE`.

Date/time:

`DATE`, `TIME`, `YEAR`, `MONTH`, `DAY`, `HOUR`, `MINUTE`, `SECOND`, `DAYS`, `EDATE`, `EOMONTH`, `WEEKDAY`, `DATEVALUE`, `TIMEVALUE`, `TODAY`, `NOW`.

These functions preserve the historical flattened-value argument-count policy. `TODAY` and `NOW` are volatile/context-read-only in their descriptors; automatic scheduling is pending.

### Eleven logical-argument statistical functions

`MEDIAN`, `MODE.SNGL`, `PERCENTILE.INC`, `QUARTILE.INC`, `VAR.P`, `VAR.S`, `STDEV.P`, `STDEV.S`, `RANK.EQ`, `LARGE`, `SMALL`.

These use SDK v1 logical arguments so a range remains one argument with source identity. Their coercion, safety and numerical semantics are defined in `docs/statistical-functions-foundation-contract.md`.

### Ten logical-argument financial functions

`PV`, `FV`, `PMT`, `NPER`, `NPV`, `IRR`, `IPMT`, `PPMT`, `SLN`, `SYD`.

These use SDK v1 logical arguments. `NPV` and `IRR` preserve range identity and row-major cash-flow order. Their sign, timing, budget, numerical and dependency semantics are defined in `docs/financial-functions-foundation-contract.md`.

## 5. AST/reference-aware functions

The evaluator recognizes **18 additional scalar/reference names**:

- lazy/error: `IF`, `IFERROR`, `IFNA`, `IFS`, `SWITCH`, `CHOOSE`;
- lookup/reference: `INDEX`, `MATCH`, `XLOOKUP`, `VLOOKUP`, `HLOOKUP`;
- visibility aggregate: `SUBTOTAL`;
- conditional aggregate: `COUNTIF`, `COUNTIFS`, `SUMIF`, `SUMIFS`, `AVERAGEIF`, `AVERAGEIFS`.

The scalar/reference surface therefore contains **131 names**. The dynamic-array engine recognizes five further names — `SEQUENCE`, `TRANSPOSE`, `FILTER`, `SORT`, `UNIQUE` — so the complete built-in subsystem recognizes **136 names**. Registered extension functions are additional.

## 6. Conditional aggregates

The conditional families share `FormulaCriterion`:

- comparison operators `=`, `<>`, `<`, `<=`, `>`, `>=`;
- invariant Boolean/number/date/error/text parsing;
- ordinal case-insensitive text;
- `*`/`?` wildcards and tilde escapes, including literal `~*` and `~?`;
- blank/non-blank and error matching.

Ranges must be canonical references with identical shapes. Multiple criteria combine by AND. Aggregate errors propagate only at matched positions. Work is bounded to two million positional passes.

## 7. Statistical functions

The first statistics family provides median, single mode, inclusive percentile/quartile, population/sample variance and standard deviation, equal rank, large and small order statistics.

- Range text/Boolean/blank cells are ignored; scalar Boolean and invariant numeric text may coerce.
- One invocation may collect at most two million numeric/date values.
- Inclusive percentiles use linear interpolation.
- Variance uses stable online accumulation.
- Statistical ranges enter the dependency graph and affected-only recalculation.

Full contract: `docs/statistical-functions-foundation-contract.md`.

## 8. Financial functions

The first finance family provides time-value, ordered cash-flow, payment decomposition and depreciation functions.

- `PV`, `FV`, `PMT` and `NPER` share cash-flow sign and timing conventions and explicit zero-rate formulas.
- `NPV` discounts the first retained flow at period one and uses compensated summation.
- `IRR` treats the first retained flow as period zero, requires positive and negative flows, and uses bounded Newton plus transformed-rate bracket/bisection.
- When both Newton and bracket candidates converge, `IRR` selects the candidate nearest the supplied guess. This prevents Newton from silently crossing into a farther root's basin.
- `IPMT` and `PPMT` use one-based payment periods and reconcile to `PMT`.
- `SLN` and `SYD` implement the first depreciation methods.
- Financial ranges enter the dependency graph and affected-only recalculation.

Full contract: `docs/financial-functions-foundation-contract.md`.

## 9. Lookup/reference semantics

- `INDEX` uses one-based indexes and returns `#REF!` outside the range.
- `MATCH` supports exact `0` and basic approximate `1`/`-1`.
- `XLOOKUP` supports exact matching and optional fallback.
- `VLOOKUP`/`HLOOKUP` support exact and conservative approximate mode.
- Lookup ranges enter the dependency graph.
- Approximate lookup assumes correctly sorted input.

Advanced search modes, wildcards and broader array returns remain pending.

## 10. Versioned invocation and compatibility

Versioned invocation preserves logical scalar/range identity and values. New SDK functions default to logical argument counting. Historical built-ins and legacy adapters explicitly use flattened values.

This distinction prevents range arguments in statistics/finance from being confused with control arguments while preserving legacy behavior such as `DATE(A1:A3)`.

## 11. Function Extension SDK

The validated SDK provides stable identity, semantic versions, host API compatibility, capabilities, volatility/state classification, aliases/conflicts, invocation metadata, dependency policy, coercion helpers and legacy compatibility.

The default registry rejects external-state and array-capable extensions. Formula-text version pinning, package discovery, signing and isolation remain pending.

## 12. Date representation

Nera stores dates as `.NET DateTime` and uses OLE Automation serial conversion for numeric date arithmetic. It does not emulate Excel's fictitious 1900-02-29.

## 13. Deliberately pending

- engineering, database and cube families;
- `RATE`, `XNPV`, `XIRR`, cumulative payment, bond/coupon/day-count and accelerated depreciation functions;
- exclusive percentile/quartile, multi-mode, correlation/regression and distributions;
- `OFFSET`, `INDIRECT`, `ADDRESS`, `ROW(S)`, `COLUMN(S)`;
- advanced lookup modes and locale-aware `TEXT`;
- complete literal/reference coercion compatibility;
- volatile scheduling;
- advanced dynamic arrays and higher-order functions;
- plugin package loading/signing/isolation;
- external Excel/LibreOffice differential corpus and fuzzing.

## 14. Validation gates

Promotion requires:

1. Core restore/build/tests and architecture verification;
2. logical/error/aggregate/math/text/date/lookup regressions;
3. SDK API/version/capability/conflict/dependency tests;
4. conditional criteria and aggregate tests;
5. statistical result/coercion/error/dependency/descriptor tests;
6. financial sign/timing/result/error/dependency/budget/multiple-root tests;
7. dynamic-array, Table, filter, XLSX, Windows and MAUI regression matrix.

PR #1 remains Draft while a newer exact-head CI is red or unknown.
