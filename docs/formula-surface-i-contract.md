# Formula Surface I contract

This document defines the validated scalar/reference formula behavior of NeraSpreadSheet. Dynamic arrays are specified separately in `docs/dynamic-arrays-contract.md`; third-party extension contracts are specified in `docs/function-extension-sdk-contract.md`.

## 1. Architecture boundary

- `FormulaParser` and AST own syntax.
- `NeraFormulaEngine` owns evaluation order, lazy branches, reference-aware functions, dependency capture and error mapping.
- `BuiltInFormulaFunctionRegistry` owns eager versioned functions.
- `FormulaValueCoercion` owns shared blank/number/Boolean/text/DateTime conversion.
- Platform hosts and OpenXml adapters do not implement formula semantics.

## 2. Current function counts

- Eager/versioned built-ins: **186**.
- AST/reference-aware built-ins: **18**.
- Scalar/reference total: **204**.
- Dynamic-array built-ins: **5**.
- Complete built-in subsystem: **209 names**.

The 186 eager/versioned names comprise the original 92 functions plus 11 Statistical Foundation functions, 39 Advanced Statistical functions, 13 financial, 19 engineering and 12 database functions.

## 3. Error and coercion model

Supported cell error values include `#DIV/0!`, `#REF!`, `#NAME?`, `#VALUE!`, `#CIRC!`, `#N/A`, `#NUM!` and `#SPILL!`. Lazy control functions evaluate only selected branches. Numeric aggregates propagate matched errors according to their explicit contract.

Shared coercion currently supports finite numbers, Booleans, blank, DateTime/OLE serial conversion and explicitly allowed invariant numeric/date text. Non-finite results fail closed. Text output is bounded to 32,767 characters.

Numerical root functions use bounded deterministic solvers. They return `#NUM!` when no admissible root reaches the family tolerance.

## 4. Function families

### Logical, information and aggregates

`IF`, `IFERROR`, `IFNA`, `IFS`, `SWITCH`, `CHOOSE`, `AND`, `OR`, `XOR`, `NOT`, `TRUE`, `FALSE`, `NA`, information predicates, `SUM`, `AVERAGE`, `MIN`, `MAX`, `COUNT`, `COUNTA`, `COUNTBLANK`, `PRODUCT`, `SUMSQ`, `SUBTOTAL`.

### Math, text and date/time

The current surface includes rounding, logarithmic/trigonometric, Unicode/text, search/replace, date construction/extraction/arithmetic and deterministic clock-context functions documented by automated tests.

### Lookup/reference

`INDEX`, `MATCH`, `XLOOKUP`, `VLOOKUP`, `HLOOKUP` with dependency capture and conservative exact/basic-approximate behavior.

### Conditional aggregates

`COUNTIF`, `COUNTIFS`, `SUMIF`, `SUMIFS`, `AVERAGEIF`, `AVERAGEIFS` use shared invariant criteria, wildcard/tilde escaping, same-shape positional ranges and bounded enumeration.

### Statistical Foundation

`MEDIAN`, `MODE.SNGL`, `PERCENTILE.INC`, `QUARTILE.INC`, `VAR.P`, `VAR.S`, `STDEV.P`, `STDEV.S`, `RANK.EQ`, `LARGE`, `SMALL`.

### Advanced Statistical Foundation

- Pairwise analysis: `COVARIANCE.P`, `COVARIANCE.S`, `CORREL`, `PEARSON`, `SLOPE`, `INTERCEPT`, `RSQ`, `STEYX`, `FORECAST.LINEAR`.
- Transformations: `STANDARDIZE`, `FISHER`, `FISHERINV`.
- Normal/log-normal/exponential/discrete/Weibull families.
- Beta, gamma, chi-square, Student-t and F density, cumulative, right/two-tail and inverse families.

Full contract: `docs/advanced-statistical-functions-foundation-contract.md`.

### Financial

`PV`, `FV`, `PMT`, `NPER`, `RATE`, `NPV`, `IRR`, `XNPV`, `XIRR`, `IPMT`, `PPMT`, `SLN`, `SYD`.

`RATE`, `IRR` and `XIRR` use bounded iterative solvers. `XNPV` and `XIRR` preserve positional value/date schedules, a 365-day basis, dependencies and affected-only recalculation. Full contract: `docs/financial-functions-foundation-contract.md`.

### Engineering

`DELTA`, `GESTEP`, five bit/shift functions and twelve binary/octal/hex/decimal conversion functions. Full contract: `docs/engineering-functions-foundation-contract.md`.

### Database

`DSUM`, `DCOUNT`, `DCOUNTA`, `DAVERAGE`, `DMAX`, `DMIN`, `DPRODUCT`, `DGET`, `DSTDEV`, `DSTDEVP`, `DVAR`, `DVARP`. Full contract: `docs/database-functions-foundation-contract.md`.

## 5. Dependency behavior

Range-aware functions preserve source identity and dependencies. Lazy functions omit unused branches. Pairwise statistical, periodic/dated financial and database ranges enter ordinary dependencies and affected-only recalculation. Scalar engineering, financial and distribution references enter the shared graph. Current deterministic functions declare no hidden dependency.

## 6. Deliberately pending

- Complete Excel coercion compatibility.
- Locale-aware `TEXT`/criteria and regional aliases.
- Advanced lookup/reference functions and modes.
- Financial cumulative payment/principal, accelerated depreciation and bond/coupon/day-count/duration/yield families.
- Statistical hypothesis tests, confidence intervals, additional distributions and extreme-tail corpus.
- Complex/unit/special engineering functions.
- Formula-expression database criteria and cube functions.
- Advanced dynamic arrays and LET/LAMBDA.
- External differential corpora and fuzzing.

## 7. Validation gates

Formula changes require parser/error/coercion tests, descriptor tests, family-specific result/domain/numerical-stability/convergence tests, dependency and affected-recalculation tests, resource-budget tests, plus the complete Core/architecture/Windows/MAUI matrix.

PR #1 remains Draft while exact-head CI is red or unknown.
