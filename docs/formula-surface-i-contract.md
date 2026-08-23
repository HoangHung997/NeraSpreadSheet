# Formula Surface I contract

This document defines the validated Formula Surface I behavior of NeraSpreadSheet. It is a Nera-owned contract. Excel and LibreOffice are compatibility references only and are not runtime dependencies.

## 1. Architecture boundary

- `FormulaParser` and the AST own syntax.
- `NeraFormulaEngine` owns evaluation order, lazy branches, reference-aware functions, dependency capture and error mapping.
- `BuiltInFormulaFunctionRegistry` owns eager scalar/range functions.
- `FormulaValueCoercion` owns shared conversion of blank, number, Boolean, text and `DateTime`.
- Workbook calculation continues to use the existing dependency graph and affected-only recalculation.
- Platform hosts must not implement function semantics.
- No OpenXml type enters the public formula contracts.

The function registry may be supplied to `NeraFormulaEngine`, but this milestone is not yet the complete, versioned plugin-function SDK.

## 2. Error model

`FormulaErrorCode` includes:

- `DivisionByZero` → `#DIV/0!`;
- `InvalidReference` → `#REF!`;
- `InvalidName` → `#NAME?`;
- `InvalidValue` → `#VALUE!`;
- `CircularReference` → `#CIRC!`;
- `NotAvailable` → `#N/A`;
- `NumericError` → `#NUM!`.

Formula-error values propagate through ordinary scalar and numeric aggregate functions. Information functions such as `ISERROR`, `ISERR` and `ISNA`, and counting functions with explicit non-propagating contracts, inspect or count errors instead of returning them.

`IFERROR` and `IFNA` evaluate the fallback only when required. `IF`, `IFS`, `SWITCH` and `CHOOSE` evaluate only the selected result branch.

## 3. Shared coercion

The shared coercion layer currently follows these rules:

- finite `Number` values remain numbers;
- Boolean converts to `1` or `0` where numeric coercion is allowed;
- blank converts to `0` in scalar numeric coercion;
- `DateTime` converts through OLE Automation serial representation for arithmetic/numeric functions;
- text is converted only where the function explicitly enables invariant numeric/date parsing;
- non-finite numeric results become `#NUM!`;
- text output is bounded to 32,767 characters.

Core currently collapses empty text to blank, so a true zero-length text cell is not represented independently.

## 4. Registered eager functions

The built-in registry contains **92 names**.

### Aggregate and information

`SUM`, `AVERAGE`, `MIN`, `MAX`, `COUNT`, `COUNTA`, `COUNTBLANK`, `PRODUCT`, `SUMSQ`, `AND`, `OR`, `XOR`, `NOT`, `TRUE`, `FALSE`, `NA`, `ISBLANK`, `ISNUMBER`, `ISTEXT`, `ISLOGICAL`, `ISERROR`, `ISERR`, `ISNA`, `N`, `T`.

Numeric aggregates propagate formula errors. For an argument set containing no numeric value:

- `SUM`, `MIN`, `MAX`, `PRODUCT` and `SUMSQ` return `0`;
- `AVERAGE` returns `#DIV/0!`;
- `COUNT`, `COUNTA` and `COUNTBLANK` retain their counting-specific non-propagating behavior.

### Math, rounding and trigonometry

`ABS`, `SIGN`, `INT`, `TRUNC`, `ROUND`, `ROUNDDOWN`, `ROUNDUP`, `MOD`, `POWER`, `SQRT`, `QUOTIENT`, `EVEN`, `ODD`, `CEILING.MATH`, `FLOOR.MATH`, `PI`, `EXP`, `LN`, `LOG10`, `LOG`, `SIN`, `COS`, `TAN`, `ASIN`, `ACOS`, `ATAN`, `ATAN2`, `DEGREES`, `RADIANS`.

Rounding uses midpoint-away-from-zero for `ROUND`. Invalid domains return `#NUM!`; zero divisors return `#DIV/0!`.

### Text and Unicode

