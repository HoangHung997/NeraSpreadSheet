# Advanced trigonometric and hyperbolic formula contract

## Scope

F013 adds exactly ten new eager/versioned functions:

`ACOT`, `ACOTH`, `COT`, `COTH`, `CSC`, `CSCH`, `SEC`, `SECH`, `ASINH`, `ACOSH`.

`DEGREES` and `RADIANS` already existed before F013 and are intentionally not counted again.

## Evaluation rules

- Inputs use the shared scalar coercion path and accept finite numeric text.
- Results are radians unless the function itself is a conversion function.
- `ACOTH` accepts only values whose absolute value is greater than 1.
- `ACOSH` accepts only values greater than or equal to 1.
- `COT`, `COTH`, `CSC`, `CSCH`, `SEC` and `SECH` require `abs(number) < 2^27`.
- Exact reciprocal singularities return `#DIV/0!`.
- Domain and magnitude violations return `#NUM!`.
- Non-numeric values return `#VALUE!`.
- Non-finite results fail closed through `FormulaValueCoercion.SafeNumber`.

## Architecture

The family is implemented in `AdvancedTrigonometricFormulaFunctions` and aggregated only through `StandardFormulaFunctions.CreateAll()`. It is deterministic, pure, platform-neutral and does not create a second registry.

## Regression gate

Ten dedicated MSTest methods cover principal ranges, positive and negative domains, numeric-text coercion, reciprocal singularities, the `2^27` boundary and hyperbolic symmetry. The validated formula suite contains 274 tests.
