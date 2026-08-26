# F013 — advanced trigonometric and hyperbolic functions

## Delivered

`ACOT`, `ACOTH`, `COT`, `COTH`, `CSC`, `CSCH`, `SEC`, `SECH`, `ASINH`, `ACOSH`.

The initial schedule contained `DEGREES` and `RADIANS`, but repository audit found both were already implemented. They were replaced with `ASINH` and `ACOSH`, preserving the rule that every public batch contributes exactly ten new names.

## Validation

- Implementation commit: `a112c8e558c66a9aa8afb7bcd2a453bfc1b9f1f2`.
- Hosted implementation CI: #903.
- Build/analyzers: 0 warnings, 0 errors.
- Formula tests: 274/274.
- Architecture verification: passed.
- Windows, Android, iOS, Mac Catalyst and MAUI Windows host gates: passed.

## Counters

- Eager/versioned: 252.
- AST/reference-aware: 34.
- Dynamic-array unique: 20.
- Total functions: **306 / at least 538**.