`LEN`, `LOWER`, `UPPER`, `PROPER`, `TRIM`, `CLEAN`, `LEFT`, `RIGHT`, `MID`, `REPT`, `EXACT`, `CONCAT`, `TEXTJOIN`, `FIND`, `SEARCH`, `REPLACE`, `SUBSTITUTE`, `VALUE`, `CHAR`, `CODE`, `UNICHAR`, `UNICODE`.

Search positions are one-based. `FIND` is ordinal case-sensitive; `SEARCH` is ordinal case-insensitive. `TEXTJOIN` applies one delimiter across evaluated scalar/range values.

### Date and time

`DATE`, `TIME`, `YEAR`, `MONTH`, `DAY`, `HOUR`, `MINUTE`, `SECOND`, `DAYS`, `EDATE`, `EOMONTH`, `WEEKDAY`, `DATEVALUE`, `TIMEVALUE`, `TODAY`, `NOW`.

`TODAY` and `NOW` use `IFormulaClockEvaluationContext` when supplied, enabling deterministic tests, previews and batch calculations. Otherwise they use the local system clock. This milestone does not yet add automatic volatile-function scheduling to the workbook calculation graph.

## 5. AST/reference-aware functions

The engine additionally recognizes **12 special names**, bringing the current recognized function surface to **104 names**:

`IF`, `IFERROR`, `IFNA`, `IFS`, `SWITCH`, `CHOOSE`, `INDEX`, `MATCH`, `XLOOKUP`, `VLOOKUP`, `HLOOKUP`, `SUBTOTAL`.

These functions remain in the evaluator because they require lazy AST evaluation, range identity, row visibility or dependency semantics.

## 6. Lookup/reference semantics

- `INDEX` uses one-based row/column indexes and returns `#REF!` outside the range.
- `MATCH` supports exact `0` and basic approximate `1`/`-1`.
- `XLOOKUP` currently supports exact matching and optional not-found fallback.
- `VLOOKUP` and `HLOOKUP` support exact mode and conservative approximate mode.
- Lookup ranges are captured as formula dependencies.
- Approximate lookup assumes input is sorted in the required direction; the engine does not sort or silently normalize source data.
- Wildcard matching, binary-search modes, reverse search, multiple return columns and array-valued results are not in this milestone.

## 7. Range and dependency behavior

Registry functions flatten range arguments into scalar values while recording the canonical source range once. Reference-aware functions record the ranges they inspect and avoid registering unused lazy branches.

The current flattening model does not retain every Excel distinction between a literal argument and the same value coming from a referenced cell. Those finer coercion differences remain compatibility work rather than being guessed.

## 8. Date representation

Nera stores date values as `.NET DateTime` and converts to/from OLE Automation serial values where numeric date arithmetic is required.

This milestone does not emulate Excel's historical fictitious 1900-02-29 date. Locale-specific parsing/formatting and the `TEXT` formatting language remain pending.

## 9. Deliberately pending

- Dynamic arrays, spill ranges and array-valued arguments/results.
- `LET`, `LAMBDA`, array helpers and a versioned plugin-function SDK.
- Conditional aggregate families such as `SUMIF(S)`, `COUNTIF(S)` and `AVERAGEIF(S)`.
- Full lookup/reference families such as `OFFSET`, `INDIRECT`, `ADDRESS`, `ROW(S)` and `COLUMN(S)`.
- Statistical, financial, engineering, database and cube functions.
- Locale-aware `TEXT`, number-format parsing and regional function aliases.
- Complete Excel coercion compatibility for literal versus referenced values.
- Volatile recalculation scheduling for `NOW`/`TODAY`.
- External Excel/LibreOffice differential corpus and fuzzing.

## 10. Validation gates

Promotion requires the exact head to pass:

1. Core restore/build/tests.
2. Architecture verification.
3. Logical/error and lazy-branch regressions.
4. Aggregate error/empty-set regressions.
5. Math/domain/rounding regressions.
6. Text/Unicode/length regressions.
7. Date/time/deterministic-clock regressions.
8. Lookup result/dependency regressions.
9. Existing formula, Table, filter, XLSX, Windows and MAUI matrix.

PR #1 remains Draft while a newer exact-head CI is red or unknown.