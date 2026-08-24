# Formula Surface I contract

This document defines the validated scalar/reference formula behavior of NeraSpreadSheet. Dynamic arrays and third-party extension contracts are specified separately.

## 1. Architecture boundary

- `FormulaParser` and AST own syntax.
- `NeraFormulaEngine` owns evaluation order, lazy branches, reference-aware functions, dependency capture and error mapping.
- `BuiltInFormulaFunctionRegistry` owns eager versioned lookup through one internal `VersionedFormulaFunctionRegistry`.
- `StandardFormulaFunctions.CreateAll()` is the sole built-in aggregation path.
- `FormulaValueCoercion` owns shared blank/number/Boolean/text/DateTime conversion.
- Platform hosts and OpenXml adapters do not implement formula semantics.

## 2. Current function counts

- Eager/versioned built-ins: **191**.
- AST/reference-aware built-ins: **18**.
- Scalar/reference total: **209**.
- Dynamic-array built-ins: **5**.
- Complete built-in subsystem: **214 names**.

The eager/versioned surface comprises 92 original functions, 11 Statistical Foundation functions, 39 Advanced Statistical functions, 18 financial, 19 engineering and 12 database functions.

## 3. Error and coercion model

Supported errors include `#DIV/0!`, `#REF!`, `#NAME?`, `#VALUE!`, `#CIRC!`, `#N/A`, `#NUM!` and `#SPILL!`. Shared coercion supports finite numbers, Booleans, blank, DateTime/OLE serial conversion and explicitly allowed invariant text. Non-finite results fail closed.

Numerical roots and long schedules have bounded iterations/evaluations. Invalid domains or exhausted budgets return explicit spreadsheet errors instead of hanging or leaking exceptions.

## 4. Function families

### Logical, aggregate, math, text and date/time

The current surface includes lazy control flow, information predicates, aggregates, rounding, logarithmic/trigonometric functions, Unicode/text operations, date construction/extraction/arithmetic and clock-context functions.

### Lookup/reference and criteria

`INDEX`, `MATCH`, `XLOOKUP`, `VLOOKUP`, `HLOOKUP`, plus `COUNTIF(S)`, `SUMIF(S)`, `AVERAGEIF(S)` with dependencies and bounded criteria enumeration.

### Statistical

- Median/mode/percentile/quartile, variance/deviation, rank/order statistics.
- Covariance/correlation/regression/forecast.
- Normal/log-normal/exponential/binomial/Poisson/Weibull/beta/gamma/chi-square/Student-t/F families.

### Financial

`PV`, `FV`, `PMT`, `NPER`, `RATE`, `NPV`, `IRR`, `XNPV`, `XIRR`, `IPMT`, `PPMT`, `CUMIPMT`, `CUMPRINC`, `SLN`, `SYD`, `DB`, `DDB`, `VDB`.

- Root functions use deterministic bounded solvers.
- Irregular schedules preserve value/date positions and dependencies.
- Cumulative loan functions reconcile interest plus principal to PMT over inclusive whole-period ranges.
- DB/DDB/VDB provide fixed, factor-based and variable declining-balance depreciation; VDB supports partial periods and optional straight-line switching.

Full contract: `docs/financial-functions-foundation-contract.md`.

### Engineering and database

Nineteen engineering functions cover bit/shift/radix/comparison behavior. Twelve database functions cover criteria-table aggregates with range identity and budgets.

## 5. Dependency behavior

Range-aware functions preserve source identity and row-major values. Lazy functions omit unused branches. Pairwise statistical, periodic/dated financial and database ranges participate in affected-only recalculation. Current cumulative/depreciation functions are scalar-only and declare no hidden dependency.

## 6. Deliberately pending

- Complete Excel coercion and locale compatibility.
- Advanced lookup/reference modes.
- `ISPMT`, EFFECT/NOMINAL, RRI/PDURATION, AMOR/date-basis and bond/coupon/treasury/price/yield/duration functions.
- Statistical hypothesis tests, confidence intervals and additional distributions.
- Complex/unit/special engineering functions.
- Formula-expression database criteria and cube functions.
- Advanced dynamic arrays and LET/LAMBDA.
- External differential corpora and fuzzing.

## 7. Validation gates

Formula changes require parser/error/coercion tests, descriptor tests, family-specific result/domain/numerical-stability/convergence/reconciliation tests, dependency tests where applicable, resource-budget tests and the complete hosted CI matrix.

PR #1 remains Draft while exact-head CI is red or unknown.
